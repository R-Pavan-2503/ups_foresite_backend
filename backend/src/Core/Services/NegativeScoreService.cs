using System.Text.RegularExpressions;
using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeFamily.Api.Core.Services;

/// <summary>
/// Service for calculating Contributor Negative Scores.
/// Detects code replacement events and calculates instability scores.
/// </summary>
public class NegativeScoreService : INegativeScoreService
{
    private readonly IDatabaseService _db;
    private readonly ILogger<NegativeScoreService> _logger;

    // Thresholds (approved by user)
    private const double SEMANTIC_DISSIMILARITY_THRESHOLD = 0.3; // Below this = exclude (likely refactor)
    private const int MAX_TIME_PROXIMITY_DAYS = 60; // Beyond this = exclude (feature evolution)
    private const int CHURN_CAP = 200; // Lines for full churn magnitude
    private const double DECAY_HALF_LIFE_WEEKS = 18.0;

    // Commit message patterns
    private static readonly Regex FixPattern = new(@"\b(fix|bug|hotfix|patch|issue|error)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RevertPattern = new(@"\b(revert|rollback|undo)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RefactorPattern = new(@"\b(refactor|cleanup|clean up|optimize|style|format|lint)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public NegativeScoreService(IDatabaseService db, ILogger<NegativeScoreService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task CalculateNegativeScoresForRepository(Guid repositoryId)
    {
        _logger.LogInformation("🔍 Starting negative score calculation for repository {RepositoryId}", repositoryId);

        try
        {
            // Step 1: Get all files in the repository
            var files = await _db.GetFilesByRepository(repositoryId);
            _logger.LogInformation("📁 Found {FileCount} files to analyze", files.Count);

            // Step 2: Detect replacement events
            var allEvents = new List<CodeReplacementEvent>();
            var authorCommitCounts = new Dictionary<string, int>();

            foreach (var file in files)
            {
                var events = await DetectReplacementEventsForFile(repositoryId, file);
                allEvents.AddRange(events);
            }

            _logger.LogInformation("⚠️ Detected {EventCount} code replacement events", allEvents.Count);

            // Step 3: Save events to database
            foreach (var evt in allEvents)
            {
                await _db.CreateCodeReplacementEvent(evt);
            }

            // Step 4: Count commits per author for normalization
            var commits = await _db.GetCommitsByRepository(repositoryId);
            foreach (var commit in commits)
            {
                var author = commit.AuthorName ?? "Unknown";
                if (!authorCommitCounts.ContainsKey(author))
                    authorCommitCounts[author] = 0;
                authorCommitCounts[author]++;
            }

            // Step 5: Aggregate scores per contributor
            var scoresByAuthor = allEvents
                .GroupBy(e => e.OriginalAuthorName)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var (author, events) in scoresByAuthor)
            {
                var rawScore = events.Sum(e => e.EventScore);
                var totalCommits = authorCommitCounts.GetValueOrDefault(author, 1);
                var normalizedScore = rawScore / Math.Max(1, totalCommits / 10.0);

                var score = new ContributorNegativeScore
                {
                    RepositoryId = repositoryId,
                    ContributorName = author,
                    RawScore = rawScore,
                    NormalizedScore = normalizedScore,
                    TotalCommits = totalCommits,
                    EventCount = events.Count,
                    LastCalculatedAt = DateTime.UtcNow
                };

                await _db.UpsertContributorNegativeScore(score);
            }

            _logger.LogInformation("✅ Negative score calculation complete. Scored {AuthorCount} contributors", scoresByAuthor.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calculating negative scores for repository {RepositoryId}", repositoryId);
            throw;
        }
    }

    private async Task<List<CodeReplacementEvent>> DetectReplacementEventsForFile(Guid repositoryId, RepositoryFile file)
    {
        var events = new List<CodeReplacementEvent>();

        try
        {
            // Get all file changes for this file
            var fileChanges = await _db.GetFileChangesByFile(file.Id);
            if (fileChanges.Count < 2) return events; // Need at least 2 changes to compare

            // Get the commits for these changes
            var commitIds = fileChanges.Select(fc => fc.CommitId).Distinct().ToList();
            var commitsDict = (await _db.GetCommitsByIds(commitIds)).ToDictionary(c => c.Id);

            // Get embeddings for this file
            var embeddings = await _db.GetEmbeddingsByFile(file.Id);
            if (embeddings.Count == 0) return events; // No embeddings, can't calculate semantic similarity

            // Sort file changes by commit date
            var sortedChanges = fileChanges
                .Where(fc => commitsDict.ContainsKey(fc.CommitId))
                .OrderBy(fc => commitsDict[fc.CommitId].CommittedAt)
                .ToList();

            // Compare consecutive changes
            for (int i = 1; i < sortedChanges.Count; i++)
            {
                var previousChange = sortedChanges[i - 1];
                var currentChange = sortedChanges[i];

                var previousCommit = commitsDict[previousChange.CommitId];
                var currentCommit = commitsDict[currentChange.CommitId];

                // Skip if same author (self-modification)
                if (previousCommit.AuthorName == currentCommit.AuthorName) continue;

                // Check commit message for refactor signal - exclude
                if (IsRefactorCommit(currentCommit.Message)) continue;

                // Calculate time proximity
                var daysBetween = (currentCommit.CommittedAt - previousCommit.CommittedAt).Days;
                if (daysBetween > MAX_TIME_PROXIMITY_DAYS) continue; // Too long = feature evolution

                // Calculate semantic dissimilarity (using latest embedding as proxy)
                // Note: In ideal case, we'd have embeddings per commit, but we use file-level embeddings
                var semanticDissimilarity = CalculateApproximateDissimilarity(embeddings, i, sortedChanges.Count);
                
                if (semanticDissimilarity < SEMANTIC_DISSIMILARITY_THRESHOLD) continue; // Too similar = refactor

                // Calculate churn magnitude
                var churn = (currentChange.Additions ?? 0) + (currentChange.Deletions ?? 0);
                var churnMagnitude = Math.Min(1.0, churn / (double)CHURN_CAP);

                // Parse commit message signal
                var messageSignal = ParseCommitMessageSignal(currentCommit.Message);

                // Calculate time proximity factor (exponential decay)
                var timeProximityFactor = Math.Exp(-daysBetween / 7.0);

                // Calculate decay factor (how long ago this happened)
                var weeksAgo = (DateTime.UtcNow - currentCommit.CommittedAt).TotalDays / 7.0;
                var decayFactor = Math.Exp(-weeksAgo / (DECAY_HALF_LIFE_WEEKS * 1.44)); // 1.44 = ln(2) * 2

                // Calculate event score
                var eventScore = semanticDissimilarity * timeProximityFactor * churnMagnitude * messageSignal * decayFactor;

                events.Add(new CodeReplacementEvent
                {
                    RepositoryId = repositoryId,
                    FileId = file.Id,
                    OriginalCommitId = previousCommit.Id,
                    ReplacementCommitId = currentCommit.Id,
                    OriginalAuthorName = previousCommit.AuthorName ?? "Unknown",
                    ReplacementAuthorName = currentCommit.AuthorName ?? "Unknown",
                    SemanticDissimilarity = semanticDissimilarity,
                    TimeProximityDays = daysBetween,
                    ChurnMagnitude = churn,
                    CommitMessageSignal = messageSignal,
                    EventScore = eventScore,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error analyzing file {FileId} for replacement events", file.Id);
        }

        return events;
    }

    /// <summary>
    /// Approximate semantic dissimilarity based on embedding changes.
    /// Since we don't have per-commit embeddings, we estimate based on position in history.
    /// </summary>
    private double CalculateApproximateDissimilarity(List<CodeEmbedding> embeddings, int changeIndex, int totalChanges)
    {
        // If we have multiple embeddings, use the semantic delta approach
        if (embeddings.Count >= 2)
        {
            var oldEmb = embeddings[Math.Max(0, embeddings.Count - 2)].Embedding;
            var newEmb = embeddings[^1].Embedding;
            
            if (oldEmb != null && newEmb != null)
            {
                return 1.0 - CalculateCosineSimilarity(oldEmb, newEmb);
            }
        }

        // Fallback: estimate based on position (later changes are more likely to be fixes)
        var positionFactor = (double)changeIndex / totalChanges;
        return 0.4 + (positionFactor * 0.2); // Range: 0.4 - 0.6
    }

    private double CalculateCosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        double dotProduct = 0;
        double normA = 0;
        double normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denominator == 0 ? 0 : dotProduct / denominator;
    }

    private bool IsRefactorCommit(string? message)
    {
        if (string.IsNullOrEmpty(message)) return false;
        return RefactorPattern.IsMatch(message);
    }

    private double ParseCommitMessageSignal(string? message)
    {
        if (string.IsNullOrEmpty(message)) return 1.0;

        if (RevertPattern.IsMatch(message)) return 2.0; // Reverts are strong signals
        if (FixPattern.IsMatch(message)) return 1.5; // Fix-related commits
        return 1.0; // Neutral
    }

    public async Task<List<ContributorNegativeScore>> GetScoresForRepository(Guid repositoryId)
    {
        return await _db.GetNegativeScoresByRepository(repositoryId);
    }

    public async Task<List<CodeReplacementEvent>> GetEventsForContributor(Guid repositoryId, string contributorName)
    {
        return await _db.GetReplacementEventsByContributor(repositoryId, contributorName);
    }
}

using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;
using System.Text.Json;

namespace CodeFamily.Api.Workers;

/// <summary>
/// Background worker that processes webhook queue.
/// This is the CORE of real-time conflict detection.
/// 
/// WORKFLOW:
/// 1. Poll webhook_queue for pending items
/// 2. Parse GitHub webhook payload
/// 3. Run incremental analysis on changed files
/// 4. Calculate risk with open PRs
/// 5. If risk ≥ 80%:
///    - Block merge via GitHub Status API
///    - Send Slack DM
///    - Publish to Supabase Realtime
/// </summary>
public class IncrementalWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<IncrementalWorker> _logger;

    public IncrementalWorker(IServiceProvider services, ILogger<IncrementalWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IncrementalWorker starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IDatabaseService>();
                var analysis = scope.ServiceProvider.GetRequiredService<IAnalysisService>();
                var github = scope.ServiceProvider.GetRequiredService<IGitHubService>();
                var slack = scope.ServiceProvider.GetRequiredService<ISlackService>();

                // Get next pending webhook
                var webhook = await db.GetNextPendingWebhook();

                if (webhook == null)
                {
                    await Task.Delay(1000, stoppingToken); // Poll every second
                    continue;
                }

                _logger.LogInformation($"Processing webhook {webhook.Id}");

                try
                {
                    await ProcessWebhook(webhook, db, analysis, github, slack);
                    await db.UpdateWebhookStatus(webhook.Id, "completed");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error processing webhook {webhook.Id}: {ex.Message}");
                    await db.UpdateWebhookStatus(webhook.Id, "failed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Worker error: {ex.Message}");
                await Task.Delay(5000, stoppingToken);
            }
        }

        _logger.LogInformation("IncrementalWorker stopping");
    }

    private async Task ProcessWebhook(
        WebhookQueueItem webhook,
        IDatabaseService db,
        IAnalysisService analysis,
        IGitHubService github,
        ISlackService slack)
    {
        var payload = JsonDocument.Parse(webhook.Payload);
        var eventType = payload.RootElement.GetProperty("action").GetString();

        // Handle push events
        if (payload.RootElement.TryGetProperty("commits", out _))
        {
            await HandlePushEvent(payload, db, analysis, github, slack);
        }
        // Handle PR events
        else if (payload.RootElement.TryGetProperty("pull_request", out _))
        {
            await HandlePullRequestEvent(payload, db);
        }
    }

    private async Task HandlePushEvent(
        JsonDocument payload,
        IDatabaseService db,
        IAnalysisService analysis,
        IGitHubService github,
        ISlackService slack)
    {
        var repository = payload.RootElement.GetProperty("repository");
        var owner = repository.GetProperty("owner").GetProperty("login").GetString()!;
        var repoName = repository.GetProperty("name").GetString()!;
        
        var headCommit = payload.RootElement.GetProperty("head_commit");
        var commitSha = headCommit.GetProperty("id").GetString()!;

        var pusher = payload.RootElement.GetProperty("pusher").GetProperty("name").GetString()!;

        // Get changed files
        var changedFiles = new List<string>();
        if (headCommit.TryGetProperty("added", out var added))
        {
            changedFiles.AddRange(added.EnumerateArray().Select(f => f.GetString()!));
        }
        if (headCommit.TryGetProperty("modified", out var modified))
        {
            changedFiles.AddRange(modified.EnumerateArray().Select(f => f.GetString()!));
        }

        _logger.LogInformation($"Push to {owner}/{repoName}: {changedFiles.Count} files changed");

        // Get repository from DB
        var repo = await db.GetRepositoryByName(owner, repoName);
        if (repo == null)
        {
            _logger.LogWarning($"Repository {owner}/{repoName} not in database");
            return;
        }

        // Run incremental analysis
        await analysis.ProcessIncrementalUpdate(repo.Id, commitSha, changedFiles);

        // Get embeddings for changed files
        var newEmbeddings = new List<float[]>();
        foreach (var filePath in changedFiles)
        {
            var file = await db.GetFileByPath(repo.Id, filePath);
            if (file != null)
            {
                var embeddings = await db.GetEmbeddingsByFile(file.Id);
                newEmbeddings.AddRange(embeddings.Select(e => e.Embedding!));
            }
        }

        // Calculate risk
        var risk = await analysis.CalculateRisk(repo.Id, changedFiles, newEmbeddings);

        _logger.LogInformation($"Risk score: {risk.RiskScore:F2}");

        // If high risk, take action
        if (risk.RiskScore >= 0.8)
        {
            _logger.LogWarning($"HIGH RISK DETECTED: {risk.RiskScore:F2}");

            // Block merge via GitHub Status API
            foreach (var conflictingPr in risk.ConflictingPrs.Where(pr => pr.Risk >= 0.8))
            {
                await github.CreateCommitStatus(
                    owner,
                    repoName,
                    commitSha,
                    "failure",
                    $"Blocked: Potential conflict with PR #{conflictingPr.PrNumber}",
                    "codefamily/conflict-detection"
                );

                _logger.LogInformation($"Blocked merge for commit {commitSha} due to PR #{conflictingPr.PrNumber}");
            }

            // Send Slack alert
            try
            {
                var message = $"⚠️ *Conflict Warning*\n\n" +
                              $"Your push to `{owner}/{repoName}` has a {risk.RiskScore:P0} conflict risk.\n\n" +
                              $"Conflicting PRs:\n" +
                              string.Join("\n", risk.ConflictingPrs.Select(pr => $"• PR #{pr.PrNumber} (Risk: {pr.Risk:P0})"));

                await slack.SendDirectMessage(pusher, message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send Slack alert: {ex.Message}");
            }
        }
    }

    private async Task HandlePullRequestEvent(JsonDocument payload, IDatabaseService db)
    {
        var action = payload.RootElement.GetProperty("action").GetString();
        var pr = payload.RootElement.GetProperty("pull_request");
        var prNumber = pr.GetProperty("number").GetInt32();
        var state = pr.GetProperty("state").GetString()!;

        var repository = payload.RootElement.GetProperty("repository");
        var owner = repository.GetProperty("owner").GetProperty("login").GetString()!;
        var repoName = repository.GetProperty("name").GetString()!;

        var repo = await db.GetRepositoryByName(owner, repoName);
        if (repo == null) return;

        // Update or create PR record
        var existingPr = await db.GetPullRequestByNumber(repo.Id, prNumber);
        if (existingPr == null)
        {
            await db.CreatePullRequest(new PullRequest
            {
                RepositoryId = repo.Id,
                PrNumber = prNumber,
                State = state
            });
        }
        else
        {
            await db.UpdatePullRequestState(existingPr.Id, state);
        }

        _logger.LogInformation($"PR #{prNumber} {action} in {owner}/{repoName}");
    }
}

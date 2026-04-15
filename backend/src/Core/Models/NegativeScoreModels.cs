namespace CodeFamily.Api.Core.Models;

// ============================================
// CONTRIBUTOR NEGATIVE SCORE MODELS
// ============================================

/// <summary>
/// Represents a code replacement event where one contributor's code
/// was significantly altered by another contributor.
/// </summary>
public class CodeReplacementEvent
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public Guid FileId { get; set; }
    public Guid OriginalCommitId { get; set; }
    public Guid ReplacementCommitId { get; set; }
    public string OriginalAuthorName { get; set; } = string.Empty;
    public string ReplacementAuthorName { get; set; } = string.Empty;
    public double SemanticDissimilarity { get; set; }
    public int TimeProximityDays { get; set; }
    public int ChurnMagnitude { get; set; }
    public double CommitMessageSignal { get; set; } = 1.0;
    public double EventScore { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Aggregated negative score for a contributor in a repository.
/// </summary>
public class ContributorNegativeScore
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public string ContributorName { get; set; } = string.Empty;
    public double RawScore { get; set; }
    public double NormalizedScore { get; set; }
    public int TotalCommits { get; set; }
    public int EventCount { get; set; }
    public DateTime? LastCalculatedAt { get; set; }
}

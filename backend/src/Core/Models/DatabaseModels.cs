namespace CodeFamily.Api.Core.Models;

public class User
{
    public Guid Id { get; set; }
    public long GithubId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
}

public class Repository
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OwnerUsername { get; set; } = string.Empty;
    public string? Status { get; set; }
    public bool IsActiveBlocking { get; set; }
    public Guid? ConnectedByUserId { get; set; }
    public bool IsMine { get; set; }
    public string? LastAnalyzedCommitSha { get; set; }
    public DateTime? LastRefreshAt { get; set; }
}

public class Branch
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? HeadCommitSha { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CommitBranch
{
    public Guid CommitId { get; set; }
    public Guid BranchId { get; set; }
    public DateTime CreatedAt { get; set; }
}
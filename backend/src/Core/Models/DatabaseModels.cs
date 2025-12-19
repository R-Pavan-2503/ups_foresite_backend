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

public class Commit
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public string Sha { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? AuthorName { get; set; }
    public string? AuthorEmail { get; set; }
    public Guid? AuthorUserId { get; set; }  // Link to users table
    public DateTime CommittedAt { get; set; }
}

public class RepositoryFile
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int? TotalLines { get; set; }
}

public class FileChange
{
    public Guid CommitId { get; set; }
    public Guid FileId { get; set; }
    public int? Additions { get; set; }
    public int? Deletions { get; set; }
}

public class FileOwnership
{
    public Guid FileId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public decimal? SemanticScore { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class Dependency
{
    public Guid SourceFileId { get; set; }
    public Guid TargetFileId { get; set; }
    public string? DependencyType { get; set; }
    public int? Strength { get; set; }
}

public class CodeEmbedding
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public float[]? Embedding { get; set; }
    public string? ChunkContent { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PullRequest
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public int PrNumber { get; set; }
    public string? Title { get; set; }
    public string? State { get; set; }
    public Guid? AuthorId { get; set; }
    public bool Merged { get; set; }
    public DateTime? MergedAt { get; set; }
}

public class PrFileChanged
{
    public Guid PrId { get; set; }
    public Guid FileId { get; set; }
}

// NEW: Track requested reviewers from GitHub PR
public class PrRequestedReviewer
{
    public Guid PrId { get; set; }
    public Guid? ReviewerId { get; set; }  // Can be null if reviewer not in our users table
    public long? GitHubUserId { get; set; }  // Store GitHub user ID for lookup
}

public class PrConflict
{
    public Guid ConflictingPrId { get; set; }
    public int PrNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public List<string> OverlappingFiles { get; set; } = new();
    public int OverlapCount { get; set; }
    public decimal ConflictPercentage { get; set; }
}

public class Review
{
    public Guid Id { get; set; }
    public Guid PrId { get; set; }
    public Guid? ReviewerId { get; set; }
    public string? State { get; set; }
}

public class WebhookQueueItem
{
    public long Id { get; set; }
    public string Payload { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
}

// ============================================
// NOTES SYSTEM MODELS
// ============================================

// File-Level Notes
public class FileStickyNote
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public Guid FileId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string NoteType { get; set; } = "text"; // "text" or "document"
    public string? Content { get; set; }
    public string? DocumentUrl { get; set; }
    public string? DocumentName { get; set; }
    public long? DocumentSize { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class FileDiscussionThread
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public Guid FileId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class FileDiscussionMessage
{
    public Guid Id { get; set; }
    public Guid ThreadId { get; set; }
    public Guid UserId { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid[]? MentionedUsers { get; set; }
    public int[]? ReferencedLineNumbers { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class FilePersonalNote
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public Guid UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public int? LineNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// Repository-Level Notes
public class RepoStickyNote
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string NoteType { get; set; } = "text"; // "text" or "document"
    public string? Content { get; set; }
    public string? DocumentUrl { get; set; }
    public string? DocumentName { get; set; }
    public long? DocumentSize { get; set; }
    public Guid[]? TaggedFileIds { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class RepoDiscussionThread
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RepoDiscussionMessage
{
    public Guid Id { get; set; }
    public Guid ThreadId { get; set; }
    public Guid UserId { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid[]? MentionedUsers { get; set; }
    public Guid[]? TaggedFileIds { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class RepoPersonalNote
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public Guid UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid[]? TaggedFileIds { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// Notifications
public class UserNotification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string NotificationType { get; set; } = "mention"; // "mention", "reply", "note_created"
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public Guid? RelatedFileId { get; set; }
    public Guid? RelatedRepositoryId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ============================================
// PERSONALIZED DASHBOARD MODELS
// ============================================

public class FileView
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid FileId { get; set; }
    public DateTime ViewedAt { get; set; }
}

public class FileBookmark
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid FileId { get; set; }
    public string? Category { get; set; }
    public DateTime CreatedAt { get; set; }
}

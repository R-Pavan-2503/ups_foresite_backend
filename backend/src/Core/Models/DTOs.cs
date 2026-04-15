namespace CodeFamily.Api.Core.Models;

// DTOs for API responses and requests

public class GitHubUserDto
{
    public long Id { get; set; }
    public string Login { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
}

public class RepositoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OwnerUsername { get; set; } = string.Empty;
    public string? Status { get; set; }
    public bool IsActiveBlocking { get; set; }
}

public class CommitDto
{
    public Guid Id { get; set; }
    public string Sha { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DateTime CommittedAt { get; set; }
    public string? AuthorName { get; set; }
    public string? AuthorEmail { get; set; }
}

public class FileDto
{
    public Guid Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int? TotalLines { get; set; }
}

public class FileAnalysisDto
{
    public string FilePath { get; set; } = string.Empty;
    public string? Purpose { get; set; }
    public List<OwnershipDto> Owners { get; set; } = new();
    public List<DependencyDto> Dependencies { get; set; } = new();
    public List<DependencyDto> Dependents { get; set; } = new();
    public List<FileDto> SemanticNeighbors { get; set; } = new();
    public int ChangeCount { get; set; }
    public string? MostFrequentAuthor { get; set; }
    public DateTime? LastModified { get; set; }
    public bool IsInOpenPr { get; set; }
}

public class OwnershipDto
{
    public string Username { get; set; } = string.Empty;
    public decimal SemanticScore { get; set; }
}

public class DependencyDto
{
    public string FilePath { get; set; } = string.Empty;
    public string? DependencyType { get; set; }
    public int? Strength { get; set; }
}

public class PullRequestDto
{
    public Guid Id { get; set; }
    public int PrNumber { get; set; }
    public string? State { get; set; }
    public string? AuthorUsername { get; set; }
    public string? Title { get; set; }
    public double? RiskScore { get; set; }
}

public class ParsedFunction
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int EndLine { get; set; }
}

public class ParsedImport
{
    public string Module { get; set; } = string.Empty;
    public string? ImportedName { get; set; }
}

public class ParseResult
{
    public List<ParsedFunction> Functions { get; set; } = new();
    public List<ParsedImport> Imports { get; set; } = new();
}

public class EmbeddingResponse
{
    public float[] Embedding { get; set; } = Array.Empty<float>();
}

public class RiskAnalysisResult
{
    public double RiskScore { get; set; }
    public double StructuralOverlap { get; set; }
    public double SemanticOverlap { get; set; }
    public List<ConflictingPr> ConflictingPrs { get; set; } = new();
}

public class ConflictingPr
{
    public int PrNumber { get; set; }
    public string? Title { get; set; }
    public double Risk { get; set; }
    public List<string> ConflictingFiles { get; set; } = new();
}

public class GitHubCommitAuthor
{
    public string Login { get; set; } = string.Empty;
    public long Id { get; set; }
    public string? AvatarUrl { get; set; }
}

// ============================================
// NOTES SYSTEM DTOs
// ============================================

// Sticky Notes DTOs
public class StickyNoteDto
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public Guid? FileId { get; set; } // null for repo-level notes
    public string NoteType { get; set; } = string.Empty;
    public string? Content { get; set; }
    public string? DocumentUrl { get; set; }
    public string? DocumentName { get; set; }
    public long? DocumentSize { get; set; }
    public List<Guid>? TaggedFileIds { get; set; } // for repo-level notes
    public List<TaggedFileDto>? TaggedFiles { get; set; }
    public List<Guid>? TaggedBranchIds { get; set; }
    public List<TaggedBranchDto>? TaggedBranches { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string CreatedByUsername { get; set; } = string.Empty;
    public string? CreatedByAvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateTextStickyNoteRequest
{
    public Guid RepositoryId { get; set; }
    public Guid? FileId { get; set; } // null for repo-level notes
    public string Content { get; set; } = string.Empty;
    public List<Guid>? TaggedFileIds { get; set; } // for repo-level notes
    public List<Guid>? TaggedBranchIds { get; set; }
}

public class UpdateStickyNoteRequest
{
    public string? Content { get; set; }
    public List<Guid>? TaggedFileIds { get; set; }
    public List<Guid>? TaggedBranchIds { get; set; }
}

// Discussion Thread DTOs
public class DiscussionThreadDto
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public Guid? FileId { get; set; }
    public List<DiscussionMessageDto> Messages { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class DiscussionMessageDto
{
    public Guid Id { get; set; }
    public Guid ThreadId { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<MentionedUserDto>? MentionedUsers { get; set; }
    public List<int>? ReferencedLineNumbers { get; set; } // for file-level
    public List<Guid>? TaggedFileIds { get; set; }
    public List<TaggedFileDto>? TaggedFiles { get; set; } // for repo-level
    public List<Guid>? TaggedBranchIds { get; set; }
    public List<TaggedBranchDto>? TaggedBranches { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class MentionedUserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}

public class TaggedFileDto
{
    public Guid Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
}

public class TaggedBranchDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

public class PostMessageRequest
{
    public string Message { get; set; } = string.Empty;
    public List<Guid>? TaggedFileIds { get; set; }
    public List<Guid>? TaggedBranchIds { get; set; }
}

public class UpdateMessageRequest
{
    public string Message { get; set; } = string.Empty;
}

// Personal Notes DTOs
public class PersonalNoteDto
{
    public Guid Id { get; set; }
    public Guid? FileId { get; set; }
    public Guid? RepositoryId { get; set; }
    public string Content { get; set; } = string.Empty;
    public int? LineNumber { get; set; } // for file-level
    public List<Guid>? TaggedFileIds { get; set; }
    public List<TaggedFileDto>? TaggedFiles { get; set; } // for repo-level
    public List<Guid>? TaggedBranchIds { get; set; }
    public List<TaggedBranchDto>? TaggedBranches { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreatePersonalNoteRequest
{
    public Guid? FileId { get; set; }
    public Guid? RepositoryId { get; set; }
    public string Content { get; set; } = string.Empty;
    public int? LineNumber { get; set; } // for file-level
    public List<Guid>? TaggedFileIds { get; set; } // for repo-level
    public List<Guid>? TaggedBranchIds { get; set; }
}

public class UpdatePersonalNoteRequest
{
    public string Content { get; set; } = string.Empty;
    public int? LineNumber { get; set; }
    public List<Guid>? TaggedFileIds { get; set; }
    public List<Guid>? TaggedBranchIds { get; set; }
}

// Notification DTOs
public class NotificationDto
{
    public Guid Id { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public Guid? RelatedFileId { get; set; }
    public Guid? RelatedRepositoryId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class NotificationsResponse
{
    public List<NotificationDto> Notifications { get; set; } = new();
    public int TotalCount { get; set; }
    public int UnreadCount { get; set; }
}

// ============================================
// LINE COMMENTS DTOs
// ============================================

public class LineCommentDto
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public Guid FileId { get; set; }
    public int LineNumber { get; set; }
    public string CommentText { get; set; } = string.Empty;
    public bool IsShared { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string CreatedByUsername { get; set; } = string.Empty;
    public string? CreatedByAvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateLineCommentRequest
{
    public Guid RepositoryId { get; set; }
    public Guid FileId { get; set; }
    public int LineNumber { get; set; }
    public string CommentText { get; set; } = string.Empty;
    public bool IsShared { get; set; }
}

public class UpdateLineCommentRequest
{
    public string CommentText { get; set; } = string.Empty;
}

// ============================================
// TEAMS & RBAC DTOs
// ============================================

public class TeamDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid RepositoryId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByUsername { get; set; }
    public string? CreatedByAvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class TeamMemberDto
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Email { get; set; }
    public string Role { get; set; } = "contributor"; // "team_leader" or "contributor"
    public Guid? AssignedByUserId { get; set; }
    public string? AssignedByUsername { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class TeamWithMembersDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid RepositoryId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByUsername { get; set; }
    public string? CreatedByAvatarUrl { get; set; }
    public List<TeamMemberDto> Members { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateTeamRequest
{
    public string Name { get; set; } = string.Empty;
}

public class UpdateTeamRequest
{
    public string Name { get; set; } = string.Empty;
}

public class AddTeamMemberRequest
{
    public Guid UserId { get; set; }
    public string Role { get; set; } = "contributor"; // "team_leader" or "contributor"
}

public class UpdateTeamMemberRoleRequest
{
    public string Role { get; set; } = string.Empty; // "team_leader" or "contributor"
}

public class RepoAdminDto
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Email { get; set; }
    public Guid? AssignedByUserId { get; set; }
    public string? AssignedByUsername { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ============================================
// TEAM ANALYTICS DTOs
// ============================================

public class MemberContributionDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = string.Empty;
    public int TotalCommits { get; set; }
    public int LinesAdded { get; set; }
    public int LinesRemoved { get; set; }
    public int FilesChanged { get; set; }
    public DateTime? LastCommitDate { get; set; }
    public bool IsActive { get; set; }
}

public class ActivityTimelineDto
{
    public string Date { get; set; } = string.Empty;
    public int Commits { get; set; }
    public int LinesAdded { get; set; }
    public int LinesRemoved { get; set; }
}

public class HotspotFileDto
{
    public Guid FileId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int ChangeCount { get; set; }
    public List<string> Contributors { get; set; } = new();
}

public class TeamContributionAnalyticsDto
{
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public int TotalMembers { get; set; }
    public int TotalCommits { get; set; }
    public int TotalLinesAdded { get; set; }
    public int TotalLinesRemoved { get; set; }
    public int TotalFilesChanged { get; set; }
    public List<MemberContributionDto> MemberContributions { get; set; } = new();
    public List<ActivityTimelineDto> ActivityTimeline { get; set; } = new();
    public List<HotspotFileDto> Hotspots { get; set; } = new();
    public Dictionary<string, int> FileTypeDistribution { get; set; } = new();
    public string MostActiveDay { get; set; } = string.Empty;
    public DateTime? FirstCommitDate { get; set; }
    public DateTime? LastCommitDate { get; set; }
}

public class IndividualContributionAnalyticsDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public int TotalCommits { get; set; }
    public int LinesAdded { get; set; }
    public int LinesRemoved { get; set; }
    public int FilesChanged { get; set; }
    public List<ActivityTimelineDto> ActivityTimeline { get; set; } = new();
    public List<HotspotFileDto> PersonalHotspots { get; set; } = new();
    public Dictionary<string, int> FileTypeDistribution { get; set; } = new();
    public string MostActiveDay { get; set; } = string.Empty;
    public DateTime? FirstCommitDate { get; set; }
    public DateTime? LastCommitDate { get; set; }
    public double AverageCommitsPerDay { get; set; }
    public List<string> CodeOwnership { get; set; } = new();
}


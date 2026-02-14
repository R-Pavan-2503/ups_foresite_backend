namespace CodeFamily.Api.Core.Models;

// Azure DevOps DTOs

public class AzureDevOpsProject
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public int Revision { get; set; }
    public string Visibility { get; set; } = string.Empty;
    public DateTime LastUpdateTime { get; set; }
}

public class AzureDevOpsTeam
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
}

public class AzureDevOpsWorkItem
{
    // Basic Info
    public int Id { get; set; }
    public int Rev { get; set; }
    public string Title { get; set; } = string.Empty;
    public string WorkItemType { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    
    // Assignment
    public string? AssignedTo { get; set; }
    public string? AssignedToDisplayName { get; set; }
    public string? AssignedToAvatarUrl { get; set; }
    
    // Dates & Timeline
    public DateTime CreatedDate { get; set; }
    public DateTime ChangedDate { get; set; }
    public DateTime? ClosedDate { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public DateTime? StateChangeDate { get; set; }
    public DateTime? ActivatedDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? FinishDate { get; set; }
    
    // People
    public string? CreatedBy { get; set; }
    public string? ChangedBy { get; set; }
    public string? ResolvedBy { get; set; }
    public string? ClosedBy { get; set; }
    public string? ActivatedBy { get; set; }
    
    // Organization
    public string? AreaPath { get; set; }
    public string? IterationPath { get; set; }
    public string? TeamProject { get; set; }
    
    // Priority & Severity
    public int? Priority { get; set; }
    public string? Severity { get; set; }
    public string? Risk { get; set; }
    
    // Effort & Planning
    public double? StoryPoints { get; set; }
    public double? Effort { get; set; }
    public double? OriginalEstimate { get; set; }
    public double? RemainingWork { get; set; }
    public double? CompletedWork { get; set; }
    public int? BusinessValue { get; set; }
    public int? TimeCriticality { get; set; }
    
    // Process Fields
    public string? Reason { get; set; }
    public string? Activity { get; set; }
    public string? ValueArea { get; set; }
    
    // Content
    public string? Description { get; set; }
    public string? AcceptanceCriteria { get; set; }
    public string? ReproSteps { get; set; }
    public string? SystemInfo { get; set; }
    
    // Metadata
    public List<string> Tags { get; set; } = new();
    public int CommentCount { get; set; }
    public int RelationCount { get; set; }
    
    // Relationships (simplified - just counts and basic info)
    public string? ParentWorkItemId { get; set; }
    public string? ParentWorkItemTitle { get; set; }
    public int ChildWorkItemsCount { get; set; }
    public int RelatedWorkItemsCount { get; set; }
    public int LinkedPullRequestsCount { get; set; }
    
    // URLs
    public string Url { get; set; } = string.Empty;
    public string? WebUrl { get; set; }
}

public class AzureDevOpsWorkItemQueryResult
{
    public string QueryType { get; set; } = string.Empty;
    public List<WorkItemReference> WorkItems { get; set; } = new();
}

public class WorkItemReference
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
}

// API Response models for deserialization
public class AzureDevOpsProjectsResponse
{
    public List<AzureDevOpsProject> Value { get; set; } = new();
    public int Count { get; set; }
}

public class AzureDevOpsTeamsResponse
{
    public List<AzureDevOpsTeam> Value { get; set; } = new();
    public int Count { get; set; }
}

public class AzureDevOpsWorkItemsResponse
{
    public int Count { get; set; }
    public List<AzureDevOpsWorkItemDetail> Value { get; set; } = new();
}

public class AzureDevOpsWorkItemDetail
{
    public int Id { get; set; }
    public int Rev { get; set; }
    public Dictionary<string, object> Fields { get; set; } = new();
    public List<WorkItemRelation>? Relations { get; set; }
    public string Url { get; set; } = string.Empty;
}

public class WorkItemRelation
{
    public string Rel { get; set; } = string.Empty;
    public string? Url { get; set; }
    public Dictionary<string, object>? Attributes { get; set; }
}

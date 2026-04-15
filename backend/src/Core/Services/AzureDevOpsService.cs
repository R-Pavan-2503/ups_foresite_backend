using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CodeFamily.Api.Core.Services;

public class AzureDevOpsService : IAzureDevOpsService
{
    private readonly AzureDevOpsSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AzureDevOpsService> _logger;

    public AzureDevOpsService(
        IOptions<AppSettings> appSettings,
        IHttpClientFactory httpClientFactory,
        ILogger<AzureDevOpsService> logger)
    {
        _settings = appSettings.Value.AzureDevOps;
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;

        // Log settings to debug configuration loading
        _logger.LogInformation($"Azure DevOps Service initialized with OrganizationUrl: '{_settings.OrganizationUrl}', PAT length: {_settings.PersonalAccessToken?.Length ?? 0}");

        if (string.IsNullOrWhiteSpace(_settings.OrganizationUrl) || string.IsNullOrWhiteSpace(_settings.PersonalAccessToken))
        {
            _logger.LogWarning("Azure DevOps settings are not properly configured. Please check your .env and settings.json files.");
        }

        // Configure HttpClient with Basic Authentication
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{_settings.PersonalAccessToken}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<List<AzureDevOpsProject>> GetProjectsAsync()
    {
        try
        {
            // Validate settings
            if (string.IsNullOrWhiteSpace(_settings.OrganizationUrl))
            {
                _logger.LogError("Azure DevOps OrganizationUrl is not configured. Please check your .env and settings.json files.");
                throw new InvalidOperationException("Azure DevOps OrganizationUrl is not configured.");
            }

            var url = $"{_settings.OrganizationUrl.TrimEnd('/')}/_apis/projects?api-version=7.0";
            _logger.LogInformation($"Fetching projects from: {url}");

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var projectsResponse = JsonSerializer.Deserialize<AzureDevOpsProjectsResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return projectsResponse?.Value ?? new List<AzureDevOpsProject>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Azure DevOps projects");
            throw;
        }
    }

    public async Task<List<AzureDevOpsTeam>> GetTeamsAsync(string projectId)
    {
        try
        {
            var url = $"{_settings.OrganizationUrl.TrimEnd('/')}/_apis/projects/{projectId}/teams?api-version=7.0";
            _logger.LogInformation($"Fetching teams from: {url}");

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var teamsResponse = JsonSerializer.Deserialize<AzureDevOpsTeamsResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return teamsResponse?.Value ?? new List<AzureDevOpsTeam>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error fetching teams for project: {projectId}");
            throw;
        }
    }

    public async Task<List<AzureDevOpsWorkItem>> GetWorkItemsAsync(string projectId, string? teamId = null, string? state = null)
    {
        try
        {
            // First, query for work item IDs using WIQL (Work Item Query Language)
            // Note: We don't filter by project in WIQL because the API endpoint is already scoped to the project
            var wiql = state != null 
                ? $"SELECT [System.Id], [System.Title], [System.State] FROM WorkItems WHERE [System.State] = '{state}' ORDER BY [System.ChangedDate] DESC"
                : $"SELECT [System.Id], [System.Title], [System.State] FROM WorkItems ORDER BY [System.ChangedDate] DESC";

            var wiqlUrl = $"{_settings.OrganizationUrl.TrimEnd('/')}/{projectId}/_apis/wit/wiql?api-version=7.0";
            
            var wiqlRequest = new { query = wiql };
            var wiqlContent = new StringContent(JsonSerializer.Serialize(wiqlRequest), Encoding.UTF8, "application/json");
            
            var wiqlResponse = await _httpClient.PostAsync(wiqlUrl, wiqlContent);
            wiqlResponse.EnsureSuccessStatusCode();

            var wiqlResult = await wiqlResponse.Content.ReadAsStringAsync();
            var queryResult = JsonSerializer.Deserialize<AzureDevOpsWorkItemQueryResult>(wiqlResult, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (queryResult == null || queryResult.WorkItems.Count == 0)
            {
                return new List<AzureDevOpsWorkItem>();
            }

            // Get detailed information for each work item (batch request)
            var ids = string.Join(",", queryResult.WorkItems.Select(wi => wi.Id));
            var workItemsUrl = $"{_settings.OrganizationUrl.TrimEnd('/')}/_apis/wit/workitems?ids={ids}&api-version=7.0&$expand=all";

            _logger.LogInformation($"Fetching work items: {workItemsUrl}");

            var response = await _httpClient.GetAsync(workItemsUrl);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var workItemsResponse = JsonSerializer.Deserialize<AzureDevOpsWorkItemsResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Map to our DTO
            return MapWorkItemDetails(workItemsResponse?.Value ?? new List<AzureDevOpsWorkItemDetail>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error fetching work items for project: {projectId}");
            throw;
        }
    }

    public async Task<AzureDevOpsWorkItem?> GetWorkItemDetailsAsync(int workItemId)
    {
        try
        {
            var url = $"{_settings.OrganizationUrl.TrimEnd('/')}/_apis/wit/workitems/{workItemId}?api-version=7.0&$expand=all";
            _logger.LogInformation($"Fetching work item details: {url}");

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var workItem = JsonSerializer.Deserialize<AzureDevOpsWorkItemDetail>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (workItem == null) return null;

            return MapWorkItemDetail(workItem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error fetching work item: {workItemId}");
            return null;
        }
    }

    // Helper method to map work item details to our DTO
    private List<AzureDevOpsWorkItem> MapWorkItemDetails(List<AzureDevOpsWorkItemDetail> details)
    {
        return details.Select(MapWorkItemDetail).ToList();
    }

    private AzureDevOpsWorkItem MapWorkItemDetail(AzureDevOpsWorkItemDetail detail)
    {
        var fields = detail.Fields;
        
        // Count relationships
        var relations = detail.Relations ?? new List<WorkItemRelation>();
        var parentRelation = relations.FirstOrDefault(r => r.Rel == "System.LinkTypes.Hierarchy-Reverse");
        var childRelations = relations.Where(r => r.Rel == "System.LinkTypes.Hierarchy-Forward").ToList();
        var relatedRelations = relations.Where(r => r.Rel == "System.LinkTypes.Related").ToList();
        var prRelations = relations.Where(r => r.Rel == "ArtifactLink" && r.Url?.Contains("/pullrequest/") == true).ToList();
        
        return new AzureDevOpsWorkItem
        {
            // Basic Info
            Id = detail.Id,
            Rev = detail.Rev,
            Title = GetFieldValue<string>(fields, "System.Title") ?? "",
            WorkItemType = GetFieldValue<string>(fields, "System.WorkItemType") ?? "",
            State = GetFieldValue<string>(fields, "System.State") ?? "",
            
            // Assignment
            AssignedTo = GetAssignedToName(fields),
            AssignedToDisplayName = GetAssignedToDisplayName(fields),
            AssignedToAvatarUrl = GetAssignedToAvatar(fields),
            
            // Dates & Timeline
            CreatedDate = GetFieldValue<DateTime>(fields, "System.CreatedDate"),
            ChangedDate = GetFieldValue<DateTime>(fields, "System.ChangedDate"),
            ClosedDate = GetNullableFieldValue<DateTime>(fields, "Microsoft.VSTS.Common.ClosedDate"),
            ResolvedDate = GetNullableFieldValue<DateTime>(fields, "Microsoft.VSTS.Common.ResolvedDate"),
            StateChangeDate = GetNullableFieldValue<DateTime>(fields, "Microsoft.VSTS.Common.StateChangeDate"),
            ActivatedDate = GetNullableFieldValue<DateTime>(fields, "Microsoft.VSTS.Common.ActivatedDate"),
            DueDate = GetNullableFieldValue<DateTime>(fields, "Microsoft.VSTS.Scheduling.DueDate"),
            StartDate = GetNullableFieldValue<DateTime>(fields, "Microsoft.VSTS.Scheduling.StartDate"),
            FinishDate = GetNullableFieldValue<DateTime>(fields, "Microsoft.VSTS.Scheduling.FinishDate"),
            
            // People
            CreatedBy = GetCreatedByName(fields),
            ChangedBy = GetPersonName(fields, "System.ChangedBy"),
            ResolvedBy = GetPersonName(fields, "Microsoft.VSTS.Common.ResolvedBy"),
            ClosedBy = GetPersonName(fields, "Microsoft.VSTS.Common.ClosedBy"),
            ActivatedBy = GetPersonName(fields, "Microsoft.VSTS.Common.ActivatedBy"),
            
            // Organization
            AreaPath = GetFieldValue<string>(fields, "System.AreaPath"),
            IterationPath = GetFieldValue<string>(fields, "System.IterationPath"),
            TeamProject = GetFieldValue<string>(fields, "System.TeamProject"),
            
            // Priority & Severity
            Priority = GetFieldValue<int?>(fields, "Microsoft.VSTS.Common.Priority"),
            Severity = GetFieldValue<string>(fields, "Microsoft.VSTS.Common.Severity"),
            Risk = GetFieldValue<string>(fields, "Microsoft.VSTS.Common.Risk"),
            
            // Effort & Planning
            StoryPoints = GetNullableFieldValue<double>(fields, "Microsoft.VSTS.Scheduling.StoryPoints"),
            Effort = GetNullableFieldValue<double>(fields, "Microsoft.VSTS.Scheduling.Effort"),
            OriginalEstimate = GetNullableFieldValue<double>(fields, "Microsoft.VSTS.Scheduling.OriginalEstimate"),
            RemainingWork = GetNullableFieldValue<double>(fields, "Microsoft.VSTS.Scheduling.RemainingWork"),
            CompletedWork = GetNullableFieldValue<double>(fields, "Microsoft.VSTS.Scheduling.CompletedWork"),
            BusinessValue = GetFieldValue<int?>(fields, "Microsoft.VSTS.Common.BusinessValue"),
            TimeCriticality = GetFieldValue<int?>(fields, "Microsoft.VSTS.Common.TimeCriticality"),
            
            // Process Fields
            Reason = GetFieldValue<string>(fields, "System.Reason"),
            Activity = GetFieldValue<string>(fields, "Microsoft.VSTS.Common.Activity"),
            ValueArea = GetFieldValue<string>(fields, "Microsoft.VSTS.Common.ValueArea"),
            
            // Content
            Description = GetFieldValue<string>(fields, "System.Description"),
            AcceptanceCriteria = GetFieldValue<string>(fields, "Microsoft.VSTS.Common.AcceptanceCriteria"),
            ReproSteps = GetFieldValue<string>(fields, "Microsoft.VSTS.TCM.ReproSteps"),
            SystemInfo = GetFieldValue<string>(fields, "Microsoft.VSTS.TCM.SystemInfo"),
            
            // Metadata
            Tags = ParseTags(GetFieldValue<string>(fields, "System.Tags")),
            CommentCount = GetFieldValue<int>(fields, "System.CommentCount"),
            RelationCount = relations.Count,
            
            // Relationships
            ParentWorkItemId = parentRelation != null ? ExtractWorkItemIdFromUrl(parentRelation.Url) : null,
            ParentWorkItemTitle = parentRelation?.Attributes?.GetValueOrDefault("name")?.ToString(),
            ChildWorkItemsCount = childRelations.Count,
            RelatedWorkItemsCount = relatedRelations.Count,
            LinkedPullRequestsCount = prRelations.Count,
            
            // URLs
            Url = detail.Url,
            WebUrl = GetFieldValue<string>(fields, "System.WebUrl") ?? 
                     $"{_settings.OrganizationUrl.TrimEnd('/')}/_workitems/edit/{detail.Id}"
        };
    }
    
    private string? ExtractWorkItemIdFromUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        var parts = url.Split('/');
        return parts.Length > 0 ? parts[^1] : null;
    }
    
    private T? GetNullableFieldValue<T>(Dictionary<string, object> fields, string fieldName) where T : struct
    {
        if (fields.TryGetValue(fieldName, out var value) && value != null)
        {
            try
            {
                if (value is JsonElement jsonElement)
                {
                    if (jsonElement.ValueKind == JsonValueKind.Null) return null;
                    return jsonElement.Deserialize<T>();
                }
                return (T?)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return null;
            }
        }
        return null;
    }
    
    private string? GetPersonName(Dictionary<string, object> fields, string fieldName)
    {
        if (fields.TryGetValue(fieldName, out var value))
        {
            try
            {
                if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
                {
                    if (jsonElement.TryGetProperty("displayName", out var displayName))
                    {
                        return displayName.GetString();
                    }
                }
                else if (value is string str)
                {
                    return str;
                }
            }
            catch { }
        }
        return null;
    }

    private T? GetFieldValue<T>(Dictionary<string, object> fields, string fieldName)
    {
        if (fields.TryGetValue(fieldName, out var value))
        {
            try
            {
                if (value is JsonElement jsonElement)
                {
                    return jsonElement.Deserialize<T>();
                }
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return default;
            }
        }
        return default;
    }

    private string? GetAssignedToName(Dictionary<string, object> fields)
    {
        if (fields.TryGetValue("System.AssignedTo", out var value))
        {
            try
            {
                if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
                {
                    if (jsonElement.TryGetProperty("uniqueName", out var uniqueName))
                    {
                        return uniqueName.GetString();
                    }
                }
            }
            catch { }
        }
        return null;
    }

    private string? GetAssignedToDisplayName(Dictionary<string, object> fields)
    {
        if (fields.TryGetValue("System.AssignedTo", out var value))
        {
            try
            {
                if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
                {
                    if (jsonElement.TryGetProperty("displayName", out var displayName))
                    {
                        return displayName.GetString();
                    }
                }
            }
            catch { }
        }
        return null;
    }

    private string? GetAssignedToAvatar(Dictionary<string, object> fields)
    {
        if (fields.TryGetValue("System.AssignedTo", out var value))
        {
            try
            {
                if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
                {
                    if (jsonElement.TryGetProperty("imageUrl", out var imageUrl))
                    {
                        return imageUrl.GetString();
                    }
                }
            }
            catch { }
        }
        return null;
    }

    private string? GetCreatedByName(Dictionary<string, object> fields)
    {
        if (fields.TryGetValue("System.CreatedBy", out var value))
        {
            try
            {
                if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
                {
                    if (jsonElement.TryGetProperty("displayName", out var displayName))
                    {
                        return displayName.GetString();
                    }
                }
            }
            catch { }
        }
        return null;
    }

    private List<string> ParseTags(string? tagsString)
    {
        if (string.IsNullOrWhiteSpace(tagsString))
        {
            return new List<string>();
        }

        return tagsString.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();
    }

    // ============================================
    // DEVELOPER INACTIVITY DETECTION
    // ============================================

    public async Task<DeveloperInactivityReport> GetDeveloperInactivityAsync(string projectId)
    {
        try
        {
            // 1. Fetch all work items for the project (not just active ones — we need the full picture)
            var allWorkItems = await GetWorkItemsAsync(projectId);

            // 2. Get the project name
            var projects = await GetProjectsAsync();
            var project = projects.FirstOrDefault(p => p.Id == projectId || p.Name == projectId);
            var projectName = project?.Name ?? projectId;

            // 3. Group work items by AssignedTo (developer)
            var developerGroups = allWorkItems
                .Where(wi => !string.IsNullOrEmpty(wi.AssignedToDisplayName))
                .GroupBy(wi => wi.AssignedToDisplayName!)
                .ToList();

            var profiles = new List<DeveloperActivityProfile>();

            foreach (var group in developerGroups)
            {
                var devName = group.Key;
                var devItems = group.ToList();
                var avatarUrl = devItems.FirstOrDefault()?.AssignedToAvatarUrl;

                var profile = ComputeInactivityProfile(devName, avatarUrl, devItems);
                profiles.Add(profile);
            }

            // Sort by inactivity score (highest = most inactive first)
            profiles = profiles.OrderByDescending(p => p.InactivityScore).ToList();

            var report = new DeveloperInactivityReport
            {
                ProjectId = projectId,
                ProjectName = projectName,
                GeneratedAt = DateTime.UtcNow,
                TotalDevelopers = profiles.Count,
                ActiveCount = profiles.Count(p => p.InactivityScore <= 20),
                AtRiskCount = profiles.Count(p => p.InactivityScore >= 61 && p.InactivityScore <= 80),
                InactiveCount = profiles.Count(p => p.InactivityScore >= 81),
                Developers = profiles
            };

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error computing developer inactivity for project: {projectId}");
            throw;
        }
    }

    private DeveloperActivityProfile ComputeInactivityProfile(
        string developerName, string? avatarUrl, List<AzureDevOpsWorkItem> assignedItems)
    {
        var now = DateTime.UtcNow;
        var reasons = new List<string>();

        // --- Categorize work items ---
        var activeStates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Active", "In Progress", "Committed", "Doing" };
        var closedStates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Closed", "Done", "Resolved", "Completed", "Removed" };
        var newStates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "New", "To Do", "Proposed" };

        var activeItems = assignedItems.Where(wi => activeStates.Contains(wi.State)).ToList();
        var newItems = assignedItems.Where(wi => newStates.Contains(wi.State)).ToList();
        var closedItems = assignedItems.Where(wi => closedStates.Contains(wi.State)).ToList();
        var inProgressItems = activeItems.Concat(newItems).ToList();

        // --- Signal 1: Days since last ADO activity (30% weight) ---
        var lastUpdate = assignedItems
            .Select(wi => wi.ChangedDate)
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();

        double daysSinceLastUpdate = (now - lastUpdate).TotalDays;
        double lastUpdateScore;

        if (daysSinceLastUpdate <= 1) lastUpdateScore = 0;
        else if (daysSinceLastUpdate <= 3) lastUpdateScore = 15;
        else if (daysSinceLastUpdate <= 7) lastUpdateScore = 35;
        else if (daysSinceLastUpdate <= 14) lastUpdateScore = 60;
        else if (daysSinceLastUpdate <= 21) lastUpdateScore = 80;
        else lastUpdateScore = 100;

        // --- Signal 2: Expected workload duration vs elapsed time (25% weight) ---
        // Story Points → Expected Work Days (1 SP ≈ 1.5 work days)
        double totalStoryPoints = 0;
        double totalEffort = 0;

        foreach (var item in inProgressItems)
        {
            if (item.StoryPoints.HasValue && item.StoryPoints.Value > 0)
            {
                totalStoryPoints += item.StoryPoints.Value;
            }
            else if (item.Effort.HasValue && item.Effort.Value > 0)
            {
                totalEffort += item.Effort.Value;
            }
            else
            {
                // Heuristic fallback: estimate based on child items and type
                totalStoryPoints += EstimateStoryPoints(item);
            }
        }

        // Convert to expected work days
        double expectedWorkDays = (totalStoryPoints * 1.5) + (totalEffort / 8.0);
        if (expectedWorkDays < 1 && inProgressItems.Count > 0)
            expectedWorkDays = inProgressItems.Count * 2; // Default: 2 days per item

        // How long have they been working on current items?
        var oldestActiveStart = inProgressItems
            .Select(wi => wi.ActivatedDate ?? wi.StateChangeDate ?? wi.CreatedDate)
            .DefaultIfEmpty(now)
            .Min();
        double daysElapsed = (now - oldestActiveStart).TotalDays;

        // workloadScore: if within expected window → low score; if way past → high score
        double workloadScore;
        if (expectedWorkDays <= 0)
        {
            workloadScore = inProgressItems.Count == 0 ? 80 : 50; // No items = high inactivity signal
        }
        else
        {
            double ratio = daysElapsed / expectedWorkDays;
            if (ratio <= 0.5) workloadScore = 0;        // Well within expected time
            else if (ratio <= 1.0) workloadScore = 15;   // On track
            else if (ratio <= 1.5) workloadScore = 40;   // Slightly overdue
            else if (ratio <= 2.0) workloadScore = 65;   // Overdue
            else workloadScore = 90;                      // Significantly overdue
        }

        // --- Signal 3: Work item state currency (20% weight) ---
        double stateScore;
        if (activeItems.Count > 0)
        {
            stateScore = 0; // Has active/in-progress items — good
            reasons.Add($"{activeItems.Count} item(s) actively in progress");
        }
        else if (newItems.Count > 0)
        {
            // Items in New state — how long have they been sitting?
            var oldestNew = newItems.Min(wi => wi.CreatedDate);
            double daysInNew = (now - oldestNew).TotalDays;

            if (daysInNew <= 3) stateScore = 20;
            else if (daysInNew <= 7) stateScore = 45;
            else if (daysInNew <= 14) stateScore = 70;
            else stateScore = 90;

            reasons.Add($"{newItems.Count} item(s) stuck in 'New' for {daysInNew:F0} days");
        }
        else if (closedItems.Count > 0 && inProgressItems.Count == 0)
        {
            // All items closed, nothing active
            var lastClosed = closedItems.Max(wi => wi.ClosedDate ?? wi.ChangedDate);
            double daysSinceClosed = (now - lastClosed).TotalDays;

            if (daysSinceClosed <= 3) stateScore = 10;  // Recently completed
            else if (daysSinceClosed <= 7) stateScore = 40;
            else stateScore = 70;

            reasons.Add($"All items closed; last closure {daysSinceClosed:F0} days ago — may need new assignments");
        }
        else
        {
            stateScore = 85;
            reasons.Add("No active or new work items found");
        }

        // --- Signal 4: Work item update cadence (15% weight) ---
        // How many of their items have been updated in the last 7 days?
        var recentlyUpdated = assignedItems.Count(wi => (now - wi.ChangedDate).TotalDays <= 7);
        double cadenceRatio = assignedItems.Count > 0
            ? (double)recentlyUpdated / assignedItems.Count
            : 0;

        double cadenceScore;
        if (cadenceRatio >= 0.5) cadenceScore = 0;
        else if (cadenceRatio >= 0.25) cadenceScore = 30;
        else if (cadenceRatio > 0) cadenceScore = 60;
        else cadenceScore = 90;

        // --- Signal 5: Has any assigned items at all? (10% weight) ---
        double assignmentScore;
        if (inProgressItems.Count >= 2)
        {
            assignmentScore = 0;
        }
        else if (inProgressItems.Count == 1)
        {
            assignmentScore = 10;
        }
        else if (assignedItems.Count > 0)
        {
            assignmentScore = 40; // Has items but none in progress
        }
        else
        {
            assignmentScore = 100; // No items at all
        }

        // --- Weighted final score ---
        double rawScore = (lastUpdateScore * 0.30)
                        + (workloadScore * 0.25)
                        + (stateScore * 0.20)
                        + (cadenceScore * 0.15)
                        + (assignmentScore * 0.10);

        // --- STORY SIZE ADJUSTMENT ---
        // Large stories get a grace period — reduce score if currently working on big items
        if (activeItems.Count > 0 && totalStoryPoints >= 8)
        {
            // Significant reduction for large stories
            double sizeReduction = Math.Min(25, totalStoryPoints * 1.5);
            rawScore = Math.Max(0, rawScore - sizeReduction);
            reasons.Add($"Working on {totalStoryPoints:F0} story points — large story grace applied");
        }
        else if (activeItems.Count > 0 && totalStoryPoints >= 5)
        {
            double sizeReduction = Math.Min(15, totalStoryPoints * 1.2);
            rawScore = Math.Max(0, rawScore - sizeReduction);
            reasons.Add($"Working on {totalStoryPoints:F0} story points — medium story grace applied");
        }

        int finalScore = Math.Clamp((int)Math.Round(rawScore), 0, 100);

        // Build reasoning
        if (daysSinceLastUpdate <= 1)
            reasons.Insert(0, "Updated work items within the last day");
        else
            reasons.Insert(0, $"Last work item update was {daysSinceLastUpdate:F0} days ago");

        if (expectedWorkDays > 0 && activeItems.Count > 0)
            reasons.Add($"Estimated {expectedWorkDays:F1} work days for current items ({daysElapsed:F0} elapsed)");

        string level = finalScore switch
        {
            <= 20 => "Active",
            <= 40 => "Low Risk",
            <= 60 => "Moderate",
            <= 80 => "At Risk",
            _ => "Inactive"
        };

        return new DeveloperActivityProfile
        {
            DeveloperName = developerName,
            AvatarUrl = avatarUrl,
            InactivityScore = finalScore,
            InactivityLevel = level,
            AssignedItems = assignedItems,
            TotalStoryPoints = (int)totalStoryPoints,
            EstimatedWorkDaysRemaining = Math.Max(0, expectedWorkDays - daysElapsed),
            LastWorkItemUpdate = lastUpdate == DateTime.MinValue ? null : lastUpdate,
            ActiveItemCount = activeItems.Count,
            TotalItemCount = assignedItems.Count,
            Reasoning = string.Join(". ", reasons) + "."
        };
    }

    /// <summary>
    /// Heuristic to estimate story points when none are set on a work item
    /// </summary>
    private double EstimateStoryPoints(AzureDevOpsWorkItem item)
    {
        // If it has child work items, it's likely larger
        if (item.ChildWorkItemsCount >= 5) return 13;
        if (item.ChildWorkItemsCount >= 3) return 8;
        if (item.ChildWorkItemsCount >= 1) return 5;

        // Estimate by type
        var type = item.WorkItemType?.ToLowerInvariant() ?? "";
        if (type.Contains("epic")) return 13;
        if (type.Contains("feature")) return 8;
        if (type.Contains("bug")) return 3;
        if (type.Contains("task")) return 2;

        // Default: medium
        return 3;
    }
}


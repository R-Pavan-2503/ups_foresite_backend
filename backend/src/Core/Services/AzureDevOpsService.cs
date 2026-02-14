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
}

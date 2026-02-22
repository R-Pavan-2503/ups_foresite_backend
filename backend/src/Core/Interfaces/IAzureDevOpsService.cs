using CodeFamily.Api.Core.Models;

namespace CodeFamily.Api.Core.Interfaces;

public interface IAzureDevOpsService
{
    Task<List<AzureDevOpsProject>> GetProjectsAsync();
    Task<List<AzureDevOpsTeam>> GetTeamsAsync(string projectId);
    Task<List<AzureDevOpsWorkItem>> GetWorkItemsAsync(string projectId, string? teamId = null, string? state = null);
    Task<AzureDevOpsWorkItem?> GetWorkItemDetailsAsync(int workItemId);
    Task<DeveloperInactivityReport> GetDeveloperInactivityAsync(string projectId);
}

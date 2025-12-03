using CodeFamily.Api.Core.Interfaces;
using LibGit2Sharp;
using Microsoft.Extensions.Options;
using CodeFamily.Api.Core.Models;

namespace CodeFamily.Api.Core.Services;

/// <summary>
/// LibGit2Sharp repository management service.
/// Handles bare cloning and historical file access.
/// </summary>
public class RepositoryService : IRepositoryService
{
    private readonly string _cloneBasePath;

    public RepositoryService(IOptions<AppSettings> appSettings)
    {
        _cloneBasePath = appSettings.Value.CloneBasePath;
        Directory.CreateDirectory(_cloneBasePath);
    }

    public async Task<string> CloneBareRepository(string cloneUrl, string owner, string repoName)
    {
        var repoPath = Path.Combine(_cloneBasePath, $"{owner}_{repoName}.git");

        if (Directory.Exists(repoPath))
        {
            return repoPath; // Already cloned
        }

        await Task.Run(() =>
        {
            LibGit2Sharp.Repository.Clone(cloneUrl, repoPath, new CloneOptions
            {
                IsBare = true
            });
        });

        return repoPath;
    }
using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LibGit2Sharp;

using LibGitRepository = LibGit2Sharp.Repository;
using LibGitCommit = LibGit2Sharp.Commit;
using DbCommit = CodeFamily.Api.Core.Models.Commit;
using DbRepository = CodeFamily.Api.Core.Models.Repository;

namespace CodeFamily.Api.Core.Services;

/// <summary>
/// ENHANCED Analysis Service implementing COMPLETE ingestion pipeline.
/// </summary>
public class AnalysisService : IAnalysisService
{
    private readonly IDatabaseService _db;
    private readonly IRepositoryService _repoService;
    private readonly ITreeSitterService _treeSitter;
    private readonly IGeminiService _gemini;
    private readonly IGitHubService _github;
    private readonly ILogger<AnalysisService> _logger;

    // Track author contributions per file for ownership calculation
    private readonly Dictionary<Guid, Dictionary<string, List<double>>> _fileAuthorDeltas = new();

    public AnalysisService(
        IDatabaseService db,
        IRepositoryService repoService,
        ITreeSitterService treeSitter,
        IGeminiService gemini,
        IGitHubService github,
        ILogger<AnalysisService> logger)
    {
        _db = db;
        _repoService = repoService;
        _treeSitter = treeSitter;
        _gemini = gemini;
        _github = github;
        _logger = logger;
    }
}
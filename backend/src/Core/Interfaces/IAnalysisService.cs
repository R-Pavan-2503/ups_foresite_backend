using CodeFamily.Api.Core.Models;

namespace CodeFamily.Api.Core.Interfaces;

public interface IAnalysisService
{
    /// <summary>
    /// Run full ingestion pipeline for a repository.
    /// This is the CORE analysis workflow:
    /// 1. Bare clone
    /// 2. Walk commits
    /// 3. Extract functions (Tree-sitter)
    /// 4. Generate embeddings (Gemini)
    /// 5. Calculate semantic ownership
    /// 6. Attach webhook
    /// </summary>
    Task AnalyzeRepository(string owner, string repoName, Guid repositoryId, Guid userId);

    /// <summary>
    /// Calculate semantic ownership for a file based on embedding deltas.
    /// NOT based on lines of code.
    /// </summary>
    Task CalculateSemanticOwnership(Guid fileId, Guid repositoryId);

    /// <summary>
    /// Calculate risk score between a push and open PRs.
    /// Risk = (Structural Overlap × 0.4) + (Semantic Overlap × 0.6)
    /// </summary>
    Task<RiskAnalysisResult> CalculateRisk(Guid repositoryId, List<string> changedFiles, List<float[]> newEmbeddings);

    /// <summary>
    /// Process incremental update from webhook.
    /// Only analyze changed files.
    /// </summary>
    Task ProcessIncrementalUpdate(Guid repositoryId, string commitSha, List<string> changedFiles);
}

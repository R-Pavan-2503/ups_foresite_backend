using CodeFamily.Api.Core.Models;

namespace CodeFamily.Api.Core.Interfaces;

public interface IAnalysisService
{

    Task AnalyzeRepository(string owner, string repoName, Guid repositoryId, Guid userId);


    Task CalculateSemanticOwnership(Guid fileId, Guid repositoryId);


    Task<RiskAnalysisResult> CalculateRisk(Guid repositoryId, List<string> changedFiles, List<float[]> newEmbeddings);


    Task ProcessIncrementalUpdate(Guid repositoryId, string commitSha, List<string> changedFiles);
}

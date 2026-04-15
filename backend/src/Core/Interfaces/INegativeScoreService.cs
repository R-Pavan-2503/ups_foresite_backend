using CodeFamily.Api.Core.Models;

namespace CodeFamily.Api.Core.Interfaces;

/// <summary>
/// Service for calculating and managing Contributor Negative Scores.
/// The negative score tracks how often a developer's code caused problems
/// that had to be fixed or rewritten by someone else.
/// </summary>
public interface INegativeScoreService
{
    /// <summary>
    /// Detect code replacement events and calculate negative scores for all contributors in a repository.
    /// This scans the file history looking for patterns where:
    /// - Code was significantly altered (low semantic similarity)
    /// - By a different author
    /// - Within a short time window
    /// - With fix/bug-related commit messages
    /// </summary>
    Task CalculateNegativeScoresForRepository(Guid repositoryId);

    /// <summary>
    /// Get all contributor negative scores for a repository.
    /// </summary>
    Task<List<ContributorNegativeScore>> GetScoresForRepository(Guid repositoryId);

    /// <summary>
    /// Get replacement events for a specific contributor.
    /// </summary>
    Task<List<CodeReplacementEvent>> GetEventsForContributor(Guid repositoryId, string contributorName);
}

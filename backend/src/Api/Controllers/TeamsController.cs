using Microsoft.AspNetCore.Mvc;
using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;

namespace CodeFamily.Api.Controllers;

[ApiController]
[Route("repositories/{repositoryId}/teams")]
public class TeamsController : ControllerBase
{
    private readonly IDatabaseService _db;
    private readonly ILogger<TeamsController> _logger;

    public TeamsController(IDatabaseService db, ILogger<TeamsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ============================================
    // TEAMS
    // ============================================

    [HttpGet]
    public async Task<IActionResult> GetTeams(Guid repositoryId, [FromQuery] Guid? userId = null)
    {
        try
        {
        // Check if user is admin or owner
        var isAdmin = false;
        if (userId.HasValue)
        {
            var repo = await _db.GetRepositoryById(repositoryId);
            isAdmin = await _db.IsRepoAdmin(userId.Value, repositoryId) || (repo != null && repo.ConnectedByUserId == userId.Value);
        }

            var teams = await _db.GetTeamsByRepository(repositoryId);
            var result = new List<TeamWithMembersDto>();

            foreach (var team in teams)
            {
                var members = await _db.GetTeamMembers(team.Id);
                
                // Get user details for team members
                var memberDtos = new List<TeamMemberDto>();
                foreach (var member in members)
                {
                    var user = await _db.GetUserById(member.UserId);
                    var assignedBy = member.AssignedByUserId.HasValue
                        ? await _db.GetUserById(member.AssignedByUserId.Value)
                        : null;

                    memberDtos.Add(new TeamMemberDto
                    {
                        Id = member.Id,
                        TeamId = member.TeamId,
                        UserId = member.UserId,
                        Username = user?.AuthorName ?? "Unknown",
                        AvatarUrl = user?.AvatarUrl,
                        Email = user?.Email,
                        Role = member.Role,
                        AssignedByUserId = member.AssignedByUserId,
                        AssignedByUsername = assignedBy?.AuthorName,
                        CreatedAt = member.CreatedAt,
                        UpdatedAt = member.UpdatedAt
                    });
                }

                var createdBy = team.CreatedByUserId.HasValue
                    ? await _db.GetUserById(team.CreatedByUserId.Value)
                    : null;

                result.Add(new TeamWithMembersDto
                {
                    Id = team.Id,
                    Name = team.Name,
                    RepositoryId = team.RepositoryId,
                    CreatedByUserId = team.CreatedByUserId,
                    CreatedByUsername = createdBy?.AuthorName,
                    CreatedByAvatarUrl = createdBy?.AvatarUrl,
                    Members = memberDtos,
                    CreatedAt = team.CreatedAt,
                    UpdatedAt = team.UpdatedAt
                });
            }

            return Ok(new { teams = result, isAdmin });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting teams: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateTeam(
        Guid repositoryId,
        [FromBody] CreateTeamRequest request,
        [FromQuery] Guid userId)
    {
        try
        {
            // Check if user is admin or owner
            var repo = await _db.GetRepositoryById(repositoryId);
            var isAdmin = await _db.IsRepoAdmin(userId, repositoryId) || (repo != null && repo.ConnectedByUserId == userId);
            if (!isAdmin)
            {
                return StatusCode(403, new { error = "Only repository owners and admins can create teams" });
            }

            var team = await _db.CreateTeam(new Team
            {
                Name = request.Name,
                RepositoryId = repositoryId,
                CreatedByUserId = userId
            });

            return Ok(team);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating team: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{teamId}")]
    public async Task<IActionResult> UpdateTeam(
        Guid repositoryId,
        Guid teamId,
        [FromBody] UpdateTeamRequest request,
        [FromQuery] Guid userId)
    {
        try
        {
            // Check if user is admin or owner
            var repo = await _db.GetRepositoryById(repositoryId);
            var isAdmin = await _db.IsRepoAdmin(userId, repositoryId) || (repo != null && repo.ConnectedByUserId == userId);
            if (!isAdmin)
            {
                return StatusCode(403, new { error = "Only repository owners and admins can update teams" });
            }

            await _db.UpdateTeamName(teamId, request.Name);
            return Ok(new { message = "Team updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error updating team: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{teamId}")]
    public async Task<IActionResult> DeleteTeam(
        Guid repositoryId,
        Guid teamId,
        [FromQuery] Guid userId)
    {
        try
        {
            // Check if user is admin or owner
            var repo = await _db.GetRepositoryById(repositoryId);
            var isAdmin = await _db.IsRepoAdmin(userId, repositoryId) || (repo != null && repo.ConnectedByUserId == userId);
            if (!isAdmin)
            {
                return StatusCode(403, new { error = "Only repository owners and admins can delete teams" });
            }

            await _db.DeleteTeam(teamId);
            return Ok(new { message = "Team deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error deleting team: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    // ============================================
    // TEAM MEMBERS
    // ============================================

    [HttpPost("{teamId}/members")]
    public async Task<IActionResult> AddTeamMember(
        Guid repositoryId,
        Guid teamId,
        [FromBody] AddTeamMemberRequest request,
        [FromQuery] Guid userId)
    {
        try
        {
            // Check if user is admin or owner
            var repo = await _db.GetRepositoryById(repositoryId);
            var isAdmin = await _db.IsRepoAdmin(userId, repositoryId) || (repo != null && repo.ConnectedByUserId == userId);
            if (!isAdmin)
            {
                return StatusCode(403, new { error = "Only repository owners and admins can add team members" });
            }

            // Check if user already in team
            var alreadyInTeam = await _db.IsUserInTeam(request.UserId, teamId);
            if (alreadyInTeam)
            {
                return BadRequest(new { error = "User is already a member of this team" });
            }

            var member = await _db.AddTeamMember(new TeamMember
            {
                TeamId = teamId,
                UserId = request.UserId,
                Role = request.Role,
                AssignedByUserId = userId
            });

            return Ok(member);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error adding team member: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{teamId}/members/{memberId}/role")]
    public async Task<IActionResult> UpdateMemberRole(
        Guid repositoryId,
        Guid teamId,
        Guid memberId,
        [FromBody] UpdateTeamMemberRoleRequest request,
        [FromQuery] Guid userId)
    {
        try
        {
            // Check if user is admin or owner
            var repo = await _db.GetRepositoryById(repositoryId);
            var isAdmin = await _db.IsRepoAdmin(userId, repositoryId) || (repo != null && repo.ConnectedByUserId == userId);
            if (!isAdmin)
            {
                return StatusCode(403, new { error = "Only repository owners and admins can update team member roles" });
            }

            await _db.UpdateTeamMemberRole(memberId, request.Role);
            return Ok(new { message = "Member role updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error updating member role: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{teamId}/members/{memberId}")]
    public async Task<IActionResult> RemoveTeamMember(
        Guid repositoryId,
        Guid teamId,
        Guid memberId,
        [FromQuery] Guid userId)
    {
        try
        {
            // Check if user is admin or owner
            var repo = await _db.GetRepositoryById(repositoryId);
            var isAdmin = await _db.IsRepoAdmin(userId, repositoryId) || (repo != null && repo.ConnectedByUserId == userId);
            if (!isAdmin)
            {
                return StatusCode(403, new { error = "Only repository owners and admins can remove team members" });
            }

            await _db.RemoveTeamMember(memberId);
            return Ok(new { message = "Member removed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error removing team member: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    // ============================================
    // TEAM ANALYTICS
    // ============================================

    [HttpGet("{teamId}/analytics")]
    public async Task<IActionResult> GetTeamAnalytics(
        Guid repositoryId,
        Guid teamId,
        [FromQuery] Guid? userId = null,
        [FromQuery] Guid? memberId = null)
    {
        try
        {
            // Get team and members
            var team = await _db.GetTeamById(teamId);
            if (team == null)
            {
                return NotFound(new { error = "Team not found" });
            }

            var members = await _db.GetTeamMembers(teamId);
            if (members.Count == 0)
            {
                return Ok(new
                {
                    teamId,
                    teamName = team.Name,
                    message = "No members in this team yet"
                });
            }

            // If memberId is provided, return individual analytics
            if (memberId.HasValue)
            {
                var member = members.FirstOrDefault(m => m.UserId == memberId.Value);
                if (member == null)
                {
                    return NotFound(new { error = "Member not found in this team" });
                }

                var individualAnalytics = await GetIndividualMemberAnalytics(repositoryId, member, team.Name);
                return Ok(individualAnalytics);
            }

            // Otherwise, return team analytics
            var teamAnalytics = await GetTeamContributionAnalytics(repositoryId, team, members);
            return Ok(teamAnalytics);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting team analytics: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    private async Task<TeamContributionAnalyticsDto> GetTeamContributionAnalytics(
        Guid repositoryId,
        Team team,
        List<TeamMember> members)
    {
        var memberUserIds = members.Select(m => m.UserId).ToList();
        var commits = await _db.GetCommitsByRepository(repositoryId);
        
        // Filter commits by team members
        var teamCommits = commits.Where(c => 
            !string.IsNullOrEmpty(c.AuthorName) && 
            memberUserIds.Any(uid => 
            {
                var user = _db.GetUserById(uid).Result;
                return user?.AuthorName == c.AuthorName;
            })
        ).ToList();

        // Get all commit IDs for file changes
        var commitIds = teamCommits.Select(c => c.Id).ToList();
        var fileChangesDict = await _db.GetFileChangesByCommitIds(commitIds);

        // Calculate member contributions
        var memberContributions = new List<MemberContributionDto>();
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

        foreach (var member in members)
        {
            var user = await _db.GetUserById(member.UserId);
            if (user == null) continue;

            var memberCommits = teamCommits.Where(c => c.AuthorName == user.AuthorName).ToList();
            var memberCommitIds = memberCommits.Select(c => c.Id).ToList();

            int linesAdded = 0, linesRemoved = 0;
            var filesChanged = new HashSet<Guid>();

            foreach (var commitId in memberCommitIds)
            {
                if (fileChangesDict.TryGetValue(commitId, out var changes))
                {
                    foreach (var change in changes)
                    {
                        linesAdded += change.Additions ?? 0;
                        linesRemoved += change.Deletions ?? 0;
                        filesChanged.Add(change.FileId);
                    }
                }
            }

            var lastCommit = memberCommits.OrderByDescending(c => c.CommittedAt).FirstOrDefault();

            memberContributions.Add(new MemberContributionDto
            {
                UserId = member.UserId,
                Username = user.AuthorName,
                AvatarUrl = user.AvatarUrl,
                Role = member.Role,
                TotalCommits = memberCommits.Count,
                LinesAdded = linesAdded,
                LinesRemoved = linesRemoved,
                FilesChanged = filesChanged.Count,
                LastCommitDate = lastCommit?.CommittedAt,
                IsActive = lastCommit != null && lastCommit.CommittedAt >= sevenDaysAgo
            });
        }

        // Activity timeline (last 30 days)
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var recentCommits = teamCommits.Where(c => c.CommittedAt >= thirtyDaysAgo);
        var timeline = recentCommits
            .GroupBy(c => c.CommittedAt.Date)
            .Select(g =>
            {
                var dayCommitIds = g.Select(c => c.Id).ToList();
                int dailyAdded = 0, dailyRemoved = 0;
                
                foreach (var cid in dayCommitIds)
                {
                    if (fileChangesDict.TryGetValue(cid, out var changes))
                    {
                        dailyAdded += changes.Sum(ch => ch.Additions ?? 0);
                        dailyRemoved += changes.Sum(ch => ch.Deletions ?? 0);
                    }
                }

                return new ActivityTimelineDto
                {
                    Date = g.Key.ToString("yyyy-MM-dd"),
                    Commits = g.Count(),
                    LinesAdded = dailyAdded,
                    LinesRemoved = dailyRemoved
                };
            })
            .OrderBy(x => x.Date)
            .ToList();

        // Hotspots - most changed files
        var fileChangeCount = new Dictionary<Guid, int>();
        var fileContributors = new Dictionary<Guid, HashSet<string>>();

        foreach (var kvp in fileChangesDict)
        {
            var commit = teamCommits.FirstOrDefault(c => c.Id == kvp.Key);
            if (commit == null) continue;

            foreach (var change in kvp.Value)
            {
                if (!fileChangeCount.ContainsKey(change.FileId))
                {
                    fileChangeCount[change.FileId] = 0;
                    fileContributors[change.FileId] = new HashSet<string>();
                }
                fileChangeCount[change.FileId]++;
                fileContributors[change.FileId].Add(commit.AuthorName ?? "Unknown");
            }
        }

        var files = await _db.GetFilesByRepository(repositoryId);
        var hotspots = fileChangeCount
            .OrderByDescending(kvp => kvp.Value)
            .Take(10)
            .Select(kvp =>
            {
                var file = files.FirstOrDefault(f => f.Id == kvp.Key);
                return new HotspotFileDto
                {
                    FileId = kvp.Key,
                    FilePath = file?.FilePath ?? "Unknown",
                    ChangeCount = kvp.Value,
                    Contributors = fileContributors[kvp.Key].ToList()
                };
            })
            .ToList();

        // File type distribution
        var fileTypes = new Dictionary<string, int>();
        foreach (var fileId in fileChangeCount.Keys)
        {
            var file = files.FirstOrDefault(f => f.Id == fileId);
            if (file != null)
            {
                var ext = Path.GetExtension(file.FilePath);
                var fileType = string.IsNullOrEmpty(ext) ? "no-ext" : ext.TrimStart('.');
                fileTypes[fileType] = fileTypes.GetValueOrDefault(fileType, 0) + fileChangeCount[fileId];
            }
        }

        // Most active day
        var dayCommits = teamCommits
            .GroupBy(c => c.CommittedAt.DayOfWeek)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .FirstOrDefault();

        return new TeamContributionAnalyticsDto
        {
            TeamId = team.Id,
            TeamName = team.Name,
            TotalMembers = members.Count,
            TotalCommits = teamCommits.Count,
            TotalLinesAdded = memberContributions.Sum(m => m.LinesAdded),
            TotalLinesRemoved = memberContributions.Sum(m => m.LinesRemoved),
            TotalFilesChanged = fileChangeCount.Count,
            MemberContributions = memberContributions.OrderByDescending(m => m.TotalCommits).ToList(),
            ActivityTimeline = timeline,
            Hotspots = hotspots,
            FileTypeDistribution = fileTypes.OrderByDescending(kvp => kvp.Value).Take(10).ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            MostActiveDay = dayCommits?.Day.ToString() ?? "N/A",
            FirstCommitDate = teamCommits.OrderBy(c => c.CommittedAt).FirstOrDefault()?.CommittedAt,
            LastCommitDate = teamCommits.OrderByDescending(c => c.CommittedAt).FirstOrDefault()?.CommittedAt
        };
    }

    private async Task<IndividualContributionAnalyticsDto> GetIndividualMemberAnalytics(
        Guid repositoryId,
        TeamMember member,
        string teamName)
    {
        var user = await _db.GetUserById(member.UserId);
        if (user == null)
        {
            throw new Exception("User not found");
        }

        var commits = await _db.GetCommitsByRepository(repositoryId);
        var userCommits = commits.Where(c => c.AuthorName == user.AuthorName).ToList();
        var commitIds = userCommits.Select(c => c.Id).ToList();
        var fileChangesDict = await _db.GetFileChangesByCommitIds(commitIds);

        int linesAdded = 0, linesRemoved = 0;
        var filesChanged = new HashSet<Guid>();
        var fileChangeCount = new Dictionary<Guid, int>();

        foreach (var kvp in fileChangesDict)
        {
            foreach (var change in kvp.Value)
            {
                linesAdded += change.Additions ?? 0;
                linesRemoved += change.Deletions ?? 0;
                filesChanged.Add(change.FileId);
                fileChangeCount[change.FileId] = fileChangeCount.GetValueOrDefault(change.FileId, 0) + 1;
            }
        }

        // Timeline (last 30 days)
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var recentCommits = userCommits.Where(c => c.CommittedAt >= thirtyDaysAgo);
        var timeline = recentCommits
            .GroupBy(c => c.CommittedAt.Date)
            .Select(g =>
            {
                var dayCommitIds = g.Select(c => c.Id).ToList();
                int dailyAdded = 0, dailyRemoved = 0;
                
                foreach (var cid in dayCommitIds)
                {
                    if (fileChangesDict.TryGetValue(cid, out var changes))
                    {
                        dailyAdded += changes.Sum(ch => ch.Additions ?? 0);
                        dailyRemoved += changes.Sum(ch => ch.Deletions ?? 0);
                    }
                }

                return new ActivityTimelineDto
                {
                    Date = g.Key.ToString("yyyy-MM-dd"),
                    Commits = g.Count(),
                    LinesAdded = dailyAdded,
                    LinesRemoved = dailyRemoved
                };
            })
            .OrderBy(x => x.Date)
            .ToList();

        // Personal hotspots
        var files = await _db.GetFilesByRepository(repositoryId);
        var hotspots = fileChangeCount
            .OrderByDescending(kvp => kvp.Value)
            .Take(10)
            .Select(kvp =>
            {
                var file = files.FirstOrDefault(f => f.Id == kvp.Key);
                return new HotspotFileDto
                {
                    FileId = kvp.Key,
                    FilePath = file?.FilePath ?? "Unknown",
                    ChangeCount = kvp.Value,
                    Contributors = new List<string> { user.AuthorName }
                };
            })
            .ToList();

        // File type distribution
        var fileTypes = new Dictionary<string, int>();
        foreach (var fileId in fileChangeCount.Keys)
        {
            var file = files.FirstOrDefault(f => f.Id == fileId);
            if (file != null)
            {
                var ext = Path.GetExtension(file.FilePath);
                var fileType = string.IsNullOrEmpty(ext) ? "no-ext" : ext.TrimStart('.');
                fileTypes[fileType] = fileTypes.GetValueOrDefault(fileType, 0) + fileChangeCount[fileId];
            }
        }

        // Most active day
        var dayCommits = userCommits
            .GroupBy(c => c.CommittedAt.DayOfWeek)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .FirstOrDefault();

        // Code ownership (top files by ownership score)
        var fileOwnershipDict = await _db.GetFileOwnershipByFileIds(filesChanged.ToList());
        var ownedFiles = fileOwnershipDict
            .Where(kvp => kvp.Value.Any(o => o.AuthorName == user.AuthorName))
            .Select(kvp =>
            {
                var file = files.FirstOrDefault(f => f.Id == kvp.Key);
                var ownership = kvp.Value.FirstOrDefault(o => o.AuthorName == user.AuthorName);
                return new { FilePath = file?.FilePath, Score = ownership?.SemanticScore ?? 0 };
            })
            .Where(x => x.FilePath != null)
            .OrderByDescending(x => x.Score)
            .Take(10)
            .Select(x => x.FilePath!)
            .ToList();

        // Average commits per day
        var daysSinceFirstCommit = userCommits.Any() 
            ? (DateTime.UtcNow - userCommits.Min(c => c.CommittedAt)).TotalDays 
            : 0;
        var avgCommitsPerDay = daysSinceFirstCommit > 0 ? userCommits.Count / daysSinceFirstCommit : 0;

        return new IndividualContributionAnalyticsDto
        {
            UserId = member.UserId,
            Username = user.AuthorName,
            AvatarUrl = user.AvatarUrl,
            Role = member.Role,
            TeamName = teamName,
            TotalCommits = userCommits.Count,
            LinesAdded = linesAdded,
            LinesRemoved = linesRemoved,
            FilesChanged = filesChanged.Count,
            ActivityTimeline = timeline,
            PersonalHotspots = hotspots,
            FileTypeDistribution = fileTypes.OrderByDescending(kvp => kvp.Value).Take(10).ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            MostActiveDay = dayCommits?.Day.ToString() ?? "N/A",
            FirstCommitDate = userCommits.OrderBy(c => c.CommittedAt).FirstOrDefault()?.CommittedAt,
            LastCommitDate = userCommits.OrderByDescending(c => c.CommittedAt).FirstOrDefault()?.CommittedAt,
            AverageCommitsPerDay = Math.Round(avgCommitsPerDay, 2),
            CodeOwnership = ownedFiles
        };
    }
}


// Separate controller for repository users endpoint
[ApiController]
[Route("repositories/{repositoryId}")]
public class RepositoryUsersController : ControllerBase
{
    private readonly IDatabaseService _db;
    private readonly ILogger<RepositoryUsersController> _logger;

    public RepositoryUsersController(IDatabaseService db, ILogger<RepositoryUsersController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetRepositoryUsers(Guid repositoryId)
    {
        try
        {
            // Get all commits from the repository to find contributors
            var commits = await _db.GetCommitsByRepository(repositoryId);
            var authorNames = commits
                .Where(c => !string.IsNullOrEmpty(c.AuthorName))
                .Select(c => c.AuthorName!)
                .Distinct()
                .ToList();

            // Get all users that have contributed
            var users = await _db.GetUsersByAuthorNames(authorNames);

            var result = users.Select(u => new
            {
                u.Id,
                u.AuthorName,
                u.Email,
                u.AvatarUrl
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting repository users: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }
}

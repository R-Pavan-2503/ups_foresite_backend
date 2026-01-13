using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Text.Json;

namespace CodeFamily.Api.Core.Services;

/// <summary>
/// Database service using raw Npgsql for maximum control.
/// Connects to Supabase PostgreSQL with pgvector support.
/// </summary>
public class DatabaseService : IDatabaseService
{
    private readonly NpgsqlDataSource _dataSource;

    public DatabaseService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    // Users
    public async Task<User?> GetUserByGitHubId(long githubId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand("SELECT id, github_id, author_name, email, avatar_url FROM users WHERE github_id = @githubId", conn);
        cmd.Parameters.AddWithValue("githubId", githubId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new User
            {
                Id = reader.GetGuid(0),
                GithubId = reader.GetInt64(1),
                AuthorName = reader.GetString(2),
                Email = reader.IsDBNull(3) ? null : reader.GetString(3),
                AvatarUrl = reader.IsDBNull(4) ? null : reader.GetString(4)
            };
        }
        return null;
    }

    public async Task<User?> GetUserByEmail(string email)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand("SELECT id, github_id, author_name, email, avatar_url FROM users WHERE email = @email", conn);
        cmd.Parameters.AddWithValue("email", email);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new User
            {
                Id = reader.GetGuid(0),
                GithubId = reader.GetInt64(1),
                AuthorName = reader.GetString(2),
                Email = reader.IsDBNull(3) ? null : reader.GetString(3),
                AvatarUrl = reader.IsDBNull(4) ? null : reader.GetString(4)
            };
        }
        return null;
    }

    public async Task<User?> GetUserByAuthorName(string authorName)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand("SELECT id, github_id, author_name, email, avatar_url FROM users WHERE author_name = @authorName", conn);
        cmd.Parameters.AddWithValue("authorName", authorName);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new User
            {
                Id = reader.GetGuid(0),
                GithubId = reader.GetInt64(1),
                AuthorName = reader.GetString(2),
                Email = reader.IsDBNull(3) ? null : reader.GetString(3),
                AvatarUrl = reader.IsDBNull(4) ? null : reader.GetString(4)
            };
        }
        return null;
    }

    // BATCH: Get multiple users at once by author names
    public async Task<List<User>> GetUsersByAuthorNames(List<string> authorNames)
    {
        if (authorNames == null || !authorNames.Any()) return new List<User>();

        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT id, github_id, author_name, email, avatar_url FROM users WHERE author_name = ANY(@names)",
            conn);

        cmd.Parameters.AddWithValue("names", authorNames.ToArray());

        var users = new List<User>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            users.Add(new User
            {
                Id = reader.GetGuid(0),
                GithubId = reader.GetInt64(1),
                AuthorName = reader.GetString(2),
                Email = reader.IsDBNull(3) ? null : reader.GetString(3),
                AvatarUrl = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }
        return users;
    }

    // Get all users who have access to a repository
    public async Task<List<User>> GetUsersWithRepositoryAccess(Guid repositoryId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            @"SELECT DISTINCT u.id, u.github_id, u.author_name, u.email, u.avatar_url 
              FROM users u
              INNER JOIN repository_user_access rua ON u.id = rua.user_id
              WHERE rua.repository_id = @repositoryId
              ORDER BY u.author_name",
            conn);

        cmd.Parameters.AddWithValue("repositoryId", repositoryId);

        var users = new List<User>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            users.Add(new User
            {
                Id = reader.GetGuid(0),
                GithubId = reader.GetInt64(1),
                AuthorName = reader.GetString(2),
                Email = reader.IsDBNull(3) ? null : reader.GetString(3),
                AvatarUrl = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }
        return users;
    }

    // Get all users in the system
    public async Task<List<User>> GetAllUsers()
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT id, github_id, author_name, email, avatar_url FROM users ORDER BY author_name",
            conn);

        var users = new List<User>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            users.Add(new User
            {
                Id = reader.GetGuid(0),
                GithubId = reader.GetInt64(1),
                AuthorName = reader.GetString(2),
                Email = reader.IsDBNull(3) ? null : reader.GetString(3),
                AvatarUrl = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }
        return users;
    }


    public async Task<User> CreateUser(User user)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "INSERT INTO users (github_id, author_name, email, avatar_url) VALUES (@githubId, @authorName, @email, @avatarUrl) RETURNING id",
            conn);

        cmd.Parameters.AddWithValue("githubId", user.GithubId);
        cmd.Parameters.AddWithValue("authorName", user.AuthorName);
        cmd.Parameters.AddWithValue("email", (object?)user.Email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("avatarUrl", (object?)user.AvatarUrl ?? DBNull.Value);

        user.Id = (Guid)(await cmd.ExecuteScalarAsync())!;
        return user;
    }

    public async Task<User?> GetUserById(Guid id)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand("SELECT id, github_id, author_name, email, avatar_url FROM users WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new User
            {
                Id = reader.GetGuid(0),
                GithubId = reader.GetInt64(1),
                AuthorName = reader.GetString(2),
                Email = reader.IsDBNull(3) ? null : reader.GetString(3),
                AvatarUrl = reader.IsDBNull(4) ? null : reader.GetString(4)
            };
        }
        return null;
    }

    public async Task UpdateUserEmail(Guid userId, string email)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand("UPDATE users SET email = @email WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("email", email);
        cmd.Parameters.AddWithValue("id", userId);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateUserAuthorName(Guid userId, string authorName)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand("UPDATE users SET author_name = @authorName WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("authorName", authorName);
        cmd.Parameters.AddWithValue("id", userId);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateUserAvatar(Guid userId, string avatarUrl)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand("UPDATE users SET avatar_url = @avatarUrl WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("avatarUrl", avatarUrl);
        cmd.Parameters.AddWithValue("id", userId);

        await cmd.ExecuteNonQueryAsync();
    }

    // Repositories
    public async Task<Repository?> GetRepositoryByName(string owner, string name)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT id, name, owner_username, status, is_active_blocking, connected_by_user_id, is_mine, last_analyzed_commit_sha, last_refresh_at FROM repositories WHERE owner_username = @owner AND name = @name",
            conn);

        cmd.Parameters.AddWithValue("owner", owner);
        cmd.Parameters.AddWithValue("name", name);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Repository
            {
                Id = reader.GetGuid(0),
                Name = reader.GetString(1),
                OwnerUsername = reader.GetString(2),
                Status = reader.IsDBNull(3) ? null : reader.GetString(3),
                IsActiveBlocking = reader.GetBoolean(4),
                ConnectedByUserId = reader.IsDBNull(5) ? null : reader.GetGuid(5),
                IsMine = reader.IsDBNull(6) ? false : reader.GetBoolean(6),
                LastAnalyzedCommitSha = reader.IsDBNull(7) ? null : reader.GetString(7),
                LastRefreshAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
            };
        }
        return null;
    }

    public async Task<Repository> CreateRepository(Repository repository)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "INSERT INTO repositories (name, owner_username, status, is_active_blocking, connected_by_user_id, is_mine) VALUES (@name, @owner, @status, @blocking, @userId, @isMine) RETURNING id",
            conn);

        cmd.Parameters.AddWithValue("name", repository.Name);
        cmd.Parameters.AddWithValue("owner", repository.OwnerUsername);
        cmd.Parameters.AddWithValue("status", (object?)repository.Status ?? DBNull.Value);
        cmd.Parameters.AddWithValue("blocking", repository.IsActiveBlocking);
        cmd.Parameters.AddWithValue("userId", (object?)repository.ConnectedByUserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("isMine", repository.IsMine);

        repository.Id = (Guid)(await cmd.ExecuteScalarAsync())!;
        return repository;
    }

    public async Task UpdateRepositoryStatus(Guid repositoryId, string status)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand("UPDATE repositories SET status = @status WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("id", repositoryId);

        await cmd.ExecuteNonQueryAsync();
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateLastRefreshTime(Guid repositoryId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand("UPDATE repositories SET last_refresh_at = @refreshTime WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("refreshTime", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("id", repositoryId);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateLastAnalyzedCommit(Guid repositoryId, string commitSha)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand("UPDATE repositories SET last_analyzed_commit_sha = @commitSha, last_refresh_at = @refreshTime WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("commitSha", commitSha);
        cmd.Parameters.AddWithValue("refreshTime", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("id", repositoryId);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<Repository>> GetUserRepositories(Guid userId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            @"SELECT r.id, r.name, r.owner_username, r.status, r.is_active_blocking, r.connected_by_user_id, r.is_mine, r.last_analyzed_commit_sha, r.last_refresh_at
              FROM repositories r
              INNER JOIN repository_user_access rua ON r.id = rua.repository_id
              WHERE rua.user_id = @userId",
            conn);

        cmd.Parameters.AddWithValue("userId", userId);

        var repositories = new List<Repository>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            repositories.Add(new Repository
            {
                Id = reader.GetGuid(0),
                Name = reader.GetString(1),
                OwnerUsername = reader.GetString(2),
                Status = reader.IsDBNull(3) ? null : reader.GetString(3),
                IsActiveBlocking = reader.GetBoolean(4),
                ConnectedByUserId = reader.IsDBNull(5) ? null : reader.GetGuid(5),
                IsMine = reader.IsDBNull(6) ? false : reader.GetBoolean(6),
                LastAnalyzedCommitSha = reader.IsDBNull(7) ? null : reader.GetString(7),
                LastRefreshAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
            });
        }
        return repositories;
    }

    public async Task<Repository?> GetRepositoryById(Guid id)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT id, name, owner_username, status, is_active_blocking, connected_by_user_id, is_mine, last_analyzed_commit_sha, last_refresh_at FROM repositories WHERE id = @id",
            conn);

        cmd.Parameters.AddWithValue("id", id);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Repository
            {
                Id = reader.GetGuid(0),
                Name = reader.GetString(1),
                OwnerUsername = reader.GetString(2),
                Status = reader.IsDBNull(3) ? null : reader.GetString(3),
                IsActiveBlocking = reader.GetBoolean(4),
                ConnectedByUserId = reader.IsDBNull(5) ? null : reader.GetGuid(5),
                IsMine = reader.IsDBNull(6) ? false : reader.GetBoolean(6),
                LastAnalyzedCommitSha = reader.IsDBNull(7) ? null : reader.GetString(7),
                LastRefreshAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
            };
        }
        return null;
    }

    public async Task DeleteRepository(Guid repositoryId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        using var cmd = new NpgsqlCommand("DELETE FROM repositories WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", repositoryId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<Repository>> GetAnalyzedRepositories(Guid userId, string filter)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        string query;
        switch (filter.ToLower())
        {
            case "your":
                // Show repositories that belong to the user (is_mine = TRUE)
                query = @"SELECT r.id, r.name, r.owner_username, r.status, r.is_active_blocking, r.connected_by_user_id, r.is_mine, r.last_analyzed_commit_sha, r.last_refresh_at
                          FROM repositories r
                          INNER JOIN repository_user_access rua ON r.id = rua.repository_id
                          WHERE rua.user_id = @userId AND r.is_mine = TRUE AND (r.status = 'ready' OR r.status = 'analyzing' OR r.status = 'pending')
                          ORDER BY rua.granted_at DESC";
                break;
            case "others":
                // Show repositories that DON'T belong to the user (is_mine = FALSE)
                query = @"SELECT r.id, r.name, r.owner_username, r.status, r.is_active_blocking, r.connected_by_user_id, r.is_mine, r.last_analyzed_commit_sha, r.last_refresh_at
                          FROM repositories r
                          INNER JOIN repository_user_access rua ON r.id = rua.repository_id
                          WHERE rua.user_id = @userId AND r.is_mine = FALSE AND (r.status = 'ready' OR r.status = 'analyzing' OR r.status = 'pending')
                          ORDER BY rua.granted_at DESC";
                break;
            case "all":
            default:
                // Show all analyzed repositories for this user
                query = @"SELECT r.id, r.name, r.owner_username, r.status, r.is_active_blocking, r.connected_by_user_id, r.is_mine, r.last_analyzed_commit_sha, r.last_refresh_at
                          FROM repositories r
                          INNER JOIN repository_user_access rua ON r.id = rua.repository_id
                          WHERE rua.user_id = @userId AND (r.status = 'ready' OR r.status = 'analyzing' OR r.status = 'pending')
                          ORDER BY rua.granted_at DESC";
                break;
        }

        using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("userId", userId);

        var repositories = new List<Repository>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            repositories.Add(new Repository
            {
                Id = reader.GetGuid(0),
                Name = reader.GetString(1),
                OwnerUsername = reader.GetString(2),
                Status = reader.IsDBNull(3) ? null : reader.GetString(3),
                IsActiveBlocking = reader.GetBoolean(4),
                ConnectedByUserId = reader.IsDBNull(5) ? null : reader.GetGuid(5),
                IsMine = reader.IsDBNull(6) ? false : reader.GetBoolean(6),
                LastAnalyzedCommitSha = reader.IsDBNull(7) ? null : reader.GetString(7),
                LastRefreshAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
            });
        }
        return repositories;
    }

    // Repository User Access
    public async Task<bool> HasRepositoryAccess(Guid userId, Guid repositoryId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT EXISTS(SELECT 1 FROM repository_user_access WHERE repository_id = @repoId AND user_id = @userId)",
            conn);

        cmd.Parameters.AddWithValue("repoId", repositoryId);
        cmd.Parameters.AddWithValue("userId", userId);

        var result = await cmd.ExecuteScalarAsync();
        return (bool)result!;
    }

    public async Task GrantRepositoryAccess(Guid userId, Guid repositoryId, Guid? grantedByUserId = null)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            @"INSERT INTO repository_user_access (repository_id, user_id, granted_at, granted_by_user_id)
              VALUES (@repoId, @userId, NOW(), @grantedBy)
              ON CONFLICT (repository_id, user_id) DO NOTHING",
            conn);

        cmd.Parameters.AddWithValue("repoId", repositoryId);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("grantedBy", (object?)grantedByUserId ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<(string? userName, DateTime? analyzedAt)> GetRepositoryAnalyzer(Guid repositoryId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            @"SELECT u.author_name, r.created_at 
              FROM repositories r
              LEFT JOIN users u ON r.connected_by_user_id = u.id
              WHERE r.id = @repoId",
            conn);

        cmd.Parameters.AddWithValue("repoId", repositoryId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var userName = reader.IsDBNull(0) ? null : reader.GetString(0);
            var analyzedAt = reader.IsDBNull(1) ? (DateTime?)null : reader.GetDateTime(1);
            return (userName, analyzedAt);
        }
        return (null, null);
    }

    // Branches

    public async Task<Branch> CreateBranch(Branch branch)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "INSERT INTO branches (repository_id, name, head_commit_sha, is_default, created_at, updated_at) VALUES (@repoId, @name, @headSha, @isDefault, @created, @updated) RETURNING id",
            conn);

        cmd.Parameters.AddWithValue("repoId", branch.RepositoryId);
        cmd.Parameters.AddWithValue("name", branch.Name);
        cmd.Parameters.AddWithValue("headSha", (object?)branch.HeadCommitSha ?? DBNull.Value);
        cmd.Parameters.AddWithValue("isDefault", branch.IsDefault);
        cmd.Parameters.AddWithValue("created", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("updated", DateTime.UtcNow);

        branch.Id = (Guid)(await cmd.ExecuteScalarAsync())!;
        return branch;
    }

    public async Task<List<Branch>> GetBranchesByRepository(Guid repositoryId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT id, repository_id, name, head_commit_sha, is_default, created_at, updated_at FROM branches WHERE repository_id = @repoId ORDER BY is_default DESC, name",
            conn);

        cmd.Parameters.AddWithValue("repoId", repositoryId);

        var branches = new List<Branch>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            branches.Add(new Branch
            {
                Id = reader.GetGuid(0),
                RepositoryId = reader.GetGuid(1),
                Name = reader.GetString(2),
                HeadCommitSha = reader.IsDBNull(3) ? null : reader.GetString(3),
                IsDefault = reader.GetBoolean(4),
                CreatedAt = reader.GetDateTime(5),
                UpdatedAt = reader.GetDateTime(6)
            });
        }
        return branches;
    }

    public async Task DeleteBranch(Guid branchId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand("DELETE FROM branches WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", branchId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<Branch?> GetDefaultBranch(Guid repositoryId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT id, repository_id, name, head_commit_sha, is_default, created_at, updated_at FROM branches WHERE repository_id = @repoId AND is_default = TRUE LIMIT 1",
            conn);

        cmd.Parameters.AddWithValue("repoId", repositoryId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Branch
            {
                Id = reader.GetGuid(0),
                RepositoryId = reader.GetGuid(1),
                Name = reader.GetString(2),
                HeadCommitSha = reader.IsDBNull(3) ? null : reader.GetString(3),
                IsDefault = reader.GetBoolean(4),
                CreatedAt = reader.GetDateTime(5),
                UpdatedAt = reader.GetDateTime(6)
            };
        }
        return null;
    }

    public async Task<Branch?> GetBranchByName(Guid repositoryId, string branchName)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT id, repository_id, name, head_commit_sha, is_default, created_at, updated_at FROM branches WHERE repository_id = @repoId AND name = @name",
            conn);

        cmd.Parameters.AddWithValue("repoId", repositoryId);
        cmd.Parameters.AddWithValue("name", branchName);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Branch
            {
                Id = reader.GetGuid(0),
                RepositoryId = reader.GetGuid(1),
                Name = reader.GetString(2),
                HeadCommitSha = reader.IsDBNull(3) ? null : reader.GetString(3),
                IsDefault = reader.GetBoolean(4),
                CreatedAt = reader.GetDateTime(5),
                UpdatedAt = reader.GetDateTime(6)
            };
        }
        return null;
    }

    public async Task UpdateBranchHead(Guid branchId, string commitSha)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand("UPDATE branches SET head_commit_sha = @headSha, updated_at = @updated WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("headSha", commitSha);
        cmd.Parameters.AddWithValue("updated", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("id", branchId);

        await cmd.ExecuteNonQueryAsync();
    }

    // Commit-Branch Junction
    public async Task LinkCommitToBranch(Guid commitId, Guid branchId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "INSERT INTO commit_branches (commit_id, branch_id, created_at) VALUES (@commitId, @branchId, @created) ON CONFLICT (commit_id, branch_id) DO NOTHING",
            conn);

        cmd.Parameters.AddWithValue("commitId", commitId);
        cmd.Parameters.AddWithValue("branchId", branchId);
        cmd.Parameters.AddWithValue("created", DateTime.UtcNow);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<Guid>> GetBranchIdsForCommit(Guid commitId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT branch_id FROM commit_branches WHERE commit_id = @commitId",
            conn);

        cmd.Parameters.AddWithValue("commitId", commitId);

        var branchIds = new List<Guid>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            branchIds.Add(reader.GetGuid(0));
        }
        return branchIds;
    }

    // Commits
    public async Task<Commit> CreateCommit(Commit commit)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "INSERT INTO commits (repository_id, sha, message, author_name, author_email, author_user_id, committed_at) VALUES (@repoId, @sha, @message, @authorName, @authorEmail, @authorUserId, @committedAt) RETURNING id",
            conn);

        cmd.Parameters.AddWithValue("repoId", commit.RepositoryId);
        cmd.Parameters.AddWithValue("sha", commit.Sha);
        cmd.Parameters.AddWithValue("message", (object?)commit.Message ?? DBNull.Value);
        cmd.Parameters.AddWithValue("authorName", (object?)commit.AuthorName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("authorEmail", (object?)commit.AuthorEmail ?? DBNull.Value);
        cmd.Parameters.AddWithValue("authorUserId", (object?)commit.AuthorUserId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("committedAt", commit.CommittedAt);

        commit.Id = (Guid)(await cmd.ExecuteScalarAsync())!;
        return commit;
    }

    public async Task<List<Commit>> GetCommitsByRepository(Guid repositoryId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT id, repository_id, sha, message, author_name, author_email, committed_at FROM commits WHERE repository_id = @repoId ORDER BY committed_at DESC",
            conn);

        cmd.Parameters.AddWithValue("repoId", repositoryId);

        var commits = new List<Commit>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            commits.Add(new Commit
            {
                Id = reader.GetGuid(0),
                RepositoryId = reader.GetGuid(1),
                Sha = reader.GetString(2),
                Message = reader.IsDBNull(3) ? null : reader.GetString(3),
                AuthorName = reader.IsDBNull(4) ? null : reader.GetString(4),
                AuthorEmail = reader.IsDBNull(5) ? null : reader.GetString(5),
                CommittedAt = reader.GetDateTime(6)
            });
        }
        return commits;
    }

    public async Task<List<Commit>> GetCommitsByBranch(Guid repositoryId, string branchName)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            @"SELECT DISTINCT c.id, c.repository_id, c.sha, c.message, c.author_name, c.author_email, c.committed_at 
              FROM commits c 
              JOIN commit_branches cb ON c.id = cb.commit_id 
              JOIN branches b ON cb.branch_id = b.id 
              WHERE c.repository_id = @repoId AND b.name = @branchName 
              ORDER BY c.committed_at DESC",
            conn);

        cmd.Parameters.AddWithValue("repoId", repositoryId);
        cmd.Parameters.AddWithValue("branchName", branchName);

        var commits = new List<Commit>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            commits.Add(new Commit
            {
                Id = reader.GetGuid(0),
                RepositoryId = reader.GetGuid(1),
                Sha = reader.GetString(2),
                Message = reader.IsDBNull(3) ? null : reader.GetString(3),
                AuthorName = reader.IsDBNull(4) ? null : reader.GetString(4),
                AuthorEmail = reader.IsDBNull(5) ? null : reader.GetString(5),
                CommittedAt = reader.GetDateTime(6)
            });
        }
        return commits;
    }

    public async Task<Commit?> GetCommitById(Guid id)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT id, repository_id, sha, message, author_name, author_email, committed_at FROM commits WHERE id = @id",
            conn);

        cmd.Parameters.AddWithValue("id", id);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Commit
            {
                Id = reader.GetGuid(0),
                RepositoryId = reader.GetGuid(1),
                Sha = reader.GetString(2),
                Message = reader.IsDBNull(3) ? null : reader.GetString(3),
                AuthorName = reader.IsDBNull(4) ? null : reader.GetString(4),
                AuthorEmail = reader.IsDBNull(5) ? null : reader.GetString(5),
                CommittedAt = reader.GetDateTime(6)
            };
        }
        return null;
    }

    public async Task<Commit?> GetCommitBySha(Guid repositoryId, string sha)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT id, repository_id, sha, message, author_name, author_email, committed_at FROM commits WHERE repository_id = @repoId AND sha = @sha",
            conn);

        cmd.Parameters.AddWithValue("repoId", repositoryId);
        cmd.Parameters.AddWithValue("sha", sha);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Commit
            {
                Id = reader.GetGuid(0),
                RepositoryId = reader.GetGuid(1),
                Sha = reader.GetString(2),
                Message = reader.IsDBNull(3) ? null : reader.GetString(3),
                AuthorName = reader.IsDBNull(4) ? null : reader.GetString(4),
                AuthorEmail = reader.IsDBNull(5) ? null : reader.GetString(5),
                CommittedAt = reader.GetDateTime(6)
            };
        }
        return null;
    }

    // BATCH: Get multiple commits at once
    public async Task<List<Commit>> GetCommitsByIds(List<Guid> commitIds)
    {
        if (commitIds == null || !commitIds.Any()) return new List<Commit>();

        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT id, repository_id, sha, message, author_name, author_email, committed_at FROM commits WHERE id = ANY(@ids)",
            conn);

        cmd.Parameters.AddWithValue("ids", commitIds.ToArray());

        var commits = new List<Commit>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            commits.Add(new Commit
            {
                Id = reader.GetGuid(0),
                RepositoryId = reader.GetGuid(1),
                Sha = reader.GetString(2),
                Message = reader.IsDBNull(3) ? null : reader.GetString(3),
                AuthorName = reader.IsDBNull(4) ? null : reader.GetString(4),
                AuthorEmail = reader.IsDBNull(5) ? null : reader.GetString(5),
                CommittedAt = reader.GetDateTime(6)
            });
        }
        return commits;
    }

    // Files
    public async Task<RepositoryFile?> GetFileByPath(Guid repositoryId, string filePath)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT id, repository_id, file_path, total_lines FROM repository_files WHERE repository_id = @repoId AND file_path = @path",
            conn);

        cmd.Parameters.AddWithValue("repoId", repositoryId);
        cmd.Parameters.AddWithValue("path", filePath);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new RepositoryFile
            {
                Id = reader.GetGuid(0),
                RepositoryId = reader.GetGuid(1),
                FilePath = reader.GetString(2),
                TotalLines = reader.IsDBNull(3) ? null : reader.GetInt32(3)
            };
        }
        return null;
    }

    public async Task<RepositoryFile> CreateFile(RepositoryFile file)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "INSERT INTO repository_files (repository_id, file_path, total_lines) VALUES (@repoId, @path, @lines) RETURNING id",
            conn);

        cmd.Parameters.AddWithValue("repoId", file.RepositoryId);
        cmd.Parameters.AddWithValue("path", file.FilePath);
        cmd.Parameters.AddWithValue("lines", (object?)file.TotalLines ?? DBNull.Value);

        file.Id = (Guid)(await cmd.ExecuteScalarAsync())!;
        return file;
    }

    public async Task<List<RepositoryFile>> GetFilesByRepository(Guid repositoryId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT id, repository_id, file_path, total_lines FROM repository_files WHERE repository_id = @repoId ORDER BY file_path",
            conn);

        cmd.Parameters.AddWithValue("repoId", repositoryId);

        var files = new List<RepositoryFile>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            files.Add(new RepositoryFile
            {
                Id = reader.GetGuid(0),
                RepositoryId = reader.GetGuid(1),
                FilePath = reader.GetString(2),
                TotalLines = reader.IsDBNull(3) ? null : reader.GetInt32(3)
            });
        }
        return files;
    }

    public async Task<List<RepositoryFile>> GetFilesByBranch(Guid repositoryId, string branchName)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        // Return ALL files in the repository
        // Note: Files exist at the repository level, not per-branch.
        // The branch parameter is kept for API compatibility but we return all repo files.
        using var cmd = new NpgsqlCommand(
            @"SELECT rf.id, rf.repository_id, rf.file_path, rf.total_lines 
              FROM repository_files rf 
              WHERE rf.repository_id = @repoId 
              ORDER BY rf.file_path",
            conn);

        cmd.Parameters.AddWithValue("repoId", repositoryId);
        // branchName is ignored - files exist at repo level

        var files = new List<RepositoryFile>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            files.Add(new RepositoryFile
            {
                Id = reader.GetGuid(0),
                RepositoryId = reader.GetGuid(1),
                FilePath = reader.GetString(2),
                TotalLines = reader.IsDBNull(3) ? null : reader.GetInt32(3)
            });
        }
        return files;
    }

    public async Task<RepositoryFile?> GetFileById(Guid fileId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT id, repository_id, file_path, total_lines FROM repository_files WHERE id = @id",
            conn);

        cmd.Parameters.AddWithValue("id", fileId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new RepositoryFile
            {
                Id = reader.GetGuid(0),
                RepositoryId = reader.GetGuid(1),
                FilePath = reader.GetString(2),
                TotalLines = reader.IsDBNull(3) ? null : reader.GetInt32(3)
            };
        }
        return null;
    }

    // BATCH: Get multiple files at once
    public async Task<List<RepositoryFile>> GetFilesByIds(List<Guid> fileIds)
    {
        if (fileIds == null || !fileIds.Any()) return new List<RepositoryFile>();

        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT id, repository_id, file_path, total_lines FROM repository_files WHERE id = ANY(@ids)",
            conn);

        cmd.Parameters.AddWithValue("ids", fileIds.ToArray());

        var files = new List<RepositoryFile>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            files.Add(new RepositoryFile
            {
                Id = reader.GetGuid(0),
                RepositoryId = reader.GetGuid(1),
                FilePath = reader.GetString(2),
                TotalLines = reader.IsDBNull(3) ? null : reader.GetInt32(3)
            });
        }
        return files;
    }


    // File Changes
    public async Task CreateFileChange(FileChange fileChange)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "INSERT INTO file_changes (commit_id, file_id, additions, deletions) VALUES (@commitId, @fileId, @additions, @deletions) ON CONFLICT (commit_id, file_id) DO UPDATE SET additions = @additions, deletions = @deletions",
            conn);

        cmd.Parameters.AddWithValue("commitId", fileChange.CommitId);
        cmd.Parameters.AddWithValue("fileId", fileChange.FileId);
        cmd.Parameters.AddWithValue("additions", (object?)fileChange.Additions ?? DBNull.Value);
        cmd.Parameters.AddWithValue("deletions", (object?)fileChange.Deletions ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<FileChange>> GetFileChangesByCommit(Guid commitId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT commit_id, file_id, additions, deletions FROM file_changes WHERE commit_id = @commitId",
            conn);

        cmd.Parameters.AddWithValue("commitId", commitId);

        var changes = new List<FileChange>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            changes.Add(new FileChange
            {
                CommitId = reader.GetGuid(0),
                FileId = reader.GetGuid(1),
                Additions = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                Deletions = reader.IsDBNull(3) ? null : reader.GetInt32(3)
            });
        }
        return changes;
    }

    public async Task<List<FileChange>> GetFileChangesByFile(Guid fileId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT commit_id, file_id, additions, deletions FROM file_changes WHERE file_id = @fileId",
            conn);

        cmd.Parameters.AddWithValue("fileId", fileId);

        var changes = new List<FileChange>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            changes.Add(new FileChange
            {
                CommitId = reader.GetGuid(0),
                FileId = reader.GetGuid(1),
                Additions = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                Deletions = reader.IsDBNull(3) ? null : reader.GetInt32(3)
            });
        }
        return changes;
    }

    // Embeddings
    public async Task<CodeEmbedding> CreateEmbedding(CodeEmbedding embedding)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "INSERT INTO code_embeddings (file_id, embedding, chunk_content, created_at) VALUES (@fileId, @embedding::vector, @content, @createdAt) RETURNING id",
            conn);

        cmd.Parameters.AddWithValue("fileId", embedding.FileId);
        cmd.Parameters.AddWithValue("embedding", $"[{string.Join(",", embedding.Embedding!)}]");
        cmd.Parameters.AddWithValue("content", (object?)embedding.ChunkContent ?? DBNull.Value);
        cmd.Parameters.AddWithValue("createdAt", embedding.CreatedAt);

        embedding.Id = (Guid)(await cmd.ExecuteScalarAsync())!;
        return embedding;
    }

    public async Task<List<CodeEmbedding>> GetEmbeddingsByFile(Guid fileId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT id, file_id, embedding::text, chunk_content, created_at FROM code_embeddings WHERE file_id = @fileId",
            conn);

        cmd.Parameters.AddWithValue("fileId", fileId);

        var embeddings = new List<CodeEmbedding>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var embeddingStr = reader.GetString(2).Trim('[', ']');
            var embeddingValues = embeddingStr.Split(',').Select(float.Parse).ToArray();

            embeddings.Add(new CodeEmbedding
            {
                Id = reader.GetGuid(0),
                FileId = reader.GetGuid(1),
                Embedding = embeddingValues,
                ChunkContent = reader.IsDBNull(3) ? null : reader.GetString(3),
                CreatedAt = reader.GetDateTime(4)
            });
        }
        return embeddings;
    }

    public async Task<List<(RepositoryFile File, double Similarity)>> FindSimilarFiles(float[] embedding, Guid repositoryId, Guid excludeFileId, int limit = 10)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        // Use pgvector cosine similarity search
        var embeddingStr = $"[{string.Join(",", embedding)}]";

        using var cmd = new NpgsqlCommand(
            @"SELECT rf.id, rf.repository_id, rf.file_path, rf.total_lines, 
                     (1 - (ce.embedding <=> @embedding::vector)) as similarity
              FROM code_embeddings ce
              JOIN repository_files rf ON ce.file_id = rf.id
              WHERE rf.repository_id = @repoId AND rf.id != @excludeFileId
              GROUP BY rf.id, rf.repository_id, rf.file_path, rf.total_lines, ce.embedding
              ORDER BY ce.embedding <=> @embedding::vector
              LIMIT @limit",
            conn);

        cmd.Parameters.AddWithValue("embedding", embeddingStr);
        cmd.Parameters.AddWithValue("repoId", repositoryId);
        cmd.Parameters.AddWithValue("excludeFileId", excludeFileId);
        cmd.Parameters.AddWithValue("limit", limit);

        var results = new List<(RepositoryFile, double)>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var file = new RepositoryFile
            {
                Id = reader.GetGuid(0),
                RepositoryId = reader.GetGuid(1),
                FilePath = reader.GetString(2),
                TotalLines = reader.IsDBNull(3) ? null : reader.GetInt32(3)
            };
            var similarity = reader.GetDouble(4);
            results.Add((file, similarity));
        }
        return results;
    }

    // Dependencies
    public async Task CreateDependency(Dependency dependency)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "INSERT INTO dependencies (source_file_id, target_file_id, dependency_type, strength) VALUES (@sourceId, @targetId, @type, @strength) ON CONFLICT (source_file_id, target_file_id) DO UPDATE SET dependency_type = @type, strength = @strength",
            conn);

        cmd.Parameters.AddWithValue("sourceId", dependency.SourceFileId);
        cmd.Parameters.AddWithValue("targetId", dependency.TargetFileId);
        cmd.Parameters.AddWithValue("type", (object?)dependency.DependencyType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("strength", (object?)dependency.Strength ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<Dependency>> GetDependenciesForFile(Guid fileId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT source_file_id, target_file_id, dependency_type, strength FROM dependencies WHERE source_file_id = @fileId",
            conn);

        cmd.Parameters.AddWithValue("fileId", fileId);

        var dependencies = new List<Dependency>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            dependencies.Add(new Dependency
            {
                SourceFileId = reader.GetGuid(0),
                TargetFileId = reader.GetGuid(1),
                DependencyType = reader.IsDBNull(2) ? null : reader.GetString(2),
                Strength = reader.IsDBNull(3) ? null : reader.GetInt32(3)
            });
        }
        return dependencies;
    }

    public async Task<List<Dependency>> GetDependentsForFile(Guid fileId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT source_file_id, target_file_id, dependency_type, strength FROM dependencies WHERE target_file_id = @fileId",
            conn);

        cmd.Parameters.AddWithValue("fileId", fileId);

        var dependencies = new List<Dependency>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            dependencies.Add(new Dependency
            {
                SourceFileId = reader.GetGuid(0),
                TargetFileId = reader.GetGuid(1),
                DependencyType = reader.IsDBNull(2) ? null : reader.GetString(2),
                Strength = reader.IsDBNull(3) ? null : reader.GetInt32(3)
            });
        }
        return dependencies;
    }

    // File Ownership
    public async Task UpsertFileOwnership(FileOwnership ownership)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "INSERT INTO file_ownership (file_id, author_name, semantic_score, last_updated) VALUES (@fileId, @authorName, @score, @updated) ON CONFLICT (file_id, author_name) DO UPDATE SET semantic_score = @score, last_updated = @updated",
            conn);

        cmd.Parameters.AddWithValue("fileId", ownership.FileId);
        cmd.Parameters.AddWithValue("authorName", ownership.AuthorName);
        cmd.Parameters.AddWithValue("score", (object?)ownership.SemanticScore ?? DBNull.Value);
        cmd.Parameters.AddWithValue("updated", ownership.LastUpdated);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<FileOwnership>> GetFileOwnership(Guid fileId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT file_id, author_name, semantic_score, last_updated FROM file_ownership WHERE file_id = @fileId ORDER BY semantic_score DESC LIMIT 3",
            conn);

        cmd.Parameters.AddWithValue("fileId", fileId);

        var ownerships = new List<FileOwnership>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ownerships.Add(new FileOwnership
            {
                FileId = reader.GetGuid(0),
                AuthorName = reader.GetString(1),
                SemanticScore = reader.IsDBNull(2) ? null : reader.GetDecimal(2),
                LastUpdated = reader.GetDateTime(3)
            });
        }
        return ownerships;
    }

    public async Task<string?> GetMostActiveAuthorForFile(Guid fileId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            @"SELECT c.author_name, COUNT(*) as change_count
              FROM file_changes fc
              JOIN commits c ON fc.commit_id = c.id
              WHERE fc.file_id = @fileId AND c.author_name IS NOT NULL
              GROUP BY c.author_name
              ORDER BY change_count DESC
              LIMIT 1",
            conn);

        cmd.Parameters.AddWithValue("fileId", fileId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return reader.GetString(0);
        }
        return null;
    }

    // Pull Requests
    public async Task<PullRequest?> GetPullRequestByNumber(Guid repositoryId, int prNumber)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT id, repository_id, pr_number, title, state, author_id, merged, merged_at FROM pull_requests WHERE repository_id = @repoId AND pr_number = @prNumber",
            conn);

        cmd.Parameters.AddWithValue("repoId", repositoryId);
        cmd.Parameters.AddWithValue("prNumber", prNumber);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new PullRequest
            {
                Id = reader.GetGuid(0),
                RepositoryId = reader.GetGuid(1),
                PrNumber = reader.GetInt32(2),
                Title = reader.IsDBNull(3) ? null : reader.GetString(3),
                State = reader.IsDBNull(4) ? null : reader.GetString(4),
                AuthorId = reader.IsDBNull(5) ? null : reader.GetGuid(5),
                Merged = reader.IsDBNull(6) ? false : reader.GetBoolean(6),
                MergedAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7)
            };
        }
        return null;
    }

    public async Task<PullRequest> CreatePullRequest(PullRequest pr)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "INSERT INTO pull_requests (repository_id, pr_number, title, state, author_id, merged, merged_at) VALUES (@repoId, @prNumber, @title, @state, @authorId, @merged, @mergedAt) RETURNING id",
            conn);

        cmd.Parameters.AddWithValue("repoId", pr.RepositoryId);
        cmd.Parameters.AddWithValue("prNumber", pr.PrNumber);
        cmd.Parameters.AddWithValue("title", (object?)pr.Title ?? DBNull.Value);
        cmd.Parameters.AddWithValue("state", (object?)pr.State ?? DBNull.Value);
        cmd.Parameters.AddWithValue("authorId", (object?)pr.AuthorId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("merged", pr.Merged);
        cmd.Parameters.AddWithValue("mergedAt", (object?)pr.MergedAt ?? DBNull.Value);

        pr.Id = (Guid)(await cmd.ExecuteScalarAsync())!;
        return pr;
    }

    public async Task<List<PullRequest>> GetOpenPullRequests(Guid repositoryId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT id, repository_id, pr_number, state, author_id FROM pull_requests WHERE repository_id = @repoId AND state = 'open'",
            conn);

        cmd.Parameters.AddWithValue("repoId", repositoryId);

        var prs = new List<PullRequest>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            prs.Add(new PullRequest
            {
                Id = reader.GetGuid(0),
                RepositoryId = reader.GetGuid(1),
                PrNumber = reader.GetInt32(2),
                State = reader.IsDBNull(3) ? null : reader.GetString(3),
                AuthorId = reader.IsDBNull(4) ? null : reader.GetGuid(4)
            });
        }
        return prs;
    }

    public async Task<List<PullRequest>> GetAllPullRequests(Guid repositoryId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT id, repository_id, pr_number, title, state, author_id, merged, merged_at FROM pull_requests WHERE repository_id = @repoId ORDER BY pr_number DESC",
            conn);

        cmd.Parameters.AddWithValue("repoId", repositoryId);

        var prs = new List<PullRequest>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            prs.Add(new PullRequest
            {
                Id = reader.GetGuid(0),
                RepositoryId = reader.GetGuid(1),
                PrNumber = reader.GetInt32(2),
                Title = reader.IsDBNull(3) ? null : reader.GetString(3),
                State = reader.IsDBNull(4) ? null : reader.GetString(4),
                AuthorId = reader.IsDBNull(5) ? null : reader.GetGuid(5),
                Merged = reader.IsDBNull(6) ? false : reader.GetBoolean(6),
                MergedAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7)
            });
        }
        return prs;
    }

    public async Task UpdatePullRequestState(Guid prId, string state)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand("UPDATE pull_requests SET state = @state WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("state", state);
        cmd.Parameters.AddWithValue("id", prId);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdatePullRequestTitle(Guid prId, string title)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand("UPDATE pull_requests SET title = @title WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("title", (object?)title ?? DBNull.Value);
        cmd.Parameters.AddWithValue("id", prId);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdatePullRequestMergedStatus(Guid prId, bool merged, DateTime? mergedAt)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand("UPDATE pull_requests SET merged = @merged, merged_at = @mergedAt WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("merged", merged);
        cmd.Parameters.AddWithValue("mergedAt", (object?)mergedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("id", prId);

        await cmd.ExecuteNonQueryAsync();
    }


    public async Task DeletePrFilesChangedByPrId(Guid prId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand("DELETE FROM pr_files_changed WHERE pr_id = @prId", conn);
        cmd.Parameters.AddWithValue("prId", prId);

        await cmd.ExecuteNonQueryAsync();
    }

    // PR Files
    public async Task CreatePrFileChanged(PrFileChanged prFile)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "INSERT INTO pr_files_changed (pr_id, file_id) VALUES (@prId, @fileId) ON CONFLICT (pr_id, file_id) DO NOTHING",
            conn);

        cmd.Parameters.AddWithValue("prId", prFile.PrId);
        cmd.Parameters.AddWithValue("fileId", prFile.FileId);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<RepositoryFile>> GetPrFiles(Guid prId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT rf.id, rf.repository_id, rf.file_path, rf.total_lines FROM repository_files rf JOIN pr_files_changed pfc ON rf.id = pfc.file_id WHERE pfc.pr_id = @prId",
            conn);

        cmd.Parameters.AddWithValue("prId", prId);

        var files = new List<RepositoryFile>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            files.Add(new RepositoryFile
            {
                Id = reader.GetGuid(0),
                RepositoryId = reader.GetGuid(1),
                FilePath = reader.GetString(2),
                TotalLines = reader.IsDBNull(3) ? null : reader.GetInt32(3)
            });
        }
        return files;
    }

    public async Task<List<PrConflict>> GetPotentialConflicts(Guid prId, Guid repositoryId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        // Step 1: Get all files in the current PR
        var currentPrFiles = new List<Guid>();
        using (var cmd = new NpgsqlCommand("SELECT file_id FROM pr_files_changed WHERE pr_id = @prId", conn))
        {
            cmd.Parameters.AddWithValue("prId", prId);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                currentPrFiles.Add(reader.GetGuid(0));
            }
        }

        if (currentPrFiles.Count == 0)
            return new List<PrConflict>();

        // Step 2: Find other open PRs with overlapping files
        var conflicts = new Dictionary<Guid, PrConflict>();

        using (var cmd = new NpgsqlCommand(@"
            SELECT DISTINCT pr.id, pr.pr_number, pr.title, f.file_path
            FROM pr_files_changed pfc
            JOIN pull_requests pr ON pfc.pr_id = pr.id
            JOIN repository_files f ON pfc.file_id = f.id
            WHERE pfc.file_id = ANY(@fileIds)
              AND pr.state = 'open'
              AND pr.id != @prId
              AND pr.repository_id = @repositoryId
            ORDER BY pr.pr_number", conn))
        {
            cmd.Parameters.AddWithValue("fileIds", currentPrFiles.ToArray());
            cmd.Parameters.AddWithValue("prId", prId);
            cmd.Parameters.AddWithValue("repositoryId", repositoryId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var conflictPrId = reader.GetGuid(0);
                var prNumber = reader.GetInt32(1);
                var title = reader.IsDBNull(2) ? "" : reader.GetString(2);
                var filePath = reader.GetString(3);

                if (!conflicts.ContainsKey(conflictPrId))
                {
                    conflicts[conflictPrId] = new PrConflict
                    {
                        ConflictingPrId = conflictPrId,
                        PrNumber = prNumber,
                        Title = title,
                        OverlappingFiles = new List<string>(),
                        OverlapCount = 0
                    };
                }

                conflicts[conflictPrId].OverlappingFiles.Add(filePath);
                conflicts[conflictPrId].OverlapCount++;
            }
        }

        // Step 3: Calculate conflict percentages
        foreach (var conflict in conflicts.Values)
        {
            conflict.ConflictPercentage = Math.Round((decimal)conflict.OverlapCount / currentPrFiles.Count * 100, 1);
        }

        return conflicts.Values.OrderByDescending(c => c.OverlapCount).ToList();
    }

    public async Task<bool> IsFileInOpenPr(Guid fileId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(@"
            SELECT 1 
            FROM pr_files_changed pfc
            JOIN pull_requests pr ON pfc.pr_id = pr.id
            WHERE pfc.file_id = @fileId
              AND pr.state = 'open'
            LIMIT 1", conn);

        cmd.Parameters.AddWithValue("fileId", fileId);

        var result = await cmd.ExecuteScalarAsync();
        return result != null;
    }

    // Webhook Queue
    public async Task<long> EnqueueWebhook(string payload)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "INSERT INTO webhook_queue (payload, status) VALUES (@payload::jsonb, 'pending') RETURNING id",
            conn);

        cmd.Parameters.AddWithValue("payload", payload);

        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task<WebhookQueueItem?> GetNextPendingWebhook()
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "UPDATE webhook_queue SET status = 'processing' WHERE id = (SELECT id FROM webhook_queue WHERE status = 'pending' ORDER BY id LIMIT 1 FOR UPDATE SKIP LOCKED) RETURNING id, payload, status",
            conn);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new WebhookQueueItem
            {
                Id = reader.GetInt64(0),
                Payload = reader.GetString(1),
                Status = reader.GetString(2)
            };
        }
        return null;
    }

    public async Task UpdateWebhookStatus(long id, string status)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand("UPDATE webhook_queue SET status = @status WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("id", id);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<Commit>> GetCommitsForFile(Guid fileId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            @"SELECT c.id, c.repository_id, c.sha, c.message, c.author_name, c.author_email, c.committed_at 
              FROM commits c 
              JOIN file_changes fc ON c.id = fc.commit_id 
              WHERE fc.file_id = @fileId 
              ORDER BY c.committed_at DESC",
            conn);

        cmd.Parameters.AddWithValue("fileId", fileId);

        var commits = new List<Commit>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            commits.Add(new Commit
            {
                Id = reader.GetGuid(0),
                RepositoryId = reader.GetGuid(1),
                Sha = reader.GetString(2),
                Message = reader.IsDBNull(3) ? null : reader.GetString(3),
                AuthorName = reader.IsDBNull(4) ? null : reader.GetString(4),
                AuthorEmail = reader.IsDBNull(5) ? null : reader.GetString(5),
                CommittedAt = reader.GetDateTime(6)
            });
        }
        return commits;
    }

    // BATCH: Get file changes for multiple files at once
    public async Task<Dictionary<Guid, List<FileChange>>> GetFileChangesByFileIds(List<Guid> fileIds)
    {
        if (fileIds == null || !fileIds.Any()) return new Dictionary<Guid, List<FileChange>>();

        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT commit_id, file_id, additions, deletions FROM file_changes WHERE file_id = ANY(@fileIds)",
            conn);

        cmd.Parameters.AddWithValue("fileIds", fileIds.ToArray());

        var result = new Dictionary<Guid, List<FileChange>>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var fileId = reader.GetGuid(1);
            var change = new FileChange
            {
                CommitId = reader.GetGuid(0),
                FileId = fileId,
                Additions = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                Deletions = reader.IsDBNull(3) ? null : reader.GetInt32(3)
            };

            if (!result.ContainsKey(fileId))
                result[fileId] = new List<FileChange>();
            result[fileId].Add(change);
        }
        return result;
    }

    // BATCH: Get file changes for multiple commits at once
    public async Task<Dictionary<Guid, List<FileChange>>> GetFileChangesByCommitIds(List<Guid> commitIds)
    {
        if (commitIds == null || !commitIds.Any()) return new Dictionary<Guid, List<FileChange>>();

        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT commit_id, file_id, additions, deletions FROM file_changes WHERE commit_id = ANY(@commitIds)",
            conn);

        cmd.Parameters.AddWithValue("commitIds", commitIds.ToArray());

        var result = new Dictionary<Guid, List<FileChange>>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var commitId = reader.GetGuid(0);
            var change = new FileChange
            {
                CommitId = commitId,
                FileId = reader.GetGuid(1),
                Additions = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                Deletions = reader.IsDBNull(3) ? null : reader.GetInt32(3)
            };

            if (!result.ContainsKey(commitId))
                result[commitId] = new List<FileChange>();
            result[commitId].Add(change);
        }
        return result;
    }

    // BATCH: Get file ownership for multiple files at once
    public async Task<Dictionary<Guid, List<FileOwnership>>> GetFileOwnershipByFileIds(List<Guid> fileIds)
    {
        if (fileIds == null || !fileIds.Any()) return new Dictionary<Guid, List<FileOwnership>>();

        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT file_id, author_name, semantic_score, last_updated FROM file_ownership WHERE file_id = ANY(@fileIds)",
            conn);

        cmd.Parameters.AddWithValue("fileIds", fileIds.ToArray());

        var result = new Dictionary<Guid, List<FileOwnership>>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var fileId = reader.GetGuid(0);
            var ownership = new FileOwnership
            {
                FileId = fileId,
                AuthorName = reader.GetString(1),
                SemanticScore = reader.IsDBNull(2) ? null : reader.GetDecimal(2),
                LastUpdated = reader.GetDateTime(3)
            };

            if (!result.ContainsKey(fileId))
                result[fileId] = new List<FileOwnership>();
            result[fileId].Add(ownership);
        }
        return result;
    }

    // ============================================
    // PERSONALIZED DASHBOARD
    // ============================================

    public async Task<List<FileView>> GetRecentFileViews(Guid userId, int limit = 10)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            @"SELECT id, user_id, file_id, viewed_at 
              FROM file_views 
              WHERE user_id = @userId 
              ORDER BY viewed_at DESC 
              LIMIT @limit",
            conn);

        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("limit", limit);

        var views = new List<FileView>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            views.Add(new FileView
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                FileId = reader.GetGuid(2),
                ViewedAt = reader.GetDateTime(3)
            });
        }
        return views;
    }

    public async Task UpsertFileView(Guid userId, Guid fileId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            @"INSERT INTO file_views (user_id, file_id, viewed_at) 
              VALUES (@userId, @fileId, NOW()) 
              ON CONFLICT (user_id, file_id) 
              DO UPDATE SET viewed_at = NOW()",
            conn);

        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("fileId", fileId);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ClearUserFileViews(Guid userId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "DELETE FROM file_views WHERE user_id = @userId",
            conn);

        cmd.Parameters.AddWithValue("userId", userId);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<FileBookmark>> GetFileBookmarks(Guid userId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            @"SELECT id, user_id, file_id, category, created_at 
              FROM file_bookmarks 
              WHERE user_id = @userId 
              ORDER BY created_at DESC",
            conn);

        cmd.Parameters.AddWithValue("userId", userId);

        var bookmarks = new List<FileBookmark>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            bookmarks.Add(new FileBookmark
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                FileId = reader.GetGuid(2),
                Category = reader.IsDBNull(3) ? null : reader.GetString(3),
                CreatedAt = reader.GetDateTime(4)
            });
        }
        return bookmarks;
    }

    public async Task<bool> IsFileBookmarked(Guid userId, Guid fileId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT 1 FROM file_bookmarks WHERE user_id = @userId AND file_id = @fileId LIMIT 1",
            conn);

        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("fileId", fileId);

        var result = await cmd.ExecuteScalarAsync();
        return result != null;
    }

    public async Task CreateFileBookmark(Guid userId, Guid fileId, string? category = null)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            @"INSERT INTO file_bookmarks (user_id, file_id, category, created_at) 
              VALUES (@userId, @fileId, @category, NOW()) 
              ON CONFLICT (user_id, file_id) DO NOTHING",
            conn);

        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("fileId", fileId);
        cmd.Parameters.AddWithValue("category", (object?)category ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteFileBookmark(Guid userId, Guid fileId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "DELETE FROM file_bookmarks WHERE user_id = @userId AND file_id = @fileId",
            conn);

        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("fileId", fileId);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ClearUserBookmarks(Guid userId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "DELETE FROM file_bookmarks WHERE user_id = @userId",
            conn);

        cmd.Parameters.AddWithValue("userId", userId);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<Commit>> GetTeamActivity(Guid userId, int limit = 20)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        // Get recent commits from repositories the user has access to
        using var cmd = new NpgsqlCommand(
            @"SELECT c.id, c.repository_id, c.sha, c.message, c.author_name, c.author_email, c.committed_at
              FROM commits c
              JOIN repository_user_access rua ON c.repository_id = rua.repository_id
              WHERE rua.user_id = @userId
              ORDER BY c.committed_at DESC
              LIMIT @limit",
            conn);

        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("limit", limit);

        var commits = new List<Commit>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            commits.Add(new Commit
            {
                Id = reader.GetGuid(0),
                RepositoryId = reader.GetGuid(1),
                Sha = reader.GetString(2),
                Message = reader.IsDBNull(3) ? null : reader.GetString(3),
                AuthorName = reader.IsDBNull(4) ? null : reader.GetString(4),
                AuthorEmail = reader.IsDBNull(5) ? null : reader.GetString(5),
                CommittedAt = reader.GetDateTime(6)
            });
        }
        return commits;
    }

    public async Task<int> GetUserFileViewCount(Guid userId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM file_views WHERE user_id = @userId",
            conn);

        cmd.Parameters.AddWithValue("userId", userId);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task<int> GetUserRepositoryCount(Guid userId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM repository_user_access WHERE user_id = @userId",
            conn);

        cmd.Parameters.AddWithValue("userId", userId);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task<int> GetUserCommitCount(Guid userId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        // Get commits where the author_user_id matches the user
        using var cmd = new NpgsqlCommand(@"
            SELECT COUNT(*) FROM commits c
            WHERE c.author_user_id = @userId
            AND c.committed_at >= NOW() - INTERVAL '7 days'",
            conn);

        cmd.Parameters.AddWithValue("userId", userId);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task<int> GetUserPrsReviewedCount(Guid userId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        // Get CLOSED PRs where user submitted a review
        // Count only increments when the PR is actually closed after being reviewed
        using var cmd = new NpgsqlCommand(@"
            SELECT COUNT(DISTINCT r.pr_id) 
            FROM reviews r
            JOIN pull_requests pr ON pr.id = r.pr_id
            WHERE r.reviewer_id = @userId
            AND pr.state = 'closed'",
            conn);

        cmd.Parameters.AddWithValue("userId", userId);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task<List<PullRequest>> GetPendingReviews(Guid userId, int limit = 10)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        
        // Get the user's GitHub ID for matching requested reviewers
        var user = await GetUserById(userId);
        
        Console.WriteLine($"[DEBUG] GetPendingReviews - userId: {userId}, githubId: {user?.GithubId}");

        // Use UNION to combine two sources of PRs:
        // 1. PRs from repos where user is a contributor (has commits) OR owner
        // 2. PRs where user is explicitly requested as reviewer
        using var cmd = new NpgsqlCommand(@"
            -- PRs from repos where user is contributor/owner
            SELECT DISTINCT pr.id, pr.repository_id, pr.pr_number, pr.title, pr.state, pr.author_id, 'contributor' as source
            FROM pull_requests pr
            JOIN repositories r ON r.id = pr.repository_id
            WHERE (
                -- User is owner of the repo
                r.connected_by_user_id = @userId
                OR
                -- User has contributed commits to this repo
                EXISTS (
                    SELECT 1 FROM commits c 
                    WHERE c.repository_id = pr.repository_id 
                    AND c.author_user_id = @userId
                )
            )
            AND pr.state = 'open'
            AND (pr.author_id IS NULL OR pr.author_id != @userId)
            AND NOT EXISTS (
                SELECT 1 FROM reviews rv 
                WHERE rv.pr_id = pr.id AND rv.reviewer_id = @userId
            )
            
            UNION
            
            -- PRs where user is a requested reviewer
            SELECT DISTINCT pr.id, pr.repository_id, pr.pr_number, pr.title, pr.state, pr.author_id, 'requested_reviewer' as source
            FROM pull_requests pr
            JOIN pr_requested_reviewers prr ON prr.pr_id = pr.id
            WHERE (prr.reviewer_id = @userId OR (prr.github_user_id IS NOT NULL AND prr.github_user_id = @githubUserId))
            AND pr.state = 'open'
            AND (pr.author_id IS NULL OR pr.author_id != @userId)
            AND NOT EXISTS (
                SELECT 1 FROM reviews rv 
                WHERE rv.pr_id = pr.id AND rv.reviewer_id = @userId
            )
            
            ORDER BY pr_number DESC
            LIMIT @limit",
            conn);

        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("githubUserId", (object?)user?.GithubId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("limit", limit);

        var prs = new List<PullRequest>();
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var source = reader.GetString(reader.GetOrdinal("source"));
            var prNumber = reader.GetInt32(reader.GetOrdinal("pr_number"));
            var title = reader.IsDBNull(reader.GetOrdinal("title")) ? null : reader.GetString(reader.GetOrdinal("title"));
            
            Console.WriteLine($"[DEBUG] PR #{prNumber} ({title}) - Source: {source}");
            
            prs.Add(new PullRequest
            {
                Id = reader.GetGuid(reader.GetOrdinal("id")),
                RepositoryId = reader.GetGuid(reader.GetOrdinal("repository_id")),
                PrNumber = prNumber,
                Title = title,
                State = reader.IsDBNull(reader.GetOrdinal("state")) ? null : reader.GetString(reader.GetOrdinal("state")),
                AuthorId = reader.IsDBNull(reader.GetOrdinal("author_id")) ? null : reader.GetGuid(reader.GetOrdinal("author_id"))
            });
        }
        
        Console.WriteLine($"[DEBUG] Total PRs returned: {prs.Count}");

        return prs;
    }

    // Debug helper methods
    public async Task<List<PullRequest>> GetAllOpenPrs()
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            SELECT id, repository_id, pr_number, title, state, author_id 
            FROM pull_requests 
            WHERE state = 'open'
            ORDER BY pr_number", conn);

        var prs = new List<PullRequest>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            prs.Add(new PullRequest
            {
                Id = reader.GetGuid(reader.GetOrdinal("id")),
                RepositoryId = reader.IsDBNull(reader.GetOrdinal("repository_id")) ? Guid.Empty : reader.GetGuid(reader.GetOrdinal("repository_id")),
                PrNumber = reader.GetInt32(reader.GetOrdinal("pr_number")),
                Title = reader.IsDBNull(reader.GetOrdinal("title")) ? null : reader.GetString(reader.GetOrdinal("title")),
                State = reader.IsDBNull(reader.GetOrdinal("state")) ? null : reader.GetString(reader.GetOrdinal("state")),
                AuthorId = reader.IsDBNull(reader.GetOrdinal("author_id")) ? null : reader.GetGuid(reader.GetOrdinal("author_id"))
            });
        }
        return prs;
    }

    public async Task<bool> CheckUserRepositoryAccess(Guid userId, Guid repositoryId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        using var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM repository_user_access WHERE user_id = @userId AND repository_id = @repositoryId",
            conn);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("repositoryId", repositoryId);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    public async Task<bool> HasUserReviewedPr(Guid userId, Guid prId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        using var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM reviews WHERE reviewer_id = @userId AND pr_id = @prId",
            conn);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("prId", prId);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    // NEW: PR Requested Reviewers methods
    public async Task CreatePrRequestedReviewer(PrRequestedReviewer reviewer)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        try
        {
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO pr_requested_reviewers (pr_id, reviewer_id, github_user_id) 
                VALUES (@prId, @reviewerId, @githubUserId)",
                conn);
            cmd.Parameters.AddWithValue("prId", reviewer.PrId);
            cmd.Parameters.AddWithValue("reviewerId", (object?)reviewer.ReviewerId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("githubUserId", (object?)reviewer.GitHubUserId ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            // Ignore duplicate key errors - reviewer already exists for this PR
        }
    }

    public async Task DeletePrRequestedReviewers(Guid prId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        using var cmd = new NpgsqlCommand(
            "DELETE FROM pr_requested_reviewers WHERE pr_id = @prId",
            conn);
        cmd.Parameters.AddWithValue("prId", prId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<PullRequest>> GetPrsWhereUserIsRequestedReviewer(Guid userId, int limit = 10)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        
        // Get the user's GitHub ID for matching
        var user = await GetUserById(userId);
        
        using var cmd = new NpgsqlCommand(@"
            SELECT DISTINCT pr.id, pr.repository_id, pr.pr_number, pr.title, pr.state, pr.author_id
            FROM pull_requests pr
            JOIN pr_requested_reviewers prr ON prr.pr_id = pr.id
            WHERE (prr.reviewer_id = @userId OR prr.github_user_id = @githubUserId)
            AND pr.state = 'open'
            AND NOT EXISTS (
                SELECT 1 FROM reviews rv 
                WHERE rv.pr_id = pr.id AND rv.reviewer_id = @userId
            )
            ORDER BY pr.pr_number DESC
            LIMIT @limit",
            conn);
        
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("githubUserId", (object?)user?.GithubId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("limit", limit);
        
        var prs = new List<PullRequest>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            prs.Add(new PullRequest
            {
                Id = reader.GetGuid(reader.GetOrdinal("id")),
                RepositoryId = reader.IsDBNull(reader.GetOrdinal("repository_id")) ? Guid.Empty : reader.GetGuid(reader.GetOrdinal("repository_id")),
                PrNumber = reader.GetInt32(reader.GetOrdinal("pr_number")),
                Title = reader.IsDBNull(reader.GetOrdinal("title")) ? null : reader.GetString(reader.GetOrdinal("title")),
                State = reader.IsDBNull(reader.GetOrdinal("state")) ? null : reader.GetString(reader.GetOrdinal("state")),
                AuthorId = reader.IsDBNull(reader.GetOrdinal("author_id")) ? null : reader.GetGuid(reader.GetOrdinal("author_id"))
            });
        }
        return prs;
    }
    
    // Debug helper methods
    public async Task<bool> CheckUserHasCommitsInRepo(Guid userId, Guid repositoryId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        using var cmd = new NpgsqlCommand(
            "SELECT EXISTS(SELECT 1 FROM commits WHERE author_user_id = @userId AND repository_id = @repositoryId)",
            conn);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("repositoryId", repositoryId);
        var result = await cmd.ExecuteScalarAsync();
        return (bool?)result ?? false;
    }

    public async Task<bool> CheckIsRequestedReviewer(Guid userId, Guid prId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        var user = await GetUserById(userId);
        using var cmd = new NpgsqlCommand(@"
            SELECT EXISTS(
                SELECT 1 FROM pr_requested_reviewers 
                WHERE pr_id = @prId 
                AND (reviewer_id = @userId OR (github_user_id IS NOT NULL AND github_user_id = @githubUserId))
            )",
            conn);
        cmd.Parameters.AddWithValue("prId", prId);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("githubUserId", (object?)user?.GithubId ?? DBNull.Value);
        var result = await cmd.ExecuteScalarAsync();
        return (bool?)result ?? false;
    }

    // ============================================
    // TEAMS & RBAC
    // ============================================
    
    // Repo Admins
    public async Task<bool> IsRepoAdmin(Guid userId, Guid repositoryId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        using var cmd = new NpgsqlCommand(
            "SELECT EXISTS(SELECT 1 FROM repo_admins WHERE user_id = @userId AND repository_id = @repositoryId)",
            conn);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("repositoryId", repositoryId);
        var result = await cmd.ExecuteScalarAsync();
        return (bool?)result ?? false;
    }

    public async Task<List<RepoAdmin>> GetRepoAdmins(Guid repositoryId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        using var cmd = new NpgsqlCommand(
            @"SELECT id, repository_id, user_id, assigned_by_user_id, created_at
              FROM repo_admins
              WHERE repository_id = @repositoryId
              ORDER BY created_at ASC",
            conn);
        cmd.Parameters.AddWithValue("repositoryId", repositoryId);

        var admins = new List<RepoAdmin>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            admins.Add(new RepoAdmin
            {
                Id = reader.GetGuid(0),
                RepositoryId = reader.GetGuid(1),
                UserId = reader.GetGuid(2),
                AssignedByUserId = reader.IsDBNull(3) ? null : reader.GetGuid(3),
                CreatedAt = reader.GetDateTime(4)
            });
        }
        return admins;
    }

    public async Task<RepoAdmin> CreateRepoAdmin(Guid repositoryId, Guid userId, Guid? assignedByUserId = null)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        using var cmd = new NpgsqlCommand(
            @"INSERT INTO repo_admins (repository_id, user_id, assigned_by_user_id, created_at)
              VALUES (@repositoryId, @userId, @assignedByUserId, NOW())
              RETURNING id, repository_id, user_id, assigned_by_user_id, created_at",
            conn);
        cmd.Parameters.AddWithValue("repositoryId", repositoryId);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("assignedByUserId", (object?)assignedByUserId ?? DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        return new RepoAdmin
        {
            Id = reader.GetGuid(0),
            RepositoryId = reader.GetGuid(1),
            UserId = reader.GetGuid(2),
            AssignedByUserId = reader.IsDBNull(3) ? null : reader.GetGuid(3),
            CreatedAt = reader.GetDateTime(4)
        };
    }

    public async Task DeleteRepoAdmin(Guid repoAdminId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        using var cmd = new NpgsqlCommand(
            "DELETE FROM repo_admins WHERE id = @id",
            conn);
        cmd.Parameters.AddWithValue("id", repoAdminId);
        await cmd.ExecuteNonQueryAsync();
    }

    // Teams
    public async Task<Team> CreateTeam(Team team)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        using var cmd = new NpgsqlCommand(
            @"INSERT INTO teams (name, repository_id, created_by_user_id, created_at, updated_at)
              VALUES (@name, @repositoryId, @createdByUserId, NOW(), NOW())
              RETURNING id, name, repository_id, created_by_user_id, created_at, updated_at",
            conn);
        cmd.Parameters.AddWithValue("name", team.Name);
        cmd.Parameters.AddWithValue("repositoryId", team.RepositoryId);
        cmd.Parameters.AddWithValue("createdByUserId", (object?)team.CreatedByUserId ?? DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        return new Team
        {
            Id = reader.GetGuid(0),
            Name = reader.GetString(1),
            RepositoryId = reader.GetGuid(2),
            CreatedByUserId = reader.IsDBNull(3) ? null : reader.GetGuid(3),
            CreatedAt = reader.GetDateTime(4),
            UpdatedAt = reader.GetDateTime(5)
        };
    }

    public async Task<List<Team>> GetTeamsByRepository(Guid repositoryId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        using var cmd = new NpgsqlCommand(
            @"SELECT id, name, repository_id, created_by_user_id, created_at, updated_at
              FROM teams
              WHERE repository_id = @repositoryId
              ORDER BY created_at ASC",
            conn);
        cmd.Parameters.AddWithValue("repositoryId", repositoryId);

        var teams = new List<Team>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            teams.Add(new Team
            {
                Id = reader.GetGuid(0),
                Name = reader.GetString(1),
                RepositoryId = reader.GetGuid(2),
                CreatedByUserId = reader.IsDBNull(3) ? null : reader.GetGuid(3),
                CreatedAt = reader.GetDateTime(4),
                UpdatedAt = reader.GetDateTime(5)
            });
        }
        return teams;
    }

    public async Task<Team?> GetTeamById(Guid teamId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        using var cmd = new NpgsqlCommand(
            @"SELECT id, name, repository_id, created_by_user_id, created_at, updated_at
              FROM teams
              WHERE id = @teamId",
            conn);
        cmd.Parameters.AddWithValue("teamId", teamId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Team
            {
                Id = reader.GetGuid(0),
                Name = reader.GetString(1),
                RepositoryId = reader.GetGuid(2),
                CreatedByUserId = reader.IsDBNull(3) ? null : reader.GetGuid(3),
                CreatedAt = reader.GetDateTime(4),
                UpdatedAt = reader.GetDateTime(5)
            };
        }
        return null;
    }

    public async Task UpdateTeamName(Guid teamId, string name)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        using var cmd = new NpgsqlCommand(
            "UPDATE teams SET name = @name, updated_at = NOW() WHERE id = @teamId",
            conn);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("teamId", teamId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteTeam(Guid teamId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        using var cmd = new NpgsqlCommand(
            "DELETE FROM teams WHERE id = @teamId",
            conn);
        cmd.Parameters.AddWithValue("teamId", teamId);
        await cmd.ExecuteNonQueryAsync();
    }

    // Team Members
    public async Task<TeamMember> AddTeamMember(TeamMember member)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        using var cmd = new NpgsqlCommand(
            @"INSERT INTO team_members (team_id, user_id, role, assigned_by_user_id, created_at, updated_at)
              VALUES (@teamId, @userId, @role, @assignedByUserId, NOW(), NOW())
              RETURNING id, team_id, user_id, role, assigned_by_user_id, created_at, updated_at",
            conn);
        cmd.Parameters.AddWithValue("teamId", member.TeamId);
        cmd.Parameters.AddWithValue("userId", member.UserId);
        cmd.Parameters.AddWithValue("role", member.Role);
        cmd.Parameters.AddWithValue("assignedByUserId", (object?)member.AssignedByUserId ?? DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        return new TeamMember
        {
            Id = reader.GetGuid(0),
            TeamId = reader.GetGuid(1),
            UserId = reader.GetGuid(2),
            Role = reader.GetString(3),
            AssignedByUserId = reader.IsDBNull(4) ? null : reader.GetGuid(4),
            CreatedAt = reader.GetDateTime(5),
            UpdatedAt = reader.GetDateTime(6)
        };
    }

    public async Task<List<TeamMember>> GetTeamMembers(Guid teamId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        using var cmd = new NpgsqlCommand(
            @"SELECT id, team_id, user_id, role, assigned_by_user_id, created_at, updated_at
              FROM team_members
              WHERE team_id = @teamId
              ORDER BY created_at ASC",
            conn);
        cmd.Parameters.AddWithValue("teamId", teamId);

        var members = new List<TeamMember>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            members.Add(new TeamMember
            {
                Id = reader.GetGuid(0),
                TeamId = reader.GetGuid(1),
                UserId = reader.GetGuid(2),
                Role = reader.GetString(3),
                AssignedByUserId = reader.IsDBNull(4) ? null : reader.GetGuid(4),
                CreatedAt = reader.GetDateTime(5),
                UpdatedAt = reader.GetDateTime(6)
            });
        }
        return members;
    }

    public async Task<TeamMember?> GetTeamMember(Guid memberId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        using var cmd = new NpgsqlCommand(
            @"SELECT id, team_id, user_id, role, assigned_by_user_id, created_at, updated_at
              FROM team_members
              WHERE id = @memberId",
            conn);
        cmd.Parameters.AddWithValue("memberId", memberId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new TeamMember
            {
                Id = reader.GetGuid(0),
                TeamId = reader.GetGuid(1),
                UserId = reader.GetGuid(2),
                Role = reader.GetString(3),
                AssignedByUserId = reader.IsDBNull(4) ? null : reader.GetGuid(4),
                CreatedAt = reader.GetDateTime(5),
                UpdatedAt = reader.GetDateTime(6)
            };
        }
        return null;
    }

    public async Task UpdateTeamMemberRole(Guid memberId, string role)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        using var cmd = new NpgsqlCommand(
            "UPDATE team_members SET role = @role, updated_at = NOW() WHERE id = @memberId",
            conn);
        cmd.Parameters.AddWithValue("role", role);
        cmd.Parameters.AddWithValue("memberId", memberId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RemoveTeamMember(Guid memberId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        using var cmd = new NpgsqlCommand(
            "DELETE FROM team_members WHERE id = @memberId",
            conn);
        cmd.Parameters.AddWithValue("memberId", memberId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> IsUserInTeam(Guid userId, Guid teamId)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        using var cmd = new NpgsqlCommand(
            "SELECT EXISTS(SELECT 1 FROM team_members WHERE user_id = @userId AND team_id = @teamId)",
            conn);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("teamId", teamId);
        var result = await cmd.ExecuteScalarAsync();
        return (bool?)result ?? false;
    }
}


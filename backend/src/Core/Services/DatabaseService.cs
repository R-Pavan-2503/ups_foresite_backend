using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Text.Json;

namespace CodeFamily.Api.Core.Services;


public class DatabaseService : IDatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(IOptions<AppSettings> appSettings, IConfiguration configuration)
    {
        // Use the direct PostgreSQL connection string from appsettings/settings.json
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new Exception("ConnectionStrings:DefaultConnection is required in settings.json");
    }

    private NpgsqlConnection GetConnection()
    {
        return new NpgsqlConnection(_connectionString);
    }

    // Users
    public async Task<User?> GetUserByGitHubId(long githubId)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

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
        using var conn = GetConnection();
        await conn.OpenAsync();

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
        using var conn = GetConnection(); await conn.OpenAsync();

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

    public async Task<User> CreateUser(User user)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

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
        using var conn = GetConnection();
        await conn.OpenAsync();

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
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = new NpgsqlCommand("UPDATE users SET email = @email WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("email", email);
        cmd.Parameters.AddWithValue("id", userId);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateUserAuthorName(Guid userId, string authorName)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = new NpgsqlCommand("UPDATE users SET author_name = @authorName WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("authorName", authorName);
        cmd.Parameters.AddWithValue("id", userId);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateUserAvatar(Guid userId, string avatarUrl)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = new NpgsqlCommand("UPDATE users SET avatar_url = @avatarUrl WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("avatarUrl", avatarUrl);
        cmd.Parameters.AddWithValue("id", userId);

        await cmd.ExecuteNonQueryAsync();
    }

    // Repositories
    public async Task<Repository?> GetRepositoryByName(string owner, string name)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

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
        using var conn = GetConnection();
        await conn.OpenAsync();

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
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = new NpgsqlCommand("UPDATE repositories SET status = @status WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("id", repositoryId);

        await cmd.ExecuteNonQueryAsync();
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateLastRefreshTime(Guid repositoryId)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = new NpgsqlCommand("UPDATE repositories SET last_refresh_at = @refreshTime WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("refreshTime", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("id", repositoryId);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateLastAnalyzedCommit(Guid repositoryId, string commitSha)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = new NpgsqlCommand("UPDATE repositories SET last_analyzed_commit_sha = @commitSha, last_refresh_at = @refreshTime WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("commitSha", commitSha);
        cmd.Parameters.AddWithValue("refreshTime", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("id", repositoryId);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<Repository>> GetUserRepositories(Guid userId)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT id, name, owner_username, status, is_active_blocking, connected_by_user_id, is_mine, last_analyzed_commit_sha, last_refresh_at FROM repositories WHERE connected_by_user_id = @userId",
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
        using var conn = GetConnection();
        await conn.OpenAsync();

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

    public async Task<List<Repository>> GetAnalyzedRepositories(Guid userId, string filter)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        string query;
        switch (filter.ToLower())
        {
            case "your":
                // Show repositories that belong to the user (is_mine = TRUE)
                query = "SELECT id, name, owner_username, status, is_active_blocking, connected_by_user_id, is_mine, last_analyzed_commit_sha, last_refresh_at FROM repositories WHERE is_mine = TRUE AND connected_by_user_id = @userId AND (status = 'ready' OR status = 'analyzing' OR status = 'pending') ORDER BY name";
                break;
            case "others":
                // Show repositories that DON'T belong to the user (is_mine = FALSE)
                query = "SELECT id, name, owner_username, status, is_active_blocking, connected_by_user_id, is_mine, last_analyzed_commit_sha, last_refresh_at FROM repositories WHERE is_mine = FALSE AND connected_by_user_id = @userId AND (status = 'ready' OR status = 'analyzing' OR status = 'pending') ORDER BY name";
                break;
            case "all":
            default:
                // Show all analyzed repositories for this user
                query = "SELECT id, name, owner_username, status, is_active_blocking, connected_by_user_id, is_mine, last_analyzed_commit_sha, last_refresh_at FROM repositories WHERE connected_by_user_id = @userId AND (status = 'ready' OR status = 'analyzing' OR status = 'pending') ORDER BY name";
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

    // Branches
    public async Task<Branch> CreateBranch(Branch branch)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

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
        using var conn = GetConnection();
        await conn.OpenAsync();

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
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = new NpgsqlCommand("DELETE FROM branches WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", branchId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<Branch?> GetDefaultBranch(Guid repositoryId)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

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
        using var conn = GetConnection();
        await conn.OpenAsync();

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
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = new NpgsqlCommand("UPDATE branches SET head_commit_sha = @headSha, updated_at = @updated WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("headSha", commitSha);
        cmd.Parameters.AddWithValue("updated", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("id", branchId);

        await cmd.ExecuteNonQueryAsync();
    }

    // Commit-Branch Junction
    public async Task LinkCommitToBranch(Guid commitId, Guid branchId)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

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
        using var conn = GetConnection();
        await conn.OpenAsync();

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
        using var conn = GetConnection();
        await conn.OpenAsync();

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
        using var conn = GetConnection();
        await conn.OpenAsync();

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
        using var conn = GetConnection();
        await conn.OpenAsync();

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
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT id, repository_id, sha, message, committed_at FROM commits WHERE id = @id",
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
                CommittedAt = reader.GetDateTime(4)
            };
        }
        return null;
    }

    public async Task<Commit?> GetCommitBySha(Guid repositoryId, string sha)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = new NpgsqlCommand(
            "SELECT id, repository_id, sha, message, committed_at FROM commits WHERE repository_id = @repoId AND sha = @sha",
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
                CommittedAt = reader.GetDateTime(4)
            };
        }
        return null;
    }


    //files
    public async Task<RepositoryFile?> GetFileByPath(Guid repositoryId, string filePath)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

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
        using var conn = GetConnection();
        await conn.OpenAsync();

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
        using var conn = GetConnection();
        await conn.OpenAsync();

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
        using var conn = GetConnection();
        await conn.OpenAsync();

        // Get files that were modified in commits on this branch using junction table
        using var cmd = new NpgsqlCommand(
            @"SELECT DISTINCT rf.id, rf.repository_id, rf.file_path, rf.total_lines 
              FROM repository_files rf 
              JOIN file_changes fc ON rf.id = fc.file_id 
              JOIN commits c ON fc.commit_id = c.id 
              JOIN commit_branches cb ON c.id = cb.commit_id
              JOIN branches b ON cb.branch_id = b.id
              WHERE rf.repository_id = @repoId AND b.name = @branchName 
              ORDER BY rf.file_path",
            conn);

        cmd.Parameters.AddWithValue("repoId", repositoryId);
        cmd.Parameters.AddWithValue("branchName", branchName);

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
        using var conn = GetConnection();
        await conn.OpenAsync();

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
}



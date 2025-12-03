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
}
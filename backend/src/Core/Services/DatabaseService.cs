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
}
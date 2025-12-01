using CodeFamily.Api.Core.Models;

namespace CodeFamily.Api.Core.Interfaces;

public interface IDatabaseService
{
    // Users
    Task<User?> GetUserByGitHubId(long githubId);
    Task<User?> GetUserByEmail(string email);
    Task<User?> GetUserByAuthorName(string authorName);
    Task<User> CreateUser(User user);
    Task<User?> GetUserById(Guid id);
    Task UpdateUserEmail(Guid userId, string email);
    Task UpdateUserAuthorName(Guid userId, string authorName);
    Task UpdateUserAvatar(Guid userId, string avatarUrl);

}
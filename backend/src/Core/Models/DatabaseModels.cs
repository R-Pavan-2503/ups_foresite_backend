namespace CodeFamily.Api.Core.Models;

public class User
{
    public Guid Id { get; set; }
    public long GithubId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
}
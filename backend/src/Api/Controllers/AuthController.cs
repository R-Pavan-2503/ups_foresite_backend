using Microsoft.AspNetCore.Mvc;
using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;

namespace CodeFamily.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IGitHubService _github;
    private readonly IDatabaseService _db;

    public AuthController(IGitHubService github, IDatabaseService db)
    {
        _github = github;
        _db = db;
    }

    [HttpGet("github")]
    public IActionResult GitHubLogin([FromQuery] string redirectUri)
    {
        var url = _github.GetOAuthUrl(redirectUri);
        return Ok(new { url });
    }

    [HttpPost("github/callback")]
    public async Task<IActionResult> GitHubCallback([FromBody] GitHubCallbackRequest request)
    {
        try
        {
            // Exchange code for token
            var accessToken = await _github.ExchangeCodeForToken(request.Code);

            // Get user info
            var githubUser = await _github.GetAuthenticatedUser(accessToken);

            // If email is null, try to fetch it from the emails API
            if (string.IsNullOrWhiteSpace(githubUser.Email))
            {
                var emails = await _github.GetUserEmails(accessToken);
                githubUser.Email = emails;
            }

            // Get or create user
            var user = await _db.GetUserByGitHubId(githubUser.Id);
            if (user == null)
            {
                user = await _db.CreateUser(new User
                {
                    GithubId = githubUser.Id,
                    AuthorName = githubUser.Login,
                    Email = githubUser.Email,
                    AvatarUrl = githubUser.AvatarUrl
                });
            }
            else
            {
                // Update existing user with latest GitHub data
                if (!string.IsNullOrWhiteSpace(githubUser.Email) && user.Email != githubUser.Email)
                {
                    await _db.UpdateUserEmail(user.Id, githubUser.Email);
                    user.Email = githubUser.Email;
                }
                if (user.AuthorName != githubUser.Login)
                {
                    await _db.UpdateUserAuthorName(user.Id, githubUser.Login);
                    user.AuthorName = githubUser.Login;
                }
                if (user.AvatarUrl != githubUser.AvatarUrl)
                {
                    await _db.UpdateUserAvatar(user.Id, githubUser.AvatarUrl);
                    user.AvatarUrl = githubUser.AvatarUrl;
                }
            }

            return Ok(new
            {
                user = new
                {
                    user.Id,
                    user.AuthorName,
                    user.Email,
                    user.AvatarUrl
                },
                accessToken
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class GitHubCallbackRequest
{
    public string Code { get; set; } = string.Empty;
}

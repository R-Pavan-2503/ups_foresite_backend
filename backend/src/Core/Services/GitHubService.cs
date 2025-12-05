using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;
using Microsoft.Extensions.Options;
using Octokit;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace CodeFamily.Api.Core.Services;

/// <summary>
/// GitHub integration service.
/// Handles OAuth, API calls, webhooks, and status checks.
/// 
/// IMPORTANT PEM FILE REQUIREMENT:
/// Before running, place your GitHub App private key at /secrets/codefamily.pem
/// </summary>
public class GitHubService : IGitHubService
{
    private readonly GitHubSettings _settings;
    private readonly GitHubClient _client;

    public GitHubService(IOptions<AppSettings> appSettings)
    {
        _settings = appSettings.Value.GitHub;
        _client = new GitHubClient(new ProductHeaderValue("CodeFamily"));
    }

    // OAuth
    public string GetOAuthUrl(string redirectUri)
    {
        return $"https://github.com/login/oauth/authorize?client_id={_settings.ClientId}&redirect_uri={redirectUri}&scope=repo,user:email";
    }

    public async Task<string> ExchangeCodeForToken(string code)
    {
        using var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token");

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", _settings.ClientId),
            new KeyValuePair<string, string>("client_secret", _settings.ClientSecret),
            new KeyValuePair<string, string>("code", code)
        });

        request.Content = content;
        request.Headers.Add("Accept", "application/json");

        var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        var json = System.Text.Json.JsonDocument.Parse(responseBody);
        return json.RootElement.GetProperty("access_token").GetString() ?? throw new Exception("No access token received");
    }

    public async Task<GitHubUserDto> GetAuthenticatedUser(string accessToken)
    {
        var client = new GitHubClient(new ProductHeaderValue("CodeFamily"))
        {
            Credentials = new Credentials(accessToken)
        };

        var user = await client.User.Current();

        return new GitHubUserDto
        {
            Id = user.Id,
            Login = user.Login,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl
        };
    }

    public async Task<string?> GetUserEmails(string accessToken)
    {
        var client = new GitHubClient(new ProductHeaderValue("CodeFamily"))
        {
            Credentials = new Credentials(accessToken)
        };

        try
        {
            var emails = await client.User.Email.GetAll();
            // Get the primary email, or the first verified email
            var primaryEmail = emails.FirstOrDefault(e => e.Primary && e.Verified);
            if (primaryEmail != null) return primaryEmail.Email;

            var verifiedEmail = emails.FirstOrDefault(e => e.Verified);
            if (verifiedEmail != null) return verifiedEmail.Email;

            return emails.FirstOrDefault()?.Email;
        }
        catch
        {
            return null;
        }
    }

    // Repositories
    public async Task<List<Octokit.Repository>> GetUserRepositories(string accessToken)
    {
        var client = new GitHubClient(new ProductHeaderValue("CodeFamily"))
        {
            Credentials = new Credentials(accessToken)
        };

        var repos = await client.Repository.GetAllForCurrent();
        return repos.ToList();
    }

    public async Task<Octokit.Repository> GetRepository(string owner, string repo)
    {
        return await _client.Repository.Get(owner, repo);
    }
    // Commits
    public async Task<IReadOnlyList<GitHubCommit>> GetCommits(string owner, string repo)
    {
        var request = new CommitRequest { };
        return await _client.Repository.Commit.GetAll(owner, repo, request);
    }

    public async Task<GitHubCommit> GetCommit(string owner, string repo, string sha, string? accessToken = null)
    {
        if (!string.IsNullOrEmpty(accessToken))
        {
            var client = new GitHubClient(new ProductHeaderValue("CodeFamily"))
            {
                Credentials = new Credentials(accessToken)
            };
            return await client.Repository.Commit.Get(owner, repo, sha);
        }

        try
        {
            // Try unauthenticated first
            return await _client.Repository.Commit.Get(owner, repo, sha);
        }
        catch (Exception)
        {
            // Fallback to authenticated
            var installationToken = await GetInstallationTokenForRepo(owner, repo);
            var client = new GitHubClient(new ProductHeaderValue("CodeFamily"))
            {
                Credentials = new Credentials(installationToken)
            };
            return await client.Repository.Commit.Get(owner, repo, sha);
        }
    }

    public async Task<GitHubCommitAuthor?> GetCommitAuthor(string owner, string repo, string sha, string? accessToken = null)
    {
        try
        {
            var commit = await GetCommit(owner, repo, sha, accessToken);

            // Extract author info from commit.Author (GitHub user)
            if (commit?.Author != null)
            {
                return new GitHubCommitAuthor
                {
                    Login = commit.Author.Login,
                    Id = commit.Author.Id,
                    AvatarUrl = commit.Author.AvatarUrl
                };
            }
            return null;
        }
        catch (Exception)
        {
            // Author not found or API error
            return null;
        }
    }

    // Pull Requests
    public async Task<IReadOnlyList<Octokit.PullRequest>> GetPullRequests(string owner, string repo, PullRequestRequest? request = null)
    {
        request ??= new PullRequestRequest();
        try
        {
            return await _client.PullRequest.GetAllForRepository(owner, repo, request);
        }
        catch (Exception)
        {
            var installationToken = await GetInstallationTokenForRepo(owner, repo);
            var client = new GitHubClient(new ProductHeaderValue("CodeFamily"))
            {
                Credentials = new Credentials(installationToken)
            };
            return await client.PullRequest.GetAllForRepository(owner, repo, request);
        }
    }

    public async Task<Octokit.PullRequest> GetPullRequest(string owner, string repo, int number, string? accessToken = null)
    {
        if (!string.IsNullOrEmpty(accessToken))
        {
            var client = new GitHubClient(new ProductHeaderValue("CodeFamily"))
            {
                Credentials = new Credentials(accessToken)
            };
            return await client.PullRequest.Get(owner, repo, number);
        }

        try
        {
            return await _client.PullRequest.Get(owner, repo, number);
        }
        catch (Exception)
        {
            var installationToken = await GetInstallationTokenForRepo(owner, repo);
            var client = new GitHubClient(new ProductHeaderValue("CodeFamily"))
            {
                Credentials = new Credentials(installationToken)
            };
            return await client.PullRequest.Get(owner, repo, number);
        }
    }

    public async Task<IReadOnlyList<PullRequestFile>> GetPullRequestFiles(string owner, string repo, int number, string? accessToken = null)
    {
        if (!string.IsNullOrEmpty(accessToken))
        {
            var client = new GitHubClient(new ProductHeaderValue("CodeFamily"))
            {
                Credentials = new Credentials(accessToken)
            };
            return await client.PullRequest.Files(owner, repo, number);
        }

        try
        {
            return await _client.PullRequest.Files(owner, repo, number);
        }
        catch (Exception)
        {
            var installationToken = await GetInstallationTokenForRepo(owner, repo);
            var client = new GitHubClient(new ProductHeaderValue("CodeFamily"))
            {
                Credentials = new Credentials(installationToken)
            };
            return await client.PullRequest.Files(owner, repo, number);
        }
    }

    // Webhooks
    public async Task<RepositoryHook> CreateWebhook(string owner, string repo, string webhookUrl, string secret)
    {
        // Use installation token
        var installationToken = await GetInstallationTokenForRepo(owner, repo);
        var authenticatedClient = new GitHubClient(new ProductHeaderValue("CodeFamily"))
        {
            Credentials = new Credentials(installationToken)
        };

        var config = new Dictionary<string, string>
        {
            { "url", $"{webhookUrl}/webhooks/github" },
            { "content_type", "json" },
            { "secret", secret }
        };

        var hook = new NewRepositoryHook("web", config)
        {
            Events = new[] { "push", "pull_request", "pull_request_target" },
            Active = true
        };

        return await authenticatedClient.Repository.Hooks.Create(owner, repo, hook);
    }

    public async Task<bool> VerifyWebhookSignature(string payload, string signature, string secret)
    {
        if (string.IsNullOrEmpty(signature) || !signature.StartsWith("sha256="))
            return false;

        var expectedSignature = signature.Substring(7); // Remove "sha256="

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var computedSignature = BitConverter.ToString(hash).Replace("-", "").ToLower();

        return expectedSignature == computedSignature;
    }

    public async Task RegisterWebhook(string owner, string repo)
    {
        try
        {
            // Check if webhook URL is configured
            var webhookUrl = Environment.GetEnvironmentVariable("WEBHOOK_URL")
                ?? "https://conflagrant-alleen-balletic.ngrok-free.dev";

            await CreateWebhook(owner, repo, webhookUrl, _settings.WebhookSecret);
        }
        catch (Exception ex)
        {
            // Webhook might already exist - this is non-fatal
            Console.WriteLine($"Webhook registration note: {ex.Message}");
        }
    }

}
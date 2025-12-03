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
}
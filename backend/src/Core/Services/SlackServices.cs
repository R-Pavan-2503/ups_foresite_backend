using CodeFamily.Api.Core.Interfaces;
using Microsoft.Extensions.Options;
using CodeFamily.Api.Core.Models;
using System.Text;
using System.Text.Json;

namespace CodeFamily.Api.Core.Services;

/// <summary>
/// Slack notification service.
/// Sends DM alerts when conflicts are detected.
/// </summary>
public class SlackService : ISlackService
{
    private readonly string _botToken;
    private readonly HttpClient _client;

    public SlackService(IOptions<AppSettings> appSettings, HttpClient httpClient)
    {
        _botToken = appSettings.Value.Slack.BotToken;
        _client = httpClient;
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_botToken}");
    }

    public async Task SendDirectMessage(string userId, string message)
    {
        // Open conversation
        var openUrl = "https://slack.com/api/conversations.open";
        var openBody = new { users = userId };
        var openJson = JsonSerializer.Serialize(openBody);
        var openContent = new StringContent(openJson, Encoding.UTF8, "application/json");

        var openResponse = await _client.PostAsync(openUrl, openContent);
        var openResponseBody = await openResponse.Content.ReadAsStringAsync();
        var openDoc = JsonDocument.Parse(openResponseBody);
        var channelId = openDoc.RootElement.GetProperty("channel").GetProperty("id").GetString();

        // Send message
        await PostMessage(channelId!, message);
    }

    public async Task PostMessage(string channel, string message)
    {
        var url = "https://slack.com/api/chat.postMessage";

        var requestBody = new
        {
            channel,
            text = message
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        await _client.PostAsync(url, content);
    }
}

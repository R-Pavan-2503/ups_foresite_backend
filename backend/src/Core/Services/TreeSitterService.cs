using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace CodeFamily.Api.Core.Services;

/// <summary>
/// Client for Node.js Tree-sitter sidecar service.
/// Sends code to be parsed and receives functions + imports.
/// </summary>
public class TreeSitterService : ITreeSitterService
{
    private readonly string _sidecarUrl;
    private readonly HttpClient _client;

    public TreeSitterService(IOptions<AppSettings> appSettings, HttpClient httpClient)
    {
        _sidecarUrl = appSettings.Value.Sidecar.Url;
        _client = httpClient;
    }

    public async Task<ParseResult> ParseCode(string code, string language)
    {
        var url = $"{_sidecarUrl}/parse";

        var requestBody = new { code, language };
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ParseResult>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result ?? new ParseResult();
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error calling sidecar: {ex.Message}");
            return new ParseResult(); // Return empty if sidecar is down
        }
    }
}

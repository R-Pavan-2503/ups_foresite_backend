using System.Text;
using System.Text.Json;
using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;
using Microsoft.Extensions.Options;

namespace CodeFamily.Api.Core.Services;

public class GroqService : IGroqService
{
    private readonly HttpClient _httpClient;
    private readonly GroqSettings _settings;
    private readonly ILogger<GroqService> _logger;

    public GroqService(
        IHttpClientFactory httpClientFactory,
        IOptions<AppSettings> appSettings,
        ILogger<GroqService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _settings = appSettings.Value.Groq;
        _logger = logger;
    }

    public async Task<(bool success, string? summary, string? error)> GenerateFileSummaryAsync(List<string> codeChunks)
    {
        try
        {
            if (codeChunks == null || !codeChunks.Any())
            {
                return (false, null, "No code chunks provided");
            }

            // Limit chunks to avoid hitting rate limits
            // Groq free tier: 12K tokens/min - keep request under ~2K tokens
            var limitedChunks = LimitChunks(codeChunks, maxChunks: 8, maxTotalChars: 6000);
            _logger.LogInformation($"Processing {limitedChunks.Count} chunks (from {codeChunks.Count} total)");

            // Combine chunks with separators
            var chunksText = string.Join("\n\n---\n\n", limitedChunks);

            // Create intelligent prompt that handles mismatched/fragmented chunks
            var prompt = $@"You are a senior software architect analyzing code chunks from a single file.

IMPORTANT: The code chunks below may be:
- Out of chronological order
- Fragmented or incomplete
- Missing context (imports, comments, etc.)

Your task:
1. Identify the overall purpose of this file
2. Understand what problem it solves
3. Recognize key patterns and responsibilities
4. Provide a concise 2-5 line summary

Focus on:
✓ What this file DOES (not how)
✓ Its role in the codebase
✓ Main functionality or exports

✗ Do not list every function
✗ Do not mention that chunks are fragmented

Code Chunks:
---
{chunksText}
---

Summary (2-5 lines):";

            // Retry logic with exponential backoff
            int maxRetries = 3;
            int delayMs = 1000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                var result = await CallGroqApiAsync(prompt);
                
                if (result.success)
                {
                    return result;
                }

                // If rate limited, wait and retry
                if (result.error?.Contains("Too many requests") == true && attempt < maxRetries)
                {
                    _logger.LogWarning($"Rate limited, retrying in {delayMs}ms (attempt {attempt}/{maxRetries})");
                    await Task.Delay(delayMs);
                    delayMs *= 2; // Exponential backoff
                    continue;
                }

                return result;
            }

            return (false, null, "Failed after multiple retries");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error generating file summary: {ex.Message}");
            return (false, null, "An unexpected error occurred");
        }
    }

    private List<string> LimitChunks(List<string> chunks, int maxChunks, int maxTotalChars)
    {
        if (chunks.Count <= maxChunks)
        {
            // If within limit, just truncate total characters
            var result = new List<string>();
            int totalChars = 0;
            foreach (var chunk in chunks)
            {
                if (totalChars + chunk.Length > maxTotalChars)
                {
                    // Truncate this chunk to fit
                    var remaining = maxTotalChars - totalChars;
                    if (remaining > 100)
                    {
                        result.Add(chunk.Substring(0, Math.Min(remaining, chunk.Length)) + "...");
                    }
                    break;
                }
                result.Add(chunk);
                totalChars += chunk.Length;
            }
            return result;
        }

        // Take representative sample: first few, some middle, last few
        var limited = new List<string>();
        int firstCount = maxChunks / 3;
        int lastCount = maxChunks / 3;
        int middleCount = maxChunks - firstCount - lastCount;

        // First chunks (usually imports, class definitions)
        limited.AddRange(chunks.Take(firstCount));

        // Middle chunks (core logic)
        int middleStart = chunks.Count / 2 - middleCount / 2;
        limited.AddRange(chunks.Skip(middleStart).Take(middleCount));

        // Last chunks (often important exports/main logic)
        limited.AddRange(chunks.TakeLast(lastCount));

        // Apply character limit
        return LimitChunks(limited, maxChunks, maxTotalChars);
    }

    private async Task<(bool success, string? summary, string? error)> CallGroqApiAsync(string prompt)
    {
        try
        {
            var requestBody = new
            {
                model = _settings.Model,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                },
                max_tokens = _settings.MaxTokens,
                temperature = _settings.Temperature
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Create fresh request with authorization
            using var request = new HttpRequestMessage(HttpMethod.Post, _settings.ApiUrl);
            request.Headers.Add("Authorization", $"Bearer {_settings.ApiKey}");
            request.Content = httpContent;

            _logger.LogInformation("Calling Groq API to generate file summary...");
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Groq API error: {response.StatusCode} - {errorContent}");

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    return (false, null, "Too many requests. Please try again later.");
                }

                return (false, null, "Service temporarily unavailable");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var responseJson = JsonSerializer.Deserialize<JsonElement>(responseContent);

            var summary = responseJson
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(summary))
            {
                return (false, null, "Failed to generate summary");
            }

            _logger.LogInformation("Successfully generated file summary");
            return (true, summary.Trim(), null);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError($"Network error calling Groq API: {ex.Message}");
            return (false, null, "Network error. Please check your connection.");
        }
    }
}

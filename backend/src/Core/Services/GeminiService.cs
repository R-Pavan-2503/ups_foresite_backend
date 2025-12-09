using CodeFamily.Api.Core.Interfaces;
using Microsoft.Extensions.Options;
using CodeFamily.Api.Core.Models;
using System.Text;
using System.Text.Json;

namespace CodeFamily.Api.Core.Services;

/// <summary>
/// Gemini API service for generating embeddings.
/// Uses the text-embedding-004 model which produces 768-dimensional vectors.
/// 
/// Stakeholder Value:
/// - Embeddings measure SEMANTICS, not syntax
/// - "Lines of code" doesn't equal expertise
/// - Vector distances capture conceptual understanding
/// </summary>
public class GeminiService : IGeminiService
{
    private readonly string _apiKey;
    private readonly HttpClient _client;
    private const string EMBEDDING_MODEL = "text-embedding-004";

    public GeminiService(IOptions<AppSettings> appSettings, HttpClient httpClient)
    {
        _apiKey = appSettings.Value.Gemini.ApiKey;
        _client = httpClient;
    }

    public async Task<float[]> GenerateEmbedding(string text)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{EMBEDDING_MODEL}:embedContent?key={_apiKey}";

        var requestBody = new
        {
            model = "models/text-embedding-004",

            content = new
            {
                parts = new[] { new { text } }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync(url, content);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(responseBody);

        var embedding = doc.RootElement
            .GetProperty("embedding")
            .GetProperty("values")
            .EnumerateArray()
            .Select(e => (float)e.GetDouble())
            .ToArray();

        // Verify 768 dimensions
        if (embedding.Length != 768)
        {
            throw new Exception($"Expected 768 dimensions, got {embedding.Length}");
        }

        return embedding;
    }

    public double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have same length");

        double dotProduct = 0;
        double magnitudeA = 0;
        double magnitudeB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        magnitudeA = Math.Sqrt(magnitudeA);
        magnitudeB = Math.Sqrt(magnitudeB);

        if (magnitudeA == 0 || magnitudeB == 0)
            return 0;

        return dotProduct / (magnitudeA * magnitudeB);
    }
}

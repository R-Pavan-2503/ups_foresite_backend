namespace CodeFamily.Api.Core.Interfaces;

public interface IGeminiService
{
    /// <summary>
    /// Generate embeddings for code text using Gemini API.
    /// Returns 768-dimensional vector.
    /// </summary>
    Task<float[]> GenerateEmbedding(string text);

    /// <summary>
    /// Calculate cosine similarity between two vectors.
    /// Returns value between -1 and 1, where 1 is identical.
    /// </summary>
    double CosineSimilarity(float[] a, float[] b);
}

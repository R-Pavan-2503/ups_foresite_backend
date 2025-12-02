namespace CodeFamily.Api.Core.Interfaces;

public interface IGeminiService
{
    Task<float[]> GenerateEmbedding(string text);

    double CosineSimilarity(float[] a, float[] b);
}

namespace CodeFamily.Api.Core.Interfaces;

public interface IGroqService
{
    Task<(bool success, string? summary, string? error)> GenerateFileSummaryAsync(List<string> codeChunks);
}

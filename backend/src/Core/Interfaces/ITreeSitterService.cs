using CodeFamily.Api.Core.Models;

namespace CodeFamily.Api.Core.Interfaces;

public interface ITreeSitterService
{
    /// <summary>
    /// Parse source code and extract functions and imports.
    /// Calls the Node.js sidecar service.
    /// </summary>
    Task<ParseResult> ParseCode(string code, string language);
}

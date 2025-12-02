using CodeFamily.Api.Core.Models;

namespace CodeFamily.Api.Core.Interfaces;

public interface ITreeSitterService
{




    Task<ParseResult> ParseCode(string code, string language);
}

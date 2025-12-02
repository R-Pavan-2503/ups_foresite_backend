namespace CodeFamily.Api.Core.Models;

// DTOs for API responses and requests

public class GitHubUserDto
{
    public long Id { get; set; }
    public string Login { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
}

public class RepositoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OwnerUsername { get; set; } = string.Empty;
    public string? Status { get; set; }
    public bool IsActiveBlocking { get; set; }
}

public class CommitDto
{
    public Guid Id { get; set; }
    public string Sha { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DateTime CommittedAt { get; set; }
    public string? AuthorName { get; set; }
    public string? AuthorEmail { get; set; }
}

public class FileDto
{
    public Guid Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int? TotalLines { get; set; }
}

public class FileAnalysisDto
{
    public string FilePath { get; set; } = string.Empty;
    public string? Purpose { get; set; }
    public List<OwnershipDto> Owners { get; set; } = new();
    public List<DependencyDto> Dependencies { get; set; } = new();
    public List<DependencyDto> Dependents { get; set; } = new();
    public List<FileDto> SemanticNeighbors { get; set; } = new();
    public int ChangeCount { get; set; }
    public string? MostFrequentAuthor { get; set; }
    public DateTime? LastModified { get; set; }
    public bool IsInOpenPr { get; set; }
}

public class OwnershipDto
{
    public string Username { get; set; } = string.Empty;
    public decimal SemanticScore { get; set; }
}

public class DependencyDto
{
    public string FilePath { get; set; } = string.Empty;
    public string? DependencyType { get; set; }
    public int? Strength { get; set; }
}

public class PullRequestDto
{
    public Guid Id { get; set; }
    public int PrNumber { get; set; }
    public string? State { get; set; }
    public string? AuthorUsername { get; set; }
    public string? Title { get; set; }
    public double? RiskScore { get; set; }
}

public class ParsedFunction
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int EndLine { get; set; }
}

public class ParsedImport
{
    public string Module { get; set; } = string.Empty;
    public string? ImportedName { get; set; }
}

public class ParseResult
{
    public List<ParsedFunction> Functions { get; set; } = new();
    public List<ParsedImport> Imports { get; set; } = new();
}

public class EmbeddingResponse
{
    public float[] Embedding { get; set; } = Array.Empty<float>();
}

public class RiskAnalysisResult
{
    public double RiskScore { get; set; }
    public double StructuralOverlap { get; set; }
    public double SemanticOverlap { get; set; }
    public List<ConflictingPr> ConflictingPrs { get; set; } = new();
}

public class ConflictingPr
{
    public int PrNumber { get; set; }
    public string? Title { get; set; }
    public double Risk { get; set; }
    public List<string> ConflictingFiles { get; set; } = new();
}

public class GitHubCommitAuthor
{
    public string Login { get; set; } = string.Empty;
    public long Id { get; set; }
    public string? AvatarUrl { get; set; }
}


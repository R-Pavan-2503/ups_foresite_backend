namespace CodeFamily.Api.Core.Models;

public class AppSettings
{
    public GitHubSettings GitHub { get; set; } = new();
    public GeminiSettings Gemini { get; set; } = new();
    public SupabaseSettings Supabase { get; set; } = new();
    public SlackSettings Slack { get; set; } = new();
    public SidecarSettings Sidecar { get; set; } = new();
    public string WebhookUrl { get; set; } = string.Empty;
    public string CloneBasePath { get; set; } = "./repos";
}

public class GitHubSettings
{
    public string AppId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string PrivateKeyPath { get; set; } = string.Empty;
}

public class GeminiSettings
{
    public string ApiKey { get; set; } = string.Empty;
}

public class SupabaseSettings
{
    public string Url { get; set; } = string.Empty;
    public string ServiceKey { get; set; } = string.Empty;
}

public class SlackSettings
{
    public string BotToken { get; set; } = string.Empty;
}

public class SidecarSettings
{
    public string Url { get; set; } = "http://localhost:3001";
}

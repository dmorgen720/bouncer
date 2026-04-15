namespace LinkedInAutoReply.Models;

public class GraphSettings
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string LinkedInFolderName { get; set; } = "LinkedIn Recruiters";
    public List<string> ExcludedSenders { get; set; } = [];
}

public class AISettings
{
    public string Provider { get; set; } = "Ollama";
    public OllamaSettings Ollama { get; set; } = new();
    public OpenAISettings OpenAI { get; set; } = new();
}

public class OllamaSettings
{
    public string Endpoint { get; set; } = "http://localhost:11434";
    public string ModelId { get; set; } = "gemma3:12b";
}

public class OpenAISettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string ModelId { get; set; } = "gpt-4o";
}

public class AutoReplySettings
{
    public bool DraftOnly { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 60;
}

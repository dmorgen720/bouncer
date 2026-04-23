using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace LinkedInAutoReply.Services;

public class JobOfferClassifier(
    IChatClient chatClient,
    IWebHostEnvironment env,
    ILogger<JobOfferClassifier> logger)
{
    private const double ConfidenceThreshold = 0.7;
    private string? _cachedPrompt;

    private record ClassifierOutput(
        [property: JsonPropertyName("isJobOffer")] bool IsJobOffer,
        [property: JsonPropertyName("confidence")] double Confidence);

    private string LoadPrompt()
    {
        if (_cachedPrompt != null) return _cachedPrompt;

        var path = Path.Combine(env.ContentRootPath, "Prompts", "classifier.md");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Classifier prompt not found at '{path}'. Ensure Prompts/classifier.md is present and copied to output.", path);

        _cachedPrompt = File.ReadAllText(path);
        logger.LogInformation("Classifier prompt loaded from {Path}", path);
        return _cachedPrompt;
    }

    public async Task<bool> IsJobOfferAsync(
        string subject, string from, string bodyPreview, CancellationToken ct = default)
    {
        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, LoadPrompt()),
                new(ChatRole.User, $"From: {from}\nSubject: {subject}\nPreview: {bodyPreview}")
            };

            var options = new ChatOptions { ResponseFormat = ChatResponseFormat.Json };
            var response = await chatClient.GetResponseAsync(messages, options, ct);
            var result = JsonSerializer.Deserialize<ClassifierOutput>(ExtractJson(response.Text ?? "{}"))
                         ?? new ClassifierOutput(true, 1.0);

            logger.LogInformation(
                "Classified [{Subject}] from {From} → isJobOffer={IsJobOffer} confidence={Confidence:P0}",
                subject, from, result.IsJobOffer, result.Confidence);

            return result.IsJobOffer && result.Confidence >= ConfidenceThreshold;
        }
        catch (Exception ex)
        {
            // On failure, pass through — better to assess and discard than silently drop
            logger.LogWarning(ex, "Classification failed for [{Subject}] — treating as job offer", subject);
            return true;
        }
    }

    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }
}

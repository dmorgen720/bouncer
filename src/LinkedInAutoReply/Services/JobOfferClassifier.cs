using System.Text.Json;
using Microsoft.Extensions.AI;

namespace LinkedInAutoReply.Services;

public class JobOfferClassifier(
    IChatClient chatClient,
    IWebHostEnvironment env,
    ILogger<JobOfferClassifier> logger)
{
    private const double ConfidenceThreshold = 0.7;
    private string? _cachedPrompt;

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

    /// <summary>
    /// Returns true if the email looks like a targeted recruiter/job offer message.
    /// Uses subject + from + body preview — no full body fetch required.
    /// </summary>
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

            var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);
            var raw = response.Text ?? string.Empty;

            var json = ExtractJson(raw);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var isJobOffer = root.TryGetProperty("isJobOffer", out var flag) && flag.GetBoolean();
            var confidence = root.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : 1.0;

            logger.LogInformation(
                "Classified [{Subject}] from {From} → isJobOffer={IsJobOffer} confidence={Confidence:P0}",
                subject, from, isJobOffer, confidence);

            return isJobOffer && confidence >= ConfidenceThreshold;
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

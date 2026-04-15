using System.Text.Json;
using LinkedInAutoReply.Models;
using Microsoft.Extensions.AI;

namespace LinkedInAutoReply.Services;

public class RecruitmentAssessor(
    IChatClient chatClient,
    IWebHostEnvironment env,
    ILogger<RecruitmentAssessor> logger)
{
    private string? _cachedPrompt;

    private string LoadPrompt()
    {
        if (_cachedPrompt != null) return _cachedPrompt;

        var path = Path.Combine(env.ContentRootPath, "Prompts", "assessment.md");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Assessment prompt not found at '{path}'. Ensure Prompts/assessment.md is present and copied to output.", path);

        _cachedPrompt = File.ReadAllText(path);
        logger.LogInformation("Assessment prompt loaded from {Path}", path);
        return _cachedPrompt;
    }

    public async Task<AssessmentResult> AssessAsync(string messageText, CancellationToken ct = default)
    {
        string? rawResponse = null;
        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, LoadPrompt()),
                new(ChatRole.User, messageText)
            };

            var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);
            rawResponse = response.Text ?? string.Empty;

            logger.LogDebug("LLM raw response: {Response}", rawResponse);
            var json = ExtractJson(rawResponse);
            return ParseResult(json);
        }
        catch (Exception ex)
        {
            var detail = $"{ex.GetType().Name}: {ex.Message}";
            if (rawResponse != null)
            {
                var preview = rawResponse.Length > 120 ? rawResponse[..120] + "…" : rawResponse;
                detail += $" | LLM output: {preview}";
            }
            logger.LogError(ex, "Error assessing recruiter message. Raw LLM output: {Raw}", rawResponse);
            return new AssessmentResult
            {
                Verdict = AssessmentVerdict.NoMatch,
                AcceptDraft = string.Empty,
                DeclineDraft = string.Empty,
                Filters =
                [
                    new("Location", FilterStatus.Warn, detail),
                    new("Role Type", FilterStatus.Warn, detail),
                    new("Seniority", FilterStatus.Warn, detail)
                ]
            };
        }
    }

    private static AssessmentResult ParseResult(string json)
    {
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var verdict = root.GetProperty("assessment").GetString() switch
        {
            "Match" => AssessmentVerdict.Match,
            "Partial" => AssessmentVerdict.Partial,
            _ => AssessmentVerdict.NoMatch
        };

        var filters = new List<FilterResult>();
        if (root.TryGetProperty("filters", out var filtersEl))
        {
            foreach (var f in filtersEl.EnumerateArray())
            {
                var status = f.GetProperty("status").GetString() switch
                {
                    "Pass" => FilterStatus.Pass,
                    "Fail" => FilterStatus.Fail,
                    _ => FilterStatus.Warn
                };
                filters.Add(new FilterResult(
                    f.GetProperty("name").GetString() ?? string.Empty,
                    status,
                    f.GetProperty("reason").GetString() ?? string.Empty));
            }
        }

        return new AssessmentResult
        {
            Verdict = verdict,
            RecruitingCompany = root.TryGetProperty("recruitingCompany", out var rc) ? rc.GetString() ?? string.Empty : string.Empty,
            HiringCompany = root.TryGetProperty("hiringCompany", out var hc) ? hc.GetString() : null,
            Filters = filters,
            AcceptDraft = root.TryGetProperty("acceptDraft", out var ad) ? ad.GetString() ?? string.Empty : string.Empty,
            DeclineDraft = root.TryGetProperty("declineDraft", out var dd) ? dd.GetString() ?? string.Empty : string.Empty,
            ReplyLanguage = root.TryGetProperty("replyLanguage", out var rl) ? rl.GetString() ?? "en" : "en"
        };
    }

    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }
}

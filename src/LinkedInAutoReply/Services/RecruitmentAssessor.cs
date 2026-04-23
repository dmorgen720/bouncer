using System.Text.Json;
using System.Text.Json.Serialization;
using LinkedInAutoReply.Models;
using Microsoft.Extensions.AI;

namespace LinkedInAutoReply.Services;

public class RecruitmentAssessor(
    IChatClient chatClient,
    IWebHostEnvironment env,
    ILogger<RecruitmentAssessor> logger)
{
    private string? _cachedPrompt;

    private record FilterResultDto(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("reason")] string Reason);

    private record AssessmentOutput(
        [property: JsonPropertyName("recruitingCompany")] string RecruitingCompany,
        [property: JsonPropertyName("hiringCompany")] string? HiringCompany,
        [property: JsonPropertyName("assessment")] string Assessment,
        [property: JsonPropertyName("filters")] List<FilterResultDto> Filters,
        [property: JsonPropertyName("acceptDraft")] string AcceptDraft,
        [property: JsonPropertyName("declineDraft")] string DeclineDraft,
        [property: JsonPropertyName("replyLanguage")] string ReplyLanguage);

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

            var schema = AIJsonUtilities.CreateJsonSchema(typeof(AssessmentOutput));
            var options = new ChatOptions
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema(schema, "AssessmentOutput")
            };

            var response = await chatClient.GetResponseAsync(messages, options, ct);
            rawResponse = response.Text ?? string.Empty;

            logger.LogDebug("LLM raw response: {Response}", rawResponse);

            var output = JsonSerializer.Deserialize<AssessmentOutput>(rawResponse)
                         ?? throw new InvalidOperationException("LLM returned null or empty JSON");

            return MapToResult(output);
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

    private static AssessmentResult MapToResult(AssessmentOutput output) => new()
    {
        Verdict = output.Assessment switch
        {
            "Match" => AssessmentVerdict.Match,
            "Partial" => AssessmentVerdict.Partial,
            _ => AssessmentVerdict.NoMatch
        },
        RecruitingCompany = output.RecruitingCompany,
        HiringCompany = output.HiringCompany,
        Filters = output.Filters.Select(f => new FilterResult(
            f.Name,
            f.Status switch
            {
                "Pass" => FilterStatus.Pass,
                "Fail" => FilterStatus.Fail,
                _ => FilterStatus.Warn
            },
            f.Reason)).ToList(),
        AcceptDraft = output.AcceptDraft,
        DeclineDraft = output.DeclineDraft,
        ReplyLanguage = output.ReplyLanguage
    };
}

using Microsoft.Extensions.AI;

namespace LinkedInAutoReply.Services;

public class MergeService(IChatClient chatClient, ILogger<MergeService> logger)
{
    public async Task<string> MergeNoteAsync(string existingDraft, string userNote, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userNote))
            return existingDraft;

        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System,
                    "You are a writing assistant. Merge the user's note into the existing draft email " +
                    "naturally. Keep the tone and length. Do not add new paragraphs unless needed. " +
                    "Return only the final merged email body, no preamble."),
                new(ChatRole.User,
                    $"DRAFT:\n{existingDraft}\n\nUSER NOTE:\n{userNote}")
            };

            var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);
            return response.Text?.Trim() ?? existingDraft;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error merging user note into draft");
            return existingDraft;
        }
    }
}

using Azure.Identity;
using LinkedInAutoReply.Models;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.Messages.Item.CreateReply;

namespace LinkedInAutoReply.Services;

public class GraphDraftService
{
    private readonly GraphServiceClient _client;
    private readonly GraphSettings _settings;
    private readonly ILogger<GraphDraftService> _logger;

    public GraphDraftService(GraphSettings settings, ILogger<GraphDraftService> logger)
    {
        _settings = settings;
        _logger = logger;

        var credential = new ClientSecretCredential(
            settings.TenantId, settings.ClientId, settings.ClientSecret);

        _client = new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
    }

    /// <summary>
    /// Creates a reply draft in the mailbox Drafts folder, threaded to the original message.
    /// Returns the draft message ID on success, throws on failure.
    /// </summary>
    public async Task<string> SaveReplyDraftAsync(
        string originalGraphMessageId, string body, CancellationToken ct = default)
    {
        // createReply creates a proper threaded reply draft in the Drafts folder.
        // The To/Subject/In-Reply-To headers are populated automatically from the original message.
        var requestBody = new CreateReplyPostRequestBody
        {
            Message = new Message
            {
                Body = new ItemBody
                {
                    ContentType = BodyType.Text,
                    Content = body
                }
            }
        };

        var draft = await _client.Users[_settings.UserId]
            .Messages[originalGraphMessageId]
            .CreateReply
            .PostAsync(requestBody, cancellationToken: ct);

        if (draft?.Id == null)
            throw new InvalidOperationException("Graph API returned null draft ID");

        _logger.LogInformation("Reply draft created for message {OriginalId}, draft id={DraftId}",
            originalGraphMessageId, draft.Id);

        return draft.Id;
    }
}

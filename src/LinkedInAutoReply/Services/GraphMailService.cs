using Azure.Identity;
using LinkedInAutoReply.Models;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.MailFolders.Item.Messages.Delta;
using Microsoft.Graph.Users.Item.Messages.Item.Move;

namespace LinkedInAutoReply.Services;

public class GraphMailService
{
    private readonly GraphServiceClient _client;
    private readonly GraphSettings _settings;
    private readonly ILogger<GraphMailService> _logger;

    // Pure noise — automated platform notifications that are never job offers.
    // Everything else is passed to the LLM classifier.
    private readonly HashSet<string> _excludedSenders;

    public GraphMailService(GraphSettings settings, ILogger<GraphMailService> logger)
    {
        _settings = settings;
        _logger = logger;
        _excludedSenders = new HashSet<string>(settings.ExcludedSenders, StringComparer.OrdinalIgnoreCase);

        var credential = new ClientSecretCredential(
            settings.TenantId, settings.ClientId, settings.ClientSecret);

        _client = new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
    }

    /// <summary>
    /// Fetches all unread inbox messages not on the noise blocklist.
    /// Source-agnostic — LinkedIn, Xing, direct outreach, job boards all pass through.
    /// The LLM classifier in MailWorker decides what is actually a job offer.
    /// </summary>
    public async Task<(List<Message> Messages, string? NewDeltaLink)> GetNewMessagesAsync(
        string? deltaLink, CancellationToken ct)
    {
        var messages = new List<Message>();
        string? newDeltaLink = null;

        try
        {
            DeltaGetResponse? page;

            if (string.IsNullOrEmpty(deltaLink))
            {
                page = await _client.Users[_settings.UserId]
                    .MailFolders["Inbox"].Messages
                    .Delta
                    .GetAsDeltaGetResponseAsync(r =>
                    {
                        r.QueryParameters.Select =
                        [
                            "id", "subject", "from", "replyTo", "receivedDateTime",
                            "bodyPreview", "isRead", "hasAttachments"
                        ];
                        r.QueryParameters.Top = 50;
                    }, ct);
            }
            else
            {
                var requestInfo = new Microsoft.Kiota.Abstractions.RequestInformation
                {
                    HttpMethod = Microsoft.Kiota.Abstractions.Method.GET,
                    UrlTemplate = deltaLink
                };
                page = await _client.RequestAdapter
                    .SendAsync(requestInfo, DeltaGetResponse.CreateFromDiscriminatorValue,
                        cancellationToken: ct);
            }

            while (page != null)
            {
                if (page.Value != null)
                {
                    var candidates = page.Value
                        .Where(m => !IsNoiseSender(m))
                        .ToList();
                    messages.AddRange(candidates);
                }

                if (page.OdataNextLink != null)
                {
                    var nextReq = new Microsoft.Kiota.Abstractions.RequestInformation
                    {
                        HttpMethod = Microsoft.Kiota.Abstractions.Method.GET,
                        UrlTemplate = page.OdataNextLink
                    };
                    page = await _client.RequestAdapter
                        .SendAsync(nextReq, DeltaGetResponse.CreateFromDiscriminatorValue,
                            cancellationToken: ct);
                }
                else
                {
                    newDeltaLink = page.OdataDeltaLink;
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching messages from Graph");
        }

        return (messages, newDeltaLink);
    }

    public async Task<string?> GetMessageBodyAsync(string messageId, CancellationToken ct)
    {
        try
        {
            var msg = await _client.Users[_settings.UserId]
                .Messages[messageId]
                .GetAsync(r => r.QueryParameters.Select = ["id", "body", "replyTo", "from"], ct);

            return msg?.Body?.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching body for {MessageId}", messageId);
            return null;
        }
    }

    public async Task<string?> GetReplyToAddressAsync(string messageId, CancellationToken ct)
    {
        try
        {
            var msg = await _client.Users[_settings.UserId]
                .Messages[messageId]
                .GetAsync(r => r.QueryParameters.Select = ["id", "replyTo", "from"], ct);

            var replyTo = msg?.ReplyTo?.FirstOrDefault()?.EmailAddress?.Address;
            return replyTo ?? msg?.From?.EmailAddress?.Address;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching reply-to for {MessageId}", messageId);
            return null;
        }
    }

    /// <summary>
    /// Returns text content from PDF and Word attachments on a message.
    /// Returns empty list if the message has no relevant attachments.
    /// </summary>
    public async Task<List<(string FileName, byte[] Content)>> GetDocumentAttachmentsAsync(
        string messageId, CancellationToken ct)
    {
        var result = new List<(string, byte[])>();
        try
        {
            var attachments = await _client.Users[_settings.UserId]
                .Messages[messageId].Attachments
                .GetAsync(cancellationToken: ct);

            if (attachments?.Value == null) return result;

            foreach (var attachment in attachments.Value)
            {
                if (attachment is not FileAttachment fa) continue;
                if (fa.ContentBytes == null || fa.Name == null) continue;

                var ext = Path.GetExtension(fa.Name).ToLowerInvariant();
                if (ext is ".pdf" or ".doc" or ".docx")
                    result.Add((fa.Name, fa.ContentBytes));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching attachments for {MessageId}", messageId);
        }
        return result;
    }

    public async Task MarkAsReadAsync(string messageId, CancellationToken ct)
    {
        try
        {
            await _client.Users[_settings.UserId]
                .Messages[messageId]
                .PatchAsync(new Message { IsRead = true }, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking {MessageId} as read", messageId);
        }
    }

    public async Task MoveToFolderAsync(string messageId, string folderName, CancellationToken ct)
    {
        try
        {
            var folderId = await GetOrCreateFolderAsync(folderName, ct);
            if (folderId == null) return;

            await _client.Users[_settings.UserId]
                .Messages[messageId]
                .Move
                .PostAsync(new MovePostRequestBody { DestinationId = folderId }, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error moving {MessageId} to folder {Folder}", messageId, folderName);
        }
    }

    private bool IsNoiseSender(Message m)
    {
        var from = m.From?.EmailAddress?.Address ?? string.Empty;
        return _excludedSenders.Contains(from);
    }

    private async Task<string?> GetOrCreateFolderAsync(string folderName, CancellationToken ct)
    {
        try
        {
            var folders = await _client.Users[_settings.UserId]
                .MailFolders
                .GetAsync(r => r.QueryParameters.Filter = $"displayName eq '{folderName.Replace("'", "''")}'", ct);

            if (folders?.Value?.Count > 0)
                return folders.Value[0].Id;

            var newFolder = await _client.Users[_settings.UserId]
                .MailFolders
                .PostAsync(new MailFolder { DisplayName = folderName }, cancellationToken: ct);

            return newFolder?.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting/creating folder {FolderName}", folderName);
            return null;
        }
    }
}

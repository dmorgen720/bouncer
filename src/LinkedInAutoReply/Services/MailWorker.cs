using System.Text;
using System.Text.Json;
using LinkedInAutoReply.Data;
using LinkedInAutoReply.Models;
using Microsoft.EntityFrameworkCore;

namespace LinkedInAutoReply.Services;

public partial class MailWorker(
    IServiceScopeFactory scopeFactory,
    GraphMailService graphMailService,
    JobOfferClassifier classifier,
    AttachmentTextExtractor attachmentExtractor,
    LinkedInMessageParser parser,
    RecruitmentAssessor assessor,
    ScanTriggerService trigger,
    AutoReplySettings settings,
    ILogger<MailWorker> logger) : BackgroundService
{
    public const string DeltaLinkKey = "recruiter_delta_link";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogServiceStarted();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNewEmailsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                LogLoopError(ex);
            }

            trigger.NotifyScanCompleted();
            await trigger.WaitAsync(
                TimeSpan.FromSeconds(settings.PollIntervalSeconds), stoppingToken);
        }
    }

    private async Task ProcessNewEmailsAsync(CancellationToken ct)
    {
        LogScanStarted();

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var syncState = await db.SyncStates.FirstOrDefaultAsync(s => s.Key == DeltaLinkKey, ct);
        var (messages, newDeltaLink) = await graphMailService.GetNewMessagesAsync(syncState?.Value, ct);

        LogFetchedMessages(messages.Count);

        int processed = 0, skipped = 0, notJobOffer = 0;

        foreach (var message in messages)
        {
            if (message.Id == null) continue;

            if (await db.RecruiterMessages.AnyAsync(r => r.GraphMessageId == message.Id, ct))
            {
                skipped++;
                continue;
            }

            var subject = message.Subject ?? string.Empty;
            var from = message.From?.EmailAddress?.Address ?? string.Empty;
            var bodyPreview = message.BodyPreview ?? string.Empty;
            var receivedAt = message.ReceivedDateTime ?? DateTimeOffset.UtcNow;

            // ── Step 1: LLM classification (cheap — uses subject + preview only) ──
            var isJobOffer = await classifier.IsJobOfferAsync(subject, from, bodyPreview, ct);
            if (!isJobOffer)
            {
                notJobOffer++;
                LogSkippedNotJobOffer(message.Id, subject);
                continue;
            }

            LogProcessingMessage(message.Id, subject);

            // ── Step 2: Fetch full HTML body ──
            var htmlBody = await graphMailService.GetMessageBodyAsync(message.Id, ct);
            if (string.IsNullOrWhiteSpace(htmlBody))
            {
                LogEmptyBody(message.Id);
                continue;
            }

            var replyTo = await graphMailService.GetReplyToAddressAsync(message.Id, ct) ?? from;
            var parsed = parser.Parse(htmlBody, replyTo, subject);

            // ── Step 3: Extract text from PDF / Word attachments ──
            var attachmentText = new StringBuilder();
            if (message.HasAttachments == true)
            {
                var attachments = await graphMailService.GetDocumentAttachmentsAsync(message.Id, ct);
                foreach (var (fileName, content) in attachments)
                {
                    var text = attachmentExtractor.ExtractText(fileName, content);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        attachmentText.AppendLine($"\n--- Attachment: {fileName} ---");
                        attachmentText.AppendLine(text);
                        LogAttachmentExtracted(message.Id, fileName, text.Length);
                    }
                }
            }

            // Combine email body text + any attachment text for assessment
            var fullText = string.IsNullOrWhiteSpace(attachmentText.ToString())
                ? parsed.MessageText
                : $"{parsed.MessageText}\n\n{attachmentText}".Trim();

            // ── Step 4: LLM assessment ──
            AssessmentResult assessment;
            try
            {
                assessment = await assessor.AssessAsync(fullText, ct);
            }
            catch (Exception ex)
            {
                LogAssessmentFailed(ex, message.Id);
                continue;
            }

            var record = new RecruiterMessage
            {
                GraphMessageId = message.Id,
                EmailSubject = subject,
                RecruiterName = parsed.RecruiterName,
                Agency = string.IsNullOrEmpty(parsed.Company) ? assessment.RecruitingCompany : parsed.Company,
                HiringCompany = assessment.HiringCompany,
                RoleTitle = ExtractRoleTitle(subject),
                OriginalMessageText = fullText,
                ReplyToAddress = parsed.ReplyToAddress,
                Verdict = assessment.Verdict,
                AssessmentJson = JsonSerializer.Serialize(assessment.Filters),
                AcceptDraft = assessment.AcceptDraft,
                DeclineDraft = assessment.DeclineDraft,
                ReplyLanguage = assessment.ReplyLanguage,
                ReceivedAt = receivedAt
            };

            db.RecruiterMessages.Add(record);
            await db.SaveChangesAsync(ct);

            LogMessageAssessed(message.Id, assessment.Verdict);

            await graphMailService.MarkAsReadAsync(message.Id, ct);
            processed++;
        }

        LogScanComplete(processed, skipped, notJobOffer);

        if (newDeltaLink != null)
        {
            if (syncState == null)
                db.SyncStates.Add(new SyncState { Key = DeltaLinkKey, Value = newDeltaLink, UpdatedAt = DateTime.UtcNow });
            else
            {
                syncState.Value = newDeltaLink;
                syncState.UpdatedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct);
        }
    }

    private static string ExtractRoleTitle(string subject) =>
        System.Text.RegularExpressions.Regex.Replace(
            subject, @"^(Re:\s*|LinkedIn\s*-?\s*)", string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();

    // ── Logging ──────────────────────────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Information, Message = "Mail worker started")]
    private partial void LogServiceStarted();

    [LoggerMessage(Level = LogLevel.Error, Message = "Error in mail worker loop")]
    private partial void LogLoopError(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "--- Scan started ---")]
    private partial void LogScanStarted();

    [LoggerMessage(Level = LogLevel.Information, Message = "Fetched {Count} candidate messages from Graph")]
    private partial void LogFetchedMessages(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skip [{MessageId}] \"{Subject}\" — not a job offer")]
    private partial void LogSkippedNotJobOffer(string messageId, string subject);

    [LoggerMessage(Level = LogLevel.Information, Message = "Processing [{MessageId}] \"{Subject}\"")]
    private partial void LogProcessingMessage(string messageId, string subject);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Empty body for [{MessageId}], skipping")]
    private partial void LogEmptyBody(string messageId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "[{MessageId}] extracted {Length} chars from attachment {FileName}")]
    private partial void LogAttachmentExtracted(string messageId, string fileName, int length);

    [LoggerMessage(Level = LogLevel.Error, Message = "Assessment failed for [{MessageId}]")]
    private partial void LogAssessmentFailed(Exception ex, string messageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "[{MessageId}] assessed as {Verdict}")]
    private partial void LogMessageAssessed(string messageId, AssessmentVerdict verdict);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "--- Scan complete: {Processed} new, {Skipped} already in DB, {NotJobOffer} not job offers ---")]
    private partial void LogScanComplete(int processed, int skipped, int notJobOffer);
}

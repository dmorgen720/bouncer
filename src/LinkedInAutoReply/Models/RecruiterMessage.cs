using System.ComponentModel.DataAnnotations;

namespace LinkedInAutoReply.Models;

public class RecruiterMessage
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string GraphMessageId { get; set; } = string.Empty;
    public string EmailSubject { get; set; } = string.Empty;
    public string RecruiterName { get; set; } = string.Empty;
    public string Agency { get; set; } = string.Empty;
    public string? HiringCompany { get; set; }
    public string RoleTitle { get; set; } = string.Empty;
    public string OriginalMessageText { get; set; } = string.Empty;
    public string ReplyToAddress { get; set; } = string.Empty;

    // Assessment
    public AssessmentVerdict Verdict { get; set; }
    public string AssessmentJson { get; set; } = string.Empty; // serialized FilterResults + details
    public string AcceptDraft { get; set; } = string.Empty;    // warm reply confirming interest
    public string DeclineDraft { get; set; } = string.Empty;   // polite decline
    public string ReplyLanguage { get; set; } = "en";

    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;

    // Set after user decides
    public bool? Approved { get; set; }
    public string? FinalDraft { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public bool Discarded { get; set; }   // not a job offer — hidden from main list
}

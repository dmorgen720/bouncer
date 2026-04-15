namespace LinkedInAutoReply.Models;

public enum AssessmentVerdict
{
    Match,
    Partial,
    NoMatch
}

public record FilterResult(string FilterName, FilterStatus Status, string Reason);

public enum FilterStatus
{
    Pass,
    Fail,
    Warn
}

public class AssessmentResult
{
    public AssessmentVerdict Verdict { get; set; }
    public string RecruitingCompany { get; set; } = string.Empty;
    public string? HiringCompany { get; set; }
    public List<FilterResult> Filters { get; set; } = [];
    public string AcceptDraft { get; set; } = string.Empty;
    public string DeclineDraft { get; set; } = string.Empty;
    public string ReplyLanguage { get; set; } = "en";
}

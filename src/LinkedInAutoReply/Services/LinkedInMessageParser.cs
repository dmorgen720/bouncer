using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace LinkedInAutoReply.Services;

public class ParsedLinkedInEmail
{
    public string RecruiterName { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;
    public string ReplyToAddress { get; set; } = string.Empty;
}

public class LinkedInMessageParser
{
    public ParsedLinkedInEmail Parse(string htmlBody, string fromAddress, string subject)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(htmlBody);

        var messageText = ExtractMessageBody(doc, htmlBody);
        var recruiterName = ExtractRecruiterName(subject, messageText);
        var company = ExtractCompany(messageText);

        return new ParsedLinkedInEmail
        {
            RecruiterName = recruiterName,
            Company = company,
            MessageText = messageText,
            ReplyToAddress = fromAddress
        };
    }

    /// <summary>
    /// Tries several strategies in order to extract the recruiter's actual message text,
    /// avoiding LinkedIn boilerplate (headers, footers, tracking links).
    /// </summary>
    private static string ExtractMessageBody(HtmlDocument doc, string rawHtml)
    {
        // Strip script/style globally
        foreach (var node in doc.DocumentNode.SelectNodes("//script|//style|//head") ?? Enumerable.Empty<HtmlNode>())
            node.Remove();

        // Strategy 1: LinkedIn InMail emails wrap the recruiter's message in a <td> or <div>
        // that sits between a greeting ("Hi David") and the footer/CTA block.
        // Look for the largest text block that doesn't contain LinkedIn infrastructure URLs.
        var candidates = doc.DocumentNode
            .SelectNodes("//td|//div|//p")
            ?.Where(n => n.ChildNodes.All(c => c.NodeType != HtmlNodeType.Element || c.Name is "br" or "span" or "strong" or "em" or "a" or "b" or "i"))
            .Select(n => new { Node = n, Text = CleanNodeText(n) })
            .Where(x => x.Text.Length > 80 && !IsBoilerplate(x.Text))
            .OrderByDescending(x => x.Text.Length)
            .ToList();

        if (candidates?.Count > 0)
        {
            // Pick the longest non-boilerplate block
            var best = candidates[0].Text;
            if (best.Length > 80)
                return best;
        }

        // Strategy 2: Full plain-text dump, then remove obvious footer/header lines
        var allText = HtmlToPlainText(doc);
        return RemoveBoilerplateLines(allText);
    }

    private static string CleanNodeText(HtmlNode node)
    {
        var text = System.Net.WebUtility.HtmlDecode(node.InnerText ?? string.Empty);
        return NormaliseWhitespace(text);
    }

    private static string HtmlToPlainText(HtmlDocument doc)
    {
        var sb = new StringBuilder();
        foreach (var node in doc.DocumentNode.DescendantsAndSelf())
        {
            if (node.NodeType == HtmlNodeType.Text)
            {
                var text = System.Net.WebUtility.HtmlDecode(node.InnerText).Trim();
                if (text.Length > 0)
                    sb.AppendLine(text);
            }
            else if (node.Name is "br" or "p" or "div" or "tr" or "td" or "li")
            {
                sb.AppendLine();
            }
        }
        return NormaliseWhitespace(sb.ToString());
    }

    /// <summary>
    /// Remove lines that are clearly LinkedIn footer/infrastructure, not recruiter content.
    /// Deliberately conservative — only strip lines that are unambiguously boilerplate.
    /// </summary>
    private static string RemoveBoilerplateLines(string text)
    {
        var lines = text.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .Where(l => !IsBoilerplateLine(l))
            .ToArray();
        return string.Join("\n", lines).Trim();
    }

    private static bool IsBoilerplateLine(string line) =>
        Regex.IsMatch(line, @"unsubscribe|you are receiving this|linkedin corporation|©\s*20\d\d|manage preferences|privacy policy|help center|this email was intended", RegexOptions.IgnoreCase)
        || Regex.IsMatch(line, @"^https?://", RegexOptions.IgnoreCase)  // bare URLs
        || Regex.IsMatch(line, @"^\s*$");

    private static bool IsBoilerplate(string text) =>
        Regex.IsMatch(text, @"unsubscribe|linkedin corporation|©\s*20\d\d|privacy policy", RegexOptions.IgnoreCase);

    private static string NormaliseWhitespace(string text)
    {
        // Collapse runs of blank lines to one, trim each line
        var lines = text.Split('\n')
            .Select(l => l.Trim())
            .ToList();

        var result = new List<string>();
        bool lastBlank = false;
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                if (!lastBlank) result.Add(string.Empty);
                lastBlank = true;
            }
            else
            {
                result.Add(line);
                lastBlank = false;
            }
        }
        return string.Join("\n", result).Trim();
    }

    private static string ExtractRecruiterName(string subject, string body)
    {
        // LinkedIn subjects: "[Name] sent you a message", "New message from [Name]", etc.
        var patterns = new[]
        {
            @"^(.+?) sent you",
            @"New message from (.+)",
            @"^(.+?) a envoyé",
            @"^(.+?) hat Ihnen",
            @"^(.+?) vous a envoyé",
        };

        foreach (var pattern in patterns)
        {
            var m = Regex.Match(subject, pattern, RegexOptions.IgnoreCase);
            if (m.Success)
                return m.Groups[1].Value.Trim();
        }

        return "Unknown Recruiter";
    }

    private static string ExtractCompany(string body)
    {
        var m = Regex.Match(body,
            @"(?:recruiter|consultant|manager|partner|director|head)\s+at\s+([A-Z][^\n,\.]{2,50})",
            RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : string.Empty;
    }
}

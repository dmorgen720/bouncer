using LinkedInAutoReply.Services;

namespace LinkedInAutoReply.Tests;

public class ParserTests
{
    private readonly LinkedInMessageParser _parser = new();

    [Fact]
    public void Parse_ExtractsRecruiterNameFromSubject_SentYouPattern()
    {
        var result = _parser.Parse(
            "<html><body>Hello David, I have an exciting opportunity...</body></html>",
            "reply@linkedin.com",
            "John Smith sent you a message");

        Assert.Equal("John Smith", result.RecruiterName);
    }

    [Fact]
    public void Parse_ExtractsRecruiterNameFromSubject_NewMessagePattern()
    {
        var result = _parser.Parse(
            "<html><body>Dear David...</body></html>",
            "reply@linkedin.com",
            "New message from Marie Dupont");

        Assert.Equal("Marie Dupont", result.RecruiterName);
    }

    [Fact]
    public void Parse_FallsBackToUnknownWhenSubjectDoesNotMatch()
    {
        var result = _parser.Parse(
            "<html><body>Some message</body></html>",
            "reply@linkedin.com",
            "LinkedIn notification");

        Assert.Equal("Unknown Recruiter", result.RecruiterName);
    }

    [Fact]
    public void Parse_UsesReplyToAsReplyAddress()
    {
        var replyTo = "inmail-reply+abc123@linkedin.com";
        var result = _parser.Parse("<html><body>Hello</body></html>", replyTo, "Test");

        Assert.Equal(replyTo, result.ReplyToAddress);
    }

    [Fact]
    public void Parse_StripsBoilerplate_KeepsMessageContent()
    {
        var html = """
            <html><body>
            <p>Dear David, I found your profile on LinkedIn.</p>
            <p>We have an architect role in Basel.</p>
            <p>© 2024 LinkedIn Corporation</p>
            <p>You are receiving this email because you are a LinkedIn member.</p>
            <p>Unsubscribe</p>
            </body></html>
            """;

        var result = _parser.Parse(html, "reply@linkedin.com", "Test sent you a message");

        Assert.DoesNotContain("LinkedIn Corporation", result.MessageText);
        Assert.DoesNotContain("Unsubscribe", result.MessageText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Basel", result.MessageText);
    }

    [Fact]
    public void Parse_PreservesLinkedInMentionsInBody()
    {
        // "linkedin.com" in the body should NOT strip legitimate content
        var html = """
            <html><body>
            <p>I saw your profile on LinkedIn and think you'd be a great fit.</p>
            <p>We are hiring a Solution Architect in Basel.</p>
            </body></html>
            """;

        var result = _parser.Parse(html, "reply@linkedin.com", "Test sent you a message");

        Assert.Contains("Basel", result.MessageText);
        Assert.Contains("Solution Architect", result.MessageText);
    }

    [Fact]
    public void Parse_HandlesEmptyBody_Gracefully()
    {
        var result = _parser.Parse(string.Empty, "reply@linkedin.com", "Test sent you a message");

        Assert.NotNull(result);
        Assert.Equal("reply@linkedin.com", result.ReplyToAddress);
    }
}

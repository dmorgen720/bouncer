using LinkedInAutoReply.Models;
using LinkedInAutoReply.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LinkedInAutoReply.Tests;

public class AssessorTests
{
    private static RecruitmentAssessor BuildAssessor(string llmResponse)
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient
            .GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
            .Returns(new ChatResponse([new ChatMessage(ChatRole.Assistant, llmResponse)]));

        // Write a stub prompt file so LoadPrompt() succeeds
        var contentRoot = Path.Combine(Path.GetTempPath(), "bouncer_tests");
        var promptDir = Path.Combine(contentRoot, "Prompts");
        Directory.CreateDirectory(promptDir);
        File.WriteAllText(Path.Combine(promptDir, "assessment.md"), "You are a test assessor.");

        var env = Substitute.For<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        env.ContentRootPath.Returns(contentRoot);

        return new RecruitmentAssessor(chatClient, env, NullLogger<RecruitmentAssessor>.Instance);
    }

    [Fact]
    public async Task AssessAsync_ParsesMatchVerdict()
    {
        var json = """
            {
              "recruitingCompany": "Acme Staffing",
              "hiringCompany": null,
              "assessment": "Match",
              "filters": [
                { "name": "Location", "status": "Pass", "reason": "Basel area role" },
                { "name": "Role Type", "status": "Pass", "reason": "Solution Architect" },
                { "name": "Seniority", "status": "Pass", "reason": "Senior level" }
              ],
              "acceptDraft": "Thank you for reaching out...",
              "declineDraft": "Thank you, but this is not the right fit.",
              "replyLanguage": "en"
            }
            """;

        var assessor = BuildAssessor(json);
        var result = await assessor.AssessAsync("I have a Solution Architect role in Basel.");

        Assert.Equal(AssessmentVerdict.Match, result.Verdict);
        Assert.Equal("Acme Staffing", result.RecruitingCompany);
        Assert.Equal(3, result.Filters.Count);
        Assert.All(result.Filters, f => Assert.Equal(FilterStatus.Pass, f.Status));
        Assert.Equal("en", result.ReplyLanguage);
    }

    [Fact]
    public async Task AssessAsync_ParsesNoMatchVerdict()
    {
        var json = """
            {
              "recruitingCompany": "Zurich Agency",
              "hiringCompany": null,
              "assessment": "NoMatch",
              "filters": [
                { "name": "Location", "status": "Fail", "reason": "Role is in Zurich" },
                { "name": "Role Type", "status": "Pass", "reason": "Architect role" },
                { "name": "Seniority", "status": "Pass", "reason": "Senior" }
              ],
              "acceptDraft": "",
              "declineDraft": "Thank you, but I am only considering Basel...",
              "replyLanguage": "en"
            }
            """;

        var assessor = BuildAssessor(json);
        var result = await assessor.AssessAsync("Senior Architect in Zurich.");

        Assert.Equal(AssessmentVerdict.NoMatch, result.Verdict);
        Assert.Equal(FilterStatus.Fail, result.Filters.First(f => f.FilterName == "Location").Status);
    }

    [Fact]
    public async Task AssessAsync_ReturnsNoMatch_WhenLlmReturnsInvalidJson()
    {
        var assessor = BuildAssessor("Sorry, I cannot help with that.");
        var result = await assessor.AssessAsync("Some message");

        Assert.Equal(AssessmentVerdict.NoMatch, result.Verdict);
    }
}

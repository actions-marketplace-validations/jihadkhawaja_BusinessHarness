using System.Text;
using System.Text.Json;
using BusinessHarness.Cli;
using BusinessHarness.Core;
using BusinessHarness.Infrastructure;
using Xunit;

namespace BusinessHarness.Tests;

public sealed class CommentTemplateTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string Custom = "# Team review\n\n{{scope}}\n\n{{status}}\n\n{{summary}}\n\n## Risks\n\n{{findings}}\n\n## Acceptance\n\n{{specifications}}\n";

    [Fact]
    public void CustomTemplateReordersCompleteEscapedSectionsWithoutEvaluatingReviewText()
    {
        var report = Report() with
        {
            Summary = "{{findings}} <script> @everyone [click](https://evil.invalid)",
            Findings = [new("S1", "high", "src/a.cs", 3, "Authorization bypass", "Require authorization.") { Category = "security" },
                new("M1", "medium", "src/b.cs", 5, "Resource leak", "Dispose the stream.") { Category = "maintainability" }],
            Requirements = [new("SPEC-1", "met", "Implemented"), new("SPEC-2", "partial", "Missing bound")],
            ExcludedFiles = ["private.key"]
        };
        var markdown = ReviewCommentTemplate.Render(report, Custom);
        Assert.StartsWith("# Team review", markdown);
        Assert.True(markdown.IndexOf("Mode:", StringComparison.Ordinal) < markdown.IndexOf("## Risks", StringComparison.Ordinal));
        Assert.Contains("2 findings to address", markdown);
        Assert.Contains("src/a.cs:3", markdown);
        Assert.Contains("src/b.cs:5", markdown);
        Assert.Contains("SPEC\\-1", markdown);
        Assert.Contains("SPEC\\-2", markdown);
        Assert.Contains("Withheld: private.key", markdown);
        Assert.DoesNotContain("<script>", markdown);
        Assert.DoesNotContain("@everyone", markdown);
        Assert.DoesNotContain("[click](", markdown);
        Assert.Contains("\\{\\{findings\\}\\}", markdown);
        Assert.Equal(1, markdown.Split("Authorization bypass", StringSplitOptions.None).Length - 1);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("{{summary}}")]
    [InlineData("{{status}}\n{{summary}}\n{{findings}}\n{{specifications}}\n{{unknown}}")]
    [InlineData("{{status}}\n{{summary}}\n{{findings}}\n{{specifications}}\n{{scope}}\n{{summary}}")]
    [InlineData("{{status}}\nSummary: {{summary}}\n{{findings}}\n{{specifications}}\n{{scope}}")]
    [InlineData("{{status}}\n{{summary}}\n{{findings}}\n{{specifications}}\n{{scope}}\n{{broken")]
    [InlineData("{{status}}\n{{summary}}\n{{findings}}\n{{specifications}}\n{{scope}}\n\0")]
    public void InvalidTemplatesFailWithSafeConfigurationError(string template)
    {
        var failure = Assert.Throws<HarnessException>(() => ReviewCommentTemplate.Render(Report(), template));
        Assert.Equal(ErrorCode.Configuration, failure.Code);
        Assert.Equal("Invalid review comment template.", failure.Message);
    }

    [Fact]
    public void TemplateLengthIsBoundedForInlinePolicy()
    {
        var atLimit = Custom + new string('x', ReviewCommentTemplate.MaxLength - Custom.Length);
        ReviewCommentTemplate.Validate(atLimit);
        Assert.Throws<HarnessException>(() => ReviewCommentTemplate.Validate(atLimit + "x"));
    }

    [Theory]
    [InlineData("github", "comment")]
    [InlineData("github", "auto")]
    [InlineData("gitlab", "comment")]
    [InlineData("gitlab", "auto")]
    public async Task HostPublishesCustomTemplateWithRedactedValuesAndLiteralSecrets(string platform, string feedback)
    {
        var posts = 0;
        using var handler = new StubHttpHandler(async request =>
        {
            if (request.Method == HttpMethod.Get) return Metadata(platform);
            posts++;
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(Ct));
            if (payload.RootElement.TryGetProperty("body", out var body))
            {
                var markdown = body.GetString()!;
                Assert.StartsWith("# Team review", markdown);
                Assert.Contains("## Acceptance", markdown);
                Assert.DoesNotContain("synthetic-host-secret", markdown);
                Assert.DoesNotContain("@everyone", markdown);
                Assert.Contains("No linked specifications supplied", markdown);
                Assert.Contains("Gate: **passed**", markdown);
            }
            return StubHttpHandler.Json("{}");
        });
        using var client = new HttpClient(handler);
        var host = new ChangeRequestHost(new(client), new() { Platform = platform, Project = "org/repo", Number = 12 },
            "synthetic-host-secret", new(["synthetic-host-secret"]), Custom + "\nsynthetic-host-secret");
        await host.PublishAsync(await host.ReadAsync(Ct), Report() with { Summary = "@everyone synthetic-host-secret" }, feedback, Ct);
        Assert.Equal(platform == "gitlab" && feedback == "auto" ? 2 : 1, posts);
        Assert.StartsWith("## BusinessHarness review", ReportWriter.Markdown(Report()));
    }

    [Theory]
    [InlineData("github")]
    [InlineData("gitlab")]
    public async Task OversizedRenderedCommentStopsBeforeAnyWrite(string platform)
    {
        using var handler = new StubHttpHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            return Task.FromResult(Metadata(platform));
        });
        using var client = new HttpClient(handler);
        var host = new ChangeRequestHost(new(client), new() { Platform = platform, Project = "org/repo", Number = 12 }, "token", new(), Custom);
        var reviewed = await host.ReadAsync(Ct);
        var failure = await Assert.ThrowsAsync<HarnessException>(() => host.PublishAsync(reviewed,
            Report() with { Passed = false, Summary = new string('x', 60_000) }, "auto", Ct));
        Assert.Equal(ErrorCode.LimitExceeded, failure.Code);
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidateAcceptsTrustedPolicyOrExplicitFileOverride(bool useFile)
    {
        using var temp = new TempDirectory();
        var config = Path.Combine(temp.Path, "policy.json");
        await File.WriteAllTextAsync(config, JsonSerializer.Serialize(new HarnessPolicy
        {
            Model = "test-model",
            ReviewCommentTemplate = useFile ? "invalid overridden template" : Custom
        }, JsonDefaults.Options), Ct);
        var args = new List<string> { "validate", "--config", config, "--output", temp.Path };
        if (useFile)
        {
            var template = Path.Combine(temp.Path, "template.md");
            await File.WriteAllTextAsync(template, Custom.Replace("\n", "\r\n", StringComparison.Ordinal), new UTF8Encoding(true), Ct);
            args.AddRange(["--comment-template", template]);
        }
        var error = new StringWriter();
        Assert.Equal(0, await new CliApplication(name => name == "OPENAI_API_KEY" ? "synthetic-key" : null)
            .RunAsync(args.ToArray(), new StringWriter(), error, Ct));
        Assert.Equal("", error.ToString());
    }

    [Theory]
    [InlineData("inline")]
    [InlineData("file")]
    [InlineData("oversized")]
    [InlineData("bom-oversized")]
    [InlineData("invalid-utf8")]
    [InlineData("missing")]
    public async Task InvalidConfigurationFailsBeforeProviderCredentialsOrCalls(string source)
    {
        using var temp = new TempDirectory();
        var config = Path.Combine(temp.Path, "policy.json");
        await File.WriteAllTextAsync(config, JsonSerializer.Serialize(new HarnessPolicy
        {
            ReviewCommentTemplate = source == "inline" ? "private-invalid-template" : null
        }, JsonDefaults.Options), Ct);
        var args = new List<string> { "validate", "--config", config, "--output", temp.Path };
        if (source != "inline")
        {
            var path = Path.Combine(temp.Path, "template.md");
            if (source == "invalid-utf8") await File.WriteAllBytesAsync(path, [0xC3, 0x28], Ct);
            else if (source != "missing")
                await File.WriteAllTextAsync(path, source is "oversized" or "bom-oversized" ?
                    (source == "bom-oversized" ? "\uFEFF" : "") + Custom + new string('x', 16_001) : "private-invalid-template", Ct);
            args.AddRange(["--comment-template", path]);
        }
        var error = new StringWriter();
        var exit = await new CliApplication(_ => null).RunAsync(args.ToArray(), new StringWriter(), error, Ct);
        Assert.Equal(source == "missing" ? 8 : 2, exit);
        Assert.DoesNotContain("private-invalid-template", error.ToString());
        Assert.DoesNotContain("Review model is not configured", error.ToString());
        Assert.True(File.Exists(Path.Combine(temp.Path, "error.json")));
    }

    [Theory]
    [InlineData("scan")]
    [InlineData("follow-up")]
    [InlineData("memory-search")]
    public void CommentTemplateOptionIsLimitedToReviewAndValidate(string command)
    {
        string[] args = command switch
        {
            "follow-up" => [command, "--event", "event.json", "--comment-template", "trusted.md"],
            "memory-search" => [command, "--query", "query", "--comment-template", "trusted.md"],
            _ => [command, "--base", "HEAD", "--comment-template", "trusted.md"]
        };
        Assert.Contains("Comment templates require review or validate", Assert.Throws<HarnessException>(() => CliOptions.Parse(args, _ => null)).Message);
    }

    private static HarnessReport Report() => new("1.0", "review", "openai", "test", new string('a', 40), new string('b', 40),
        DateTimeOffset.UnixEpoch, true, "Review summary.", ["src/a.cs"], [], [], []);

    private static System.Net.Http.HttpResponseMessage Metadata(string platform) => StubHttpHandler.Json(JsonSerializer.Serialize(new
    {
        title = "Review",
        body = "",
        description = "",
        head = new { sha = new string('b', 40) },
        sha = new string('b', 40),
        @base = new { sha = new string('a', 40) },
        diff_refs = new { base_sha = new string('a', 40) },
        draft = false,
        state = platform == "github" ? "open" : "opened"
    }));
}

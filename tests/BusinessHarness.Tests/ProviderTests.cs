using System.Net;
using System.Text.Json;
using BusinessHarness.Core;
using BusinessHarness.Infrastructure;
using Xunit;

namespace BusinessHarness.Tests;

public sealed class ProviderTests
{
    private static readonly ChangeSet Changes = new("base", "head", [new("sample.cs", "+// example", [1])], [], []);
    private const string Result = """{"summary":"No defects found.","findings":[],"requirements":[]}""";

    [Fact]
    public async Task OpenAiSendsStrictSchemaWithoutToolsAndDoesNotStore()
    {
        using var handler = new StubHttpHandler(async request =>
        {
            Assert.Equal("https://api.openai.com/v1/responses", request.RequestUri!.AbsoluteUri);
            Assert.Equal("test-key", request.Headers.Authorization!.Parameter);
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var root = payload.RootElement;
            Assert.False(root.GetProperty("store").GetBoolean());
            Assert.False(root.TryGetProperty("tools", out _));
            Assert.True(root.GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean());
            Assert.Contains("UNTRUSTED DATA", root.GetProperty("instructions").GetString());
            return StubHttpHandler.Json(JsonSerializer.Serialize(new { status = "completed", output = new[] { new { type = "message", content = new[] { new { type = "output_text", text = Result } } } } }));
        });
        using var client = new HttpClient(handler);
        var provider = new OpenAiReviewProvider(new(client), new() { Model = "test-model" }, "test-key");
        Assert.Empty((await provider.ReviewAsync(Changes, [], TestContext.Current.CancellationToken)).Findings);
    }

    [Fact]
    public async Task ClaudeUsesNativeOutputConfig()
    {
        using var handler = new StubHttpHandler(async request =>
        {
            Assert.Equal("https://api.anthropic.com/v1/messages", request.RequestUri!.AbsoluteUri);
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal("json_schema", payload.RootElement.GetProperty("output_config").GetProperty("format").GetProperty("type").GetString());
            Assert.False(payload.RootElement.TryGetProperty("tools", out _));
            return StubHttpHandler.Json(JsonSerializer.Serialize(new { stop_reason = "end_turn", content = new[] { new { type = "text", text = Result } } }));
        });
        using var client = new HttpClient(handler);
        var provider = new AnthropicReviewProvider(new(client), new() { Provider = "anthropic", Model = "test-model" }, "test-key");
        Assert.Empty((await provider.ReviewAsync(Changes, [], TestContext.Current.CancellationToken)).Findings);
    }

    [Theory]
    [InlineData(401, ErrorCode.Authentication)]
    [InlineData(403, ErrorCode.Authentication)]
    [InlineData(429, ErrorCode.Network)]
    [InlineData(500, ErrorCode.Network)]
    [InlineData(302, ErrorCode.Network)]
    public async Task ClassifiesHttpFailuresAndNeverLogsRawBodies(int status, ErrorCode expected)
    {
        using var handler = new StubHttpHandler(_ => Task.FromResult(StubHttpHandler.Json("PRIVATE-SECRET-RESPONSE", (HttpStatusCode)status)));
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.invalid");
        var error = await Assert.ThrowsAsync<HarnessException>(() => new SafeHttp(client).SendAsync(request, "Test", TestContext.Current.CancellationToken));
        Assert.Equal(expected, error.Code);
        Assert.DoesNotContain("PRIVATE-SECRET", error.ToString());
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("""{"status":"incomplete","output":[]}""")]
    [InlineData("""{"status":"completed","output":[{"type":"message","content":[{"type":"refusal","refusal":"no"}]}]}""")]
    [InlineData("""{"status":"completed","output":[]}""")]
    [InlineData("[]")]
    [InlineData("""{"status":42,"output":[]}""")]
    [InlineData("""{"status":"completed","output":[{"type":"message","content":[{"type":"output_text","text":42}]}]}""")]
    public async Task RefusedAndIncompleteResponsesCannotPass(string response)
    {
        using var handler = new StubHttpHandler(_ => Task.FromResult(StubHttpHandler.Json(response)));
        using var client = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<HarnessException>(() => new OpenAiReviewProvider(new(client), new(), "key").ReviewAsync(Changes, [], TestContext.Current.CancellationToken));
        Assert.Equal(ErrorCode.ProviderResponse, error.Code);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"summary":"ok","findings":[],"requirements":[],"unexpected":true}""")]
    [InlineData("""{"summary":null,"findings":[],"requirements":[]}""")]
    public void RejectsInvalidReviewShape(string json) => Assert.Throws<HarnessException>(() => ReviewContract.Parse(json));

    [Theory]
    [InlineData("max_tokens")]
    [InlineData("refusal")]
    [InlineData("tool_use")]
    public async Task ClaudeRejectsNonCompletedGeneration(string reason)
    {
        using var handler = new StubHttpHandler(_ => Task.FromResult(StubHttpHandler.Json(JsonSerializer.Serialize(new
        {
            stop_reason = reason,
            content = new[] { new { type = "text", text = Result } }
        }))));
        using var client = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<HarnessException>(() => new AnthropicReviewProvider(new(client), new(), "key")
            .ReviewAsync(Changes, [], TestContext.Current.CancellationToken));
        Assert.Equal(ErrorCode.ProviderResponse, error.Code);
    }

    [Fact]
    public async Task HttpResponseBodyHasAnEnforcedSizeLimit()
    {
        using var handler = new StubHttpHandler(_ => Task.FromResult(StubHttpHandler.Json(new string('x', 1_000_001))));
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.invalid");
        var error = await Assert.ThrowsAsync<HarnessException>(() => new SafeHttp(client).SendAsync(request, "Test", TestContext.Current.CancellationToken));
        Assert.Equal(ErrorCode.LimitExceeded, error.Code);
    }

    [Fact]
    public async Task JiraFlattensAdfAndIncludesAcceptanceCriteria()
    {
        using var handler = new StubHttpHandler(request =>
        {
            Assert.Contains("customfield_10001", request.RequestUri!.Query);
            Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
            return Task.FromResult(StubHttpHandler.Json("""
                {"key":"PROJ-123","fields":{"summary":"Example","description":{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"Requirement text"}]}]},"customfield_10001":"Must pass validation"}}
                """));
        });
        using var client = new HttpClient(handler);
        var source = new JiraIssueSource(new(client), new() { JiraSite = "https://team.atlassian.net", JiraAcceptanceCriteriaField = "customfield_10001" }, "test@example.invalid", "token", new());
        var issue = await source.GetIssueAsync("PROJ-123", TestContext.Current.CancellationToken);
        Assert.Equal("Requirement text", issue.Description);
        Assert.Equal("Must pass validation", issue.AcceptanceCriteria);
    }
}

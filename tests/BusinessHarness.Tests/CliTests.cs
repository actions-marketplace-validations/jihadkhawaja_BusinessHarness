using System.Text.Json;
using BusinessHarness.Cli;
using BusinessHarness.Core;
using BusinessHarness.Infrastructure;
using Xunit;

namespace BusinessHarness.Tests;

public sealed class CliTests
{
    [Fact]
    public async Task MissingSecretShowsActionableErrorAndWritesStructuredFailure()
    {
        using var temp = new TempDirectory();
        var stderr = new StringWriter();
        var cli = new CliApplication(name => name == "HARNESS_MODEL" ? "test-model" : null);
        var code = await cli.RunAsync(["validate", "--output", temp.Path], new StringWriter(), stderr, TestContext.Current.CancellationToken);
        Assert.Equal(2, code);
        Assert.Contains("OPENAI_API_KEY", stderr.ToString());
        Assert.Contains("Fix:", stderr.ToString());
        using var failure = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(temp.Path, "error.json"), TestContext.Current.CancellationToken));
        Assert.Equal("BH1002", failure.RootElement.GetProperty("errorCode").GetString());
        Assert.Equal(32, failure.RootElement.GetProperty("correlationId").GetString()!.Length);
    }

    [Fact]
    public async Task InvalidPolicyCannotLeaveStalePassingReports()
    {
        using var temp = new TempDirectory();
        var config = Path.Combine(temp.Path, "policy.json");
        await File.WriteAllTextAsync(config, "{\"unknownSetting\":true}", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "review.json"), "{\"passed\":true}", TestContext.Current.CancellationToken);
        var cli = new CliApplication(_ => null);
        var code = await cli.RunAsync(["scan", "--base", "HEAD", "--output", temp.Path, "--config", config], new StringWriter(), new StringWriter(), TestContext.Current.CancellationToken);
        Assert.Equal(2, code);
        Assert.False(File.Exists(Path.Combine(temp.Path, "review.json")));
        Assert.True(File.Exists(Path.Combine(temp.Path, "error.json")));
    }

    [Fact]
    public async Task OfflineScanProducesAllFormatsAndMeaningfulGateExitCode()
    {
        using var repo = new TestRepository();
        using var reports = new TempDirectory();
        repo.Commit("sample.cs", "// initial\nstring password = \"abc123456789XYZ\";\n");
        var code = await new CliApplication(_ => null).RunAsync(["scan", "--repo", repo.Path,
            "--base", "HEAD^", "--output", reports.Path], new StringWriter(), new StringWriter(), TestContext.Current.CancellationToken);
        Assert.Equal(1, code);
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(reports.Path, "review.json"), TestContext.Current.CancellationToken));
        Assert.Equal("scan", json.RootElement.GetProperty("mode").GetString());
        Assert.False(json.RootElement.GetProperty("passed").GetBoolean());
        using var sarif = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(reports.Path, "review.sarif"), TestContext.Current.CancellationToken));
        Assert.Equal("2.1.0", sarif.RootElement.GetProperty("version").GetString());
        Assert.Single(sarif.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray());
        foreach (var file in Directory.EnumerateFiles(reports.Path))
            Assert.DoesNotContain("abc123456789XYZ", await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("--unknown", "value")]
    [InlineData("--repo", "--head")]
    public void RejectsBadOptions(string option, string value) =>
        Assert.Throws<HarnessException>(() => CliOptions.Parse(["scan", "--base", "HEAD", option, value], _ => null));

    [Fact]
    public async Task ReportsEscapeModelMarkupAndRedactEchoedSecrets()
    {
        using var temp = new TempDirectory();
        ReportWriter.Prepare(temp.Path);
        var writer = new ReportWriter(new SecretRedactor(["known-secret-value"]));
        var report = new HarnessReport("1.0", "review", "openai", "test", new string('a', 40), new string('b', 40), DateTimeOffset.UtcNow,
            true, "<script>alert(1)</script> [click](https://evil.invalid) @everyone known-secret-value", [], [], [], []);
        await writer.WriteAsync(report, temp.Path, TestContext.Current.CancellationToken);
        var markdown = await File.ReadAllTextAsync(Path.Combine(temp.Path, "review.md"), TestContext.Current.CancellationToken);
        Assert.DoesNotContain("<script>", markdown);
        Assert.DoesNotContain("[click](", markdown);
        Assert.DoesNotContain("@everyone", markdown);
        Assert.DoesNotContain("known-secret-value", markdown);
    }

    [Fact]
    public async Task CancellationCannotProducePassingReports()
    {
        using var repo = new TestRepository();
        using var reports = new TempDirectory();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var stderr = new StringWriter();
        var code = await new CliApplication(_ => null).RunAsync(["scan", "--base", "HEAD", "--repo", repo.Path,
            "--output", reports.Path], new StringWriter(), stderr, cancellation.Token);
        Assert.Equal(9, code);
        Assert.Contains("BH1009", stderr.ToString());
        Assert.False(File.Exists(Path.Combine(reports.Path, "review.json")));
        Assert.True(File.Exists(Path.Combine(reports.Path, "error.json")));
    }

    [Fact]
    public async Task EmptyOptionalEnvironmentValueDoesNotEraseTrustedPolicy()
    {
        using var temp = new TempDirectory();
        var config = Path.Combine(temp.Path, "policy.json");
        await File.WriteAllTextAsync(config, "{\"model\":\"test-model\"}", TestContext.Current.CancellationToken);
        var environment = new Dictionary<string, string> { ["HARNESS_MODEL"] = "", ["OPENAI_API_KEY"] = "test-secret-key" };
        var code = await new CliApplication(name => environment.GetValueOrDefault(name)).RunAsync(["validate", "--config", config,
            "--output", temp.Path], new StringWriter(), new StringWriter(), TestContext.Current.CancellationToken);
        Assert.Equal(0, code);
    }
}

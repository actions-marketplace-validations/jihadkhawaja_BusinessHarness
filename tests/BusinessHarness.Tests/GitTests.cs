using BusinessHarness.Core;
using Xunit;

namespace BusinessHarness.Tests;

public sealed class GitTests
{
    [Fact]
    public async Task ReadsCommittedDiffRedactsSecretsAndIgnoresWorkingTree()
    {
        using var repo = new TestRepository();
        repo.Commit("sample.cs", "// initial\nvar api_key = \"abcdefgh12345678\";\n");
        File.WriteAllText(Path.Combine(repo.Path, "untracked.cs"), "not part of review");
        File.WriteAllText(Path.Combine(repo.Path, "sample.cs"), "uncommitted changes");
        var changes = await repo.Reader().ReadChangesAsync("HEAD^", "HEAD", TestContext.Current.CancellationToken);
        var file = Assert.Single(changes.Files);
        Assert.Equal("sample.cs", file.Path);
        Assert.Contains(2, file.AddedLines);
        Assert.DoesNotContain("abcdefgh12345678", file.Patch);
        Assert.DoesNotContain("uncommitted", file.Patch);
        Assert.Equal("BH001", Assert.Single(changes.SecurityFindings).RuleId);
    }

    [Fact]
    public async Task UsesMergeBaseRatherThanTargetTip()
    {
        using var repo = new TestRepository();
        repo.Git("checkout", "-b", "feature");
        repo.Commit("feature.cs", "// feature\n");
        var head = repo.Git("rev-parse", "HEAD");
        repo.Git("checkout", "main");
        repo.Commit("target.cs", "// unrelated target change\n");
        var changes = await repo.Reader().ReadChangesAsync("main", head, TestContext.Current.CancellationToken);
        Assert.Equal("feature.cs", Assert.Single(changes.Files).Path);
    }

    [Fact]
    public async Task WithholdsSensitiveFilesAndCreatesBlockingFinding()
    {
        using var repo = new TestRepository();
        repo.Commit(".env", "PASSWORD=very-sensitive-value\n");
        var changes = await repo.Reader().ReadChangesAsync("HEAD^", "HEAD", TestContext.Current.CancellationToken);
        Assert.Empty(changes.Files);
        Assert.Equal(".env", Assert.Single(changes.ExcludedFiles));
        Assert.Equal("BH002", Assert.Single(changes.SecurityFindings).RuleId);
    }

    [Fact]
    public async Task RejectsOversizedPatchInsteadOfTruncating()
    {
        using var repo = new TestRepository();
        repo.Commit("big.cs", new string('x', 3000));
        var exception = await Assert.ThrowsAsync<HarnessException>(() => repo.Reader(new HarnessPolicy { MaxFileDiffBytes = 1000 })
            .ReadChangesAsync("HEAD^", "HEAD", TestContext.Current.CancellationToken));
        Assert.Equal(ErrorCode.LimitExceeded, exception.Code);
    }

    [Theory]
    [InlineData("missing-ref", ErrorCode.Git)]
    [InlineData("--output=owned", ErrorCode.Configuration)]
    public async Task ExplainsInvalidRefs(string reference, ErrorCode code)
    {
        using var repo = new TestRepository();
        var error = await Assert.ThrowsAsync<HarnessException>(() => repo.Reader().ReadChangesAsync(reference, "HEAD", TestContext.Current.CancellationToken));
        Assert.Equal(code, error.Code);
    }

    [Fact]
    public async Task DoesNotExecuteExternalDiffDriver()
    {
        using var repo = new TestRepository();
        repo.Git("config", "diff.external", "this-command-must-never-run");
        repo.Commit("sample.cs", "// safe\n");
        var changes = await repo.Reader().ReadChangesAsync("HEAD^", "HEAD", TestContext.Current.CancellationToken);
        Assert.Single(changes.Files);
    }

    [Fact]
    public async Task RejectsBinaryChanges()
    {
        using var repo = new TestRepository();
        repo.Commit("binary.dat", "\0\x01\x02\0");
        var error = await Assert.ThrowsAsync<HarnessException>(() => repo.Reader().ReadChangesAsync("HEAD^", "HEAD", TestContext.Current.CancellationToken));
        Assert.Equal(ErrorCode.Git, error.Code);
    }

    [Theory]
    [InlineData("120000")]
    [InlineData("160000")]
    public async Task RejectsLinkAndSubmoduleModesWithoutReadingTargets(string mode)
    {
        using var repo = new TestRepository();
        var hash = mode == "120000" ? repo.Git("hash-object", "-w", "--", "sample.cs") : repo.Git("rev-parse", "HEAD");
        repo.Git("update-index", "--add", "--cacheinfo", mode, hash, "link-or-module");
        repo.Git("commit", "-m", "add special mode");
        var error = await Assert.ThrowsAsync<HarnessException>(() => repo.Reader().ReadChangesAsync("HEAD^", "HEAD", TestContext.Current.CancellationToken));
        Assert.Equal(ErrorCode.Git, error.Code);
        Assert.Contains("submodule", error.Message);
    }
}

using BusinessHarness.Core;
using BusinessHarness.Infrastructure;
using Xunit;

namespace BusinessHarness.Tests;

public sealed class SecurityTests
{
    [Fact]
    public void RedactsTokensAssignmentsAndKnownSecrets()
    {
        var secret = "specific-runtime-value";
        var redactor = new SecretRedactor([secret]);
        var result = redactor.Redact("api_key = abcdefghijklmnop\n" + secret + "\nsk-proj-abcdefghijklmnop12345678");
        Assert.DoesNotContain(secret, result);
        Assert.DoesNotContain("abcdefghijklmnop", result);
        Assert.Contains("[REDACTED]", result);
    }

    [Theory]
    [InlineData("-----BEGIN PRIVATE KEY-----\nVERY-SENSITIVE-KEY-BODY\n-----END PRIVATE KEY-----")]
    [InlineData("-----BEGIN RSA PRIVATE KEY-----\n+VERY-SENSITIVE-KEY-BODY")]
    public void RedactsEntirePrivateKeyIncludingTruncatedBlocks(string key)
    {
        var result = new SecretRedactor().Redact(key);
        Assert.DoesNotContain("VERY-SENSITIVE", result);
        Assert.Equal(key.Count(c => c == '\n'), result.Count(c => c == '\n'));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("C:/outside")]
    [InlineData("/root/file")]
    [InlineData("src\\file.cs")]
    [InlineData("src/file\n.cs")]
    public void RejectsUnsafePaths(string path) => Assert.False(SafePaths.IsRepositoryPath(path));

    [Theory]
    [InlineData(".env.production")]
    [InlineData("config/credentials.json")]
    [InlineData("certs/client.pfx")]
    public void SensitiveFilenamesAreExcluded(string path) => Assert.True(SafePaths.IsExcluded(path));

    [Theory]
    [InlineData("http://team.atlassian.net")]
    [InlineData("https://team.atlassian.net.evil.invalid")]
    [InlineData("https://127.0.0.1")]
    [InlineData("https://team.atlassian.net:444")]
    [InlineData("https://user:password@team.atlassian.net")]
    [InlineData("https://team.atlassian.net/path")]
    [InlineData("https://nested.team.atlassian.net")]
    public void JiraRejectsUntrustedEndpoints(string site) => Assert.Throws<HarnessException>(() => JiraIssueSource.ValidateSite(site));

    [Fact]
    public void JiraAcceptsCloudSite() => Assert.Equal("team.atlassian.net", JiraIssueSource.ValidateSite("https://team.atlassian.net").Host);
}

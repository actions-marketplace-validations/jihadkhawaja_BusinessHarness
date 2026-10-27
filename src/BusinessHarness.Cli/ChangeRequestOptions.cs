using System.Text.Json;
using System.Text.RegularExpressions;
using BusinessHarness.Core;
using BusinessHarness.Infrastructure;

namespace BusinessHarness.Cli;

public static class ChangeRequestOptions
{
    public static async Task<ChangeRequestPolicy?> ResolveAsync(string? url, ChangeRequestPolicy? configured,
        Func<string, string?> environment, CancellationToken ct)
    {
        if (url is not null)
        {
            var uri = IntegrationEndpoint.Validate(url);
            if (uri.Query.Length != 0) throw Invalid();
            var github = uri.Host == "github.com";
            if (!github && uri.Host != "gitlab.com") throw Invalid();
            var match = Regex.Match(uri.AbsolutePath, github ? @"^/(?<project>[^/]+/[^/]+)/pull/(?<number>[1-9][0-9]*)/?$" : @"^/(?<project>.+)/-/merge_requests/(?<number>[1-9][0-9]*)/?$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            if (!match.Success || !int.TryParse(match.Groups["number"].Value, out var number)) throw Invalid();
            var selected = new ChangeRequestPolicy { Platform = github ? "github" : "gitlab", Project = match.Groups["project"].Value, Number = number };
            // Explicit public URLs must never inherit credentials or an endpoint intended for another host.
            selected.Validate(requireIdentity: true);
            return selected;
        }
        if (configured is not null)
        {
            if (configured.Number > 0 && configured.Project.Length > 0) return Checked(configured);
            var inferred = await FromCiAsync(environment, configured.ApiBase is not null, ct);
            if (inferred is null || inferred.Platform != configured.Platform) throw Invalid();
            return Checked(configured with
            {
                Project = configured.Project.Length > 0 ? configured.Project : inferred.Project,
                Number = configured.Number > 0 ? configured.Number : inferred.Number
            });
        }
        return await FromCiAsync(environment, false, ct);
    }

    private static async Task<ChangeRequestPolicy?> FromCiAsync(Func<string, string?> environment, bool trustedEndpoint, CancellationToken ct)
    {
        if (environment("GITHUB_EVENT_NAME") is "pull_request" or "pull_request_target" or "issue_comment" or "pull_request_review_comment" or "pull_request_review")
        {
            if (!trustedEndpoint && environment("GITHUB_SERVER_URL") is { Length: > 0 } githubServer && githubServer != "https://github.com") throw Invalid();
            var path = environment("GITHUB_EVENT_PATH");
            if (string.IsNullOrWhiteSpace(path)) throw Invalid();
            SafePaths.RejectLinks(path);
            await using var stream = File.OpenRead(path);
            if (stream.Length > 1_000_000) throw Invalid();
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = document.RootElement;
            int id;
            if (environment("GITHUB_EVENT_NAME") == "issue_comment")
            {
                if (!root.TryGetProperty("issue", out var issue) || !issue.TryGetProperty("pull_request", out _)) return null;
                if (!issue.TryGetProperty("number", out var issueNumber) || !issueNumber.TryGetInt32(out id)) throw Invalid();
            }
            else if (root.TryGetProperty("number", out var number) && number.TryGetInt32(out id)) { }
            else if (root.TryGetProperty("pull_request", out var pr) && pr.TryGetProperty("number", out var prNumber) && prNumber.TryGetInt32(out id)) { }
            else throw Invalid();
            return Checked(new() { Platform = "github", Project = environment("GITHUB_REPOSITORY") ?? "", Number = id });
        }
        if (environment("CI_PIPELINE_SOURCE") == "merge_request_event")
        {
            if (!trustedEndpoint && environment("CI_SERVER_URL") is { Length: > 0 } gitlabServer && gitlabServer != "https://gitlab.com") throw Invalid();
            if (!int.TryParse(environment("CI_MERGE_REQUEST_IID"), out var number)) throw Invalid();
            return Checked(new() { Platform = "gitlab", Project = environment("CI_PROJECT_PATH") ?? "", Number = number });
        }
        return null;
    }

    private static ChangeRequestPolicy Checked(ChangeRequestPolicy policy) { policy.Validate(requireIdentity: true); return policy; }
    private static HarnessException Invalid() => new(ErrorCode.Configuration, "Could not identify the pull/merge request.",
        "Use --change with a github.com PR or gitlab.com MR URL, trusted changeRequest policy for a private host, or standard GitHub/GitLab CI metadata.");
}

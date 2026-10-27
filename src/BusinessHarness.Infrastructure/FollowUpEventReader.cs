using System.Text;
using System.Text.Json;
using BusinessHarness.Core;

namespace BusinessHarness.Infrastructure;

/// <summary>Parses already authenticated webhook/CI payloads. Authentication belongs to the trusted receiver.</summary>
public static class FollowUpEventReader
{
    public static async Task<FollowUpEvent> ReadAsync(string path, MemoryIdentity identity, ChangeRequestPolicy? expectedRequest, string? jiraSite, CancellationToken ct)
    {
        SafePaths.RejectLinks(path);
        await using var stream = File.OpenRead(path);
        if (stream.Length > 1_000_000) throw Invalid();
        using var body = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions { MaxDepth = 32 }, ct);
        var root = body.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw Invalid();
        if (root.TryGetProperty("source", out _))
            return root.Deserialize<FollowUpEvent>(JsonDefaults.Options) ?? throw Invalid();
        if (root.TryGetProperty("repository", out var repository) && repository.ValueKind == JsonValueKind.Object && repository.TryGetProperty("full_name", out _))
        {
            var comment = root.TryGetProperty("comment", out var githubComment) ? githubComment : Object(root, "review");
            var project = Required(repository, "full_name");
            var url = Required(comment, "html_url");
            var item = root.TryGetProperty("issue", out var issue) ? issue : Object(root, "pull_request");
            var thread = (item.TryGetProperty("pull_request", out _) || root.TryGetProperty("pull_request", out _) ? "pr:" : "issue:") + Id(item, "number");
            VerifyProject(project, "github", new Uri(url).Host, thread, identity, expectedRequest);
            var source = root.TryGetProperty("review", out _) ? "github-review" : root.TryGetProperty("issue", out _) ? "github-issue-comment" : "github-review-comment";
            return new(source, Id(comment, "id"), Required(Object(comment, "user"), "login"), Required(comment, "body"), url,
                identity.RepositoryId, thread);
        }
        if (ApiJson.String(root, "object_kind") == "note")
        {
            var note = Object(root, "object_attributes");
            var project = Required(Object(root, "project"), "path_with_namespace");
            var url = Required(note, "url");
            var item = root.TryGetProperty("merge_request", out var mr) ? mr : Object(root, "issue");
            var thread = (root.TryGetProperty("merge_request", out _) ? "mr:" : "issue:") + Id(item, "iid");
            VerifyProject(project, "gitlab", new Uri(url).Host, thread, identity, expectedRequest);
            return new("gitlab", Id(note, "id"), Required(Object(root, "user"), "username"), Required(note, "note"), url,
                identity.RepositoryId, thread);
        }
        if (root.TryGetProperty("webhookEvent", out _) && root.TryGetProperty("issue", out var jiraIssue))
        {
            var comment = Object(root, "comment");
            var key = Required(jiraIssue, "key");
            if (!JiraIssueSource.IsIssueKey(key)) throw Invalid();
            var site = JiraIssueSource.ValidateSite(jiraSite);
            var self = IntegrationEndpoint.Validate(Required(jiraIssue, "self"));
            if (self.Authority != site.Authority) throw Invalid();
            var author = Object(comment, "author");
            var actor = ApiJson.String(author, "accountId") ?? throw Invalid();
            if (!comment.TryGetProperty("body", out var text)) throw Invalid();
            return new("jira", Id(comment, "id"), actor, Flatten(text), new Uri(site, "browse/" + key).AbsoluteUri,
                identity.RepositoryId, "jira:" + key, [key]);
        }
        throw Invalid();
    }

    private static void VerifyProject(string project, string platform, string host, string thread, MemoryIdentity identity, ChangeRequestPolicy? request)
    {
        var canonical = platform + ":" + host + "/" + (platform == "github" ? project.ToLowerInvariant() : project);
        var expectedHost = request?.ApiBase is null ? request?.Platform == "github" ? "github.com" : "gitlab.com" : new Uri(request.ApiBase).Host;
        if (expectedHost == "api.github.com") expectedHost = "github.com";
        if (request is not null ? request.Platform != platform || host != expectedHost ||
            !string.Equals(project, request.Project, platform == "github" ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
            thread != (platform == "github" ? "pr:" : "mr:") + request.Number : canonical != identity.RepositoryId)
            throw new HarnessException(ErrorCode.Configuration, "Follow-up repository does not match trusted configuration.", "Route the event to its repository's policy and stable memory identity.");
    }

    private static JsonElement Object(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object ? value : throw Invalid();
    private static string Required(JsonElement root, string name) => ApiJson.String(root, name) is { Length: > 0 } value ? value : throw Invalid();
    private static string Id(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String or JsonValueKind.Number ? value.ToString() : throw Invalid();

    private static string Flatten(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String) return element.GetString()!;
        var text = new StringBuilder();
        Append(element, text);
        return text.ToString().Trim();
    }

    private static void Append(JsonElement element, StringBuilder text)
    {
        if (element.ValueKind != JsonValueKind.Object) return;
        if (ApiJson.String(element, "text") is { } value) text.Append(value);
        if (ApiJson.String(element, "type") == "mention" && element.TryGetProperty("attrs", out var attrs))
        {
            if (ApiJson.String(attrs, "text") is { } mention) text.Append(mention);
            if (ApiJson.String(attrs, "id") is { } accountId) text.Append(" @").Append(accountId).Append(' ');
        }
        if (element.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            foreach (var child in content.EnumerateArray()) Append(child, text);
        if (ApiJson.String(element, "type") is "paragraph" or "hardBreak" or "listItem") text.AppendLine();
    }

    private static HarnessException Invalid() => new(ErrorCode.Configuration, "Invalid or unsupported follow-up event.",
        "Supply a verified GitHub comment, GitLab note, Jira issue comment, or normalized FollowUpEvent JSON through --event. Never trust an unauthenticated webhook body.");
}

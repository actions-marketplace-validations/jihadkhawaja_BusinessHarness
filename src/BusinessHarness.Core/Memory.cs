namespace BusinessHarness.Core;

public sealed record MemoryPolicy
{
    public bool Enabled { get; init; }
    public string Scope { get; init; } = "repository";
    public List<string> ReadScopes { get; init; } = [];
    public string Namespace { get; init; } = "default";
    public string? Directory { get; init; }
    public string? RepositoryId { get; init; }
    public string? SessionId { get; init; }
    public int RetentionDays { get; init; } = 90;
    public int MaxEntries { get; init; } = 2000;
    public int TopK { get; init; } = 8;
    public int MaxContextCharacters { get; init; } = 16_000;
    public bool CaptureReviews { get; init; } = true;
    public bool AcceptFollowUps { get; init; }
    public List<string> AgentMentions { get; init; } = ["@businessharness"];
    public List<string> AllowedActors { get; init; } = [];

    public void Validate()
    {
        if (!IsScope(Scope) || ReadScopes is null || ReadScopes.Count > 3 || ReadScopes.Any(s => !IsScope(s)) ||
            ReadScopes.Distinct(StringComparer.Ordinal).Count() != ReadScopes.Count ||
            !IsIdentifier(Namespace) || (RepositoryId is not null && !IsIdentifier(RepositoryId)) ||
            (SessionId is not null && !IsIdentifier(SessionId)) ||
            (Directory is not null && string.IsNullOrWhiteSpace(Directory)) ||
            RetentionDays is < 1 or > 3650 || MaxEntries is < 10 or > 10_000 || TopK is < 1 or > 50 ||
            MaxContextCharacters is < 1000 or > 64_000 || AgentMentions is null || AllowedActors is null ||
            AgentMentions.Count > 20 || AllowedActors.Count > 100 ||
            AgentMentions.Any(m => !IsIdentifier(m) || !m.StartsWith('@') || m.Any(char.IsWhiteSpace)) ||
            AllowedActors.Any(a => !IsIdentifier(a)) ||
            (AcceptFollowUps && (AgentMentions.Count == 0 || AllowedActors.Count == 0)))
            throw new HarnessException(ErrorCode.Configuration, "Invalid long-term memory configuration.",
                "Use global/repository/session scopes, bounded storage/retrieval limits, and explicit mentions plus allowedActors when accepting follow-ups. See docs/memory.md.");
    }

    public static bool IsScope(string scope) => scope is "global" or "repository" or "session";
    public static bool IsIdentifier(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 500 && !value.Any(char.IsControl);
}

public sealed record MemoryIdentity(string Namespace, string RepositoryId, string? SessionId);
public sealed record ReviewMemoryEntry(string Id, string Text, string Source, string Actor, string Kind, DateTimeOffset CreatedAt);
public sealed record MemoryQuery(string Text, IReadOnlyList<string> References);
public sealed record FollowUpEvent(string Source, string EventId, string Actor, string Text, string Url,
    string? RepositoryId = null, string? SessionId = null, IReadOnlyList<string>? References = null);

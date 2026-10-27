using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BusinessHarness.Application;
using BusinessHarness.Core;
using MagiCore;
using Memory = MagiCore.Memory;

namespace BusinessHarness.Infrastructure;

/// <summary>Durable scoped snapshots of MAGICore memories, using its local embeddings and hybrid retrieval.</summary>
public sealed class MagiCoreReviewMemory : IReviewMemory
{
    private const int MaximumFileBytes = 32_000_000;
    private readonly MemoryPolicy policy;
    private readonly MemoryIdentity identity;
    private readonly SecretRedactor redactor;
    private readonly string directory;

    public MagiCoreReviewMemory(MemoryPolicy policy, MemoryIdentity identity, string directory, SecretRedactor redactor)
    {
        policy.Validate();
        if (!policy.Enabled || !MemoryPolicy.IsIdentifier(identity.Namespace) || !MemoryPolicy.IsIdentifier(identity.RepositoryId) ||
            ((policy.Scope == "session" || policy.ReadScopes.Contains("session", StringComparer.Ordinal)) && !MemoryPolicy.IsIdentifier(identity.SessionId ?? "")))
            throw new HarnessException(ErrorCode.Configuration, "Memory scope is missing a stable identity.", "Enable memory and configure repository/session IDs. Session scope requires --session, a PR/MR identity, or HARNESS_SESSION_ID.");
        this.policy = policy;
        this.identity = identity;
        this.directory = Path.GetFullPath(directory);
        this.redactor = redactor;
        SafePaths.RejectLinks(this.directory);
    }

    public async Task<IReadOnlyList<ReviewMemoryEntry>> RecallAsync(MemoryQuery query, CancellationToken cancellationToken)
    {
        List<(Memory Memory, double Score)> candidates = [];
        foreach (var scope in policy.ReadScopes.Count == 0 ? [policy.Scope] : policy.ReadScopes)
        {
            var found = await WithPartitionAsync(scope, false, async (service, partition) =>
            {
                var all = await service.GetAllAsync(new MemoryFilter(UserId: partition), cancellationToken);
                var ranked = await service.SearchAsync(string.IsNullOrWhiteSpace(query.Text) ? "repository review" : query.Text,
                    new MemorySearchOptions { Filter = new(UserId: partition), TopK = policy.TopK, Threshold = 0.05, Hybrid = true }, cancellationToken);
                // Exact thread and reference matches make follow-ups available even when lexical overlap is weak.
                return all.Select(m => (Memory: m, Score: IsSameThread(m) ? 3.0 : References(m).Intersect(query.References, StringComparer.Ordinal).Any() ? 2.0 : -1.0))
                    .Where(item => item.Score > 0).Concat(ranked.Select(r => (Memory: r.Memory, r.Score))).ToArray();
            }, cancellationToken);
            candidates.AddRange(found);
        }
        List<ReviewMemoryEntry> result = [];
        var characters = 0;
        foreach (var item in candidates.OrderByDescending(c => c.Score).ThenByDescending(c => c.Memory.UpdatedAt).DistinctBy(c => c.Memory.Id))
        {
            var entry = ToContext(item.Memory);
            var size = JsonSerializer.Serialize(entry, JsonDefaults.Options).Length;
            if (characters + size > policy.MaxContextCharacters) continue;
            result.Add(entry);
            characters += size;
            if (result.Count == policy.TopK) break;
        }
        return result;
    }

    public async Task RememberReviewAsync(MemoryQuery query, HarnessReport report, CancellationToken cancellationToken)
    {
        if (!policy.CaptureReviews) return;
        var safe = new ReportWriter(redactor).Sanitize(report);
        // A compact historical outcome, never a claim that an approval was published or tests executed.
        var summary = JsonSerializer.Serialize(new
        {
            kind = "historical_review_gate",
            report.BaseCommit,
            report.HeadCommit,
            report.Passed,
            safe.Summary,
            findings = safe.Findings.Take(20).Select(f => new { f.RuleId, f.Severity, f.Path, f.Line, f.Title }),
            totalFindings = safe.Findings.Count,
            requirements = safe.Requirements.Select(r => new { r.IssueKey, r.Status })
        }, JsonDefaults.Options);
        if (summary.Length > 16_000)
            summary = $"Historical review gate; base={report.BaseCommit}; head={report.HeadCommit}; passed={report.Passed}; findings={safe.Findings.Count}.\n" + safe.Summary;
        await SaveAsync("review:" + report.BaseCommit + ":" + report.HeadCommit, "review", "businessharness", "review", summary,
            query.References, cancellationToken);
    }

    public async Task<bool> RememberFollowUpAsync(FollowUpEvent followUp, CancellationToken cancellationToken)
    {
        if (!policy.AcceptFollowUps || !policy.AllowedActors.Contains(followUp.Actor, StringComparer.Ordinal) ||
            !policy.AgentMentions.Any(mention => Regex.IsMatch(followUp.Text,
                @"(?<![\w@-])" + Regex.Escape(mention) + @"(?![\w-])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100))))
            return false;
        if (!MemoryPolicy.IsIdentifier(followUp.Source) || !MemoryPolicy.IsIdentifier(followUp.EventId) ||
            !MemoryPolicy.IsIdentifier(followUp.Actor) || string.IsNullOrWhiteSpace(followUp.Text) || followUp.Text.Length > 16_000 ||
            followUp.Url.Length > 2000 || followUp.Url.Any(char.IsControl) || (followUp.RepositoryId is not null && followUp.RepositoryId != identity.RepositoryId) ||
            followUp.References?.Count > 20 || followUp.References?.Any(r => !MemoryPolicy.IsIdentifier(r)) == true)
            throw new HarnessException(ErrorCode.Configuration, "Follow-up identity or content is invalid.", "Route verified events to the configured repository, with bounded IDs, references and text. See docs/memory.md.");
        // Comment URLs use fragments for note IDs. These URLs are provenance and are never fetched.
        _ = IntegrationEndpoint.Validate(followUp.Url.Split('#', 2)[0]);
        await SaveAsync(followUp.Source + ":" + followUp.EventId, followUp.Url, followUp.Actor, "user_follow_up", followUp.Text,
            followUp.References ?? [], cancellationToken);
        return true;
    }

    private async Task SaveAsync(string eventId, string source, string actor, string kind, string text, IReadOnlyList<string> references, CancellationToken ct)
    {
        await WithPartitionAsync(policy.Scope, true, async (service, partition) =>
        {
            var eventKey = Hash(JsonSerializer.Serialize(new[] { identity.RepositoryId, identity.SessionId ?? "", source, eventId }));
            var metadata = new Dictionary<string, string>
            {
                ["event"] = eventKey,
                ["repository"] = identity.RepositoryId,
                ["session"] = identity.SessionId ?? "",
                ["source"] = redactor.Redact(source),
                ["actor"] = redactor.Redact(actor),
                ["kind"] = kind,
                ["references"] = JsonSerializer.Serialize(references.Select(redactor.Redact).Distinct(StringComparer.Ordinal).ToArray())
            };
            var existing = (await service.GetAllAsync(new MemoryFilter(UserId: partition), ct))
                .SingleOrDefault(m => m.Metadata.GetValueOrDefault("event") == eventKey);
            var safeText = redactor.Redact(text);
            if (safeText.Length > 16_000)
                throw new HarnessException(ErrorCode.LimitExceeded, "Redacted memory exceeds 16,000 characters.", "Reduce the follow-up text; memory content is never silently truncated.");
            if (existing is not null)
                await service.UpdateAsync(existing.Id, new MemoryUpdate
                {
                    Text = safeText,
                    Metadata = metadata,
                    UpdateExpiration = true,
                    ExpiresAt = DateTimeOffset.UtcNow.AddDays(policy.RetentionDays)
                }, ct);
            else
                await service.AddAsync(safeText, new MemoryAddOptions
                {
                    UserId = partition,
                    Scope = ToScope(policy.Scope),
                    Infer = false,
                    Deduplicate = false,
                    Metadata = metadata,
                    ExpiresAt = DateTimeOffset.UtcNow.AddDays(policy.RetentionDays),
                    MemoryType = kind
                }, ct);
            return true;
        }, ct);
    }

    private async Task<T> WithPartitionAsync<T>(string scope, bool write, Func<MemoryService, string, Task<T>> operation, CancellationToken ct)
    {
        var partition = Hash(JsonSerializer.Serialize(new[] { identity.Namespace, scope, scope == "global" ? "" : identity.RepositoryId,
            scope == "session" ? identity.SessionId : "" }));
        SafePaths.RejectLinks(directory);
        if (!OperatingSystem.IsWindows()) Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        else Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, partition + ".json");
        var lockPath = Path.Combine(directory, partition + ".lock");
        SafePaths.RejectLinks(path);
        SafePaths.RejectLinks(lockPath);
        await using var gate = await LockAsync(lockPath, ct);
        using var embeddings = new LocalEmbeddingGenerator();
        var store = new InMemoryStore();
        if (File.Exists(path))
        {
            await using var stream = File.OpenRead(path);
            if (stream.Length > MaximumFileBytes) throw Limit();
            Snapshot snapshot;
            try { snapshot = await JsonSerializer.DeserializeAsync<Snapshot>(stream, JsonDefaults.Options, ct) ?? throw Corrupt(); }
            catch (JsonException) { throw Corrupt(); }
            if (snapshot.Version != 1 || snapshot.Partition != partition || snapshot.Memories.Count > 10_000 ||
                snapshot.Memories.Any(m => m is null || m.UserId != partition || m.Text is null || m.Text.Length > 16_000 ||
                    m.Metadata is null || m.Id is null || m.Id.Length > 100 || m.Metadata.Count > 20 ||
                    m.Metadata.Any(p => p.Key.Length > 100 || p.Value is null || p.Value.Length > 16_000)) ||
                snapshot.Memories.Select(m => m.Id).Distinct(StringComparer.Ordinal).Count() != snapshot.Memories.Count) throw Corrupt();
            foreach (var memory in snapshot.Memories.Where(m => m.ExpiresAt > DateTimeOffset.UtcNow))
                await store.SaveAsync(memory, await embeddings.GenerateVectorCoreAsync(memory.Text, ct), ct);
        }
        var service = new MemoryService(store, embeddings);
        var result = await operation(service, partition);
        if (write)
        {
            var memories = (await service.GetAllAsync(new MemoryFilter(UserId: partition), ct))
                .OrderByDescending(m => m.UpdatedAt).Take(policy.MaxEntries).ToList();
            var data = JsonSerializer.SerializeToUtf8Bytes(new Snapshot(1, partition, memories), JsonDefaults.Options);
            if (data.Length > MaximumFileBytes) throw Limit();
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await using (var output = OpenPrivate(temporary, FileMode.CreateNew))
                {
                    await output.WriteAsync(data, ct);
                    await output.FlushAsync(ct);
                    output.Flush(flushToDisk: true);
                }
                SafePaths.RejectLinks(path);
                File.Move(temporary, path, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        return result;
    }

    private static async Task<FileStream> LockAsync(string path, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try { return OpenPrivate(path, FileMode.OpenOrCreate); }
            catch (IOException) when (attempt < 100) { await Task.Delay(50, ct); }
            catch (IOException) { throw new HarnessException(ErrorCode.FileSystem, "Memory partition is locked or unavailable.", "Retry after the other writer completes and check storage permissions. Use a local filesystem with reliable exclusive locks."); }
        }
    }

    private static FileStream OpenPrivate(string path, FileMode mode)
    {
        var options = new FileStreamOptions { Mode = mode, Access = FileAccess.ReadWrite, Share = FileShare.None, Options = FileOptions.Asynchronous };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return new FileStream(path, options);
    }

    private bool IsSameThread(Memory memory) => identity.SessionId is not null && memory.Metadata.GetValueOrDefault("repository") == identity.RepositoryId &&
        memory.Metadata.GetValueOrDefault("session") == identity.SessionId;

    private static IReadOnlyList<string> References(Memory memory)
    {
        try { return JsonSerializer.Deserialize<string[]>(memory.Metadata.GetValueOrDefault("references") ?? "[]") ?? []; }
        catch (JsonException) { throw Corrupt(); }
    }

    private ReviewMemoryEntry ToContext(Memory memory) => new(memory.Id, redactor.Redact(memory.Text),
        redactor.Redact(memory.Metadata.GetValueOrDefault("source") ?? "unknown"), redactor.Redact(memory.Metadata.GetValueOrDefault("actor") ?? "unknown"),
        memory.Metadata.GetValueOrDefault("kind") ?? "historical_context", memory.CreatedAt);

    private static MemoryScope ToScope(string scope) => scope == "session" ? MemoryScope.Session : scope == "repository" ? MemoryScope.Agent : MemoryScope.User;
    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static HarnessException Corrupt() => new(ErrorCode.FileSystem, "Memory snapshot is invalid or belongs to another scope.", "Restore a trusted backup or select a new memory directory. The existing file was not overwritten.");
    private static HarnessException Limit() => new(ErrorCode.LimitExceeded, "Memory snapshot exceeds 32 MB.", "Reduce retention or maxEntries, or select a new memory namespace.");
    private sealed record Snapshot(int Version, string Partition, List<Memory> Memories);
}

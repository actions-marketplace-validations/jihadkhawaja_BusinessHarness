using System.Text.Json;
using BusinessHarness.Core;
using BusinessHarness.Infrastructure;

namespace BusinessHarness.Cli;

public static class MemorySettings
{
    public static async Task<MemoryPolicy> ResolveAsync(MemoryPolicy? repositoryPolicy, CliOptions options,
        Func<string, string?> environment, CancellationToken ct)
    {
        MemoryPolicy? global = null;
        if (environment("HARNESS_MEMORY_CONFIG") is { Length: > 0 } path)
        {
            SafePaths.RejectLinks(path);
            await using var stream = File.OpenRead(path);
            if (stream.Length > 64_000) throw new HarnessException(ErrorCode.Configuration, "Global memory settings exceed 64 KB.", "Use a small trusted JSON file with a memory object.");
            global = (await JsonSerializer.DeserializeAsync<GlobalSettings>(stream, JsonDefaults.Options, ct))?.Memory;
        }
        var policy = repositoryPolicy ?? global ?? new MemoryPolicy();
        var scope = options.MemoryScope ?? NonEmpty(environment("HARNESS_MEMORY_SCOPE"));
        policy = policy with
        {
            Enabled = scope is null ? policy.Enabled : scope != "off",
            Scope = scope is null or "off" ? policy.Scope : scope,
            Directory = options.MemoryDirectory ?? NonEmpty(environment("HARNESS_MEMORY_DIRECTORY")) ?? policy.Directory,
            RepositoryId = NonEmpty(environment("HARNESS_MEMORY_REPOSITORY")) ?? policy.RepositoryId,
            SessionId = options.SessionId ?? NonEmpty(environment("HARNESS_SESSION_ID")) ?? policy.SessionId
        };
        policy.Validate();
        return policy;
    }

    public static MemoryIdentity Identity(MemoryPolicy policy, string repository, ChangeRequestPolicy? request)
    {
        var host = request?.ApiBase is null ? request?.Platform == "github" ? "github.com" : "gitlab.com" : new Uri(request.ApiBase).Host;
        if (host == "api.github.com") host = "github.com";
        var id = policy.RepositoryId ?? (request is null ? "path:" + Normalize(repository) :
            request.Platform + ":" + host + "/" +
            (request.Platform == "github" ? request.Project.ToLowerInvariant() : request.Project));
        return new(policy.Namespace, id, policy.SessionId ?? (request is null ? null : (request.Platform == "github" ? "pr:" : "mr:") + request.Number));
    }

    public static string DirectoryPath(MemoryPolicy policy, string repository, string outputDirectory)
    {
        var path = Path.GetFullPath(policy.Directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BusinessHarness", "memory"));
        var repo = Normalize(repository).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var output = Normalize(outputDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if ((Normalize(path) + Path.DirectorySeparatorChar).StartsWith(repo, comparison) ||
            (Normalize(path) + Path.DirectorySeparatorChar).StartsWith(output, comparison))
            throw new HarnessException(ErrorCode.Configuration, "Memory storage overlaps the reviewed checkout or report directory.", "Use a trusted persistent directory outside the reviewed repository and disposable reports.");
        return path;
    }

    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    private static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private sealed record GlobalSettings(MemoryPolicy Memory);
}

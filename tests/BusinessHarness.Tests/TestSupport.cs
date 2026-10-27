using System.Diagnostics;
using System.Net;
using System.Text;
using BusinessHarness.Core;
using BusinessHarness.Infrastructure;

namespace BusinessHarness.Tests;

internal sealed class TempDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "businessharness-tests", Guid.NewGuid().ToString("N"));
    public TempDirectory() => Directory.CreateDirectory(Path);
    public void Dispose()
    {
        // This exact generated directory is the only deletion target.
        if (!Directory.Exists(Path)) return;
        foreach (var file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
            if (!File.GetAttributes(file).HasFlag(FileAttributes.ReparsePoint)) File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(Path, recursive: true);
    }
}

internal sealed class TestRepository : IDisposable
{
    private readonly TempDirectory temporary = new();
    public string Path => temporary.Path;
    public TestRepository()
    {
        Git("init", "--initial-branch=main");
        Git("config", "user.email", "test@example.invalid");
        Git("config", "user.name", "Test");
        Git("config", "core.autocrlf", "false");
        File.WriteAllText(System.IO.Path.Combine(Path, "sample.cs"), "// initial\n");
        Git("add", "."); Git("commit", "-m", "base");
    }

    public void Commit(string filename, string content)
    {
        File.WriteAllText(System.IO.Path.Combine(Path, filename), content, new UTF8Encoding(false));
        Git("add", "--", filename); Git("commit", "-m", "change");
    }

    public string Git(params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = Path,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException(error);
        return output.Trim();
    }

    public GitRepository Reader(HarnessPolicy? policy = null) => new(Path, policy ?? new(), new SecretRedactor());
    public void Dispose() => temporary.Dispose();
}

internal sealed class StubHttpHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) : HttpMessageHandler
{
    public int Calls { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        return handle(request);
    }
    public static HttpResponseMessage Json(string text, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
}

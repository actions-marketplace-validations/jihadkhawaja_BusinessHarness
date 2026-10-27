using System.Net;
using System.Text;
using System.Text.Json;
using BusinessHarness.Application;
using BusinessHarness.Core;

namespace BusinessHarness.Infrastructure;

public sealed class ReportWriter(SecretRedactor redactor) : IReportWriter
{
    private static readonly string[] ReportNames = ["review.json", "review.md", "review.sarif", "error.json"];

    public static void Prepare(string directory)
    {
        SafePaths.RejectLinks(directory);
        Directory.CreateDirectory(directory);
        foreach (var name in ReportNames)
        {
            var file = Path.Combine(directory, name);
            SafePaths.RejectLinks(file);
            if (File.Exists(file)) File.Delete(file);
        }
    }

    public async Task WriteAsync(HarnessReport report, string outputDirectory, CancellationToken cancellationToken)
    {
        report = Sanitize(report);
        await WriteFileAsync(outputDirectory, "review.json", JsonSerializer.Serialize(report, JsonDefaults.Options), cancellationToken);
        await WriteFileAsync(outputDirectory, "review.md", Markdown(report), cancellationToken);
        await WriteFileAsync(outputDirectory, "review.sarif", Sarif(report), cancellationToken);
    }

    public HarnessReport Sanitize(HarnessReport report) => report with
    {
        Summary = redactor.Redact(report.Summary),
        Findings = report.Findings.Select(f => f with { Title = redactor.Redact(f.Title), Description = redactor.Redact(f.Description) }).ToArray(),
        Requirements = report.Requirements.Select(r => r with { Evidence = redactor.Redact(r.Evidence) }).ToArray()
    };

    public static Task WriteFailureAsync(FailureReport report, string directory, CancellationToken ct) =>
        WriteFileAsync(directory, "error.json", JsonSerializer.Serialize(report, JsonDefaults.Options), ct);

    private static async Task WriteFileAsync(string directory, string name, string content, CancellationToken ct)
    {
        var target = Path.Combine(Path.GetFullPath(directory), name);
        SafePaths.RejectLinks(target);
        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.Asynchronous))
            await using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                await writer.WriteAsync(content.AsMemory(), ct);
            File.Move(temporary, target, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static string Markdown(HarnessReport report)
    {
        var text = new StringBuilder();
        text.AppendLine("# BusinessHarness").AppendLine();
        text.AppendLine($"Mode: **{Escape(report.Mode)}** · Gate: **{(report.Passed ? "passed" : "failed")}**").AppendLine();
        text.AppendLine(Escape(report.Summary)).AppendLine();
        text.AppendLine($"Compared `{report.BaseCommit}` → `{report.HeadCommit}`.").AppendLine();
        foreach (var finding in report.Findings)
        {
            text.AppendLine($"- **{Escape(finding.Severity)} / {Escape(finding.RuleId)}**: {Escape(finding.Title)} ({Escape(finding.Path)}:{finding.Line})");
            text.AppendLine($"  {Escape(finding.Description)}");
        }
        if (report.Requirements.Count > 0)
        {
            text.AppendLine().AppendLine("## Reference validation").AppendLine();
            foreach (var requirement in report.Requirements)
                text.AppendLine($"- **{Escape(requirement.IssueKey)} — {Escape(requirement.Status)}**: {Escape(requirement.Evidence)}");
        }
        text.AppendLine().AppendLine($"Files inspected: {report.ReviewedFiles.Count}. Files withheld: {report.ExcludedFiles.Count}.");
        foreach (var path in report.ExcludedFiles) text.AppendLine($"- Withheld: {Escape(path)}");
        return text.ToString();
    }

    private static string Escape(string text)
    {
        text = WebUtility.HtmlEncode(text).Replace("@", "&#64;", StringComparison.Ordinal);
        foreach (var character in new[] { "\\", "`", "*", "_", "{", "}", "[", "]", "(", ")", "#", "+", "-", "!", "|" })
            text = text.Replace(character, "\\" + character, StringComparison.Ordinal);
        return text.Replace("\r", "", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    }

    private static string Sarif(HarnessReport report)
    {
        var results = report.Findings.Select(f => (object)new
        {
            ruleId = f.RuleId,
            level = f.Severity is "critical" or "high" ? "error" : f.Severity is "medium" or "low" ? "warning" : "note",
            message = new { text = f.Title + ": " + f.Description },
            locations = new[] { new { physicalLocation = new
            {
                artifactLocation = new { uri = string.Join('/', f.Path.Split('/').Select(Uri.EscapeDataString)), uriBaseId = "%SRCROOT%" },
                region = new { startLine = f.Line }
            } } },
            properties = new { severity = f.Severity }
        }).Concat(report.Requirements.Where(r => r.Status != "met").Select(r => (object)new
        {
            ruleId = "BH003",
            level = "error",
            message = new { text = r.IssueKey + ": " + r.Status + ". " + r.Evidence }
        })).ToArray();
        return JsonSerializer.Serialize(new
        {
            version = "2.1.0",
            runs = new[] { new
            {
                tool = new { driver = new { name = "BusinessHarness", version = "0.1.0" } },
                invocations = new[] { new { executionSuccessful = true } },
                properties = new { mode = report.Mode, gatePassed = report.Passed }, results
            } }
        }, JsonDefaults.Options);
    }
}

using BusinessHarness.Core;

namespace BusinessHarness.Infrastructure;

/// <summary>Literal trusted Markdown with whole-line placeholders, never executable expressions.</summary>
public static class ReviewCommentTemplate
{
    public const int MaxLength = 16_000;
    private static readonly string[] Placeholders = ["{{status}}", "{{summary}}", "{{findings}}", "{{specifications}}", "{{scope}}"];
    public static string Default { get; } = LoadDefault();

    public static void Validate(string? template)
    {
        if (template is null) return;
        if (string.IsNullOrWhiteSpace(template) || template.Length > MaxLength ||
            template.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            throw Invalid();
        var remaining = new HashSet<string>(Placeholders, StringComparer.Ordinal);
        foreach (var line in Lines(template))
        {
            var value = line.Trim();
            if (Placeholders.Contains(value, StringComparer.Ordinal))
            {
                if (!remaining.Remove(value)) throw Invalid();
            }
            else if (line.Contains("{{", StringComparison.Ordinal) || line.Contains("}}", StringComparison.Ordinal))
                throw Invalid();
        }
        if (remaining.Count > 0) throw Invalid();
    }

    public static string Render(HarnessReport report, string? template = null)
    {
        template ??= Default;
        Validate(template);
        var sections = ReportWriter.MarkdownSections(report);
        // Substitute only the original template lines. Review text is never parsed as a template.
        return string.Join('\n', Lines(template).Select(line => sections.GetValueOrDefault(line.Trim()) ?? line));
    }

    private static string[] Lines(string template) => template.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    private static string LoadDefault()
    {
        using var stream = typeof(ReviewCommentTemplate).Assembly.GetManifestResourceStream("BusinessHarness.ReviewCommentTemplate.md")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static HarnessException Invalid() => new(ErrorCode.Configuration, "Invalid review comment template.",
        "Use at most 16,000 characters and include {{status}}, {{summary}}, {{findings}}, {{specifications}} and {{scope}} exactly once each on separate lines. See docs/configuration.md.");
}

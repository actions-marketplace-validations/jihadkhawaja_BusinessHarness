using System.Text.RegularExpressions;

namespace BusinessHarness.Infrastructure;

public sealed partial class SecretRedactor(IEnumerable<string>? secrets = null)
{
    private readonly string[] knownSecrets = (secrets ?? []).Where(s => s.Length >= 8)
        .Distinct(StringComparer.Ordinal).OrderByDescending(s => s.Length).ToArray();

    [GeneratedRegex(@"(?i)(?:sk-(?:proj-|ant-)?[a-z0-9_-]{16,}|gh[pousr]_[a-z0-9]{20,}|github_pat_[a-z0-9_]{20,}|AKIA[0-9A-Z]{16}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----)", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex TokenPattern();

    [GeneratedRegex(@"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----[\s\S]*?(?:-----END (?:RSA |EC |OPENSSH )?PRIVATE KEY-----|\z)", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex PrivateKeyPattern();

    [GeneratedRegex("(?im)([\"']?(?:api[_-]?key|password|passwd|secret|access[_-]?token|client[_-]?secret)[\"']?\\s*[:=]\\s*[\"']?)([^\\s\"'`,;}{]{8,})", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex AssignmentPattern();

    public string Redact(string text)
    {
        foreach (var secret in knownSecrets) text = text.Replace(secret, "[REDACTED]", StringComparison.Ordinal);
        text = PrivateKeyPattern().Replace(text, match => string.Join('\n', match.Value.Split('\n').Select(_ => "[REDACTED PRIVATE KEY]")));
        text = TokenPattern().Replace(text, "[REDACTED]");
        return AssignmentPattern().Replace(text, "$1[REDACTED]");
    }

    public bool ContainsSecret(string text) => Redact(text) != text;
}

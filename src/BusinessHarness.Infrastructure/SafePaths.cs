using BusinessHarness.Core;

namespace BusinessHarness.Infrastructure;

public static class SafePaths
{
    public static bool IsRepositoryPath(string path) => path.Length is > 0 and <= 1024 &&
        !path.StartsWith('/') && !path.Contains('\\') && !path.Contains(':') &&
        !path.Any(char.IsControl) && !path.Split('/').Any(p => p is "" or "." or "..");

    public static void RejectLinks(string path)
    {
        var current = Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(current))
        {
            var info = new FileInfo(current);
            if (info.LinkTarget is not null || (info.Exists && info.Attributes.HasFlag(FileAttributes.ReparsePoint)))
                throw new HarnessException(ErrorCode.FileSystem, "A trusted input or output path contains a link.",
                    "Use a real directory outside the reviewed checkout; symlinks and reparse points are rejected.");
            current = Path.GetDirectoryName(current);
        }
    }

    public static bool IsExcluded(string path)
    {
        var segments = path.Split('/');
        var name = segments[^1];
        var extension = Path.GetExtension(name);
        return segments.Any(s => s.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("node_modules", StringComparison.OrdinalIgnoreCase) || s is "bin" or "obj") ||
            name.StartsWith(".env", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("id_rsa", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("id_ed25519", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("credentials", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("secrets", StringComparison.OrdinalIgnoreCase) ||
            new[] { ".pem", ".key", ".pfx", ".p12", ".jks", ".snk" }.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}

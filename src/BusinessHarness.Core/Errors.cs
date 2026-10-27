namespace BusinessHarness.Core;

public enum ErrorCode
{
    Configuration = 2,
    Git = 3,
    Authentication = 4,
    Network = 5,
    ProviderResponse = 6,
    LimitExceeded = 7,
    FileSystem = 8,
    Cancelled = 9,
    Unexpected = 10
}

/// <summary>Only safe, actionable messages belong here; never attach raw provider bodies or secrets.</summary>
public sealed class HarnessException(ErrorCode code, string message, string hint) : Exception(message)
{
    public ErrorCode Code { get; } = code;
    public string Hint { get; } = hint;
    public string Id => $"BH{1000 + (int)Code}";
}

public sealed record FailureReport(string SchemaVersion, string ErrorCode, string Category,
    string Stage, string Message, string Hint, int ExitCode, string CorrelationId);

using BusinessHarness.Core;

namespace BusinessHarness.Application;

public interface IChangeSource
{
    Task<ChangeSet> ReadChangesAsync(string baseRef, string headRef, CancellationToken cancellationToken);
}

public interface IReviewProvider
{
    Task<ReviewResult> ReviewAsync(ChangeSet changes, IReadOnlyList<ReferenceDocument> issues, CancellationToken cancellationToken);
}

public interface IIssueSource
{
    Task<ReferenceDocument> GetIssueAsync(string key, CancellationToken cancellationToken);
}

public interface IReportWriter
{
    Task WriteAsync(HarnessReport report, string outputDirectory, CancellationToken cancellationToken);
}

public interface IReferenceResolver
{
    Task<IReadOnlyList<ReferenceDocument>> ResolveAsync(string text, IReadOnlyList<string> explicitReferences, CancellationToken cancellationToken);
}

public interface IChangeRequestHost
{
    Task<ChangeRequestContext> ReadAsync(CancellationToken cancellationToken);
    Task PublishAsync(ChangeRequestContext reviewed, HarnessReport report, string feedback, CancellationToken cancellationToken);
}

public interface IReviewMemory
{
    Task<IReadOnlyList<ReviewMemoryEntry>> RecallAsync(MemoryQuery query, CancellationToken cancellationToken);
    Task RememberReviewAsync(MemoryQuery query, HarnessReport report, CancellationToken cancellationToken);
    Task<bool> RememberFollowUpAsync(FollowUpEvent followUp, CancellationToken cancellationToken);
}

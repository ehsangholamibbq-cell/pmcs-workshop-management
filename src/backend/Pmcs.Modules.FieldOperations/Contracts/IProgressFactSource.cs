namespace Pmcs.Modules.FieldOperations.Contracts;

public interface IProgressFactSource
{
    Task<IReadOnlyCollection<ProgressFactRecord>> LoadAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken = default);
}

public sealed record ProgressFactRecord(
    Guid FactId,
    Guid ReportId,
    DateOnly ReportDate,
    ProgressFactReviewState ReviewState,
    Guid? MeasurementItemId,
    string? Category,
    decimal? Quantity,
    string? Unit,
    DateTimeOffset ChangedAt);

public enum ProgressFactReviewState
{
    Provisional = 1,
    Approved = 2
}

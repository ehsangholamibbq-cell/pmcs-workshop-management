namespace Pmcs.BuildingBlocks.Application;

public interface IMeasurementItemDirectory
{
    Task<MeasurementItemValidation> ValidateAsync(
        Guid tenantId,
        Guid projectId,
        Guid measurementItemId,
        string? unit,
        CancellationToken cancellationToken = default);
}

public sealed record MeasurementItemValidation(
    bool IsValid,
    string? ErrorCode = null,
    string? Title = null,
    string? Unit = null)
{
    public static MeasurementItemValidation Valid(string title, string unit) =>
        new(true, null, title, unit);

    public static MeasurementItemValidation Invalid(string errorCode) =>
        new(false, errorCode);
}

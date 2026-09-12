namespace Pmcs.Modules.Commercial.Contracts;

public interface ICommercialReferenceDirectory
{
    Task<CommercialReferenceValidation> ValidateAsync(
        Guid tenantId,
        Guid projectId,
        Guid? contractId,
        Guid? commitmentId,
        CancellationToken cancellationToken = default);
}

public sealed record CommercialReferenceValidation(bool IsValid, string? ErrorCode)
{
    public static readonly CommercialReferenceValidation Valid = new(true, null);
}

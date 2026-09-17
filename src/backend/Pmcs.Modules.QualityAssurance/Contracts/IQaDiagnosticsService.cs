namespace Pmcs.Modules.QualityAssurance.Contracts;

public sealed record QaHealthProbe(
    string Name,
    string Status,
    long DurationMilliseconds,
    string? Description);

public sealed record QaDiagnosticsSnapshot(
    int ContractVersion,
    string Environment,
    string Database,
    string ReleaseCommit,
    string ReleaseVersion,
    string ReleaseBuiltAt,
    string OverallHealth,
    IReadOnlyCollection<QaHealthProbe> Probes,
    IReadOnlyDictionary<string, string> SafetyBoundaries,
    DateTimeOffset CapturedAt);

public interface IQaDiagnosticsService
{
    Task<QaDiagnosticsSnapshot> ReadAsync(CancellationToken cancellationToken = default);
}

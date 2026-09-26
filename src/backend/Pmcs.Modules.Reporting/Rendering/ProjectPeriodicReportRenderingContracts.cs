using System.Text.Json;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Rendering;

internal static class ProjectPeriodicReportRenderSnapshot
{
    public static ProjectPeriodicReportSemanticSnapshot Parse(string payloadJson)
    {
        try
        {
            var snapshot = JsonSerializer.Deserialize<ProjectPeriodicReportSemanticSnapshot>(
                payloadJson,
                CanonicalJson.SerializerOptions)
                ?? throw Invalid("Project-periodic snapshot payload is empty.");
            ProjectPeriodicReportRenderingContract.ValidateSnapshot(snapshot);
            return snapshot;
        }
        catch (ReportRenderingException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw Invalid("Project-periodic snapshot payload cannot be rendered.", exception);
        }
        catch (NotSupportedException exception)
        {
            throw Invalid("Project-periodic snapshot payload uses an unsupported value.", exception);
        }
    }

    private static ReportRenderingException Invalid(string message, Exception? innerException = null) => new(
        "reporting.periodic.snapshot.payload_invalid",
        transient: false,
        message,
        innerException);
}

internal sealed record ProjectPeriodicReportRenderRequest(
    Guid RunId,
    Guid OutputId,
    Guid SnapshotId,
    Guid TemplateVersionId,
    string DefinitionCode,
    string DefinitionVersion,
    string TemplateVersion,
    string TemplateContentDigest,
    string RendererContractVersion,
    string LayoutContractVersion,
    ReportFormat Format,
    string FileName,
    string VerificationCode,
    string ManifestSha256,
    string SnapshotSha256,
    string SourceManifestSha256,
    DateTimeOffset SourceCutoffUtc,
    ProjectPeriodicReportSemanticSnapshot Snapshot);

internal interface IProjectPeriodicReportRenderer
{
    ReportFormat Format { get; }

    RenderedReportArtifact Render(ProjectPeriodicReportRenderRequest request);
}

internal sealed record ProjectPeriodicReportFactRow(
    DailyReportReportingClassification Classification,
    DailyReportReportingVersion Version,
    DailyReportReportingFact Fact);

internal sealed class ProjectPeriodicReportRenderModel
{
    private ProjectPeriodicReportRenderModel(ProjectPeriodicReportRenderRequest request)
    {
        Request = request;
        Snapshot = request.Snapshot;
        ReasonCodes = Snapshot.ReasonCodes.OrderBy(item => item).ToArray();
        OfficialReports = Snapshot.OfficialReports
            .OrderBy(item => item.Version.ReportDate)
            .ThenBy(item => item.Version.RootReportId)
            .ThenBy(item => item.Version.ReportId)
            .ToArray();
        Facts = OfficialReports
            .SelectMany(report => report.Version.Facts
                .OrderBy(fact => fact.CreatedAt)
                .ThenBy(fact => fact.FactId)
                .Select(fact => new ProjectPeriodicReportFactRow(
                    report.Classification,
                    report.Version,
                    fact)))
            .ToArray();
        FactCounts = Snapshot.FactCounts
            .OrderBy(item => item.Kind)
            .ToArray();
        QuantityTotals = Snapshot.QuantityTotals
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.UnitState)
            .ThenBy(item => item.SourceUnit, StringComparer.Ordinal)
            .ToArray();
        ResourceObservationTotals = Snapshot.ResourceObservationTotals
            .OrderBy(item => item.Kind)
            .ToArray();
        HighImpactFacts = Snapshot.HighImpactFacts
            .OrderBy(item => item.ReportDate)
            .ThenBy(item => item.RootReportId)
            .ThenBy(item => item.ReportId)
            .ThenBy(item => item.Fact.Kind)
            .ThenBy(item => item.Fact.FactId)
            .ToArray();
        CoverageSlots = Snapshot.Coverage.Slots?
            .OrderBy(item => item.SlotDate)
            .ThenBy(item => item.StartLocalDate)
            .ToArray() ?? [];
        ExpectedDates = Snapshot.Coverage.ExpectedDates?.OrderBy(item => item).ToArray() ?? [];
        OfficialReportDates = Snapshot.Coverage.OfficialReportDates.OrderBy(item => item).ToArray();
        MissingExpectedDates = Snapshot.Coverage.MissingExpectedDates?.OrderBy(item => item).ToArray() ?? [];
    }

    public ProjectPeriodicReportRenderRequest Request { get; }

    public ProjectPeriodicReportSemanticSnapshot Snapshot { get; }

    public IReadOnlyList<ProjectPeriodicReportReasonCode> ReasonCodes { get; }

    public IReadOnlyList<ProjectPeriodicOfficialReport> OfficialReports { get; }

    public IReadOnlyList<ProjectPeriodicReportFactRow> Facts { get; }

    public IReadOnlyList<ProjectPeriodicFactCount> FactCounts { get; }

    public IReadOnlyList<ProjectPeriodicQuantityTotal> QuantityTotals { get; }

    public IReadOnlyList<ProjectPeriodicResourceObservationTotal> ResourceObservationTotals { get; }

    public IReadOnlyList<ProjectPeriodicHighImpactFact> HighImpactFacts { get; }

    public IReadOnlyList<ProjectPeriodicReportCoverageSlot> CoverageSlots { get; }

    public IReadOnlyList<DateOnly> ExpectedDates { get; }

    public IReadOnlyList<DateOnly> OfficialReportDates { get; }

    public IReadOnlyList<DateOnly> MissingExpectedDates { get; }

    public static ProjectPeriodicReportRenderModel Create(ProjectPeriodicReportRenderRequest request)
    {
        ProjectPeriodicReportRenderingContract.ValidateRequest(request);
        return new ProjectPeriodicReportRenderModel(request);
    }
}

internal static class ProjectPeriodicReportRenderingContract
{
    public static void ValidateRequest(ProjectPeriodicReportRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSnapshot(request.Snapshot);
        var extension = request.Format switch
        {
            ReportFormat.Pdf => ".pdf",
            ReportFormat.Xlsx => ".xlsx",
            _ => throw InvalidRequest("The project-periodic output format is unsupported.")
        };
        if (request.RunId == Guid.Empty || request.OutputId == Guid.Empty ||
            request.SnapshotId == Guid.Empty || request.TemplateVersionId == Guid.Empty ||
            !string.Equals(request.DefinitionCode, ProjectPeriodicReportRuntimeContract.DefinitionCode, StringComparison.Ordinal) ||
            !string.Equals(request.DefinitionVersion, ProjectPeriodicReportRuntimeContract.DefinitionVersion, StringComparison.Ordinal) ||
            !string.Equals(request.TemplateVersion, ProjectPeriodicReportRuntimeContract.TemplateVersion, StringComparison.Ordinal) ||
            !string.Equals(request.RendererContractVersion, ProjectPeriodicReportRuntimeContract.RendererContractVersion, StringComparison.Ordinal) ||
            !string.Equals(request.LayoutContractVersion, ProjectPeriodicReportRuntimeContract.LayoutContractVersion, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(request.FileName) ||
            !request.FileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(request.VerificationCode) ||
            !IsSha256(request.TemplateContentDigest) ||
            !IsSha256(request.ManifestSha256) ||
            !IsSha256(request.SnapshotSha256) ||
            !IsSha256(request.SourceManifestSha256))
        {
            throw InvalidRequest("The project-periodic render request violates its pinned contract.");
        }

        var canonicalSnapshot = CanonicalJson.Serialize(request.Snapshot);
        if (!string.Equals(
                CanonicalJson.Sha256(canonicalSnapshot),
                request.SnapshotSha256,
                StringComparison.Ordinal) ||
            !string.Equals(
                request.SourceManifestSha256,
                request.Snapshot.SourceManifestSha256,
                StringComparison.Ordinal) ||
            request.SourceCutoffUtc.ToUniversalTime() != request.Snapshot.Period.SourceCutoffUtc.ToUniversalTime())
        {
            throw InvalidRequest("The render request does not match its immutable snapshot identity.");
        }
    }

    public static void ValidateSnapshot(ProjectPeriodicReportSemanticSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!string.Equals(snapshot.SchemaVersion, ProjectPeriodicReportRuntimeContract.SnapshotSchemaVersion, StringComparison.Ordinal) ||
            !string.Equals(snapshot.DefinitionCode, ProjectPeriodicReportRuntimeContract.DefinitionCode, StringComparison.Ordinal) ||
            !string.Equals(snapshot.DefinitionVersion, ProjectPeriodicReportRuntimeContract.DefinitionVersion, StringComparison.Ordinal) ||
            !Enum.IsDefined(snapshot.DataStatus) || snapshot.DataStatus == ReportDataStatus.Pending ||
            snapshot.Project is null || snapshot.Period is null || snapshot.Coverage is null ||
            snapshot.ReasonCodes is null || snapshot.OfficialReports is null || snapshot.FactCounts is null ||
            snapshot.QuantityTotals is null || snapshot.ResourceObservationTotals is null ||
            snapshot.HighImpactFacts is null || !IsSha256(snapshot.SourceManifestSha256))
        {
            throw InvalidSnapshot("Project-periodic snapshot identity or required collections are invalid.");
        }

        if (snapshot.Project.Id == Guid.Empty || snapshot.Project.TenantId == Guid.Empty ||
            string.IsNullOrWhiteSpace(snapshot.Project.Code) || string.IsNullOrWhiteSpace(snapshot.Project.Name) ||
            string.IsNullOrWhiteSpace(snapshot.Project.TimeZone) || snapshot.Project.Revision <= 0 ||
            snapshot.Project.ConfigurationVersion <= 0 ||
            !Enum.IsDefined(snapshot.Project.ReportingFrequency) ||
            !Enum.IsDefined(snapshot.Project.DailyReportWorkflow) ||
            !Enum.IsDefined(snapshot.Project.CalendarState))
        {
            throw InvalidSnapshot("Project-periodic project identity is invalid.");
        }

        if (!Enum.IsDefined(snapshot.Period.Kind) ||
            snapshot.Period.StartLocalDate >= snapshot.Period.EndLocalDateExclusive ||
            snapshot.Period.StartUtc >= snapshot.Period.EndUtcExclusive ||
            snapshot.Period.SourceCutoffUtc < snapshot.Period.StartUtc)
        {
            throw InvalidSnapshot("Project-periodic period identity is invalid.");
        }

        if (snapshot.ReasonCodes.Any(reason => !Enum.IsDefined(reason)) ||
            snapshot.ReasonCodes.Distinct().Count() != snapshot.ReasonCodes.Count)
        {
            throw InvalidSnapshot("Project-periodic reason codes are invalid.");
        }

        ValidateCoverage(snapshot.Coverage);
        ValidateReports(snapshot.OfficialReports, snapshot.Period);
        ValidateAggregates(snapshot);

        if (snapshot.DataStatus == ReportDataStatus.Available && snapshot.ReasonCodes.Count > 0 ||
            snapshot.DataStatus == ReportDataStatus.NoData && snapshot.OfficialReports.Count > 0)
        {
            throw InvalidSnapshot("Project-periodic data status is inconsistent with its semantic content.");
        }
    }

    private static void ValidateCoverage(ProjectPeriodicReportCoverage coverage)
    {
        if (coverage.OfficialReportDates is null ||
            coverage.ExpectedSlotCount < 0 || coverage.CoveredSlotCount < 0 ||
            coverage.ExpectedSlotCount.HasValue != coverage.CoveredSlotCount.HasValue ||
            coverage.ExpectedSlotCount.HasValue != (coverage.ExpectedDates is not null) ||
            coverage.ExpectedSlotCount.HasValue != (coverage.MissingExpectedDates is not null) ||
            coverage.ExpectedSlotCount.HasValue != (coverage.Slots is not null) ||
            coverage.ExpectedSlotCount.HasValue &&
            (coverage.CoveredSlotCount > coverage.ExpectedSlotCount ||
                coverage.ExpectedDates!.Count != coverage.ExpectedSlotCount ||
                coverage.Slots!.Count != coverage.ExpectedSlotCount ||
                coverage.Slots.Count(slot => slot.Covered) != coverage.CoveredSlotCount))
        {
            throw InvalidSnapshot("Project-periodic coverage is invalid.");
        }

        if (coverage.Slots is not null && coverage.Slots.Any(slot =>
                slot is null || slot.StartLocalDate >= slot.EndLocalDateExclusive ||
                slot.OfficialReportDates is null))
        {
            throw InvalidSnapshot("Project-periodic coverage slots are invalid.");
        }
    }

    private static void ValidateReports(
        IReadOnlyCollection<ProjectPeriodicOfficialReport> reports,
        ProjectPeriodicReportPeriodIdentity period)
    {
        var reportIds = new HashSet<Guid>();
        var rootIds = new HashSet<Guid>();
        var factIds = new HashSet<Guid>();
        foreach (var report in reports)
        {
            if (report is null || !Enum.IsDefined(report.Classification) || report.Version is null ||
                report.Version.ReportId == Guid.Empty || report.Version.RootReportId == Guid.Empty ||
                !reportIds.Add(report.Version.ReportId) || !rootIds.Add(report.Version.RootReportId) ||
                report.Version.VersionNumber <= 0 || report.Version.Revision <= 0 ||
                report.Version.ReportDate < period.StartLocalDate ||
                report.Version.ReportDate >= period.EndLocalDateExclusive ||
                !Enum.IsDefined(report.Version.State) || report.Version.Facts is null)
            {
                throw InvalidSnapshot("An official project-periodic report is invalid.");
            }

            foreach (var fact in report.Version.Facts)
            {
                if (fact is null || fact.FactId == Guid.Empty || !factIds.Add(fact.FactId) ||
                    !Enum.IsDefined(fact.Kind) ||
                    fact.ImpactLevel.HasValue && !Enum.IsDefined(fact.ImpactLevel.Value) ||
                    fact.Quantity < 0 || fact.ResourceCount <= 0 || fact.Hours < 0)
                {
                    throw InvalidSnapshot("An official project-periodic fact is invalid.");
                }
            }
        }
    }

    private static void ValidateAggregates(ProjectPeriodicReportSemanticSnapshot snapshot)
    {
        if (snapshot.FactCounts.Any(item => item is null || !Enum.IsDefined(item.Kind) || item.Count <= 0) ||
            snapshot.QuantityTotals.Any(item =>
                item is null || !Enum.IsDefined(item.Kind) || !Enum.IsDefined(item.UnitState) || item.Quantity < 0 ||
                item.UnitState == ProjectPeriodicReportUnitState.UnitMissing && item.SourceUnit is not null ||
                item.UnitState == ProjectPeriodicReportUnitState.SourceUnit && string.IsNullOrWhiteSpace(item.SourceUnit)) ||
            snapshot.ResourceObservationTotals.Any(item =>
                item is null || !Enum.IsDefined(item.Kind) || item.ResourceCount <= 0 || item.Hours < 0 ||
                !item.ResourceCount.HasValue && !item.Hours.HasValue) ||
            snapshot.HighImpactFacts.Any(item =>
                item is null || item.RootReportId == Guid.Empty || item.ReportId == Guid.Empty || item.Fact is null ||
                item.Fact.Kind is not (DailyReportReportingFactKind.Issue or DailyReportReportingFactKind.Stoppage) ||
                item.Fact.ImpactLevel is not (DailyReportReportingImpactLevel.High or DailyReportReportingImpactLevel.Critical)))
        {
            throw InvalidSnapshot("Project-periodic aggregates are invalid.");
        }

        var facts = snapshot.OfficialReports.SelectMany(item => item.Version.Facts).ToArray();
        var expectedCounts = facts
            .GroupBy(item => item.Kind)
            .ToDictionary(group => group.Key, group => group.Count());
        if (snapshot.FactCounts.Count != expectedCounts.Count ||
            snapshot.FactCounts.Any(item => !expectedCounts.TryGetValue(item.Kind, out var count) || count != item.Count))
        {
            throw InvalidSnapshot("Project-periodic fact counts do not match the official facts.");
        }
    }

    private static bool IsSha256(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static ReportRenderingException InvalidSnapshot(string message) => new(
        "reporting.periodic.snapshot.payload_invalid",
        transient: false,
        message);

    private static ReportRenderingException InvalidRequest(string message) => new(
        "reporting.periodic.render_request.invalid",
        transient: false,
        message);
}

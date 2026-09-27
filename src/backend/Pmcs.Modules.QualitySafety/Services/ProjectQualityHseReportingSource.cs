using System.Data;
using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.QualitySafety.Contracts;
using Pmcs.Modules.QualitySafety.Domain;
using Pmcs.Modules.QualitySafety.Persistence;

namespace Pmcs.Modules.QualitySafety.Services;

internal sealed class ProjectQualityHseReportingSource(
    QualitySafetyDbContext db, IProjectDirectory projects) : IProjectQualityHseReportingSource
{
    public async Task<ProjectQualityHseReportingResult> LoadAsync(
        Guid tenantId, Guid projectId, DateOnly cutoffLocalDate,
        DateTimeOffset sourceCutoffUtc, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(ProjectQualityHseReportingContract.MaximumReadSeconds));
        var token = timeout.Token;
        var cutoff = sourceCutoffUtc.ToUniversalTime();
        var project = await projects.FindProfileAsync(tenantId, projectId, token)
            ?? throw Invalid("project.not_found", "Project scope is missing.");
        if (cutoff == default || cutoff > DateTimeOffset.UtcNow ||
            project.Status != ProjectStatus.Active ||
            !Enum.IsDefined(project.Quality) || !Enum.IsDefined(project.Hse) ||
            project.ConfigurationVersion <= 0 || project.ConfigurationChangedAt is null ||
            project.ConfigurationChangedAt > cutoff)
            throw Invalid("configuration.history", "Project configuration is not provable at the cutoff.");
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(project.TimeZone); }
        catch (TimeZoneNotFoundException) { throw Invalid("time_zone.invalid", "Unknown project time zone."); }
        catch (InvalidTimeZoneException) { throw Invalid("time_zone.invalid", "Invalid project time zone."); }
        if (DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(cutoff, zone).DateTime) != cutoffLocalDate)
            throw Invalid("cutoff.invalid", "Local project cutoff does not match the pinned instant.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
        var configuration = await db.Configurations.AsNoTracking().SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.ProjectId == projectId, token);
        if (configuration?.ChangedAt > cutoff)
            throw Invalid("configuration.history", "Current Quality/HSE configuration postdates the cutoff.");
        if (configuration is not null &&
            (!Enum.IsDefined(configuration.QualityMode) || !Enum.IsDefined(configuration.HseMode)))
            throw Invalid("configuration.mode", "Owner configuration mode is unknown.");

        // These are full bounded owner reads under one PostgreSQL repeatable-read view.
        // The capped /state endpoint is never a reporting source.
        var intakes = await Read(db.Intakes.Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.CreatedAt <= cutoff), token);
        var inspections = await Read(db.Inspections.Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.CreatedAt <= cutoff), token);
        var ncrs = await Read(db.NonConformances.Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.CreatedAt <= cutoff), token);
        var defects = await Read(db.Defects.Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.CreatedAt <= cutoff), token);
        var incidents = await Read(db.Incidents.Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.CreatedAt <= cutoff), token);
        var actions = await Read(db.CorrectiveActions.Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.CreatedAt <= cutoff), token);
        var permits = await Read(db.Permits.Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.CreatedAt <= cutoff), token);
        var talks = await Read(db.ToolboxTalks.Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.CreatedAt <= cutoff), token);
        var plans = await Read(db.InspectionTestPlans.Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.CreatedAt <= cutoff), token);
        var checklists = await Read(db.ChecklistTemplates.Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.CreatedAt <= cutoff), token);
        var tests = await Read(db.TestRecords.Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.CreatedAt <= cutoff), token);
        var competencies = await Read(db.CompetencyRecords.Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.CreatedAt <= cutoff), token);
        var exposure = await Read(db.ExposureHours.Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.ApprovedAt <= cutoff), token);
        var matrices = await Read(db.RiskMatrices.Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.CreatedAt <= cutoff), token);

        // References are checked against this same complete owner view. A missing target
        // could belong to another project or a later instant, so it cannot be certified.
        var intakeIds = intakes.Select(x => x.Id).ToHashSet();
        var inspectionIds = inspections.Select(x => x.Id).ToHashSet();
        var ncrIds = ncrs.Select(x => x.Id).ToHashSet();
        var defectIds = defects.Select(x => x.Id).ToHashSet();
        var incidentIds = incidents.Select(x => x.Id).ToHashSet();
        var planIds = plans.Select(x => x.Id).ToHashSet();
        var matrixIds = matrices.Select(x => x.Id).ToHashSet();
        var allIds = intakes.Select(x => x.Id).Concat(inspections.Select(x => x.Id))
            .Concat(ncrs.Select(x => x.Id)).Concat(defects.Select(x => x.Id))
            .Concat(incidents.Select(x => x.Id)).Concat(actions.Select(x => x.Id))
            .Concat(permits.Select(x => x.Id)).Concat(talks.Select(x => x.Id))
            .Concat(plans.Select(x => x.Id)).Concat(checklists.Select(x => x.Id))
            .Concat(tests.Select(x => x.Id)).Concat(competencies.Select(x => x.Id))
            .Concat(exposure.Select(x => x.Id)).Concat(matrices.Select(x => x.Id)).ToArray();
        if (allIds.Any(x => x == Guid.Empty) || allIds.Distinct().Count() != allIds.Length)
            throw Invalid("identity.duplicate", "Owner source identities are missing or duplicated.");
        if (inspections.Any(x => !Linked(x.SourceIntakeId, intakeIds) ||
                !Linked(x.InspectionTestPlanVersionId, planIds)) ||
            ncrs.Any(x => !Linked(x.SourceIntakeId, intakeIds) || !Linked(x.InspectionId, inspectionIds)) ||
            defects.Any(x => !Linked(x.SourceIntakeId, intakeIds)) ||
            incidents.Any(x => !Linked(x.SourceIntakeId, intakeIds) || !matrixIds.Contains(x.MatrixVersionId)) ||
            tests.Any(x => !Linked(x.InspectionId, inspectionIds)) ||
            actions.Any(x => !ActionLinked(x, intakeIds, inspectionIds, ncrIds, defectIds, incidentIds)) ||
            intakes.Any(x => x.Status == IntakeStatus.Converted &&
                !ConversionLinked(x, intakeIds, ncrIds, defectIds, incidentIds)))
            throw Invalid("linkage.invalid", "An owner reference does not resolve inside the pinned project scope.");

        foreach (var classification in intakes.Select(x => x.Classification)
                     .Concat(incidents.Select(x => x.Classification))
                     .Concat(competencies.Select(x => x.Classification)))
        {
            if (!Enum.IsDefined(classification) || classification is
                DataClassification.PersonalMedical or DataClassification.LegalInvestigation)
                throw Invalid("classification.denied", "Source classification is not approved for certified publication.");
        }
        var classificationResult = intakes.Any(x => x.Classification == DataClassification.RestrictedQuality) ||
            incidents.Any(x => x.Classification == DataClassification.RestrictedQuality) ||
            competencies.Any(x => x.Classification == DataClassification.RestrictedQuality)
                ? QualityHseReportingClassification.Restricted
                : QualityHseReportingClassification.Confidential;
        var qualityClassification = intakes.Any(x => x.IsQuality && x.Classification == DataClassification.RestrictedQuality)
            ? QualityHseReportingClassification.Restricted : QualityHseReportingClassification.Confidential;
        var hseClassification = intakes.Any(x => x.IsHse && x.Classification == DataClassification.RestrictedQuality) ||
            incidents.Any(x => x.Classification == DataClassification.RestrictedQuality) ||
            competencies.Any(x => x.Classification == DataClassification.RestrictedQuality)
                ? QualityHseReportingClassification.Restricted : QualityHseReportingClassification.Confidential;

        // A recorded event cannot certify an occurrence or coverage period still in the future.
        var futureTests = tests.Any(x => x.TestedAt > cutoff);
        var futureTalks = talks.Any(x => x.HeldAt > cutoff);
        var futureExposure = exposure.Any(x => x.PeriodEnd > cutoffLocalDate || x.Hours <= 0);

        var qualityFacts = tests.Where(x => x.TestedAt <= cutoff).Select(x => new QualityHseReportingFact(x.Id, x.Number,
                QualityHseFactKind.QualityTest, x.Result.ToString(), x.TestedAt))
            .Concat(inspections.Where(x => x.Status == InspectionStatus.Requested && x.Revision == 1)
                .Select(x => new QualityHseReportingFact(x.Id, x.Number,
                    QualityHseFactKind.InspectionRequested, "Requested", x.CreatedAt)))
            .Concat(inspections.Where(x => x.Status == InspectionStatus.ResultRecorded &&
                    x.InspectedAt.HasValue && x.InspectedAt <= cutoff && x.Result.HasValue)
                .Select(x => new QualityHseReportingFact(x.Id, x.Number,
                    QualityHseFactKind.InspectionResult, x.Result!.Value.ToString(), x.InspectedAt!.Value)))
            .OrderBy(x => x.OfficialAtUtc).ThenBy(x => x.Id).ThenBy(x => x.Kind).ToArray();
        var hseFacts = talks.Where(x => x.HeldAt <= cutoff).Select(x => new QualityHseReportingFact(x.Id, x.Number,
                QualityHseFactKind.ToolboxTalk, "Recorded", x.HeldAt))
            .Concat(exposure.Where(x => x.PeriodEnd <= cutoffLocalDate && x.Hours > 0)
                .Select(x => new QualityHseReportingFact(x.Id, "EXH-" + x.Id.ToString("N")[..8],
                QualityHseFactKind.ExposureHours, "Approved", x.ApprovedAt, x.Hours)))
            .OrderBy(x => x.OfficialAtUtc).ThenBy(x => x.Id).ThenBy(x => x.Kind).ToArray();
        if (qualityFacts.Length + hseFacts.Length > ProjectQualityHseReportingContract.MaximumFacts)
            throw Invalid("facts.overflow", "Certified fact budget was exceeded.");

        var qualityHasRecords = intakes.Any(x => x.IsQuality) || inspections.Length > 0 ||
            ncrs.Length > 0 || defects.Length > 0 || actions.Any(x => x.SourceArea == ControlArea.Quality) ||
            tests.Length > 0 || plans.Length > 0 || checklists.Length > 0;
        var hseHasRecords = intakes.Any(x => x.IsHse) || incidents.Length > 0 || permits.Length > 0 ||
            actions.Any(x => x.SourceArea == ControlArea.Hse) || talks.Length > 0 ||
            competencies.Length > 0 || exposure.Length > 0;
        var configurationUnproven = configuration is { Revision: > 1 };
        var qualityEnabled = project.Quality == ProjectFeatureState.Active &&
            configuration is not null && configuration.QualityMode != QualityOperatingMode.Disabled;
        var hseEnabled = project.Hse == ProjectFeatureState.Active &&
            configuration is not null && configuration.HseMode != HseOperatingMode.Disabled;
        var qualityMatrixValid = configuration?.QualityMatrixVersionId is { } qMatrix &&
            matrices.Any(x => x.Id == qMatrix && x.Area == ControlArea.Quality && x.EffectiveFrom <= cutoff);
        var hseMatrixValid = configuration?.HseMatrixVersionId is { } hMatrix &&
            matrices.Any(x => x.Id == hMatrix && x.Area == ControlArea.Hse && x.EffectiveFrom <= cutoff);
        var qualityIncomplete = configurationUnproven || futureTests ||
            qualityEnabled && (configuration is null || !configuration.QualityReady || !qualityMatrixValid) ||
            intakes.Any(x => x.IsQuality && x.Status != IntakeStatus.Captured) ||
            inspections.Any(x => x.Status != InspectionStatus.Requested &&
                (x.Status != InspectionStatus.ResultRecorded || !x.InspectedAt.HasValue || x.InspectedAt > cutoff)) ||
            ncrs.Length > 0 || defects.Length > 0 ||
            actions.Any(x => x.SourceArea == ControlArea.Quality);
        var hseIncomplete = configurationUnproven || futureTalks || futureExposure ||
            hseEnabled && (configuration is null || !configuration.HseReady || !hseMatrixValid) ||
            intakes.Any(x => x.IsHse && x.Status != IntakeStatus.Captured) ||
            incidents.Length > 0 || permits.Length > 0 || competencies.Length > 0 ||
            actions.Any(x => x.SourceArea == ControlArea.Hse);
        var quality = Section(qualityEnabled, qualityHasRecords, qualityIncomplete, qualityFacts,
            QualityHseReportingReason.QualityNotConfigured, QualityHseReportingReason.NoOfficialQualityFact,
            configurationUnproven, qualityClassification);
        var hse = Section(hseEnabled, hseHasRecords, hseIncomplete, hseFacts,
            QualityHseReportingReason.HseNotConfigured, QualityHseReportingReason.NoOfficialHseFact,
            configurationUnproven, hseClassification);
        if (exposure.Length > 0 && hse.Status == QualityHseReportingStatus.Available)
            hse = hse with { Reasons = hse.Reasons.Append(QualityHseReportingReason.ExposureBasisIncomplete).ToArray() };

        var registers = new[]
        {
            Register("intakes", intakes), Register("inspections", inspections),
            Register("nonconformances", ncrs), Register("defects", defects),
            Register("incidents", incidents), Register("corrective_actions", actions),
            Register("permits", permits), Register("toolbox_talks", talks),
            Register("inspection_test_plan_versions", plans), Register("checklist_template_versions", checklists),
            Register("test_records", tests), Register("competency_records", competencies),
            Register("exposure_hours", exposure), Register("risk_matrix_versions", matrices)
        };
        var manifest = new ProjectQualityHseSourceManifest(
            ProjectQualityHseReportingContract.ManifestVersion, ProjectQualityHseReportingContract.Version,
            ProjectQualityHseReportingContract.PolicyVersion, tenantId, projectId, cutoffLocalDate,
            cutoff, cutoff, project.ConfigurationVersion, project.ConfigurationChangedAt.Value.ToUniversalTime(),
            configuration?.Revision, configuration?.ChangedAt.ToUniversalTime(),
            configuration?.QualityMode.ToString(), configuration?.HseMode.ToString(),
            configuration?.QualityReady ?? false, configuration?.HseReady ?? false,
            configuration?.QualityMatrixVersionId, configuration?.HseMatrixVersionId,
            qualityEnabled, hseEnabled,
            classificationResult, registers);
        var sections = new[] { quality.Status, hse.Status };
        var dataStatus = sections.Contains(QualityHseReportingStatus.InsufficientData)
            ? QualityHseReportingStatus.InsufficientData
            : sections.Contains(QualityHseReportingStatus.Available)
                ? QualityHseReportingStatus.Available
                : sections.All(x => x == QualityHseReportingStatus.NotConfigured)
                    ? QualityHseReportingStatus.NotConfigured : QualityHseReportingStatus.NoData;
        var result = new ProjectQualityHseReportingResult(
            ProjectQualityHseReportingContract.Version, ProjectQualityHseReportingContract.PolicyVersion,
            tenantId, projectId, cutoffLocalDate, cutoff, classificationResult, dataStatus,
            quality.Reasons.Concat(hse.Reasons).Distinct().OrderBy(x => x).ToArray(), quality, hse,
            manifest, QualityHseReportingHash.Compute(manifest), "");
        result = result with { SemanticSha256 = QualityHseReportingHash.Result(result) };
        await transaction.CommitAsync(token);
        var current = await projects.FindProfileAsync(tenantId, projectId, token);
        if (current is null || current.Revision != project.Revision ||
            current.ConfigurationVersion != project.ConfigurationVersion || current.TimeZone != project.TimeZone)
            throw Invalid("project.changed", "Project profile changed during source collection.");
        return result;
    }

    private static async Task<T[]> Read<T>(IQueryable<T> query, CancellationToken token)
        where T : AggregateRoot
    {
        var items = await query.AsNoTracking().OrderBy(x => EF.Property<Guid>(x, "Id"))
            .Take(ProjectQualityHseReportingContract.MaximumPerRegister + 1).ToArrayAsync(token);
        if (items.Length > ProjectQualityHseReportingContract.MaximumPerRegister)
            throw Invalid("source.overflow", "The owner register exceeds its complete read bound.");
        return items;
    }

    private static QualityHseReportingRegister Register<T>(string name, IReadOnlyCollection<T> items)
        where T : AggregateRoot => new(name, items.Count,
            QualityHseReportingHash.Compute(items.Select(x => new
            {
                Id = (Guid)x.GetType().GetProperty("Id")!.GetValue(x)!, x.Revision
            }).ToArray()));

    private static bool Linked(Guid? id, HashSet<Guid> targets) => !id.HasValue || targets.Contains(id.Value);

    private static bool ConversionLinked(QualitySafetyIntake intake, HashSet<Guid> intakes,
        HashSet<Guid> ncrs, HashSet<Guid> defects, HashSet<Guid> incidents) =>
        intake.ConvertedRecordId is { } id && (intake.ConversionType switch
        {
            IntakeConversionType.QualityObservation or IntakeConversionType.HseObservation => intakes.Contains(id) && id == intake.Id,
            IntakeConversionType.Defect => defects.Contains(id),
            IntakeConversionType.NonConformance => ncrs.Contains(id),
            IntakeConversionType.Incident => incidents.Contains(id),
            _ => false
        });

    private static bool ActionLinked(CorrectiveAction action, HashSet<Guid> intakes,
        HashSet<Guid> inspections, HashSet<Guid> ncrs, HashSet<Guid> defects,
        HashSet<Guid> incidents) => action.SourceArea switch
        {
            ControlArea.Quality when action.SourceRecordType.Equals("NCR", StringComparison.OrdinalIgnoreCase) => ncrs.Contains(action.SourceRecordId),
            ControlArea.Quality when action.SourceRecordType.Equals("Defect", StringComparison.OrdinalIgnoreCase) => defects.Contains(action.SourceRecordId),
            ControlArea.Quality when action.SourceRecordType.Equals("Inspection", StringComparison.OrdinalIgnoreCase) => inspections.Contains(action.SourceRecordId),
            ControlArea.Hse when action.SourceRecordType.Equals("Incident", StringComparison.OrdinalIgnoreCase) => incidents.Contains(action.SourceRecordId),
            ControlArea.Hse when action.SourceRecordType.Equals("Intake", StringComparison.OrdinalIgnoreCase) => intakes.Contains(action.SourceRecordId),
            _ => false
        };

    private static QualityHseReportingSection Section(
        bool enabled, bool hasRecords, bool incomplete,
        QualityHseReportingFact[] facts,
        QualityHseReportingReason disabledReason, QualityHseReportingReason noFactReason,
        bool configurationUnproven, QualityHseReportingClassification classification)
    {
        if (!enabled && !hasRecords && !incomplete)
            return new(QualityHseReportingStatus.NotConfigured, null, [], [disabledReason], classification);
        if (!enabled || incomplete)
            return new(QualityHseReportingStatus.InsufficientData, null, [],
                [configurationUnproven ? QualityHseReportingReason.ConfigurationHistoryUnavailable :
                    QualityHseReportingReason.HistoricalTransitionUnavailable], classification);
        return facts.Length == 0
            ? new(QualityHseReportingStatus.NoData, 0, [], [noFactReason], classification)
            : new(QualityHseReportingStatus.Available, facts.Length, facts, [], classification);
    }

    private static DomainRuleException Invalid(string code, string message) =>
        new($"quality_safety.reporting.{code}", message);
}

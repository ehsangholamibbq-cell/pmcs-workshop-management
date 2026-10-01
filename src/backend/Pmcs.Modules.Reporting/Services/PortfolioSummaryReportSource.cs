using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Commercial.Contracts;
using Pmcs.Modules.Finance.Contracts;
using Pmcs.Modules.ProjectIntelligence.Contracts;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Services;

/// <summary>
/// Read-only owner orchestration for the pinned tenant cohort. The source scopes
/// profile reads before enrichment and never filters an unrestricted result afterwards.
/// </summary>
internal sealed class PortfolioSummaryReportSource(
    IProjectPermissionService permissions,
    IProjectDirectory projects,
    IProjectStateReportingSource projectState,
    IProjectFinancialPositionReportingSource finance,
    IProjectCommercialProcurementSupplyReportingSource commercial)
{
    private static readonly string[] FinancialPermissions = [
        "financial-state.read", "finance.records.read", "finance.obligations.read",
        "budget.baselines.read"
    ];
    private static readonly string[] CommercialPermissions = [
        "commercial-state.read", "commercial.parties.read", "contracts.read",
        "procurement.requests.read", "procurement.orders.read", "supply.read"
    ];

    public Task<PortfolioSummarySelection> LoadAsync(
        Guid tenantId, Guid requestedBy, DateTimeOffset sourceCutoffUtc,
        CancellationToken cancellationToken = default) =>
        LoadCoreAsync(tenantId, requestedBy, sourceCutoffUtc, null, cancellationToken);

    public async Task<PortfolioPinnedCohort> PinAsync(
        Guid tenantId, Guid requestedBy, DateTimeOffset sourceCutoffUtc,
        CancellationToken cancellationToken = default)
    {
        var profiles = await SelectProfilesAsync(
            tenantId, requestedBy, null, cancellationToken);
        var pinned = new PortfolioPinnedCohort(
            PortfolioPinnedCohort.Version, tenantId, requestedBy,
            sourceCutoffUtc.ToUniversalTime(), profiles.Select(item => new PortfolioPinnedProject(
                item.Profile.Id, item.FinancialAllowed, item.CommercialAllowed,
                item.PolicyVersion)).ToArray());
        pinned.Validate();
        return pinned;
    }

    public Task<PortfolioSummarySelection> LoadPinnedAsync(
        PortfolioPinnedCohort pinned, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pinned);
        pinned.Validate();
        return LoadCoreAsync(pinned.TenantId, pinned.RequestedBy, pinned.AsOfUtc,
            pinned, cancellationToken);
    }

    public async Task<bool> CanAccessPinnedAsync(
        PortfolioPinnedCohort pinned, Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            pinned.Validate();
            if (actorUserId != pinned.RequestedBy &&
                !await permissions.HasTenantPermissionAsync(pinned.TenantId,
                    actorUserId, "reporting.template.publish", cancellationToken))
                return false;
            _ = await SelectProfilesAsync(pinned.TenantId, actorUserId, pinned, cancellationToken);
            return true;
        }
        catch (DomainRuleException)
        {
            return false;
        }
    }

    private async Task<PortfolioSummarySelection> LoadCoreAsync(
        Guid tenantId, Guid requestedBy, DateTimeOffset sourceCutoffUtc,
        PortfolioPinnedCohort? pinned, CancellationToken cancellationToken)
    {
        var profiles = await SelectProfilesAsync(tenantId, requestedBy, pinned, cancellationToken);
        var cutoff = sourceCutoffUtc.ToUniversalTime();
        var selected = new List<PortfolioProjectSelection>(profiles.Length);
        foreach (var authorized in profiles)
        {
            var profile = authorized.Profile;
            var financialAllowed = authorized.FinancialAllowed;
            var commercialAllowed = authorized.CommercialAllowed;
            var timeZone = FindTimeZone(profile.TimeZone);
            var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(cutoff, timeZone).DateTime);
            var proven = profile.ConfigurationVersion > 0 &&
                profile.ConfigurationChangedAt.HasValue &&
                profile.ConfigurationChangedAt.Value.ToUniversalTime() <= cutoff;

            if (!proven || profile.Status != ProjectStatus.Active)
            {
                var status = !proven ? PortfolioDimensionStatus.InsufficientData
                    : profile.Status == ProjectStatus.OnHold
                        ? PortfolioDimensionStatus.Suspended
                        : PortfolioDimensionStatus.NotConfigured;
                selected.Add(new PortfolioProjectSelection(
                    profile.Id, proven ? profile.Code : null, proven ? profile.Name : null,
                    profile.Status, localDate, profile.TimeZone,
                    proven ? profile.BaseCurrencyCode : null,
                    proven ? profile.ConfigurationVersion : null,
                    proven ? profile.ConfigurationChangedAt?.ToUniversalTime() : null,
                    proven, authorized.PolicyVersion,
                    status, null, null, null, null, null, null, null, null,
                    proven ? "project.lifecycle" : "project.configuration_history_unavailable",
                    financialAllowed ? UnknownFinancial(status) : HiddenFinancial(),
                    commercialAllowed ? UnknownCommercial(status) : HiddenCommercial(),
                    ReportClassification.Confidential));
                continue;
            }

            var operationalSource = await projectState.LoadAsync(
                tenantId, profile.Id, localDate, cutoff, cancellationToken);
            var operationalProof = ExecutiveProjectStateReportSnapshotBuilder.Build(
                Guid.NewGuid(), tenantId, profile, cutoff, operationalSource, cutoff, cutoff);
            var operationalStatus = operationalProof.DataStatus switch {
                ReportDataStatus.Available => PortfolioDimensionStatus.Available,
                ReportDataStatus.NoData => PortfolioDimensionStatus.NoData,
                ReportDataStatus.NotConfigured => PortfolioDimensionStatus.NotConfigured,
                _ => PortfolioDimensionStatus.InsufficientData
            };
            var state = operationalStatus == PortfolioDimensionStatus.Available
                ? operationalSource.SelectedSnapshot : null;
            var financial = financialAllowed
                ? await LoadFinancialAsync(tenantId, profile, localDate, cutoff, cancellationToken)
                : HiddenFinancial();
            var commercialDimension = commercialAllowed
                ? await LoadCommercialAsync(tenantId, profile, localDate, cutoff, cancellationToken)
                : HiddenCommercial();
            var classification = (ReportClassification)new[] {
                (int)ReportClassification.Confidential, (int)operationalProof.Classification,
                (int)financial.Classification, (int)commercialDimension.Classification
            }.Max();
            selected.Add(new PortfolioProjectSelection(
                profile.Id, profile.Code, profile.Name, profile.Status, localDate,
                profile.TimeZone, profile.BaseCurrencyCode, profile.ConfigurationVersion,
                profile.ConfigurationChangedAt?.ToUniversalTime(),
                true, authorized.PolicyVersion, operationalStatus,
                state?.OperationalStatus, state?.CoverageStatus, state?.FreshnessStatus,
                state?.ConfidenceStatus, state?.IsPartial, state?.SnapshotId,
                state?.CalculatedAt.ToUniversalTime(),
                operationalProof.SourceManifestSha256,
                operationalStatus == PortfolioDimensionStatus.Available ? null : "project_state.unavailable",
                financial, commercialDimension, classification));
        }
        return new PortfolioSummarySelection(tenantId, requestedBy, cutoff, selected);
    }

    private async Task<AuthorizedProfile[]> SelectProfilesAsync(
        Guid tenantId, Guid requestedBy, PortfolioPinnedCohort? pinned,
        CancellationToken cancellationToken)
    {
        if (pinned is not null && pinned.TenantId != tenantId)
            throw Invalid("cohort.invalid");
        if (tenantId == Guid.Empty || requestedBy == Guid.Empty ||
            !await permissions.HasTenantPermissionAsync(
                tenantId, requestedBy, "portfolio.read", cancellationToken))
        {
            throw Invalid("permission.denied");
        }

        var scope = await permissions.GetProjectScopeAsync(
            tenantId, requestedBy, "project-state.read", cancellationToken);
        if (scope is null || (pinned is null && !scope.AllProjects &&
            scope.ProjectIds.Count > PortfolioSummaryReportRuntimeContract.MaximumProjects))
        {
            throw Invalid("cohort.limit_exceeded");
        }
        var allowedIds = pinned is null
            ? scope.AllProjects ? null : scope.ProjectIds.Order().ToArray()
            : pinned.Projects.Select(item => item.ProjectId).ToArray();
        if (pinned is not null && !scope.AllProjects &&
            allowedIds!.Any(id => !scope.ProjectIds.Contains(id)))
            throw Invalid("permission.revoked");
        var profiles = allowedIds is { Length: 0 }
            ? Array.Empty<ProjectControlProfile>()
            : (await projects.ListProfilesAsync(tenantId, allowedIds, cancellationToken)).ToArray();
        if (profiles.Length > PortfolioSummaryReportRuntimeContract.MaximumProjects ||
            profiles.Any(item => item.TenantId != tenantId || item.Id == Guid.Empty) ||
            profiles.Select(item => item.Id).Distinct().Count() != profiles.Length ||
            (allowedIds is not null && profiles.Any(item => !allowedIds.Contains(item.Id))) ||
            (pinned is not null && profiles.Length != pinned.Projects.Count))
        {
            throw Invalid("cohort.invalid");
        }
        var selected = new List<AuthorizedProfile>(profiles.Length);
        foreach (var profile in profiles.OrderBy(item => item.Id))
        {
            var preview = await permissions.PreviewProjectPermissionsAsync(
                tenantId, requestedBy, profile.Id,
                operations: ["project-state.read", .. FinancialPermissions, .. CommercialPermissions],
                cancellationToken: cancellationToken);
            if (preview.UserId != requestedBy || preview.ProjectId != profile.Id ||
                string.IsNullOrWhiteSpace(preview.PolicyVersion) ||
                !Allowed(preview, "project-state.read"))
            {
                throw Invalid("permission.revoked");
            }
            var financialAllowed = FinancialPermissions.All(operation => Allowed(preview, operation));
            var commercialAllowed = CommercialPermissions.All(operation => Allowed(preview, operation));
            if (pinned is not null)
            {
                var entry = pinned.Projects.Single(item => item.ProjectId == profile.Id);
                if (entry.FinancialAuthorized && !financialAllowed ||
                    entry.CommercialAuthorized && !commercialAllowed)
                    throw Invalid("permission.revoked");
                financialAllowed &= entry.FinancialAuthorized;
                commercialAllowed &= entry.CommercialAuthorized;
            }
            selected.Add(new AuthorizedProfile(profile, financialAllowed,
                commercialAllowed, preview.PolicyVersion));
        }
        return selected.ToArray();
    }

    private sealed record AuthorizedProfile(ProjectControlProfile Profile,
        bool FinancialAllowed, bool CommercialAllowed, string PolicyVersion);

    private async Task<PortfolioFinancialDimension> LoadFinancialAsync(
        Guid tenantId, ProjectControlProfile profile, DateOnly localDate,
        DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        if (profile.Finance is ProjectFeatureState.NotEnabled)
            return UnknownFinancial(PortfolioDimensionStatus.NotEnabled);
        var result = await finance.LoadAsync(tenantId, profile.Id, localDate, cutoff, cancellationToken);
        var proof = ProjectFinancialPositionReportSnapshotBuilder.Build(
            Guid.NewGuid(), tenantId, profile, cutoff, result, cutoff, cutoff);
        var status = result.DataStatus switch {
            ProjectFinancialPositionDataStatus.Available when
                result.CashStatus == ProjectFinancialPositionSectionStatus.Available =>
                    PortfolioDimensionStatus.Available,
            ProjectFinancialPositionDataStatus.NotConfigured => PortfolioDimensionStatus.NotConfigured,
            ProjectFinancialPositionDataStatus.NoData => PortfolioDimensionStatus.NoData,
            _ => PortfolioDimensionStatus.InsufficientData
        };
        var currency = result.Configuration?.BaseCurrencyCode;
        return new PortfolioFinancialDimension(status, currency,
            status == PortfolioDimensionStatus.Available ? result.Cash.RecognizedSpend : null,
            status == PortfolioDimensionStatus.Available ? result.Cash.ExternalNetCash : null,
            proof.SourceManifestSha256,
            status == PortfolioDimensionStatus.Available ? null : "finance.source_unavailable",
            proof.Classification);
    }

    private async Task<PortfolioCommercialDimension> LoadCommercialAsync(
        Guid tenantId, ProjectControlProfile profile, DateOnly localDate,
        DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        if (profile.Procurement is ProjectFeatureState.NotEnabled)
            return UnknownCommercial(PortfolioDimensionStatus.NotEnabled);
        var result = await commercial.LoadAsync(tenantId, profile.Id, localDate, cutoff, cancellationToken);
        var proof = ProjectCommercialProcurementSupplyReportSnapshotBuilder.Build(
            Guid.NewGuid(), tenantId, profile, cutoff, result, cutoff, cutoff);
        var status = result.DataStatus switch {
            ProjectCommercialReportingDataStatus.Available when
                result.ProcurementStatus == ProjectCommercialReportingSectionStatus.Available =>
                    PortfolioDimensionStatus.Available,
            ProjectCommercialReportingDataStatus.NotConfigured => PortfolioDimensionStatus.NotConfigured,
            ProjectCommercialReportingDataStatus.NoData => PortfolioDimensionStatus.NoData,
            _ => PortfolioDimensionStatus.InsufficientData
        };
        var currency = result.Configuration?.BaseCurrencyCode;
        return new PortfolioCommercialDimension(status, currency,
            status == PortfolioDimensionStatus.Available ? result.ProcurementSummary?.TotalIssuedOrderAmount : null,
            status == PortfolioDimensionStatus.Available ? result.ProcurementSummary?.OpenOrderAmount : null,
            proof.SourceManifestSha256,
            status == PortfolioDimensionStatus.Available ? null : "commercial.source_unavailable",
            proof.Classification);
    }

    private static PortfolioFinancialDimension HiddenFinancial() => new(
        PortfolioDimensionStatus.NotAuthorized, null, null, null, null, null,
        ReportClassification.Internal);
    private static PortfolioCommercialDimension HiddenCommercial() => new(
        PortfolioDimensionStatus.NotAuthorized, null, null, null, null, null,
        ReportClassification.Internal);
    private static PortfolioFinancialDimension UnknownFinancial(PortfolioDimensionStatus status) => new(
        status, null, null, null, null, "source.unavailable", ReportClassification.Confidential);
    private static PortfolioCommercialDimension UnknownCommercial(PortfolioDimensionStatus status) => new(
        status, null, null, null, null, "source.unavailable", ReportClassification.Confidential);

    private static bool Allowed(EffectivePermissionPreview preview, string operation) =>
        preview.Decisions.Count(item => string.Equals(item.Operation, operation, StringComparison.Ordinal)) == 1 &&
        preview.Decisions.Single(item => string.Equals(item.Operation, operation, StringComparison.Ordinal)).Allowed;

    private static TimeZoneInfo FindTimeZone(string value)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(value); }
        catch (TimeZoneNotFoundException) { throw Invalid("time_zone.invalid"); }
        catch (InvalidTimeZoneException) { throw Invalid("time_zone.invalid"); }
    }

    private static DomainRuleException Invalid(string suffix) => new(
        $"reporting.portfolio.{suffix}", "Portfolio selection failed its access or source contract.");
}

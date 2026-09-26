using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Commercial.Contracts;
using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Services;

internal static class ProjectCommercialProcurementSupplyReportSnapshotBuilder
{
    public static ReportSnapshot Build(
        Guid runId,
        Guid tenantId,
        Pmcs.Modules.Projects.Contracts.ProjectControlProfile project,
        DateTimeOffset sourceCutoffUtc,
        ProjectCommercialProcurementSupplyReportingResult source,
        DateTimeOffset validatedAtUtc,
        DateTimeOffset builtAt) => Build(
        runId,
        tenantId,
        ProjectCommercialProcurementSupplyPinnedProjectProfile.Capture(project, validatedAtUtc),
        sourceCutoffUtc,
        source,
        validatedAtUtc,
        builtAt);

    public static ReportSnapshot Build(
        Guid runId,
        Guid tenantId,
        ProjectCommercialProcurementSupplyPinnedProjectProfile project,
        DateTimeOffset sourceCutoffUtc,
        ProjectCommercialProcurementSupplyReportingResult source,
        DateTimeOffset validatedAtUtc,
        DateTimeOffset builtAt)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(source);
        var cutoff = sourceCutoffUtc.ToUniversalTime();
        var acceptedAt = validatedAtUtc.ToUniversalTime();
        var timeZone = project.ValidateForRun(tenantId, project.Id, cutoff, acceptedAt);
        var cutoffLocalDate = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(cutoff, timeZone).DateTime);
        var canonical = ValidateAndCanonicalizeSource(
            tenantId,
            project.Id,
            cutoffLocalDate,
            cutoff,
            source);
        var sourceManifestJson = CanonicalJson.Serialize(canonical.SourceManifest);
        var sourceManifestSha256 = CanonicalJson.Sha256(sourceManifestJson);
        if (!string.Equals(
                sourceManifestSha256,
                canonical.SourceManifestSha256,
                StringComparison.Ordinal))
        {
            throw Invalid(
                "source_manifest.hash_mismatch",
                "The Commercial source manifest hash does not match its canonical content.");
        }

        var classification = MapClassification(canonical.Classification);
        var payload = new ProjectCommercialProcurementSupplyReportSemanticSnapshot(
            ProjectCommercialProcurementSupplyReportRuntimeContract.SnapshotSchemaVersion,
            ProjectCommercialProcurementSupplyReportRuntimeContract.SemanticContractId,
            ProjectCommercialProcurementSupplyReportRuntimeContract.DefinitionCode,
            ProjectCommercialProcurementSupplyReportRuntimeContract.DefinitionVersion,
            canonical.PolicyVersion,
            MapDataStatus(canonical.DataStatus),
            canonical.ReasonCodes,
            new ProjectCommercialProcurementSupplyReportParameters(),
            new ProjectCommercialProcurementSupplyReportProjectIdentity(
                project.Id,
                project.TenantId,
                project.Code,
                project.Name,
                project.TimeZone,
                project.BaseCurrencyCode,
                project.Revision,
                project.ConfigurationVersion,
                project.ConfigurationChangedAt.ToUniversalTime(),
                project.CapturedAtUtc.ToUniversalTime()),
            new ProjectCommercialProcurementSupplyReportCutoffIdentity(cutoff, cutoffLocalDate),
            classification,
            MapConfiguration(canonical.Configuration),
            canonical.ContractStatus,
            canonical.ContractSummary,
            canonical.ContractRegister.Select(item =>
                new ProjectCommercialProcurementSupplyReportContractRow(
                    item.Number,
                    item.Title,
                    item.PartyCode,
                    item.PartyName,
                    item.PartyType,
                    item.Type,
                    item.State,
                    item.StartDate,
                    item.OriginalEndDate,
                    item.EffectiveEndDate,
                    item.OriginalApprovedAmount,
                    item.ApprovedAmountDelta,
                    item.EffectiveApprovedAmount,
                    item.ApprovedAmendmentCount,
                    item.ApprovedExtensionDays)).ToArray(),
            canonical.ApprovedAmendments.Select(item =>
                new ProjectCommercialProcurementSupplyReportAmendmentRow(
                    item.ContractNumber,
                    item.Number,
                    item.Title,
                    item.Type,
                    item.AmountDelta,
                    item.ExtensionDays,
                    item.ApprovedAt)).ToArray(),
            canonical.ProcurementStatus,
            canonical.ProcurementSummary,
            canonical.PurchaseOrders.Select(item =>
                new ProjectCommercialProcurementSupplyReportOrderRow(
                    item.Number,
                    item.Title,
                    item.PurchaseRequestNumber,
                    item.ContractNumber,
                    item.PartyCode,
                    item.PartyName,
                    item.PartyType,
                    item.Amount,
                    item.DeliveryDueDate,
                    item.State,
                    item.ItemCode,
                    item.ItemName,
                    item.ItemKind,
                    item.OrderedQuantity,
                    item.UnitCode,
                    item.OrderedBaseQuantity,
                    item.BaseUnit,
                    item.ConversionVersion,
                    item.DeliveredBaseQuantity,
                    item.AcceptedBaseQuantity,
                    item.RejectedBaseQuantity,
                    item.QuarantinedBaseQuantity,
                    item.RemainingOrderedQuantity,
                    item.AcceptedExcessQuantity,
                    item.FulfillmentPercent,
                    item.CompletionDate,
                    item.DeliveryStatus,
                    item.GoodsReceiptCount,
                    item.PendingInspectionCount,
                    item.ServiceAcceptanceCount,
                    item.RejectedOrQuarantinedEvidenceCount)).ToArray(),
            canonical.SupplyStatus,
            canonical.SupplySummaries,
            canonical.SupplierPerformance.Select(item =>
                new ProjectCommercialProcurementSupplyReportSupplierPerformance(
                    item.PartyCode,
                    item.PartyName,
                    item.PartyType,
                    item.IssuedOrderCount,
                    item.OpenOrderCount,
                    item.ClosedOrderCount,
                    item.CancelledOrderCount,
                    item.AssessableCompletedOrderCount,
                    item.OnTimeFulfilledCount,
                    item.LateFulfilledCount,
                    item.OverdueOpenCount,
                    item.ClosedShortCount,
                    item.NotAssessableCount,
                    item.GoodsReceiptCount,
                    item.PendingInspectionCount,
                    item.ReceiptWithRejectedOrQuarantinedCount,
                    item.ServiceAcceptanceCount,
                    item.ServiceAcceptanceWithRejectedCount,
                    item.OnTimeFulfillmentRate)).ToArray(),
            canonical.SourceCounts,
            canonical.SourceMaxChangedAt,
            sourceManifestSha256);

        return ReportSnapshot.Create(
            Guid.NewGuid(),
            runId,
            tenantId,
            project.Id,
            ProjectCommercialProcurementSupplyReportRuntimeContract.SnapshotSchemaVersion,
            MapDataStatus(canonical.DataStatus),
            CanonicalJson.Serialize(payload),
            sourceManifestJson,
            classification,
            builtAt,
            cutoff);
    }

    private static ProjectCommercialProcurementSupplyReportingResult ValidateAndCanonicalizeSource(
        Guid tenantId,
        Guid projectId,
        DateOnly cutoffLocalDate,
        DateTimeOffset sourceCutoffUtc,
        ProjectCommercialProcurementSupplyReportingResult source)
    {
        if (!string.Equals(
                source.ContractVersion,
                ProjectCommercialProcurementSupplyReportingContract.Version,
                StringComparison.Ordinal) ||
            !string.Equals(
                source.PolicyVersion,
                ProjectCommercialProcurementSupplyReportingContract.PolicyVersion,
                StringComparison.Ordinal) ||
            source.TenantId != tenantId || source.ProjectId != projectId ||
            source.CutoffLocalDate != cutoffLocalDate ||
            source.SourceCutoffUtc.ToUniversalTime() != sourceCutoffUtc ||
            !Enum.IsDefined(source.Classification) ||
            source.Classification < ProjectCommercialReportingClassification.Confidential ||
            !Enum.IsDefined(source.DataStatus) || !Enum.IsDefined(source.ContractStatus) ||
            !Enum.IsDefined(source.ProcurementStatus) || !Enum.IsDefined(source.SupplyStatus) ||
            source.ReasonCodes is null || source.ContractRegister is null ||
            source.ApprovedAmendments is null || source.PurchaseOrders is null ||
            source.SupplySummaries is null || source.SupplierPerformance is null ||
            source.SourceCounts is null || source.SourceManifest is null ||
            string.IsNullOrWhiteSpace(source.SourceManifestSha256) ||
            source.SourceMaxChangedAt?.ToUniversalTime() > sourceCutoffUtc)
        {
            throw Invalid("source.invalid", "The Commercial source result violates the report contract.");
        }

        ValidateConfiguration(source.Configuration, sourceCutoffUtc);
        ValidateStatuses(source);
        ValidateContracts(source);
        ValidateProcurementAndSupply(source);
        ValidateCounts(source.SourceCounts);
        ValidateManifest(source, sourceCutoffUtc);

        var reasons = source.ReasonCodes
            .Select(item => Enum.IsDefined(item)
                ? item
                : throw Invalid("reason.invalid", "The Commercial source returned an unknown reason code."))
            .Distinct()
            .OrderBy(item => item)
            .ToArray();
        if (reasons.Length != source.ReasonCodes.Count)
        {
            throw Invalid("reason.duplicate", "The Commercial source returned duplicate reason codes.");
        }

        return source with
        {
            SourceCutoffUtc = sourceCutoffUtc,
            ReasonCodes = reasons,
            ContractRegister = source.ContractRegister
                .OrderBy(item => item.Number, StringComparer.Ordinal)
                .ThenBy(item => item.ContractId.ToString("D"), StringComparer.Ordinal)
                .ToArray(),
            ApprovedAmendments = source.ApprovedAmendments
                .OrderBy(item => item.ApprovedAt)
                .ThenBy(item => item.Number, StringComparer.Ordinal)
                .ThenBy(item => item.AmendmentId.ToString("D"), StringComparer.Ordinal)
                .ToArray(),
            PurchaseOrders = source.PurchaseOrders
                .OrderBy(item => item.Number, StringComparer.Ordinal)
                .ThenBy(item => item.PurchaseOrderId.ToString("D"), StringComparer.Ordinal)
                .ToArray(),
            SupplySummaries = source.SupplySummaries
                .OrderBy(item => item.ItemCode, StringComparer.Ordinal)
                .ThenBy(item => item.BaseUnit, StringComparer.Ordinal)
                .ToArray(),
            SupplierPerformance = source.SupplierPerformance
                .OrderBy(item => item.PartyCode, StringComparer.Ordinal)
                .ThenBy(item => item.PartyId.ToString("D"), StringComparer.Ordinal)
                .ToArray(),
            SourceMaxChangedAt = source.SourceMaxChangedAt?.ToUniversalTime()
        };
    }

    private static void ValidateConfiguration(
        ProjectCommercialConfigurationVersion? configuration,
        DateTimeOffset cutoff)
    {
        if (configuration is null)
        {
            return;
        }
        if (configuration.ConfigurationVersion <= 0 || configuration.ProjectRevision <= 0 ||
            !Enum.IsDefined(configuration.ContractModel) ||
            !Enum.IsDefined(configuration.ContractState) ||
            !Enum.IsDefined(configuration.ProcurementState) ||
            string.IsNullOrWhiteSpace(configuration.TimeZone) ||
            !IsCurrency(configuration.BaseCurrencyCode) ||
            configuration.EffectiveFromUtc.ToUniversalTime() > cutoff ||
            configuration.EffectiveToUtc?.ToUniversalTime() <= cutoff ||
            !Enum.IsDefined(configuration.Classification) ||
            configuration.Classification < ProjectCommercialReportingClassification.Confidential)
        {
            throw Invalid(
                "configuration.invalid",
                "The effective Commercial configuration violates the report contract.");
        }
    }

    private static void ValidateStatuses(ProjectCommercialProcurementSupplyReportingResult source)
    {
        var statuses = new[] { source.ContractStatus, source.ProcurementStatus, source.SupplyStatus };
        var active = statuses.Where(item => item is ProjectCommercialReportingSectionStatus.NoData or
            ProjectCommercialReportingSectionStatus.InsufficientData or
            ProjectCommercialReportingSectionStatus.Available).ToArray();
        var valid = source.DataStatus switch
        {
            ProjectCommercialReportingDataStatus.NotConfigured => active.Length == 0,
            ProjectCommercialReportingDataStatus.NoData => active.Length > 0 &&
                active.All(item => item == ProjectCommercialReportingSectionStatus.NoData),
            ProjectCommercialReportingDataStatus.InsufficientData =>
                active.Contains(ProjectCommercialReportingSectionStatus.InsufficientData),
            ProjectCommercialReportingDataStatus.Available =>
                !active.Contains(ProjectCommercialReportingSectionStatus.InsufficientData) &&
                active.Contains(ProjectCommercialReportingSectionStatus.Available),
            _ => false
        };
        if (!valid)
        {
            throw Invalid("status.invalid", "Commercial data status conflicts with section statuses.");
        }
    }

    private static void ValidateContracts(ProjectCommercialProcurementSupplyReportingResult source)
    {
        if (source.ContractStatus == ProjectCommercialReportingSectionStatus.Available)
        {
            if (source.ContractSummary is null || source.ContractRegister.Count == 0 ||
                source.ContractSummary.OfficialContractCount != source.ContractRegister.Count ||
                source.ContractRegister.Select(item => item.ContractId).Distinct().Count() !=
                    source.ContractRegister.Count ||
                source.ContractRegister.Any(item => string.IsNullOrWhiteSpace(item.Number) ||
                    string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.PartyCode) ||
                    string.IsNullOrWhiteSpace(item.PartyName) || !Enum.IsDefined(item.PartyType) ||
                    !Enum.IsDefined(item.Type) || !Enum.IsDefined(item.State) ||
                    item.ApprovedAmendmentCount < 0 || item.ApprovedExtensionDays < 0 ||
                    item.EffectiveApprovedAmount < 0))
            {
                throw Invalid("contract.invalid", "Contract summary or register data is invalid.");
            }
        }
        else if (source.ContractSummary is not null || source.ContractRegister.Count > 0 ||
            source.ApprovedAmendments.Count > 0)
        {
            throw Invalid(
                "contract.invalid",
                "An unavailable Contract section cannot expose partial register data.");
        }
    }

    private static void ValidateProcurementAndSupply(
        ProjectCommercialProcurementSupplyReportingResult source)
    {
        if (source.ProcurementStatus == ProjectCommercialReportingSectionStatus.Available)
        {
            if (source.ProcurementSummary is null ||
                source.PurchaseOrders.Select(item => item.PurchaseOrderId).Distinct().Count() !=
                    source.PurchaseOrders.Count ||
                source.PurchaseOrders.Any(item => string.IsNullOrWhiteSpace(item.Number) ||
                    string.IsNullOrWhiteSpace(item.Title) ||
                    string.IsNullOrWhiteSpace(item.PurchaseRequestNumber) ||
                    string.IsNullOrWhiteSpace(item.PartyCode) ||
                    string.IsNullOrWhiteSpace(item.PartyName) || !Enum.IsDefined(item.PartyType) ||
                    !Enum.IsDefined(item.State) || !Enum.IsDefined(item.DeliveryStatus) ||
                    item.Amount <= 0 || decimal.Round(item.Amount, 2, MidpointRounding.AwayFromZero) !=
                        item.Amount || item.FulfillmentPercent < 0))
            {
                throw Invalid("procurement.invalid", "Procurement summary or Order data is invalid.");
            }
        }
        else if (source.ProcurementSummary is not null || source.PurchaseOrders.Count > 0 ||
            source.SupplierPerformance.Count > 0)
        {
            throw Invalid(
                "procurement.invalid",
                "An unavailable Procurement section cannot expose partial Order data.");
        }

        if (source.SupplyStatus is ProjectCommercialReportingSectionStatus.NotConfigured or
            ProjectCommercialReportingSectionStatus.SetupRequired or
            ProjectCommercialReportingSectionStatus.Suspended or
            ProjectCommercialReportingSectionStatus.InsufficientData)
        {
            if (source.SupplySummaries.Count > 0)
            {
                throw Invalid(
                    "supply.invalid",
                    "An unavailable Supply section cannot expose aggregate quantity data.");
            }
        }
        else if (source.SupplySummaries.Any(item => string.IsNullOrWhiteSpace(item.ItemCode) ||
            string.IsNullOrWhiteSpace(item.ItemName) || string.IsNullOrWhiteSpace(item.BaseUnit) ||
            !Enum.IsDefined(item.ItemKind) || item.OrderCount <= 0 ||
            item.OrderedBaseQuantity <= 0 || item.DeliveredBaseQuantity < 0 ||
            item.AcceptedBaseQuantity < 0 || item.RejectedBaseQuantity < 0 ||
            item.QuarantinedBaseQuantity < 0))
        {
            throw Invalid("supply.invalid", "A Supply summary violates the report contract.");
        }
    }

    private static void ValidateCounts(ProjectCommercialSourceCounts counts)
    {
        var values = new[]
        {
            counts.PartySnapshotSourceCount,
            counts.EffectivePartySnapshotCount,
            counts.ItemSnapshotSourceCount,
            counts.EffectiveItemSnapshotCount,
            counts.ContractSourceCount,
            counts.OfficialContractCount,
            counts.PendingContractCount,
            counts.AmendmentSourceCount,
            counts.ApprovedAmendmentCount,
            counts.PendingAmendmentCount,
            counts.PurchaseRequestSourceCount,
            counts.PurchaseRequestAtCutoffCount,
            counts.PurchaseOrderSourceCount,
            counts.OfficialPurchaseOrderCount,
            counts.GoodsReceiptSourceCount,
            counts.EligibleGoodsReceiptCount,
            counts.ServiceAcceptanceSourceCount,
            counts.EligibleServiceAcceptanceCount,
            counts.IncompleteCollectionCount
        };
        if (values.Any(item => item < 0) || counts.IncompleteCollectionCount > 7 ||
            counts.EffectivePartySnapshotCount > counts.PartySnapshotSourceCount ||
            counts.EffectiveItemSnapshotCount > counts.ItemSnapshotSourceCount ||
            counts.OfficialContractCount > counts.ContractSourceCount ||
            counts.ApprovedAmendmentCount > counts.AmendmentSourceCount ||
            counts.PurchaseRequestAtCutoffCount > counts.PurchaseRequestSourceCount ||
            counts.OfficialPurchaseOrderCount > counts.PurchaseOrderSourceCount ||
            counts.EligibleGoodsReceiptCount > counts.GoodsReceiptSourceCount ||
            counts.EligibleServiceAcceptanceCount > counts.ServiceAcceptanceSourceCount)
        {
            throw Invalid("source_counts.invalid", "Commercial source counts are inconsistent.");
        }
    }

    private static void ValidateManifest(
        ProjectCommercialProcurementSupplyReportingResult source,
        DateTimeOffset cutoff)
    {
        var manifest = source.SourceManifest;
        if (!string.Equals(
                manifest.ManifestVersion,
                ProjectCommercialProcurementSupplyReportingContract.SourceManifestVersion,
                StringComparison.Ordinal) ||
            !string.Equals(
                manifest.SourceContractVersion,
                ProjectCommercialProcurementSupplyReportingContract.Version,
                StringComparison.Ordinal) ||
            !string.Equals(
                manifest.PolicyVersion,
                ProjectCommercialProcurementSupplyReportingContract.PolicyVersion,
                StringComparison.Ordinal) ||
            manifest.TenantId != source.TenantId || manifest.ProjectId != source.ProjectId ||
            manifest.CutoffLocalDate != source.CutoffLocalDate ||
            manifest.SourceCutoffUtc.ToUniversalTime() != cutoff ||
            manifest.PartySnapshots is null || manifest.ItemSnapshots is null ||
            manifest.Contracts is null || manifest.Amendments is null ||
            manifest.PurchaseRequests is null || manifest.PurchaseOrders is null ||
            manifest.GoodsReceipts is null || manifest.ServiceAcceptances is null)
        {
            throw Invalid("source_manifest.invalid", "The Commercial source manifest is invalid.");
        }
    }

    private static ProjectCommercialProcurementSupplyReportConfiguration? MapConfiguration(
        ProjectCommercialConfigurationVersion? configuration) => configuration is null
        ? null
        : new ProjectCommercialProcurementSupplyReportConfiguration(
            configuration.ConfigurationVersion,
            configuration.ProjectRevision,
            configuration.ContractModel,
            configuration.ContractState,
            configuration.ProcurementState,
            configuration.TimeZone,
            configuration.BaseCurrencyCode,
            configuration.EffectiveFromUtc,
            configuration.EffectiveToUtc);

    private static ReportDataStatus MapDataStatus(ProjectCommercialReportingDataStatus status) =>
        status switch
        {
            ProjectCommercialReportingDataStatus.NotConfigured => ReportDataStatus.NotConfigured,
            ProjectCommercialReportingDataStatus.NoData => ReportDataStatus.NoData,
            ProjectCommercialReportingDataStatus.InsufficientData => ReportDataStatus.InsufficientData,
            ProjectCommercialReportingDataStatus.Available => ReportDataStatus.Available,
            _ => throw Invalid("data_status.invalid", "The Commercial source data status is unknown.")
        };

    private static ReportClassification MapClassification(
        ProjectCommercialReportingClassification classification) => classification switch
    {
        ProjectCommercialReportingClassification.Confidential => ReportClassification.Confidential,
        ProjectCommercialReportingClassification.Restricted => ReportClassification.Restricted,
        _ => throw Invalid(
            "classification.invalid",
            "Certified Commercial output cannot use an unknown or Internal classification.")
    };

    private static bool IsCurrency(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length == 3 &&
        value.All(char.IsAsciiLetterUpper);

    private static DomainRuleException Invalid(string suffix, string message) =>
        new($"reporting.project_commercial_procurement_supply.{suffix}", message);
}

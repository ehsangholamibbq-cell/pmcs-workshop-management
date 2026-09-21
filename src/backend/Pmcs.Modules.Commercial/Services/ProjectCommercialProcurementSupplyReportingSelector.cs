using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Commercial.Contracts;
using Pmcs.Modules.Commercial.Domain;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Commercial.Services;

internal static class ProjectCommercialProcurementSupplyReportingSelector
{
    public static ProjectCommercialProcurementSupplyReportingSelection Select(
        ProjectCommercialProcurementSupplyReportingProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        var cutoff = projection.SourceCutoffUtc.ToUniversalTime();
        ValidateScope(projection, cutoff);

        var configurations = projection.Configurations
            .Select(ValidateAndNormalize)
            .OrderBy(item => item.EffectiveFromUtc)
            .ThenBy(item => item.ConfigurationVersion)
            .ToArray();
        EnsureDistinct(
            configurations.Select(item => item.ConfigurationVersion),
            "configuration.duplicate",
            "Commercial reporting contains duplicate configuration versions.");
        EnsureNonOverlapping(
            configurations.Select(item => (item.EffectiveFromUtc, item.EffectiveToUtc)),
            "configuration.overlap",
            "Commercial reporting contains overlapping configuration versions.");
        var effectiveConfigurations = configurations
            .Where(item => IsEffectiveAt(item.EffectiveFromUtc, item.EffectiveToUtc, cutoff))
            .ToArray();
        if (effectiveConfigurations.Length > 1)
        {
            throw Invalid(
                "configuration.overlap",
                "More than one Commercial configuration is effective at the reporting cutoff.");
        }

        var configuration = effectiveConfigurations.SingleOrDefault();
        if (configuration is not null)
        {
            ValidateLocalCutoff(configuration.TimeZone, cutoff, projection.CutoffLocalDate);
        }

        var parties = projection.PartySnapshots
            .Select(item => ValidateAndNormalize(item, projection.TenantId, projection.ProjectId))
            .OrderBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.PartyId.ToString("D"), StringComparer.Ordinal)
            .ThenBy(item => item.EffectiveFromUtc)
            .ToArray();
        EnsureDistinct(
            parties.Select(item => (item.PartyId, item.Revision)),
            "party_snapshot.duplicate",
            "Commercial reporting contains duplicate Party snapshot revisions.");
        EnsureSnapshotWindows(
            parties.GroupBy(item => item.PartyId)
                .Select(group => group.Select(item => (item.EffectiveFromUtc, item.EffectiveToUtc))),
            "party_snapshot.overlap",
            "Commercial reporting contains overlapping Party snapshots.");
        var effectiveParties = parties
            .Where(item => IsEffectiveAt(item.EffectiveFromUtc, item.EffectiveToUtc, cutoff))
            .ToDictionary(item => item.PartyId);

        var items = projection.ItemSnapshots
            .Select(item => ValidateAndNormalize(item, projection.TenantId, projection.ProjectId))
            .OrderBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.ItemId.ToString("D"), StringComparer.Ordinal)
            .ThenBy(item => item.EffectiveFromUtc)
            .ToArray();
        EnsureDistinct(
            items.Select(item => (item.ItemId, item.Revision)),
            "item_snapshot.duplicate",
            "Commercial reporting contains duplicate Item snapshot revisions.");
        EnsureSnapshotWindows(
            items.GroupBy(item => item.ItemId)
                .Select(group => group.Select(item => (item.EffectiveFromUtc, item.EffectiveToUtc))),
            "item_snapshot.overlap",
            "Commercial reporting contains overlapping Item snapshots.");
        var effectiveItems = items
            .Where(item => IsEffectiveAt(item.EffectiveFromUtc, item.EffectiveToUtc, cutoff))
            .ToDictionary(item => item.ItemId);

        var contracts = projection.Contracts
            .Select(item => ValidateAndNormalize(item, projection.TenantId, projection.ProjectId))
            .OrderBy(item => item.Number, StringComparer.Ordinal)
            .ThenBy(item => item.ContractId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        EnsureDistinct(
            contracts.Select(item => item.ContractId),
            "contract.duplicate",
            "Commercial reporting contains duplicate Contract identities.");
        var contractById = contracts.ToDictionary(item => item.ContractId);
        var officialContracts = contracts
            .Select(item => SelectContract(item, cutoff, effectiveParties, projection.PartySnapshotCompleteness))
            .Where(item => item is not null &&
                (item.State == ProjectCommercialContractWorkflowState.Active ||
                    item.State == ProjectCommercialContractWorkflowState.Suspended ||
                    item.State == ProjectCommercialContractWorkflowState.Closed ||
                    item.State == ProjectCommercialContractWorkflowState.Terminated))
            .Cast<ProjectCommercialContractAtCutoff>()
            .ToArray();
        var officialContractById = officialContracts.ToDictionary(item => item.Source.ContractId);
        var pendingContractCount = contracts.Count(item =>
            item.CreatedAt <= cutoff &&
            ContractStateAt(item, cutoff) == ProjectCommercialContractWorkflowState.Submitted);

        var amendments = projection.Amendments
            .Select(item => ValidateAndNormalize(item, projection.TenantId, projection.ProjectId))
            .OrderBy(item => item.Number, StringComparer.Ordinal)
            .ThenBy(item => item.AmendmentId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        EnsureDistinct(
            amendments.Select(item => item.AmendmentId),
            "amendment.duplicate",
            "Commercial reporting contains duplicate Amendment identities.");
        foreach (var amendment in amendments)
        {
            if (!contractById.TryGetValue(amendment.ContractId, out var contract))
            {
                throw Invalid("amendment.contract.invalid", "An Amendment references an unknown Contract.");
            }
            if (!string.Equals(amendment.CurrencyCode, contract.CurrencyCode, StringComparison.Ordinal))
            {
                throw Invalid(
                    "amendment.currency_mismatch",
                    "An Amendment does not use its Contract currency.");
            }
        }
        var approvedAmendments = amendments
            .Select(item => SelectAmendment(item, cutoff, officialContractById))
            .Where(item => item is not null)
            .Cast<ProjectCommercialAmendmentAtCutoff>()
            .OrderBy(item => item.ApprovedAt)
            .ThenBy(item => item.Source.Number, StringComparer.Ordinal)
            .ThenBy(item => item.Source.AmendmentId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        var pendingAmendmentCount = amendments.Count(item =>
            item.CreatedAt <= cutoff && AmendmentStateAt(item, cutoff) == AmendmentWorkflowState.Submitted);

        var requests = projection.PurchaseRequests
            .Select(item => ValidateAndNormalize(item, projection.TenantId, projection.ProjectId))
            .OrderBy(item => item.Number, StringComparer.Ordinal)
            .ThenBy(item => item.PurchaseRequestId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        EnsureDistinct(
            requests.Select(item => item.PurchaseRequestId),
            "request.duplicate",
            "Commercial reporting contains duplicate Purchase Request identities.");
        var requestById = requests.ToDictionary(item => item.PurchaseRequestId);
        var requestsAtCutoff = requests
            .Where(item => item.CreatedAt <= cutoff)
            .Select(item => new ProjectCommercialPurchaseRequestAtCutoff(
                item,
                MapRequestState(RequestStateAt(item, cutoff))))
            .ToArray();

        var orders = projection.PurchaseOrders
            .Select(item => ValidateAndNormalize(item, projection.TenantId, projection.ProjectId))
            .OrderBy(item => item.Number, StringComparer.Ordinal)
            .ThenBy(item => item.PurchaseOrderId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        EnsureDistinct(
            orders.Select(item => item.PurchaseOrderId),
            "order.duplicate",
            "Commercial reporting contains duplicate Purchase Order identities.");
        var officialOrders = orders
            .Select(item => SelectOrder(
                item,
                cutoff,
                requestById,
                officialContractById,
                effectiveParties,
                items,
                projection.ContractLifecycleCompleteness,
                projection.PartySnapshotCompleteness,
                projection.ItemSnapshotCompleteness))
            .Where(item => item is not null)
            .Cast<ProjectCommercialPurchaseOrderAtCutoff>()
            .ToArray();
        EnsureDistinct(
            officialOrders.Select(item => item.Source.PurchaseRequestId),
            "order.request_duplicate",
            "More than one Purchase Order is linked to the same Purchase Request.");
        var officialOrderById = officialOrders.ToDictionary(item => item.Source.PurchaseOrderId);
        foreach (var request in requestsAtCutoff.Where(item =>
            item.State == ProjectCommercialPurchaseRequestState.Ordered))
        {
            if (!officialOrders.Any(item => item.Source.PurchaseRequestId == request.Source.PurchaseRequestId))
            {
                throw Invalid(
                    "request.order_lineage.invalid",
                    "An Ordered Purchase Request does not have one official Purchase Order.");
            }
        }

        if (configuration is null &&
            (contracts.Length > 0 || amendments.Length > 0 || requests.Length > 0 ||
                orders.Length > 0 || projection.GoodsReceipts.Count > 0 ||
                projection.ServiceAcceptances.Count > 0))
        {
            throw Invalid(
                "configuration.missing",
                "Commercial evidence cannot be selected without an effective versioned configuration.");
        }
        if (configuration is not null)
        {
            ValidateCurrencies(configuration, contracts, amendments, requests, orders);
        }

        var receipts = projection.GoodsReceipts
            .Select(item => ValidateAndNormalize(item, projection.TenantId, projection.ProjectId))
            .OrderBy(item => item.ArrivedAt)
            .ThenBy(item => item.Number, StringComparer.Ordinal)
            .ThenBy(item => item.ReceiptId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        EnsureDistinct(
            receipts.Select(item => item.ReceiptId),
            "receipt.duplicate",
            "Commercial reporting contains duplicate Goods Receipt identities.");
        var selectedReceipts = receipts
            .Select(item => SelectReceipt(
                item,
                cutoff,
                configuration,
                officialOrderById))
            .Where(item => item is not null)
            .Cast<ProjectCommercialGoodsReceiptAtCutoff>()
            .ToArray();

        var acceptances = projection.ServiceAcceptances
            .Select(item => ValidateAndNormalize(item, projection.TenantId, projection.ProjectId))
            .OrderBy(item => item.PeriodEnd)
            .ThenBy(item => item.Number, StringComparer.Ordinal)
            .ThenBy(item => item.AcceptanceId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        EnsureDistinct(
            acceptances.Select(item => item.AcceptanceId),
            "service_acceptance.duplicate",
            "Commercial reporting contains duplicate Service Acceptance identities.");
        var selectedAcceptances = acceptances
            .Select(item => SelectServiceAcceptance(
                item,
                cutoff,
                projection.CutoffLocalDate,
                officialOrderById))
            .Where(item => item is not null)
            .Cast<ProjectCommercialServiceAcceptanceAtCutoff>()
            .ToArray();

        ValidateExcessApproval(officialOrders, selectedReceipts, selectedAcceptances, cutoff);

        var classification = ResolveClassification(
            projection.Classification,
            configurations,
            parties,
            items,
            contracts,
            amendments,
            requests,
            orders,
            receipts,
            acceptances);
        var incompleteCollectionCount = new[]
        {
            projection.ContractLifecycleCompleteness,
            projection.AmendmentLifecycleCompleteness,
            projection.ProcurementLifecycleCompleteness,
            projection.PartySnapshotCompleteness,
            projection.ItemSnapshotCompleteness,
            projection.ReceiptInspectionCompleteness,
            projection.ServiceAcceptanceCompleteness
        }.Count(item => item == ProjectCommercialReportingSourceCompleteness.Incomplete);
        var counts = new ProjectCommercialSourceCounts(
            parties.Length,
            effectiveParties.Count,
            items.Length,
            effectiveItems.Count,
            contracts.Length,
            officialContracts.Length,
            pendingContractCount,
            amendments.Length,
            approvedAmendments.Length,
            pendingAmendmentCount,
            requests.Length,
            requestsAtCutoff.Length,
            orders.Length,
            officialOrders.Length,
            receipts.Length,
            selectedReceipts.Length,
            acceptances.Length,
            selectedAcceptances.Length,
            incompleteCollectionCount);
        var sourceMaxChangedAt = ResolveSourceMaxChangedAt(
            configurations,
            parties,
            items,
            contracts,
            amendments,
            requests,
            orders,
            receipts,
            acceptances,
            cutoff);
        var manifest = BuildManifest(
            projection,
            cutoff,
            configuration,
            parties,
            items,
            contracts,
            amendments,
            requests,
            orders,
            receipts,
            acceptances);

        return new ProjectCommercialProcurementSupplyReportingSelection(
            ProjectCommercialProcurementSupplyReportingContract.Version,
            projection.TenantId,
            projection.ProjectId,
            projection.CutoffLocalDate,
            cutoff,
            configuration,
            officialContracts,
            pendingContractCount,
            approvedAmendments,
            pendingAmendmentCount,
            requestsAtCutoff,
            officialOrders,
            selectedReceipts,
            selectedAcceptances,
            projection.ContractLifecycleCompleteness,
            projection.AmendmentLifecycleCompleteness,
            projection.ProcurementLifecycleCompleteness,
            projection.PartySnapshotCompleteness,
            projection.ItemSnapshotCompleteness,
            projection.ReceiptInspectionCompleteness,
            projection.ServiceAcceptanceCompleteness,
            classification,
            counts,
            sourceMaxChangedAt,
            manifest,
            ProjectCommercialProcurementSupplyCanonicalJson.Sha256(
                ProjectCommercialProcurementSupplyCanonicalJson.Serialize(manifest)));
    }

    private static void ValidateScope(
        ProjectCommercialProcurementSupplyReportingProjection projection,
        DateTimeOffset cutoff)
    {
        if (!string.Equals(
                projection.ContractVersion,
                ProjectCommercialProcurementSupplyReportingContract.Version,
                StringComparison.Ordinal) ||
            projection.TenantId == Guid.Empty || projection.ProjectId == Guid.Empty ||
            projection.CutoffLocalDate == default || cutoff == default ||
            projection.Configurations is null || projection.PartySnapshots is null ||
            projection.ItemSnapshots is null || projection.Contracts is null ||
            projection.Amendments is null || projection.PurchaseRequests is null ||
            projection.PurchaseOrders is null || projection.GoodsReceipts is null ||
            projection.ServiceAcceptances is null ||
            projection.Contracts.Count > ProjectCommercialProcurementSupplyReportingContract.MaximumContracts ||
            projection.Amendments.Count > ProjectCommercialProcurementSupplyReportingContract.MaximumAmendments ||
            projection.PurchaseRequests.Count > ProjectCommercialProcurementSupplyReportingContract.MaximumPurchaseRequests ||
            projection.PurchaseOrders.Count > ProjectCommercialProcurementSupplyReportingContract.MaximumPurchaseOrders ||
            projection.GoodsReceipts.Count + projection.ServiceAcceptances.Count >
                ProjectCommercialProcurementSupplyReportingContract.MaximumSupplyEvidence ||
            projection.PartySnapshots.Count + projection.ItemSnapshots.Count >
                ProjectCommercialProcurementSupplyReportingContract.MaximumPartyAndItemSnapshots ||
            !Enum.IsDefined(projection.ContractLifecycleCompleteness) ||
            !Enum.IsDefined(projection.AmendmentLifecycleCompleteness) ||
            !Enum.IsDefined(projection.ProcurementLifecycleCompleteness) ||
            !Enum.IsDefined(projection.PartySnapshotCompleteness) ||
            !Enum.IsDefined(projection.ItemSnapshotCompleteness) ||
            !Enum.IsDefined(projection.ReceiptInspectionCompleteness) ||
            !Enum.IsDefined(projection.ServiceAcceptanceCompleteness))
        {
            throw Invalid(
                "scope.invalid",
                "The Commercial projection violates its bounded versioned scope.");
        }

        RequireConfidential(projection.Classification, "classification.invalid");
    }

    private static ProjectCommercialConfigurationVersion ValidateAndNormalize(
        ProjectCommercialConfigurationVersion item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var effectiveFrom = item.EffectiveFromUtc.ToUniversalTime();
        var effectiveTo = item.EffectiveToUtc?.ToUniversalTime();
        if (item.ConfigurationVersion <= 0 || item.ProjectRevision <= 0 ||
            !Enum.IsDefined(item.ContractModel) || !Enum.IsDefined(item.ContractState) ||
            !Enum.IsDefined(item.ProcurementState) || string.IsNullOrWhiteSpace(item.TimeZone) ||
            effectiveFrom == default || effectiveTo <= effectiveFrom ||
            (item.ContractModel == ContractModel.NotConfigured &&
                item.ContractState == ProjectFeatureState.Active))
        {
            throw Invalid(
                "configuration.invalid",
                "A Commercial configuration version violates the reporting contract.");
        }

        RequireConfidential(item.Classification, "configuration.classification.invalid");
        ValidateTimeZone(item.TimeZone);
        return item with
        {
            TimeZone = item.TimeZone.Trim(),
            BaseCurrencyCode = NormalizeCurrency(item.BaseCurrencyCode, "configuration.currency.invalid"),
            EffectiveFromUtc = effectiveFrom,
            EffectiveToUtc = effectiveTo
        };
    }

    private static ProjectCommercialPartySnapshotVersion ValidateAndNormalize(
        ProjectCommercialPartySnapshotVersion item,
        Guid tenantId,
        Guid projectId)
    {
        ArgumentNullException.ThrowIfNull(item);
        var effectiveFrom = item.EffectiveFromUtc.ToUniversalTime();
        var effectiveTo = item.EffectiveToUtc?.ToUniversalTime();
        if (item.PartyId == Guid.Empty || item.TenantId != tenantId || item.ProjectId != projectId ||
            item.Revision <= 0 || string.IsNullOrWhiteSpace(item.Code) || item.Code.Trim().Length > 32 ||
            string.IsNullOrWhiteSpace(item.Name) || item.Name.Trim().Length > 200 ||
            !Enum.IsDefined(item.Type) || effectiveFrom == default || effectiveTo <= effectiveFrom)
        {
            throw Invalid("party_snapshot.invalid", "A Party snapshot violates the reporting contract.");
        }

        RequireConfidential(item.Classification, "party_snapshot.classification.invalid");
        return item with
        {
            Code = item.Code.Trim(),
            Name = item.Name.Trim(),
            EffectiveFromUtc = effectiveFrom,
            EffectiveToUtc = effectiveTo
        };
    }

    private static ProjectCommercialItemSnapshotVersion ValidateAndNormalize(
        ProjectCommercialItemSnapshotVersion item,
        Guid tenantId,
        Guid projectId)
    {
        ArgumentNullException.ThrowIfNull(item);
        var effectiveFrom = item.EffectiveFromUtc.ToUniversalTime();
        var effectiveTo = item.EffectiveToUtc?.ToUniversalTime();
        if (item.ItemId == Guid.Empty || item.TenantId != tenantId || item.ProjectId != projectId ||
            item.Revision <= 0 || string.IsNullOrWhiteSpace(item.Code) || item.Code.Trim().Length > 48 ||
            string.IsNullOrWhiteSpace(item.Name) || item.Name.Trim().Length > 240 ||
            !Enum.IsDefined(item.Kind) || string.IsNullOrWhiteSpace(item.BaseUnit) ||
            item.BaseUnit.Trim().Length > 24 || effectiveFrom == default || effectiveTo <= effectiveFrom)
        {
            throw Invalid("item_snapshot.invalid", "an Item snapshot violates the reporting contract.");
        }

        RequireConfidential(item.Classification, "item_snapshot.classification.invalid");
        return item with
        {
            Code = item.Code.Trim(),
            Name = item.Name.Trim(),
            BaseUnit = item.BaseUnit.Trim().ToUpperInvariant(),
            EffectiveFromUtc = effectiveFrom,
            EffectiveToUtc = effectiveTo
        };
    }

    private static ProjectCommercialContractVersion ValidateAndNormalize(
        ProjectCommercialContractVersion item,
        Guid tenantId,
        Guid projectId)
    {
        ArgumentNullException.ThrowIfNull(item);
        var createdAt = item.CreatedAt.ToUniversalTime();
        if (item.ContractId == Guid.Empty || item.TenantId != tenantId || item.ProjectId != projectId ||
            item.Revision <= 0 || item.PartyId == Guid.Empty ||
            string.IsNullOrWhiteSpace(item.Number) || item.Number.Trim().Length > 80 ||
            string.IsNullOrWhiteSpace(item.Title) || item.Title.Trim().Length > 240 ||
            !Enum.IsDefined(item.Type) ||
            (item.OriginalApprovedAmount.HasValue && !IsPositiveMoney(item.OriginalApprovedAmount.Value)) ||
            item.StartDate.HasValue && item.OriginalEndDate.HasValue &&
                item.OriginalEndDate.Value < item.StartDate.Value || createdAt == default ||
            item.Lifecycle is null)
        {
            throw Invalid("contract.invalid", "A Contract version violates the reporting contract.");
        }

        RequireConfidential(item.Classification, "contract.classification.invalid");
        var lifecycle = NormalizeContractLifecycle(item.Lifecycle, createdAt);
        return item with
        {
            Number = item.Number.Trim(),
            Title = item.Title.Trim(),
            CurrencyCode = NormalizeCurrency(item.CurrencyCode, "contract.currency.invalid"),
            CreatedAt = createdAt,
            Lifecycle = lifecycle
        };
    }

    private static ProjectCommercialAmendmentVersion ValidateAndNormalize(
        ProjectCommercialAmendmentVersion item,
        Guid tenantId,
        Guid projectId)
    {
        ArgumentNullException.ThrowIfNull(item);
        var createdAt = item.CreatedAt.ToUniversalTime();
        var validShape = item.Type switch
        {
            ContractAmendmentType.ScopeChange => item.AmountDelta is null && item.ExtensionDays is null,
            ContractAmendmentType.ValueChange => item.AmountDelta is not null and not 0 &&
                item.ExtensionDays is null,
            ContractAmendmentType.TimeExtension => item.AmountDelta is null && item.ExtensionDays > 0,
            ContractAmendmentType.Mixed => item.AmountDelta is not null and not 0 && item.ExtensionDays > 0,
            _ => false
        };
        if (item.AmendmentId == Guid.Empty || item.TenantId != tenantId || item.ProjectId != projectId ||
            item.Revision <= 0 || item.ContractId == Guid.Empty ||
            string.IsNullOrWhiteSpace(item.Number) || item.Number.Trim().Length > 80 ||
            string.IsNullOrWhiteSpace(item.Title) || item.Title.Trim().Length > 240 ||
            !validShape || item.AmountDelta.HasValue && !IsMoney(item.AmountDelta.Value) ||
            createdAt == default || item.Lifecycle is null)
        {
            throw Invalid("amendment.invalid", "An Amendment version violates the reporting contract.");
        }

        RequireConfidential(item.Classification, "amendment.classification.invalid");
        var lifecycle = NormalizeAmendmentLifecycle(item.Lifecycle, createdAt);
        return item with
        {
            Number = item.Number.Trim(),
            Title = item.Title.Trim(),
            CurrencyCode = NormalizeCurrency(item.CurrencyCode, "amendment.currency.invalid"),
            CreatedAt = createdAt,
            Lifecycle = lifecycle
        };
    }

    private static ProjectCommercialPurchaseRequestVersion ValidateAndNormalize(
        ProjectCommercialPurchaseRequestVersion item,
        Guid tenantId,
        Guid projectId)
    {
        ArgumentNullException.ThrowIfNull(item);
        var createdAt = item.CreatedAt.ToUniversalTime();
        if (item.PurchaseRequestId == Guid.Empty || item.TenantId != tenantId ||
            item.ProjectId != projectId || item.Revision <= 0 ||
            string.IsNullOrWhiteSpace(item.Number) || item.Number.Trim().Length > 80 ||
            string.IsNullOrWhiteSpace(item.Title) || item.Title.Trim().Length > 240 ||
            item.EstimatedAmount.HasValue && !IsPositiveMoney(item.EstimatedAmount.Value) ||
            createdAt == default || item.Lifecycle is null)
        {
            throw Invalid("request.invalid", "A Purchase Request version violates the reporting contract.");
        }

        RequireConfidential(item.Classification, "request.classification.invalid");
        var lifecycle = NormalizeRequestLifecycle(item.Lifecycle, createdAt);
        return item with
        {
            Number = item.Number.Trim(),
            Title = item.Title.Trim(),
            CurrencyCode = NormalizeCurrency(item.CurrencyCode, "request.currency.invalid"),
            CreatedAt = createdAt,
            Lifecycle = lifecycle
        };
    }

    private static ProjectCommercialPurchaseOrderVersion ValidateAndNormalize(
        ProjectCommercialPurchaseOrderVersion item,
        Guid tenantId,
        Guid projectId)
    {
        ArgumentNullException.ThrowIfNull(item);
        var rawBasisPresent = item.ItemId.HasValue || item.OrderedQuantity.HasValue ||
            !string.IsNullOrWhiteSpace(item.UnitCode);
        var rawBasisComplete = item.ItemId.HasValue && item.OrderedQuantity.HasValue &&
            !string.IsNullOrWhiteSpace(item.UnitCode);
        var baseBasisPresent = item.OrderedBaseQuantity.HasValue || !string.IsNullOrWhiteSpace(item.BaseUnit) ||
            item.ConversionVersion.HasValue;
        var baseBasisComplete = item.OrderedBaseQuantity.HasValue &&
            !string.IsNullOrWhiteSpace(item.BaseUnit) && item.ConversionVersion > 0;
        if (item.PurchaseOrderId == Guid.Empty || item.TenantId != tenantId ||
            item.ProjectId != projectId || item.Revision <= 0 || item.PurchaseRequestId == Guid.Empty ||
            item.PartyId == Guid.Empty || string.IsNullOrWhiteSpace(item.Number) ||
            item.Number.Trim().Length > 80 || string.IsNullOrWhiteSpace(item.Title) ||
            item.Title.Trim().Length > 240 || !IsPositiveMoney(item.Amount) ||
            rawBasisPresent != rawBasisComplete || baseBasisPresent != baseBasisComplete ||
            baseBasisComplete && !rawBasisComplete ||
            item.OrderedQuantity.HasValue && !IsPositiveQuantity(item.OrderedQuantity.Value) ||
            item.OrderedBaseQuantity.HasValue && !IsPositiveQuantity(item.OrderedBaseQuantity.Value) ||
            item.Lifecycle is null)
        {
            throw Invalid("order.invalid", "A Purchase Order version violates the reporting contract.");
        }

        RequireConfidential(item.Classification, "order.classification.invalid");
        var lifecycle = NormalizeOrderLifecycle(item.Lifecycle);
        return item with
        {
            Number = item.Number.Trim(),
            Title = item.Title.Trim(),
            CurrencyCode = NormalizeCurrency(item.CurrencyCode, "order.currency.invalid"),
            UnitCode = OptionalUnit(item.UnitCode),
            BaseUnit = OptionalUnit(item.BaseUnit),
            Lifecycle = lifecycle
        };
    }

    private static ProjectCommercialGoodsReceiptVersion ValidateAndNormalize(
        ProjectCommercialGoodsReceiptVersion item,
        Guid tenantId,
        Guid projectId)
    {
        ArgumentNullException.ThrowIfNull(item);
        var createdAt = item.CreatedAt.ToUniversalTime();
        var arrivedAt = item.ArrivedAt.ToUniversalTime();
        var inspectedAt = item.InspectedAt?.ToUniversalTime();
        var excessApprovedAt = item.ExcessApprovedAt?.ToUniversalTime();
        var inspectedStatus = item.Status is GoodsReceiptStatus.Accepted or
            GoodsReceiptStatus.PartiallyAccepted or GoodsReceiptStatus.Rejected or
            GoodsReceiptStatus.Quarantined;
        var balancedStatus = item.Status switch
        {
            GoodsReceiptStatus.Received or GoodsReceiptStatus.PendingInspection =>
                item.AcceptedBaseQuantity == 0 && item.RejectedBaseQuantity == 0 &&
                item.QuarantinedBaseQuantity == 0,
            GoodsReceiptStatus.Accepted =>
                item.AcceptedBaseQuantity == item.ReceivedBaseQuantity &&
                item.RejectedBaseQuantity == 0 && item.QuarantinedBaseQuantity == 0,
            GoodsReceiptStatus.PartiallyAccepted => item.AcceptedBaseQuantity > 0 &&
                item.AcceptedBaseQuantity < item.ReceivedBaseQuantity,
            GoodsReceiptStatus.Rejected => item.AcceptedBaseQuantity == 0 &&
                item.RejectedBaseQuantity == item.ReceivedBaseQuantity,
            GoodsReceiptStatus.Quarantined => item.AcceptedBaseQuantity == 0 &&
                item.QuarantinedBaseQuantity > 0,
            _ => false
        };
        if (item.ReceiptId == Guid.Empty || item.TenantId != tenantId || item.ProjectId != projectId ||
            item.Revision <= 0 || string.IsNullOrWhiteSpace(item.Number) ||
            item.Number.Trim().Length > 80 || item.PurchaseOrderId == Guid.Empty ||
            item.PartyId == Guid.Empty || item.ItemId == Guid.Empty || createdAt == default ||
            arrivedAt == default || !IsPositiveQuantity(item.ReceivedBaseQuantity) ||
            string.IsNullOrWhiteSpace(item.BaseUnit) || item.BaseUnit.Trim().Length > 24 ||
            item.ConversionVersion <= 0 || !Enum.IsDefined(item.Status) ||
            !balancedStatus ||
            !IsQuantityOrZero(item.AcceptedBaseQuantity) ||
            !IsQuantityOrZero(item.RejectedBaseQuantity) ||
            !IsQuantityOrZero(item.QuarantinedBaseQuantity) ||
            inspectedStatus != inspectedAt.HasValue ||
            inspectedStatus && item.AcceptedBaseQuantity + item.RejectedBaseQuantity +
                item.QuarantinedBaseQuantity != item.ReceivedBaseQuantity ||
            !inspectedStatus && (item.AcceptedBaseQuantity != 0 || item.RejectedBaseQuantity != 0 ||
                item.QuarantinedBaseQuantity != 0) ||
            inspectedAt < createdAt ||
            item.ExcessApprovalId.HasValue != excessApprovedAt.HasValue ||
            excessApprovedAt < createdAt)
        {
            throw Invalid("receipt.invalid", "A Goods Receipt version violates the reporting contract.");
        }

        RequireConfidential(item.Classification, "receipt.classification.invalid");
        return item with
        {
            Number = item.Number.Trim(),
            CreatedAt = createdAt,
            ArrivedAt = arrivedAt,
            BaseUnit = item.BaseUnit.Trim().ToUpperInvariant(),
            InspectedAt = inspectedAt,
            ExcessApprovedAt = excessApprovedAt
        };
    }

    private static ProjectCommercialServiceAcceptanceVersion ValidateAndNormalize(
        ProjectCommercialServiceAcceptanceVersion item,
        Guid tenantId,
        Guid projectId)
    {
        ArgumentNullException.ThrowIfNull(item);
        var verifiedAt = item.VerifiedAt.ToUniversalTime();
        var excessApprovedAt = item.ExcessApprovedAt?.ToUniversalTime();
        if (item.AcceptanceId == Guid.Empty || item.TenantId != tenantId || item.ProjectId != projectId ||
            item.Revision <= 0 || string.IsNullOrWhiteSpace(item.Number) ||
            item.Number.Trim().Length > 80 || item.PurchaseOrderId == Guid.Empty ||
            item.PartyId == Guid.Empty || item.ItemId == Guid.Empty || item.PeriodStart == default ||
            item.PeriodEnd < item.PeriodStart || !IsPositiveQuantity(item.DeliveredBaseQuantity) ||
            !IsQuantityOrZero(item.AcceptedBaseQuantity) || !IsQuantityOrZero(item.RejectedBaseQuantity) ||
            item.AcceptedBaseQuantity + item.RejectedBaseQuantity != item.DeliveredBaseQuantity ||
            string.IsNullOrWhiteSpace(item.BaseUnit) || item.BaseUnit.Trim().Length > 24 ||
            verifiedAt == default || item.ExcessApprovalId.HasValue != excessApprovedAt.HasValue ||
            excessApprovedAt > verifiedAt)
        {
            throw Invalid(
                "service_acceptance.invalid",
                "A Service Acceptance version violates the reporting contract.");
        }

        RequireConfidential(item.Classification, "service_acceptance.classification.invalid");
        return item with
        {
            Number = item.Number.Trim(),
            BaseUnit = item.BaseUnit.Trim().ToUpperInvariant(),
            VerifiedAt = verifiedAt,
            ExcessApprovedAt = excessApprovedAt
        };
    }

    private static ProjectCommercialContractAtCutoff? SelectContract(
        ProjectCommercialContractVersion item,
        DateTimeOffset cutoff,
        IReadOnlyDictionary<Guid, ProjectCommercialPartySnapshotVersion> parties,
        ProjectCommercialReportingSourceCompleteness partyCompleteness)
    {
        if (item.CreatedAt > cutoff)
        {
            return null;
        }

        var state = ContractStateAt(item, cutoff);
        parties.TryGetValue(item.PartyId, out var party);
        if (party is null && partyCompleteness == ProjectCommercialReportingSourceCompleteness.Complete)
        {
            throw Invalid(
                "contract.party_snapshot.missing",
                "An official or pending Contract does not have an effective Party snapshot.");
        }

        return new ProjectCommercialContractAtCutoff(item, state, party);
    }

    private static ProjectCommercialAmendmentAtCutoff? SelectAmendment(
        ProjectCommercialAmendmentVersion item,
        DateTimeOffset cutoff,
        IReadOnlyDictionary<Guid, ProjectCommercialContractAtCutoff> contracts)
    {
        if (item.CreatedAt > cutoff || AmendmentStateAt(item, cutoff) != AmendmentWorkflowState.Approved)
        {
            return null;
        }
        if (!contracts.ContainsKey(item.ContractId))
        {
            throw Invalid(
                "amendment.contract_not_official",
                "An approved Amendment references a Contract that is not official at the cutoff.");
        }

        var approvedAt = item.Lifecycle.Single(eventItem =>
            eventItem.Type == ProjectCommercialAmendmentEventType.Approved).OccurredAt;
        return new ProjectCommercialAmendmentAtCutoff(item, approvedAt);
    }

    private static ProjectCommercialPurchaseOrderAtCutoff? SelectOrder(
        ProjectCommercialPurchaseOrderVersion item,
        DateTimeOffset cutoff,
        IReadOnlyDictionary<Guid, ProjectCommercialPurchaseRequestVersion> requests,
        IReadOnlyDictionary<Guid, ProjectCommercialContractAtCutoff> contracts,
        IReadOnlyDictionary<Guid, ProjectCommercialPartySnapshotVersion> parties,
        IReadOnlyCollection<ProjectCommercialItemSnapshotVersion> items,
        ProjectCommercialReportingSourceCompleteness contractCompleteness,
        ProjectCommercialReportingSourceCompleteness partyCompleteness,
        ProjectCommercialReportingSourceCompleteness itemCompleteness)
    {
        var issuedEvent = item.Lifecycle.Single(eventItem =>
            eventItem.Type == ProjectCommercialPurchaseOrderEventType.Issued);
        if (issuedEvent.OccurredAt > cutoff)
        {
            return null;
        }
        if (!requests.TryGetValue(item.PurchaseRequestId, out var request))
        {
            throw Invalid("order.request.invalid", "A Purchase Order references an unknown Purchase Request.");
        }

        var requestStateAtIssue = RequestStateAt(request, issuedEvent.OccurredAt);
        var approval = request.Lifecycle.SingleOrDefault(eventItem =>
            eventItem.Type == ProjectCommercialPurchaseRequestEventType.Approved);
        var ordered = request.Lifecycle.SingleOrDefault(eventItem =>
            eventItem.Type == ProjectCommercialPurchaseRequestEventType.Ordered);
        if (requestStateAtIssue is not (RequestWorkflowState.Approved or RequestWorkflowState.Ordered) ||
            approval is null || approval.OccurredAt > issuedEvent.OccurredAt || ordered is null ||
            ordered.OccurredAt < issuedEvent.OccurredAt || ordered.OccurredAt > cutoff)
        {
            throw Invalid(
                "order.request_lifecycle.invalid",
                "A Purchase Order was issued without complete approved Request lineage.");
        }

        parties.TryGetValue(item.PartyId, out var party);
        if (party is null && partyCompleteness == ProjectCommercialReportingSourceCompleteness.Complete)
        {
            throw Invalid(
                "order.party_snapshot.missing",
                "A Purchase Order does not have an effective Party snapshot.");
        }

        ProjectCommercialContractAtCutoff? contract = null;
        if (item.ContractId.HasValue)
        {
            contracts.TryGetValue(item.ContractId.Value, out contract);
            if (contract is null &&
                contractCompleteness == ProjectCommercialReportingSourceCompleteness.Complete)
            {
                throw Invalid(
                    "order.contract.invalid",
                    "A Purchase Order references a Contract that is not official at the cutoff.");
            }
            if (contract?.Source.PartyId != item.PartyId)
            {
                throw Invalid(
                    "order.contract_party_mismatch",
                    "A Purchase Order Party does not match its linked Contract Party.");
            }
        }

        ProjectCommercialItemSnapshotVersion? supplyItem = null;
        if (item.ItemId.HasValue)
        {
            supplyItem = items.SingleOrDefault(snapshot =>
                snapshot.ItemId == item.ItemId.Value &&
                IsEffectiveAt(
                    snapshot.EffectiveFromUtc,
                    snapshot.EffectiveToUtc,
                    issuedEvent.OccurredAt));
            if (supplyItem is null && itemCompleteness == ProjectCommercialReportingSourceCompleteness.Complete)
            {
                throw Invalid(
                    "order.item_snapshot.missing",
                    "A quantified Purchase Order does not have an Item snapshot pinned at Issue.");
            }
            if (supplyItem is not null && item.BaseUnit is not null &&
                !string.Equals(supplyItem.BaseUnit, item.BaseUnit, StringComparison.Ordinal))
            {
                throw Invalid(
                    "order.item_unit_mismatch",
                    "A Purchase Order base unit does not match its Item snapshot.");
            }
        }

        return new ProjectCommercialPurchaseOrderAtCutoff(
            item,
            request,
            MapOrderState(OrderStateAt(item, cutoff)),
            issuedEvent.OccurredAt,
            party,
            contract,
            supplyItem);
    }

    private static ProjectCommercialGoodsReceiptAtCutoff? SelectReceipt(
        ProjectCommercialGoodsReceiptVersion item,
        DateTimeOffset cutoff,
        ProjectCommercialConfigurationVersion? configuration,
        IReadOnlyDictionary<Guid, ProjectCommercialPurchaseOrderAtCutoff> orders)
    {
        if (item.CreatedAt > cutoff || item.ArrivedAt > cutoff)
        {
            return null;
        }
        if (configuration is null || !orders.TryGetValue(item.PurchaseOrderId, out var order))
        {
            throw Invalid(
                "receipt.order.invalid",
                "A Goods Receipt references a Purchase Order that is not official at the cutoff.");
        }
        if (order.Source.PartyId != item.PartyId || order.Source.ItemId != item.ItemId)
        {
            throw Invalid(
                "receipt.lineage.invalid",
                "A Goods Receipt does not match its Purchase Order Party and Item lineage.");
        }

        var supplyItem = order.Item;
        if (supplyItem is not null && supplyItem.Kind != SupplyItemKind.Material)
        {
            throw Invalid("receipt.item_kind.invalid", "Goods Receipt evidence may only fulfill a Material order.");
        }
        if (order.Source.BaseUnit is not null &&
            (!string.Equals(order.Source.BaseUnit, item.BaseUnit, StringComparison.Ordinal) ||
                order.Source.ConversionVersion != item.ConversionVersion))
        {
            throw Invalid(
                "receipt.quantity_basis.invalid",
                "A Goods Receipt does not use the Purchase Order pinned quantity basis.");
        }

        var inspected = item.InspectedAt.HasValue && item.InspectedAt.Value <= cutoff;
        var timeZone = FindTimeZone(configuration.TimeZone);
        var deliveryDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(item.ArrivedAt, timeZone).DateTime);
        return new ProjectCommercialGoodsReceiptAtCutoff(
            item,
            deliveryDate,
            inspected,
            inspected ? item.AcceptedBaseQuantity : 0,
            inspected ? item.RejectedBaseQuantity : 0,
            inspected ? item.QuarantinedBaseQuantity : 0);
    }

    private static ProjectCommercialServiceAcceptanceAtCutoff? SelectServiceAcceptance(
        ProjectCommercialServiceAcceptanceVersion item,
        DateTimeOffset cutoff,
        DateOnly cutoffLocalDate,
        IReadOnlyDictionary<Guid, ProjectCommercialPurchaseOrderAtCutoff> orders)
    {
        if (item.VerifiedAt > cutoff || item.PeriodEnd > cutoffLocalDate)
        {
            return null;
        }
        if (!orders.TryGetValue(item.PurchaseOrderId, out var order))
        {
            throw Invalid(
                "service_acceptance.order.invalid",
                "A Service Acceptance references a Purchase Order that is not official at the cutoff.");
        }
        if (order.Source.PartyId != item.PartyId || order.Source.ItemId != item.ItemId)
        {
            throw Invalid(
                "service_acceptance.lineage.invalid",
                "A Service Acceptance does not match its Purchase Order Party and Item lineage.");
        }

        var supplyItem = order.Item;
        if (supplyItem is not null && supplyItem.Kind == SupplyItemKind.Material)
        {
            throw Invalid(
                "service_acceptance.item_kind.invalid",
                "Service Acceptance evidence cannot fulfill a Material order.");
        }
        if (order.Source.BaseUnit is not null &&
            !string.Equals(order.Source.BaseUnit, item.BaseUnit, StringComparison.Ordinal))
        {
            throw Invalid(
                "service_acceptance.quantity_basis.invalid",
                "A Service Acceptance does not use the Purchase Order pinned base unit.");
        }

        return new ProjectCommercialServiceAcceptanceAtCutoff(item);
    }

    private static void ValidateExcessApproval(
        IReadOnlyCollection<ProjectCommercialPurchaseOrderAtCutoff> orders,
        IReadOnlyCollection<ProjectCommercialGoodsReceiptAtCutoff> receipts,
        IReadOnlyCollection<ProjectCommercialServiceAcceptanceAtCutoff> acceptances,
        DateTimeOffset cutoff)
    {
        foreach (var order in orders.Where(item => item.Source.OrderedBaseQuantity.HasValue))
        {
            var evidence = receipts
                .Where(item => item.Source.PurchaseOrderId == order.Source.PurchaseOrderId && item.Inspected)
                .Select(item => new ExcessEvidence(
                    item.DeliveryDate,
                    item.Source.Number,
                    item.Source.ReceiptId,
                    item.AcceptedBaseQuantity,
                    item.Source.ExcessApprovalId,
                    item.Source.ExcessApprovedAt))
                .Concat(acceptances
                    .Where(item => item.Source.PurchaseOrderId == order.Source.PurchaseOrderId)
                    .Select(item => new ExcessEvidence(
                        item.Source.PeriodEnd,
                        item.Source.Number,
                        item.Source.AcceptanceId,
                        item.Source.AcceptedBaseQuantity,
                        item.Source.ExcessApprovalId,
                        item.Source.ExcessApprovedAt)))
                .OrderBy(item => item.DeliveryDate)
                .ThenBy(item => item.Number, StringComparer.Ordinal)
                .ThenBy(item => item.Id.ToString("D"), StringComparer.Ordinal)
                .ToArray();
            var cumulative = 0m;
            foreach (var item in evidence)
            {
                cumulative = CheckedAdd(cumulative, item.AcceptedQuantity, "supply.quantity.overflow");
                if (cumulative > order.Source.OrderedBaseQuantity.Value &&
                    (!item.ExcessApprovalId.HasValue || !item.ExcessApprovedAt.HasValue ||
                        item.ExcessApprovedAt.Value > cutoff))
                {
                    throw Invalid(
                        "supply.excess_approval.missing",
                        "Accepted supply above the ordered quantity has no eligible approval lineage.");
                }
            }
        }
    }

    private static ProjectCommercialContractLifecycleEvent[] NormalizeContractLifecycle(
        IReadOnlyCollection<ProjectCommercialContractLifecycleEvent> events,
        DateTimeOffset createdAt)
    {
        var normalized = events
            .Select(item => item with { OccurredAt = item.OccurredAt.ToUniversalTime() })
            .OrderBy(item => item.Sequence)
            .ToArray();
        ValidateEventSequence(normalized.Select(item => (item.Sequence, item.OccurredAt)), createdAt,
            "contract.lifecycle.invalid");
        var state = ProjectCommercialContractWorkflowState.Draft;
        foreach (var item in normalized)
        {
            if (!Enum.IsDefined(item.Type))
            {
                throw Invalid("contract.lifecycle.invalid", "A Contract lifecycle event type is unknown.");
            }
            state = item.Type switch
            {
                ProjectCommercialContractEventType.Submitted
                    when state is ProjectCommercialContractWorkflowState.Draft or
                        ProjectCommercialContractWorkflowState.Returned =>
                    ProjectCommercialContractWorkflowState.Submitted,
                ProjectCommercialContractEventType.Returned
                    when state == ProjectCommercialContractWorkflowState.Submitted =>
                    ProjectCommercialContractWorkflowState.Returned,
                ProjectCommercialContractEventType.Activated
                    when state is ProjectCommercialContractWorkflowState.Submitted or
                        ProjectCommercialContractWorkflowState.Suspended =>
                    ProjectCommercialContractWorkflowState.Active,
                ProjectCommercialContractEventType.Suspended
                    when state == ProjectCommercialContractWorkflowState.Active =>
                    ProjectCommercialContractWorkflowState.Suspended,
                ProjectCommercialContractEventType.Closed
                    when state is ProjectCommercialContractWorkflowState.Active or
                        ProjectCommercialContractWorkflowState.Suspended =>
                    ProjectCommercialContractWorkflowState.Closed,
                ProjectCommercialContractEventType.Terminated
                    when state is ProjectCommercialContractWorkflowState.Active or
                        ProjectCommercialContractWorkflowState.Suspended =>
                    ProjectCommercialContractWorkflowState.Terminated,
                _ => throw Invalid(
                    "contract.lifecycle.invalid",
                    "Contract lifecycle events do not form an allowed immutable transition chain.")
            };
        }
        return normalized;
    }

    private static ProjectCommercialAmendmentLifecycleEvent[] NormalizeAmendmentLifecycle(
        IReadOnlyCollection<ProjectCommercialAmendmentLifecycleEvent> events,
        DateTimeOffset createdAt)
    {
        var normalized = events
            .Select(item => item with { OccurredAt = item.OccurredAt.ToUniversalTime() })
            .OrderBy(item => item.Sequence)
            .ToArray();
        ValidateEventSequence(normalized.Select(item => (item.Sequence, item.OccurredAt)), createdAt,
            "amendment.lifecycle.invalid");
        var state = AmendmentWorkflowState.Draft;
        foreach (var item in normalized)
        {
            if (!Enum.IsDefined(item.Type))
            {
                throw Invalid("amendment.lifecycle.invalid", "An Amendment lifecycle event type is unknown.");
            }
            state = item.Type switch
            {
                ProjectCommercialAmendmentEventType.Submitted
                    when state is AmendmentWorkflowState.Draft or AmendmentWorkflowState.Returned =>
                    AmendmentWorkflowState.Submitted,
                ProjectCommercialAmendmentEventType.Returned when state == AmendmentWorkflowState.Submitted =>
                    AmendmentWorkflowState.Returned,
                ProjectCommercialAmendmentEventType.Approved when state == AmendmentWorkflowState.Submitted =>
                    AmendmentWorkflowState.Approved,
                _ => throw Invalid(
                    "amendment.lifecycle.invalid",
                    "Amendment lifecycle events do not form an allowed immutable transition chain.")
            };
        }
        return normalized;
    }

    private static ProjectCommercialPurchaseRequestLifecycleEvent[] NormalizeRequestLifecycle(
        IReadOnlyCollection<ProjectCommercialPurchaseRequestLifecycleEvent> events,
        DateTimeOffset createdAt)
    {
        var normalized = events
            .Select(item => item with { OccurredAt = item.OccurredAt.ToUniversalTime() })
            .OrderBy(item => item.Sequence)
            .ToArray();
        ValidateEventSequence(normalized.Select(item => (item.Sequence, item.OccurredAt)), createdAt,
            "request.lifecycle.invalid");
        var state = RequestWorkflowState.Draft;
        foreach (var item in normalized)
        {
            if (!Enum.IsDefined(item.Type))
            {
                throw Invalid("request.lifecycle.invalid", "A Purchase Request lifecycle event type is unknown.");
            }
            state = item.Type switch
            {
                ProjectCommercialPurchaseRequestEventType.Submitted
                    when state is RequestWorkflowState.Draft or RequestWorkflowState.Returned =>
                    RequestWorkflowState.Submitted,
                ProjectCommercialPurchaseRequestEventType.Returned when state == RequestWorkflowState.Submitted =>
                    RequestWorkflowState.Returned,
                ProjectCommercialPurchaseRequestEventType.Approved when state == RequestWorkflowState.Submitted =>
                    RequestWorkflowState.Approved,
                ProjectCommercialPurchaseRequestEventType.Ordered when state == RequestWorkflowState.Approved =>
                    RequestWorkflowState.Ordered,
                ProjectCommercialPurchaseRequestEventType.Cancelled
                    when state is RequestWorkflowState.Draft or RequestWorkflowState.Submitted or
                        RequestWorkflowState.Returned or RequestWorkflowState.Approved =>
                    RequestWorkflowState.Cancelled,
                _ => throw Invalid(
                    "request.lifecycle.invalid",
                    "Purchase Request lifecycle events do not form an allowed immutable transition chain.")
            };
        }
        return normalized;
    }

    private static ProjectCommercialPurchaseOrderLifecycleEvent[] NormalizeOrderLifecycle(
        IReadOnlyCollection<ProjectCommercialPurchaseOrderLifecycleEvent> events)
    {
        var normalized = events
            .Select(item => item with { OccurredAt = item.OccurredAt.ToUniversalTime() })
            .OrderBy(item => item.Sequence)
            .ToArray();
        if (normalized.Length == 0)
        {
            throw Invalid("order.lifecycle.invalid", "A Purchase Order must have an Issue event.");
        }
        ValidateEventSequence(normalized.Select(item => (item.Sequence, item.OccurredAt)),
            normalized[0].OccurredAt, "order.lifecycle.invalid");
        var state = OrderWorkflowState.NotIssued;
        foreach (var item in normalized)
        {
            if (!Enum.IsDefined(item.Type))
            {
                throw Invalid("order.lifecycle.invalid", "A Purchase Order lifecycle event type is unknown.");
            }
            state = item.Type switch
            {
                ProjectCommercialPurchaseOrderEventType.Issued when state == OrderWorkflowState.NotIssued =>
                    OrderWorkflowState.Issued,
                ProjectCommercialPurchaseOrderEventType.Closed when state == OrderWorkflowState.Issued =>
                    OrderWorkflowState.Closed,
                ProjectCommercialPurchaseOrderEventType.Cancelled when state == OrderWorkflowState.Issued =>
                    OrderWorkflowState.Cancelled,
                _ => throw Invalid(
                    "order.lifecycle.invalid",
                    "Purchase Order lifecycle events do not form an allowed immutable transition chain.")
            };
        }
        return normalized;
    }

    private static ProjectCommercialContractWorkflowState ContractStateAt(
        ProjectCommercialContractVersion item,
        DateTimeOffset cutoff)
    {
        var state = ProjectCommercialContractWorkflowState.Draft;
        foreach (var eventItem in item.Lifecycle.Where(eventItem => eventItem.OccurredAt <= cutoff))
        {
            state = eventItem.Type switch
            {
                ProjectCommercialContractEventType.Submitted => ProjectCommercialContractWorkflowState.Submitted,
                ProjectCommercialContractEventType.Returned => ProjectCommercialContractWorkflowState.Returned,
                ProjectCommercialContractEventType.Activated => ProjectCommercialContractWorkflowState.Active,
                ProjectCommercialContractEventType.Suspended => ProjectCommercialContractWorkflowState.Suspended,
                ProjectCommercialContractEventType.Closed => ProjectCommercialContractWorkflowState.Closed,
                ProjectCommercialContractEventType.Terminated => ProjectCommercialContractWorkflowState.Terminated,
                _ => throw Invalid("contract.lifecycle.invalid", "A Contract lifecycle event type is unknown.")
            };
        }
        return state;
    }

    private static AmendmentWorkflowState AmendmentStateAt(
        ProjectCommercialAmendmentVersion item,
        DateTimeOffset cutoff)
    {
        var state = AmendmentWorkflowState.Draft;
        foreach (var eventItem in item.Lifecycle.Where(eventItem => eventItem.OccurredAt <= cutoff))
        {
            state = eventItem.Type switch
            {
                ProjectCommercialAmendmentEventType.Submitted => AmendmentWorkflowState.Submitted,
                ProjectCommercialAmendmentEventType.Returned => AmendmentWorkflowState.Returned,
                ProjectCommercialAmendmentEventType.Approved => AmendmentWorkflowState.Approved,
                _ => throw Invalid("amendment.lifecycle.invalid", "An Amendment lifecycle event type is unknown.")
            };
        }
        return state;
    }

    private static RequestWorkflowState RequestStateAt(
        ProjectCommercialPurchaseRequestVersion item,
        DateTimeOffset cutoff)
    {
        var state = RequestWorkflowState.Draft;
        foreach (var eventItem in item.Lifecycle.Where(eventItem => eventItem.OccurredAt <= cutoff))
        {
            state = eventItem.Type switch
            {
                ProjectCommercialPurchaseRequestEventType.Submitted => RequestWorkflowState.Submitted,
                ProjectCommercialPurchaseRequestEventType.Returned => RequestWorkflowState.Returned,
                ProjectCommercialPurchaseRequestEventType.Approved => RequestWorkflowState.Approved,
                ProjectCommercialPurchaseRequestEventType.Ordered => RequestWorkflowState.Ordered,
                ProjectCommercialPurchaseRequestEventType.Cancelled => RequestWorkflowState.Cancelled,
                _ => throw Invalid("request.lifecycle.invalid", "A Purchase Request lifecycle event type is unknown.")
            };
        }
        return state;
    }

    private static OrderWorkflowState OrderStateAt(
        ProjectCommercialPurchaseOrderVersion item,
        DateTimeOffset cutoff)
    {
        var state = OrderWorkflowState.NotIssued;
        foreach (var eventItem in item.Lifecycle.Where(eventItem => eventItem.OccurredAt <= cutoff))
        {
            state = eventItem.Type switch
            {
                ProjectCommercialPurchaseOrderEventType.Issued => OrderWorkflowState.Issued,
                ProjectCommercialPurchaseOrderEventType.Closed => OrderWorkflowState.Closed,
                ProjectCommercialPurchaseOrderEventType.Cancelled => OrderWorkflowState.Cancelled,
                _ => throw Invalid("order.lifecycle.invalid", "A Purchase Order lifecycle event type is unknown.")
            };
        }
        return state;
    }

    private static ProjectCommercialPurchaseRequestState MapRequestState(RequestWorkflowState state) =>
        state switch
        {
            RequestWorkflowState.Draft => ProjectCommercialPurchaseRequestState.Draft,
            RequestWorkflowState.Submitted => ProjectCommercialPurchaseRequestState.Submitted,
            RequestWorkflowState.Returned => ProjectCommercialPurchaseRequestState.Returned,
            RequestWorkflowState.Approved => ProjectCommercialPurchaseRequestState.Approved,
            RequestWorkflowState.Ordered => ProjectCommercialPurchaseRequestState.Ordered,
            RequestWorkflowState.Cancelled => ProjectCommercialPurchaseRequestState.Cancelled,
            _ => throw Invalid("request.lifecycle.invalid", "A Purchase Request state is unknown.")
        };

    private static ProjectCommercialPurchaseOrderState MapOrderState(OrderWorkflowState state) =>
        state switch
        {
            OrderWorkflowState.Issued => ProjectCommercialPurchaseOrderState.Issued,
            OrderWorkflowState.Closed => ProjectCommercialPurchaseOrderState.Closed,
            OrderWorkflowState.Cancelled => ProjectCommercialPurchaseOrderState.Cancelled,
            _ => throw Invalid("order.lifecycle.invalid", "A Purchase Order is not issued at the cutoff.")
        };

    private static void ValidateCurrencies(
        ProjectCommercialConfigurationVersion configuration,
        IReadOnlyCollection<ProjectCommercialContractVersion> contracts,
        IReadOnlyCollection<ProjectCommercialAmendmentVersion> amendments,
        IReadOnlyCollection<ProjectCommercialPurchaseRequestVersion> requests,
        IReadOnlyCollection<ProjectCommercialPurchaseOrderVersion> orders)
    {
        if (contracts.Any(item => !string.Equals(
                item.CurrencyCode, configuration.BaseCurrencyCode, StringComparison.Ordinal)) ||
            amendments.Any(item => !string.Equals(
                item.CurrencyCode, configuration.BaseCurrencyCode, StringComparison.Ordinal)) ||
            requests.Any(item => !string.Equals(
                item.CurrencyCode, configuration.BaseCurrencyCode, StringComparison.Ordinal)) ||
            orders.Any(item => !string.Equals(
                item.CurrencyCode, configuration.BaseCurrencyCode, StringComparison.Ordinal)))
        {
            throw Invalid(
                "currency_mismatch",
                "Commercial evidence does not use the effective Project Base Currency.");
        }
    }

    private static ProjectCommercialReportingClassification ResolveClassification(
        ProjectCommercialReportingClassification source,
        IEnumerable<ProjectCommercialConfigurationVersion> configurations,
        IEnumerable<ProjectCommercialPartySnapshotVersion> parties,
        IEnumerable<ProjectCommercialItemSnapshotVersion> items,
        IEnumerable<ProjectCommercialContractVersion> contracts,
        IEnumerable<ProjectCommercialAmendmentVersion> amendments,
        IEnumerable<ProjectCommercialPurchaseRequestVersion> requests,
        IEnumerable<ProjectCommercialPurchaseOrderVersion> orders,
        IEnumerable<ProjectCommercialGoodsReceiptVersion> receipts,
        IEnumerable<ProjectCommercialServiceAcceptanceVersion> acceptances) =>
        new[] { source }
            .Concat(configurations.Select(item => item.Classification))
            .Concat(parties.Select(item => item.Classification))
            .Concat(items.Select(item => item.Classification))
            .Concat(contracts.Select(item => item.Classification))
            .Concat(amendments.Select(item => item.Classification))
            .Concat(requests.Select(item => item.Classification))
            .Concat(orders.Select(item => item.Classification))
            .Concat(receipts.Select(item => item.Classification))
            .Concat(acceptances.Select(item => item.Classification))
            .Max();

    private static DateTimeOffset? ResolveSourceMaxChangedAt(
        IEnumerable<ProjectCommercialConfigurationVersion> configurations,
        IEnumerable<ProjectCommercialPartySnapshotVersion> parties,
        IEnumerable<ProjectCommercialItemSnapshotVersion> items,
        IEnumerable<ProjectCommercialContractVersion> contracts,
        IEnumerable<ProjectCommercialAmendmentVersion> amendments,
        IEnumerable<ProjectCommercialPurchaseRequestVersion> requests,
        IEnumerable<ProjectCommercialPurchaseOrderVersion> orders,
        IEnumerable<ProjectCommercialGoodsReceiptVersion> receipts,
        IEnumerable<ProjectCommercialServiceAcceptanceVersion> acceptances,
        DateTimeOffset cutoff)
    {
        var values = new List<DateTimeOffset>();
        foreach (var item in configurations)
        {
            AddAtOrBefore(values, item.EffectiveFromUtc, cutoff);
            AddAtOrBefore(values, item.EffectiveToUtc, cutoff);
        }
        foreach (var item in parties)
        {
            AddAtOrBefore(values, item.EffectiveFromUtc, cutoff);
            AddAtOrBefore(values, item.EffectiveToUtc, cutoff);
        }
        foreach (var item in items)
        {
            AddAtOrBefore(values, item.EffectiveFromUtc, cutoff);
            AddAtOrBefore(values, item.EffectiveToUtc, cutoff);
        }
        foreach (var item in contracts)
        {
            AddAtOrBefore(values, item.CreatedAt, cutoff);
            foreach (var eventItem in item.Lifecycle)
            {
                AddAtOrBefore(values, eventItem.OccurredAt, cutoff);
            }
        }
        foreach (var item in amendments)
        {
            AddAtOrBefore(values, item.CreatedAt, cutoff);
            foreach (var eventItem in item.Lifecycle)
            {
                AddAtOrBefore(values, eventItem.OccurredAt, cutoff);
            }
        }
        foreach (var item in requests)
        {
            AddAtOrBefore(values, item.CreatedAt, cutoff);
            foreach (var eventItem in item.Lifecycle)
            {
                AddAtOrBefore(values, eventItem.OccurredAt, cutoff);
            }
        }
        foreach (var item in orders)
        {
            foreach (var eventItem in item.Lifecycle)
            {
                AddAtOrBefore(values, eventItem.OccurredAt, cutoff);
            }
        }
        foreach (var item in receipts)
        {
            AddAtOrBefore(values, item.CreatedAt, cutoff);
            AddAtOrBefore(values, item.ArrivedAt, cutoff);
            AddAtOrBefore(values, item.InspectedAt, cutoff);
            AddAtOrBefore(values, item.ExcessApprovedAt, cutoff);
        }
        foreach (var item in acceptances)
        {
            AddAtOrBefore(values, item.VerifiedAt, cutoff);
            AddAtOrBefore(values, item.ExcessApprovedAt, cutoff);
        }
        return values.Count == 0 ? null : values.Max().ToUniversalTime();
    }

    private static ProjectCommercialProcurementSupplySourceManifest BuildManifest(
        ProjectCommercialProcurementSupplyReportingProjection projection,
        DateTimeOffset cutoff,
        ProjectCommercialConfigurationVersion? configuration,
        IEnumerable<ProjectCommercialPartySnapshotVersion> parties,
        IEnumerable<ProjectCommercialItemSnapshotVersion> items,
        IEnumerable<ProjectCommercialContractVersion> contracts,
        IEnumerable<ProjectCommercialAmendmentVersion> amendments,
        IEnumerable<ProjectCommercialPurchaseRequestVersion> requests,
        IEnumerable<ProjectCommercialPurchaseOrderVersion> orders,
        IEnumerable<ProjectCommercialGoodsReceiptVersion> receipts,
        IEnumerable<ProjectCommercialServiceAcceptanceVersion> acceptances) => new(
        ProjectCommercialProcurementSupplyReportingContract.SourceManifestVersion,
        ProjectCommercialProcurementSupplyReportingContract.Version,
        ProjectCommercialProcurementSupplyReportingContract.PolicyVersion,
        projection.TenantId,
        projection.ProjectId,
        projection.CutoffLocalDate,
        cutoff,
        configuration is null
            ? null
            : new ProjectCommercialConfigurationManifest(
                configuration.ConfigurationVersion,
                configuration.ProjectRevision,
                configuration.ContractModel,
                configuration.ContractState,
                configuration.ProcurementState,
                configuration.TimeZone,
                configuration.BaseCurrencyCode,
                configuration.EffectiveFromUtc,
                configuration.EffectiveToUtc),
        Manifest(parties, item => item.PartyId, item => item.Revision),
        Manifest(items, item => item.ItemId, item => item.Revision),
        Manifest(contracts, item => item.ContractId, item => item.Revision),
        Manifest(amendments, item => item.AmendmentId, item => item.Revision),
        Manifest(requests, item => item.PurchaseRequestId, item => item.Revision),
        Manifest(orders, item => item.PurchaseOrderId, item => item.Revision),
        Manifest(receipts, item => item.ReceiptId, item => item.Revision),
        Manifest(acceptances, item => item.AcceptanceId, item => item.Revision),
        projection.ContractLifecycleCompleteness,
        projection.AmendmentLifecycleCompleteness,
        projection.ProcurementLifecycleCompleteness,
        projection.PartySnapshotCompleteness,
        projection.ItemSnapshotCompleteness,
        projection.ReceiptInspectionCompleteness,
        projection.ServiceAcceptanceCompleteness);

    private static ProjectCommercialSourceEntryManifest[] Manifest<T>(
        IEnumerable<T> values,
        Func<T, Guid> id,
        Func<T, long> revision) => values
        .Select(item => new ProjectCommercialSourceEntryManifest(
            id(item),
            revision(item),
            ProjectCommercialProcurementSupplyCanonicalJson.Sha256(
                ProjectCommercialProcurementSupplyCanonicalJson.Serialize(item))))
        .OrderBy(item => item.SourceId.ToString("D"), StringComparer.Ordinal)
        .ThenBy(item => item.Revision)
        .ToArray();

    private static void ValidateEventSequence(
        IEnumerable<(long Sequence, DateTimeOffset OccurredAt)> events,
        DateTimeOffset notBefore,
        string suffix)
    {
        var sequence = events.ToArray();
        if (sequence.Any(item => item.Sequence <= 0 || item.OccurredAt == default ||
                item.OccurredAt < notBefore) ||
            sequence.Select((item, index) => item.Sequence != index + 1L).Any(item => item) ||
            sequence.Select(item => item.Sequence).Distinct().Count() != sequence.Length ||
            sequence.Zip(sequence.Skip(1)).Any(pair =>
                pair.Second.Sequence <= pair.First.Sequence ||
                pair.Second.OccurredAt < pair.First.OccurredAt))
        {
            throw Invalid(suffix, "Lifecycle events are not a complete ordered immutable history.");
        }
    }

    private static void EnsureSnapshotWindows(
        IEnumerable<IEnumerable<(DateTimeOffset EffectiveFromUtc, DateTimeOffset? EffectiveToUtc)>> groups,
        string suffix,
        string message)
    {
        foreach (var group in groups)
        {
            EnsureNonOverlapping(group, suffix, message);
        }
    }

    private static void EnsureNonOverlapping(
        IEnumerable<(DateTimeOffset EffectiveFromUtc, DateTimeOffset? EffectiveToUtc)> windows,
        string suffix,
        string message)
    {
        var ordered = windows.OrderBy(item => item.EffectiveFromUtc).ToArray();
        for (var index = 1; index < ordered.Length; index++)
        {
            if (!ordered[index - 1].EffectiveToUtc.HasValue ||
                ordered[index].EffectiveFromUtc < ordered[index - 1].EffectiveToUtc.Value)
            {
                throw Invalid(suffix, message);
            }
        }
    }

    private static void EnsureDistinct<T>(
        IEnumerable<T> values,
        string suffix,
        string message)
        where T : notnull
    {
        var all = values.ToArray();
        if (all.Distinct().Count() != all.Length)
        {
            throw Invalid(suffix, message);
        }
    }

    private static bool IsEffectiveAt(
        DateTimeOffset effectiveFromUtc,
        DateTimeOffset? effectiveToUtc,
        DateTimeOffset cutoffUtc) =>
        effectiveFromUtc <= cutoffUtc && (!effectiveToUtc.HasValue || cutoffUtc < effectiveToUtc.Value);

    private static void ValidateLocalCutoff(
        string timeZoneId,
        DateTimeOffset cutoff,
        DateOnly cutoffLocalDate)
    {
        var derived = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(cutoff, FindTimeZone(timeZoneId)).DateTime);
        if (derived != cutoffLocalDate)
        {
            throw Invalid(
                "cutoff.invalid",
                "The local cutoff date does not match the versioned Project time zone.");
        }
    }

    private static void ValidateTimeZone(string timeZoneId) => _ = FindTimeZone(timeZoneId);

    private static TimeZoneInfo FindTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            throw Invalid("time_zone.invalid", "The Project time zone is unknown or invalid.");
        }
        catch (InvalidTimeZoneException)
        {
            throw Invalid("time_zone.invalid", "The Project time zone is unknown or invalid.");
        }
    }

    private static string NormalizeCurrency(string value, string suffix)
    {
        var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length != 3 || normalized.Any(character => !char.IsAsciiLetterUpper(character)))
        {
            throw Invalid(suffix, "Currency must be a three-letter uppercase ISO-style code.");
        }
        return normalized;
    }

    private static string? OptionalUnit(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static bool IsPositiveMoney(decimal value) => value > 0 && IsMoney(value);

    private static bool IsMoney(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero) == value;

    private static bool IsPositiveQuantity(decimal value) => value > 0 && IsQuantityOrZero(value);

    private static bool IsQuantityOrZero(decimal value) =>
        value >= 0 && decimal.Round(value, 6, MidpointRounding.AwayFromZero) == value;

    private static decimal CheckedAdd(decimal left, decimal right, string suffix)
    {
        try
        {
            return checked(left + right);
        }
        catch (OverflowException)
        {
            throw Invalid(suffix, "A Commercial quantity exceeds the supported numeric range.");
        }
    }

    private static void AddAtOrBefore(
        ICollection<DateTimeOffset> values,
        DateTimeOffset? candidate,
        DateTimeOffset cutoff)
    {
        if (candidate.HasValue && candidate.Value.ToUniversalTime() <= cutoff)
        {
            values.Add(candidate.Value.ToUniversalTime());
        }
    }

    private static void RequireConfidential(
        ProjectCommercialReportingClassification classification,
        string suffix)
    {
        if (!Enum.IsDefined(classification) ||
            classification < ProjectCommercialReportingClassification.Confidential)
        {
            throw Invalid(
                suffix,
                "Certified Commercial evidence must have an explicit Confidential or Restricted classification.");
        }
    }

    private static DomainRuleException Invalid(string suffix, string message) =>
        new($"commercial.project_commercial_procurement_supply_reporting.{suffix}", message);

    private enum AmendmentWorkflowState
    {
        Draft,
        Submitted,
        Returned,
        Approved
    }

    private enum RequestWorkflowState
    {
        Draft,
        Submitted,
        Returned,
        Approved,
        Ordered,
        Cancelled
    }

    private enum OrderWorkflowState
    {
        NotIssued,
        Issued,
        Closed,
        Cancelled
    }

    private sealed record ExcessEvidence(
        DateOnly DeliveryDate,
        string Number,
        Guid Id,
        decimal AcceptedQuantity,
        Guid? ExcessApprovalId,
        DateTimeOffset? ExcessApprovedAt);
}

internal sealed record ProjectCommercialProcurementSupplyReportingSelection(
    string ContractVersion,
    Guid TenantId,
    Guid ProjectId,
    DateOnly CutoffLocalDate,
    DateTimeOffset SourceCutoffUtc,
    ProjectCommercialConfigurationVersion? Configuration,
    IReadOnlyCollection<ProjectCommercialContractAtCutoff> OfficialContracts,
    int PendingContractCount,
    IReadOnlyCollection<ProjectCommercialAmendmentAtCutoff> ApprovedAmendments,
    int PendingAmendmentCount,
    IReadOnlyCollection<ProjectCommercialPurchaseRequestAtCutoff> PurchaseRequests,
    IReadOnlyCollection<ProjectCommercialPurchaseOrderAtCutoff> PurchaseOrders,
    IReadOnlyCollection<ProjectCommercialGoodsReceiptAtCutoff> GoodsReceipts,
    IReadOnlyCollection<ProjectCommercialServiceAcceptanceAtCutoff> ServiceAcceptances,
    ProjectCommercialReportingSourceCompleteness ContractLifecycleCompleteness,
    ProjectCommercialReportingSourceCompleteness AmendmentLifecycleCompleteness,
    ProjectCommercialReportingSourceCompleteness ProcurementLifecycleCompleteness,
    ProjectCommercialReportingSourceCompleteness PartySnapshotCompleteness,
    ProjectCommercialReportingSourceCompleteness ItemSnapshotCompleteness,
    ProjectCommercialReportingSourceCompleteness ReceiptInspectionCompleteness,
    ProjectCommercialReportingSourceCompleteness ServiceAcceptanceCompleteness,
    ProjectCommercialReportingClassification Classification,
    ProjectCommercialSourceCounts SourceCounts,
    DateTimeOffset? SourceMaxChangedAt,
    ProjectCommercialProcurementSupplySourceManifest SourceManifest,
    string SourceManifestSha256);

internal sealed record ProjectCommercialContractAtCutoff(
    ProjectCommercialContractVersion Source,
    ProjectCommercialContractWorkflowState State,
    ProjectCommercialPartySnapshotVersion? Party);

internal enum ProjectCommercialContractWorkflowState
{
    Draft,
    Submitted,
    Returned,
    Active,
    Suspended,
    Closed,
    Terminated
}

internal sealed record ProjectCommercialAmendmentAtCutoff(
    ProjectCommercialAmendmentVersion Source,
    DateTimeOffset ApprovedAt);

internal sealed record ProjectCommercialPurchaseRequestAtCutoff(
    ProjectCommercialPurchaseRequestVersion Source,
    ProjectCommercialPurchaseRequestState State);

internal sealed record ProjectCommercialPurchaseOrderAtCutoff(
    ProjectCommercialPurchaseOrderVersion Source,
    ProjectCommercialPurchaseRequestVersion Request,
    ProjectCommercialPurchaseOrderState State,
    DateTimeOffset IssuedAt,
    ProjectCommercialPartySnapshotVersion? Party,
    ProjectCommercialContractAtCutoff? Contract,
    ProjectCommercialItemSnapshotVersion? Item);

internal sealed record ProjectCommercialGoodsReceiptAtCutoff(
    ProjectCommercialGoodsReceiptVersion Source,
    DateOnly DeliveryDate,
    bool Inspected,
    decimal AcceptedBaseQuantity,
    decimal RejectedBaseQuantity,
    decimal QuarantinedBaseQuantity);

internal sealed record ProjectCommercialServiceAcceptanceAtCutoff(
    ProjectCommercialServiceAcceptanceVersion Source);

internal static class ProjectCommercialProcurementSupplyCanonicalJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize<T>(T value)
    {
        var element = JsonSerializer.SerializeToElement(value, SerializerOptions);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonical(writer, element);
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string Sha256(string canonicalJson) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson))).ToLowerInvariant();

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(
                    item => item.Name,
                    StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(writer, item);
                }
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText(), skipInputValidation: true);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                writer.WriteNullValue();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(element), element.ValueKind, null);
        }
    }
}

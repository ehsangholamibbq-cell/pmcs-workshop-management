using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Commercial.Contracts;
using Pmcs.Modules.Commercial.Domain;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Commercial.Services;

internal static class ProjectCommercialProcurementSupplyReportingCalculator
{
    public static ProjectCommercialProcurementSupplyReportingResult Calculate(
        ProjectCommercialProcurementSupplyReportingSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ValidateSelection(selection);

        var reasons = new HashSet<ProjectCommercialReportingReasonCode>();
        var contractStatus = ResolveContractStatus(selection, reasons);
        var procurementStatus = ResolveProcurementStatus(selection, reasons);
        var supplyStatus = ResolveSupplyStatus(selection, procurementStatus, reasons);

        var contract = CalculateContracts(selection, contractStatus, reasons);
        var orders = CalculateOrders(selection, procurementStatus, supplyStatus, reasons);
        var procurement = CalculateProcurement(selection, procurementStatus);
        var supplySummaries = CalculateSupplySummaries(selection, orders, supplyStatus);
        var supplierPerformance = CalculateSupplierPerformance(selection, orders, procurementStatus);
        var dataStatus = ResolveDataStatus(contractStatus, procurementStatus, supplyStatus);

        return new ProjectCommercialProcurementSupplyReportingResult(
            ProjectCommercialProcurementSupplyReportingContract.Version,
            ProjectCommercialProcurementSupplyReportingContract.PolicyVersion,
            selection.TenantId,
            selection.ProjectId,
            selection.CutoffLocalDate,
            selection.SourceCutoffUtc,
            selection.Classification,
            dataStatus,
            reasons.OrderBy(item => item).ToArray(),
            selection.Configuration,
            contractStatus,
            contract.Summary,
            contract.Contracts,
            contract.Amendments,
            procurementStatus,
            procurement,
            orders,
            supplyStatus,
            supplySummaries,
            supplierPerformance,
            selection.SourceCounts,
            selection.SourceMaxChangedAt,
            selection.SourceManifest,
            selection.SourceManifestSha256);
    }

    private static ProjectCommercialReportingSectionStatus ResolveContractStatus(
        ProjectCommercialProcurementSupplyReportingSelection selection,
        HashSet<ProjectCommercialReportingReasonCode> reasons)
    {
        if (selection.Configuration is null ||
            selection.Configuration.ContractModel == ContractModel.NotConfigured ||
            selection.Configuration.ContractState is ProjectFeatureState.NotConfigured or
                ProjectFeatureState.NotEnabled)
        {
            reasons.Add(ProjectCommercialReportingReasonCode.CommercialReportingNotConfigured);
            return ProjectCommercialReportingSectionStatus.NotConfigured;
        }
        if (selection.Configuration.ContractState == ProjectFeatureState.SetupRequired)
        {
            reasons.Add(ProjectCommercialReportingReasonCode.ContractSetupRequired);
            return ProjectCommercialReportingSectionStatus.SetupRequired;
        }
        if (selection.Configuration.ContractState == ProjectFeatureState.Suspended)
        {
            reasons.Add(ProjectCommercialReportingReasonCode.ContractSuspended);
            return ProjectCommercialReportingSectionStatus.Suspended;
        }
        if (selection.Configuration.ContractState != ProjectFeatureState.Active)
        {
            throw Invalid("configuration.contract_state.invalid", "The Contract feature state is unknown.");
        }

        var incomplete = false;
        if (selection.ContractLifecycleCompleteness == ProjectCommercialReportingSourceCompleteness.Incomplete)
        {
            reasons.Add(ProjectCommercialReportingReasonCode.ContractLifecycleIncomplete);
            incomplete = true;
        }
        if (selection.AmendmentLifecycleCompleteness == ProjectCommercialReportingSourceCompleteness.Incomplete)
        {
            reasons.Add(ProjectCommercialReportingReasonCode.AmendmentLifecycleIncomplete);
            incomplete = true;
        }
        if (selection.PartySnapshotCompleteness == ProjectCommercialReportingSourceCompleteness.Incomplete ||
            selection.OfficialContracts.Any(item => item.Party is null))
        {
            reasons.Add(ProjectCommercialReportingReasonCode.PartySnapshotUnavailable);
            incomplete = true;
        }
        if (incomplete)
        {
            return ProjectCommercialReportingSectionStatus.InsufficientData;
        }
        if (selection.OfficialContracts.Count == 0)
        {
            reasons.Add(ProjectCommercialReportingReasonCode.OfficialContractsMissing);
            return ProjectCommercialReportingSectionStatus.NoData;
        }
        return ProjectCommercialReportingSectionStatus.Available;
    }

    private static ProjectCommercialReportingSectionStatus ResolveProcurementStatus(
        ProjectCommercialProcurementSupplyReportingSelection selection,
        HashSet<ProjectCommercialReportingReasonCode> reasons)
    {
        if (selection.Configuration is null ||
            selection.Configuration.ProcurementState is ProjectFeatureState.NotConfigured or
                ProjectFeatureState.NotEnabled)
        {
            reasons.Add(ProjectCommercialReportingReasonCode.CommercialReportingNotConfigured);
            return ProjectCommercialReportingSectionStatus.NotConfigured;
        }
        if (selection.Configuration.ProcurementState == ProjectFeatureState.SetupRequired)
        {
            reasons.Add(ProjectCommercialReportingReasonCode.ProcurementSetupRequired);
            return ProjectCommercialReportingSectionStatus.SetupRequired;
        }
        if (selection.Configuration.ProcurementState == ProjectFeatureState.Suspended)
        {
            reasons.Add(ProjectCommercialReportingReasonCode.ProcurementSuspended);
            return ProjectCommercialReportingSectionStatus.Suspended;
        }
        if (selection.Configuration.ProcurementState != ProjectFeatureState.Active)
        {
            throw Invalid(
                "configuration.procurement_state.invalid",
                "The Procurement feature state is unknown.");
        }

        if (selection.ProcurementLifecycleCompleteness ==
                ProjectCommercialReportingSourceCompleteness.Incomplete ||
            selection.PartySnapshotCompleteness == ProjectCommercialReportingSourceCompleteness.Incomplete ||
            selection.PurchaseOrders.Any(item => item.Party is null ||
                item.Source.ContractId.HasValue && item.Contract is null))
        {
            reasons.Add(ProjectCommercialReportingReasonCode.ProcurementLifecycleIncomplete);
            if (selection.PartySnapshotCompleteness == ProjectCommercialReportingSourceCompleteness.Incomplete ||
                selection.PurchaseOrders.Any(item => item.Party is null))
            {
                reasons.Add(ProjectCommercialReportingReasonCode.PartySnapshotUnavailable);
            }
            return ProjectCommercialReportingSectionStatus.InsufficientData;
        }
        if (selection.PurchaseRequests.Count == 0 && selection.PurchaseOrders.Count == 0)
        {
            reasons.Add(ProjectCommercialReportingReasonCode.OfficialProcurementMissing);
            return ProjectCommercialReportingSectionStatus.NoData;
        }
        return ProjectCommercialReportingSectionStatus.Available;
    }

    private static ProjectCommercialReportingSectionStatus ResolveSupplyStatus(
        ProjectCommercialProcurementSupplyReportingSelection selection,
        ProjectCommercialReportingSectionStatus procurementStatus,
        HashSet<ProjectCommercialReportingReasonCode> reasons)
    {
        var missingPinnedBasis = selection.PurchaseOrders.Any(item =>
            item.Source.OrderedQuantity.HasValue && !item.Source.OrderedBaseQuantity.HasValue);
        if (missingPinnedBasis)
        {
            reasons.Add(ProjectCommercialReportingReasonCode.UnitConversionHistoryUnavailable);
            reasons.Add(ProjectCommercialReportingReasonCode.OrderQuantityBasisUnavailable);
        }
        if (procurementStatus is ProjectCommercialReportingSectionStatus.NotConfigured or
            ProjectCommercialReportingSectionStatus.SetupRequired or
            ProjectCommercialReportingSectionStatus.Suspended)
        {
            return procurementStatus;
        }
        if (procurementStatus == ProjectCommercialReportingSectionStatus.InsufficientData)
        {
            reasons.Add(ProjectCommercialReportingReasonCode.SupplyLineageIncomplete);
            return ProjectCommercialReportingSectionStatus.InsufficientData;
        }

        var missingItem = selection.PurchaseOrders.Any(item => item.Source.ItemId.HasValue && item.Item is null);
        var incomplete = selection.ItemSnapshotCompleteness ==
                ProjectCommercialReportingSourceCompleteness.Incomplete ||
            selection.ReceiptInspectionCompleteness ==
                ProjectCommercialReportingSourceCompleteness.Incomplete ||
            selection.ServiceAcceptanceCompleteness ==
                ProjectCommercialReportingSourceCompleteness.Incomplete ||
            missingItem || missingPinnedBasis;
        if (incomplete)
        {
            reasons.Add(ProjectCommercialReportingReasonCode.SupplyLineageIncomplete);
            return ProjectCommercialReportingSectionStatus.InsufficientData;
        }
        if (selection.GoodsReceipts.Count == 0 && selection.ServiceAcceptances.Count == 0)
        {
            reasons.Add(ProjectCommercialReportingReasonCode.OfficialSupplyEvidenceMissing);
            return ProjectCommercialReportingSectionStatus.NoData;
        }
        return ProjectCommercialReportingSectionStatus.Available;
    }

    private static ContractCalculation CalculateContracts(
        ProjectCommercialProcurementSupplyReportingSelection selection,
        ProjectCommercialReportingSectionStatus status,
        HashSet<ProjectCommercialReportingReasonCode> reasons)
    {
        if (status != ProjectCommercialReportingSectionStatus.Available)
        {
            return new ContractCalculation(null, [], []);
        }

        var rows = new List<ProjectCommercialContractReportRow>();
        foreach (var contract in selection.OfficialContracts)
        {
            var amendments = selection.ApprovedAmendments
                .Where(item => item.Source.ContractId == contract.Source.ContractId)
                .ToArray();
            var amountDelta = CheckedSum(
                amendments.Where(item => item.Source.Type is ContractAmendmentType.ValueChange or
                        ContractAmendmentType.Mixed)
                    .Select(item => item.Source.AmountDelta!.Value),
                "contract.amount.overflow");
            var extensionDays = CheckedSum(
                amendments.Where(item => item.Source.Type is ContractAmendmentType.TimeExtension or
                        ContractAmendmentType.Mixed)
                    .Select(item => item.Source.ExtensionDays!.Value),
                "contract.extension.overflow");
            decimal? effectiveAmount = null;
            if (contract.Source.OriginalApprovedAmount.HasValue)
            {
                effectiveAmount = CheckedAdd(
                    contract.Source.OriginalApprovedAmount.Value,
                    amountDelta,
                    "contract.amount.overflow");
                if (effectiveAmount.HasValue && effectiveAmount.Value < 0)
                {
                    throw Invalid(
                        "contract.effective_amount.negative",
                        "Approved Amendments make a Contract ceiling negative.");
                }
            }
            else
            {
                reasons.Add(ProjectCommercialReportingReasonCode.ContractCeilingUnavailable);
            }

            DateOnly? effectiveEndDate = null;
            if (contract.Source.OriginalEndDate.HasValue)
            {
                try
                {
                    effectiveEndDate = contract.Source.OriginalEndDate.Value.AddDays(extensionDays);
                }
                catch (ArgumentOutOfRangeException)
                {
                    throw Invalid(
                        "contract.end_date.overflow",
                        "Approved Contract extensions exceed the supported date range.");
                }
            }
            else
            {
                reasons.Add(ProjectCommercialReportingReasonCode.ContractEndDateUnavailable);
            }

            var state = contract.State switch
            {
                ProjectCommercialContractWorkflowState.Active
                    when effectiveEndDate.HasValue &&
                        effectiveEndDate.Value < selection.CutoffLocalDate =>
                    ProjectCommercialContractState.Expired,
                ProjectCommercialContractWorkflowState.Active => ProjectCommercialContractState.Active,
                ProjectCommercialContractWorkflowState.Suspended => ProjectCommercialContractState.Suspended,
                ProjectCommercialContractWorkflowState.Closed => ProjectCommercialContractState.Closed,
                ProjectCommercialContractWorkflowState.Terminated => ProjectCommercialContractState.Terminated,
                _ => throw Invalid("contract.state.invalid", "An official Contract state is invalid.")
            };
            var party = contract.Party ?? throw Invalid(
                "contract.party_snapshot.missing",
                "An available Contract has no effective Party snapshot.");
            rows.Add(new ProjectCommercialContractReportRow(
                contract.Source.ContractId,
                contract.Source.Number,
                contract.Source.Title,
                party.Code,
                party.Name,
                party.Type,
                contract.Source.Type,
                state,
                contract.Source.StartDate,
                contract.Source.OriginalEndDate,
                effectiveEndDate,
                contract.Source.OriginalApprovedAmount,
                amountDelta,
                effectiveAmount,
                amendments.Length,
                extensionDays));
        }

        var orderedRows = rows
            .OrderBy(item => item.Number, StringComparer.Ordinal)
            .ThenBy(item => item.ContractId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        var effectiveAmounts = orderedRows
            .Where(item => item.EffectiveApprovedAmount.HasValue)
            .Select(item => item.EffectiveApprovedAmount!.Value)
            .ToArray();
        var knownSubtotal = CheckedSum(effectiveAmounts, "contract.amount.overflow");
        var completeTotal = effectiveAmounts.Length == orderedRows.Length ? knownSubtotal : (decimal?)null;
        var approvedAmendments = selection.ApprovedAmendments
            .Select(item => new ProjectCommercialAmendmentReportRow(
                item.Source.AmendmentId,
                item.Source.ContractId,
                orderedRows.Single(contract => contract.ContractId == item.Source.ContractId).Number,
                item.Source.Number,
                item.Source.Title,
                item.Source.Type,
                item.Source.AmountDelta,
                item.Source.ExtensionDays,
                item.ApprovedAt))
            .OrderBy(item => item.ApprovedAt)
            .ThenBy(item => item.Number, StringComparer.Ordinal)
            .ThenBy(item => item.AmendmentId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        var summary = new ProjectCommercialContractSummary(
            orderedRows.Length,
            orderedRows.Count(item => item.State == ProjectCommercialContractState.Active),
            orderedRows.Count(item => item.State == ProjectCommercialContractState.Suspended),
            orderedRows.Count(item => item.State == ProjectCommercialContractState.Closed),
            orderedRows.Count(item => item.State == ProjectCommercialContractState.Terminated),
            orderedRows.Count(item => item.State == ProjectCommercialContractState.Expired),
            selection.PendingContractCount,
            approvedAmendments.Length,
            selection.PendingAmendmentCount,
            CheckedSum(approvedAmendments.Where(item => item.AmountDelta.HasValue)
                .Select(item => item.AmountDelta!.Value), "contract.amount.overflow"),
            CheckedSum(approvedAmendments.Where(item => item.ExtensionDays.HasValue)
                .Select(item => item.ExtensionDays!.Value), "contract.extension.overflow"),
            knownSubtotal,
            completeTotal);
        return new ContractCalculation(summary, orderedRows, approvedAmendments);
    }

    private static ProjectCommercialProcurementSummary? CalculateProcurement(
        ProjectCommercialProcurementSupplyReportingSelection selection,
        ProjectCommercialReportingSectionStatus status)
    {
        if (status != ProjectCommercialReportingSectionStatus.Available)
        {
            return null;
        }

        var requests = selection.PurchaseRequests;
        var orders = selection.PurchaseOrders;
        var orderedRequestIds = orders.Select(item => item.Source.PurchaseRequestId).ToHashSet();
        return new ProjectCommercialProcurementSummary(
            requests.Count(item => item.State == ProjectCommercialPurchaseRequestState.Draft),
            requests.Count(item => item.State == ProjectCommercialPurchaseRequestState.Submitted),
            requests.Count(item => item.State == ProjectCommercialPurchaseRequestState.Returned),
            requests.Count(item => item.State == ProjectCommercialPurchaseRequestState.Approved),
            requests.Count(item => item.State == ProjectCommercialPurchaseRequestState.Ordered),
            requests.Count(item => item.State == ProjectCommercialPurchaseRequestState.Cancelled),
            requests.Count(item => item.State == ProjectCommercialPurchaseRequestState.Approved &&
                !orderedRequestIds.Contains(item.Source.PurchaseRequestId)),
            orders.Count(item => item.State == ProjectCommercialPurchaseOrderState.Issued),
            orders.Count(item => item.State == ProjectCommercialPurchaseOrderState.Closed),
            orders.Count(item => item.State == ProjectCommercialPurchaseOrderState.Cancelled),
            CheckedSum(orders.Where(item => item.State is ProjectCommercialPurchaseOrderState.Issued or
                    ProjectCommercialPurchaseOrderState.Closed)
                .Select(item => item.Source.Amount), "order.amount.overflow"),
            CheckedSum(orders.Where(item => item.State == ProjectCommercialPurchaseOrderState.Issued)
                .Select(item => item.Source.Amount), "order.amount.overflow"));
    }

    private static ProjectCommercialPurchaseOrderReportRow[] CalculateOrders(
        ProjectCommercialProcurementSupplyReportingSelection selection,
        ProjectCommercialReportingSectionStatus procurementStatus,
        ProjectCommercialReportingSectionStatus supplyStatus,
        HashSet<ProjectCommercialReportingReasonCode> reasons)
    {
        if (procurementStatus != ProjectCommercialReportingSectionStatus.Available)
        {
            return [];
        }

        var rows = new List<ProjectCommercialPurchaseOrderReportRow>();
        foreach (var order in selection.PurchaseOrders)
        {
            var receipts = selection.GoodsReceipts
                .Where(item => item.Source.PurchaseOrderId == order.Source.PurchaseOrderId)
                .ToArray();
            var acceptances = selection.ServiceAcceptances
                .Where(item => item.Source.PurchaseOrderId == order.Source.PurchaseOrderId)
                .ToArray();
            if (receipts.Any(item => item.Inspected is false))
            {
                reasons.Add(ProjectCommercialReportingReasonCode.PendingInspection);
            }
            if (receipts.Any(item => item.RejectedBaseQuantity > 0 ||
                    item.QuarantinedBaseQuantity > 0) ||
                acceptances.Any(item => item.Source.RejectedBaseQuantity > 0))
            {
                reasons.Add(ProjectCommercialReportingReasonCode.RejectedOrQuarantinedSupply);
            }

            var basisComplete = order.Source.OrderedBaseQuantity.HasValue &&
                order.Source.BaseUnit is not null && order.Source.ConversionVersion.HasValue &&
                order.Item is not null &&
                supplyStatus != ProjectCommercialReportingSectionStatus.InsufficientData;
            decimal? delivered = null;
            decimal? accepted = null;
            decimal? rejected = null;
            decimal? quarantined = null;
            decimal? remaining = null;
            decimal? excess = null;
            decimal? fulfillment = null;
            DateOnly? completionDate = null;
            var deliveryStatus = ProjectCommercialDeliveryStatus.NotAssessable;
            if (basisComplete)
            {
                delivered = CheckedSum(
                    receipts.Select(item => item.Source.ReceivedBaseQuantity)
                        .Concat(acceptances.Select(item => item.Source.DeliveredBaseQuantity)),
                    "supply.quantity.overflow");
                accepted = CheckedSum(
                    receipts.Select(item => item.AcceptedBaseQuantity)
                        .Concat(acceptances.Select(item => item.Source.AcceptedBaseQuantity)),
                    "supply.quantity.overflow");
                rejected = CheckedSum(
                    receipts.Select(item => item.RejectedBaseQuantity)
                        .Concat(acceptances.Select(item => item.Source.RejectedBaseQuantity)),
                    "supply.quantity.overflow");
                quarantined = CheckedSum(
                    receipts.Select(item => item.QuarantinedBaseQuantity),
                    "supply.quantity.overflow");
                var ordered = order.Source.OrderedBaseQuantity!.Value;
                remaining = Math.Max(ordered - accepted.Value, 0);
                excess = Math.Max(accepted.Value - ordered, 0);
                fulfillment = decimal.Round(
                    accepted.Value * 100m / ordered,
                    1,
                    MidpointRounding.AwayFromZero);
                completionDate = FindCompletionDate(receipts, acceptances, ordered);
                deliveryStatus = ResolveDeliveryStatus(
                    order,
                    accepted.Value,
                    ordered,
                    completionDate,
                    selection.CutoffLocalDate);
                if (deliveryStatus == ProjectCommercialDeliveryStatus.ClosedShort)
                {
                    reasons.Add(ProjectCommercialReportingReasonCode.ClosedOrderSupplyGap);
                }
            }
            else
            {
                reasons.Add(ProjectCommercialReportingReasonCode.OrderQuantityBasisUnavailable);
                if (order.Source.OrderedQuantity.HasValue && !order.Source.OrderedBaseQuantity.HasValue)
                {
                    reasons.Add(ProjectCommercialReportingReasonCode.UnitConversionHistoryUnavailable);
                }
            }
            if (!order.Source.DeliveryDueDate.HasValue)
            {
                reasons.Add(ProjectCommercialReportingReasonCode.DeliveryDueDateUnavailable);
                deliveryStatus = order.State == ProjectCommercialPurchaseOrderState.Cancelled
                    ? ProjectCommercialDeliveryStatus.Cancelled
                    : ProjectCommercialDeliveryStatus.NotAssessable;
            }

            var party = order.Party ?? throw Invalid(
                "order.party_snapshot.missing",
                "An available Purchase Order has no effective Party snapshot.");
            rows.Add(new ProjectCommercialPurchaseOrderReportRow(
                order.Source.PurchaseOrderId,
                order.Source.Number,
                order.Source.Title,
                order.Request.Number,
                order.Contract?.Source.Number,
                party.Code,
                party.Name,
                party.Type,
                order.Source.Amount,
                order.Source.DeliveryDueDate,
                order.State,
                order.Item?.Code,
                order.Item?.Name,
                order.Item?.Kind,
                order.Source.OrderedQuantity,
                order.Source.UnitCode,
                order.Source.OrderedBaseQuantity,
                order.Source.BaseUnit,
                order.Source.ConversionVersion,
                delivered,
                accepted,
                rejected,
                quarantined,
                remaining,
                excess,
                fulfillment,
                completionDate,
                order.State == ProjectCommercialPurchaseOrderState.Cancelled
                    ? ProjectCommercialDeliveryStatus.Cancelled
                    : deliveryStatus,
                receipts.Length,
                receipts.Count(item => !item.Inspected),
                acceptances.Length,
                receipts.Count(item => item.RejectedBaseQuantity > 0 ||
                        item.QuarantinedBaseQuantity > 0) +
                    acceptances.Count(item => item.Source.RejectedBaseQuantity > 0)));
        }
        return rows
            .OrderBy(item => item.Number, StringComparer.Ordinal)
            .ThenBy(item => item.PurchaseOrderId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
    }

    private static ProjectCommercialSupplySummary[] CalculateSupplySummaries(
        ProjectCommercialProcurementSupplyReportingSelection selection,
        IReadOnlyCollection<ProjectCommercialPurchaseOrderReportRow> rows,
        ProjectCommercialReportingSectionStatus status)
    {
        if (status is not (ProjectCommercialReportingSectionStatus.Available or
                ProjectCommercialReportingSectionStatus.NoData))
        {
            return [];
        }

        return selection.PurchaseOrders
            .Where(item => item.Item is not null && item.Source.OrderedBaseQuantity.HasValue &&
                item.Source.BaseUnit is not null)
            .GroupBy(item => (item.Item!.ItemId, item.Source.BaseUnit))
            .Select(group =>
            {
                var first = group.First();
                var orderRows = rows.Where(row => group.Any(item =>
                    item.Source.PurchaseOrderId == row.PurchaseOrderId)).ToArray();
                return new ProjectCommercialSupplySummary(
                    first.Item!.Code,
                    first.Item.Name,
                    first.Item.Kind,
                    group.Key.BaseUnit!,
                    group.Count(),
                    CheckedSum(group.Select(item => item.Source.OrderedBaseQuantity!.Value),
                        "supply.quantity.overflow"),
                    CheckedSum(orderRows.Select(item => item.DeliveredBaseQuantity ?? 0),
                        "supply.quantity.overflow"),
                    CheckedSum(orderRows.Select(item => item.AcceptedBaseQuantity ?? 0),
                        "supply.quantity.overflow"),
                    CheckedSum(orderRows.Select(item => item.RejectedBaseQuantity ?? 0),
                        "supply.quantity.overflow"),
                    CheckedSum(orderRows.Select(item => item.QuarantinedBaseQuantity ?? 0),
                        "supply.quantity.overflow"));
            })
            .OrderBy(item => item.ItemCode, StringComparer.Ordinal)
            .ThenBy(item => item.BaseUnit, StringComparer.Ordinal)
            .ToArray();
    }

    private static ProjectCommercialSupplierPerformance[] CalculateSupplierPerformance(
        ProjectCommercialProcurementSupplyReportingSelection selection,
        IReadOnlyCollection<ProjectCommercialPurchaseOrderReportRow> rows,
        ProjectCommercialReportingSectionStatus status)
    {
        if (status != ProjectCommercialReportingSectionStatus.Available)
        {
            return [];
        }

        return selection.PurchaseOrders
            .GroupBy(item => item.Source.PartyId)
            .Select(group =>
            {
                var party = group.First().Party ?? throw Invalid(
                    "supplier.party_snapshot.missing",
                    "Supplier performance requires an effective Party snapshot.");
                var orderIds = group.Select(item => item.Source.PurchaseOrderId).ToHashSet();
                var orderRows = rows.Where(item => orderIds.Contains(item.PurchaseOrderId)).ToArray();
                var assessableCompleted = orderRows.Count(item => item.DeliveryStatus is
                    ProjectCommercialDeliveryStatus.OnTimeFulfilled or
                    ProjectCommercialDeliveryStatus.LateFulfilled or
                    ProjectCommercialDeliveryStatus.ClosedShort);
                var onTime = orderRows.Count(item =>
                    item.DeliveryStatus == ProjectCommercialDeliveryStatus.OnTimeFulfilled);
                var receipts = selection.GoodsReceipts
                    .Where(item => orderIds.Contains(item.Source.PurchaseOrderId)).ToArray();
                var acceptances = selection.ServiceAcceptances
                    .Where(item => orderIds.Contains(item.Source.PurchaseOrderId)).ToArray();
                return new ProjectCommercialSupplierPerformance(
                    party.PartyId,
                    party.Code,
                    party.Name,
                    party.Type,
                    orderRows.Length,
                    orderRows.Count(item => item.State == ProjectCommercialPurchaseOrderState.Issued),
                    orderRows.Count(item => item.State == ProjectCommercialPurchaseOrderState.Closed),
                    orderRows.Count(item => item.State == ProjectCommercialPurchaseOrderState.Cancelled),
                    assessableCompleted,
                    onTime,
                    orderRows.Count(item => item.DeliveryStatus ==
                        ProjectCommercialDeliveryStatus.LateFulfilled),
                    orderRows.Count(item => item.DeliveryStatus ==
                        ProjectCommercialDeliveryStatus.OverdueOpen),
                    orderRows.Count(item => item.DeliveryStatus ==
                        ProjectCommercialDeliveryStatus.ClosedShort),
                    orderRows.Count(item => item.DeliveryStatus ==
                        ProjectCommercialDeliveryStatus.NotAssessable),
                    receipts.Length,
                    receipts.Count(item => !item.Inspected),
                    receipts.Count(item => item.RejectedBaseQuantity > 0 ||
                        item.QuarantinedBaseQuantity > 0),
                    acceptances.Length,
                    acceptances.Count(item => item.Source.RejectedBaseQuantity > 0),
                    assessableCompleted == 0
                        ? null
                        : decimal.Round(
                            onTime * 100m / assessableCompleted,
                            1,
                            MidpointRounding.AwayFromZero));
            })
            .OrderBy(item => item.PartyCode, StringComparer.Ordinal)
            .ThenBy(item => item.PartyId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
    }

    private static DateOnly? FindCompletionDate(
        IEnumerable<ProjectCommercialGoodsReceiptAtCutoff> receipts,
        IEnumerable<ProjectCommercialServiceAcceptanceAtCutoff> acceptances,
        decimal orderedQuantity)
    {
        var evidence = receipts
            .Where(item => item.Inspected)
            .Select(item => new CompletionEvidence(
                item.DeliveryDate,
                item.Source.Number,
                item.Source.ReceiptId,
                item.AcceptedBaseQuantity))
            .Concat(acceptances.Select(item => new CompletionEvidence(
                item.Source.PeriodEnd,
                item.Source.Number,
                item.Source.AcceptanceId,
                item.Source.AcceptedBaseQuantity)))
            .OrderBy(item => item.DeliveryDate)
            .ThenBy(item => item.Number, StringComparer.Ordinal)
            .ThenBy(item => item.Id.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        var cumulative = 0m;
        foreach (var item in evidence)
        {
            cumulative = CheckedAdd(cumulative, item.AcceptedQuantity, "supply.quantity.overflow");
            if (cumulative >= orderedQuantity)
            {
                return item.DeliveryDate;
            }
        }
        return null;
    }

    private static ProjectCommercialDeliveryStatus ResolveDeliveryStatus(
        ProjectCommercialPurchaseOrderAtCutoff order,
        decimal acceptedQuantity,
        decimal orderedQuantity,
        DateOnly? completionDate,
        DateOnly cutoffLocalDate)
    {
        if (order.State == ProjectCommercialPurchaseOrderState.Cancelled)
        {
            return ProjectCommercialDeliveryStatus.Cancelled;
        }
        if (!order.Source.DeliveryDueDate.HasValue)
        {
            return ProjectCommercialDeliveryStatus.NotAssessable;
        }
        if (acceptedQuantity >= orderedQuantity)
        {
            if (!completionDate.HasValue)
            {
                throw Invalid(
                    "supply.completion_date.missing",
                    "Fulfilled supply does not have a canonical completion date.");
            }
            return completionDate.Value <= order.Source.DeliveryDueDate.Value
                ? ProjectCommercialDeliveryStatus.OnTimeFulfilled
                : ProjectCommercialDeliveryStatus.LateFulfilled;
        }
        if (order.State == ProjectCommercialPurchaseOrderState.Closed)
        {
            return ProjectCommercialDeliveryStatus.ClosedShort;
        }
        return order.Source.DeliveryDueDate.Value < cutoffLocalDate
            ? ProjectCommercialDeliveryStatus.OverdueOpen
            : ProjectCommercialDeliveryStatus.PendingDue;
    }

    private static ProjectCommercialReportingDataStatus ResolveDataStatus(
        ProjectCommercialReportingSectionStatus contractStatus,
        ProjectCommercialReportingSectionStatus procurementStatus,
        ProjectCommercialReportingSectionStatus supplyStatus)
    {
        var statuses = new[] { contractStatus, procurementStatus, supplyStatus };
        var active = statuses.Where(item => item is ProjectCommercialReportingSectionStatus.NoData or
            ProjectCommercialReportingSectionStatus.InsufficientData or
            ProjectCommercialReportingSectionStatus.Available).ToArray();
        if (active.Length == 0)
        {
            return ProjectCommercialReportingDataStatus.NotConfigured;
        }
        if (active.Contains(ProjectCommercialReportingSectionStatus.InsufficientData))
        {
            return ProjectCommercialReportingDataStatus.InsufficientData;
        }
        if (active.All(item => item == ProjectCommercialReportingSectionStatus.NoData))
        {
            return ProjectCommercialReportingDataStatus.NoData;
        }
        return ProjectCommercialReportingDataStatus.Available;
    }

    private static void ValidateSelection(ProjectCommercialProcurementSupplyReportingSelection selection)
    {
        if (!string.Equals(
                selection.ContractVersion,
                ProjectCommercialProcurementSupplyReportingContract.Version,
                StringComparison.Ordinal) ||
            selection.TenantId == Guid.Empty || selection.ProjectId == Guid.Empty ||
            selection.CutoffLocalDate == default || selection.SourceCutoffUtc == default ||
            !Enum.IsDefined(selection.Classification) ||
            selection.Classification < ProjectCommercialReportingClassification.Confidential ||
            selection.OfficialContracts is null || selection.ApprovedAmendments is null ||
            selection.PurchaseRequests is null || selection.PurchaseOrders is null ||
            selection.GoodsReceipts is null || selection.ServiceAcceptances is null ||
            selection.SourceCounts is null || selection.SourceManifest is null ||
            string.IsNullOrWhiteSpace(selection.SourceManifestSha256) ||
            selection.SourceMaxChangedAt?.ToUniversalTime() >
                selection.SourceCutoffUtc.ToUniversalTime() ||
            !string.Equals(
                selection.SourceManifestSha256,
                ProjectCommercialProcurementSupplyCanonicalJson.Sha256(
                    ProjectCommercialProcurementSupplyCanonicalJson.Serialize(selection.SourceManifest)),
                StringComparison.Ordinal) ||
            !string.Equals(
                selection.SourceManifest.ManifestVersion,
                ProjectCommercialProcurementSupplyReportingContract.SourceManifestVersion,
                StringComparison.Ordinal) ||
            !string.Equals(
                selection.SourceManifest.SourceContractVersion,
                ProjectCommercialProcurementSupplyReportingContract.Version,
                StringComparison.Ordinal) ||
            !string.Equals(
                selection.SourceManifest.PolicyVersion,
                ProjectCommercialProcurementSupplyReportingContract.PolicyVersion,
                StringComparison.Ordinal) ||
            selection.SourceManifest.TenantId != selection.TenantId ||
            selection.SourceManifest.ProjectId != selection.ProjectId ||
            selection.SourceManifest.CutoffLocalDate != selection.CutoffLocalDate ||
            selection.SourceManifest.SourceCutoffUtc.ToUniversalTime() !=
                selection.SourceCutoffUtc.ToUniversalTime())
        {
            throw Invalid(
                "selection.invalid",
                "The selected Commercial source violates its versioned contract.");
        }
    }

    private static decimal CheckedSum(IEnumerable<decimal> values, string suffix)
    {
        var total = 0m;
        foreach (var value in values)
        {
            total = CheckedAdd(total, value, suffix);
        }
        return total;
    }

    private static int CheckedSum(IEnumerable<int> values, string suffix)
    {
        var total = 0;
        try
        {
            foreach (var value in values)
            {
                total = checked(total + value);
            }
            return total;
        }
        catch (OverflowException)
        {
            throw Invalid(suffix, "A Commercial duration exceeds the supported numeric range.");
        }
    }

    private static decimal CheckedAdd(decimal left, decimal right, string suffix)
    {
        try
        {
            return checked(left + right);
        }
        catch (OverflowException)
        {
            throw Invalid(suffix, "A Commercial amount or quantity exceeds the supported numeric range.");
        }
    }

    private static DomainRuleException Invalid(string suffix, string message) =>
        new($"commercial.project_commercial_procurement_supply_reporting.{suffix}", message);

    private sealed record ContractCalculation(
        ProjectCommercialContractSummary? Summary,
        ProjectCommercialContractReportRow[] Contracts,
        ProjectCommercialAmendmentReportRow[] Amendments);

    private sealed record CompletionEvidence(
        DateOnly DeliveryDate,
        string Number,
        Guid Id,
        decimal AcceptedQuantity);
}

using System.Text.Json;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Commercial.Contracts;
using Pmcs.Modules.Commercial.Domain;
using Pmcs.Modules.Commercial.Services;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Services;

namespace Pmcs.Domain.Tests;

public sealed class ProjectCommercialProcurementSupplyReportingTests
{
    private static readonly Guid TenantId = Id(1);
    private static readonly Guid ProjectId = Id(2);
    private static readonly Guid PartyId = Id(3);
    private static readonly Guid ItemId = Id(4);
    private static readonly DateTimeOffset Cutoff = new(2026, 9, 21, 8, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly CutoffLocalDate = new(2026, 9, 21);

    [Fact]
    public void F06C01ContractLifecycleUsesOnlyEventsAtOrBeforeCutoff()
    {
        var contract = Contract(lifecycle:
        [
            ContractEvent(1, ProjectCommercialContractEventType.Submitted, -20),
            ContractEvent(2, ProjectCommercialContractEventType.Activated, -19),
            ContractEvent(3, ProjectCommercialContractEventType.Closed, 1)
        ]);

        var result = Calculate(Projection(parties: [Party()], contracts: [contract]));

        Assert.Equal(ProjectCommercialContractState.Active, Assert.Single(result.ContractRegister).State);
        Assert.Equal(ProjectCommercialReportingDataStatus.Available, result.DataStatus);
    }

    [Fact]
    public void F06C02OnlyApprovedAmendmentsAtCutoffAffectAmountAndDuration()
    {
        var result = Calculate(Projection(
            parties: [Party()],
            contracts: [Contract()],
            amendments:
            [
                Amendment(11, amountDelta: 100m, approvedDayOffset: -2),
                Amendment(12, amountDelta: 500m, approvedDayOffset: 1)
            ]));

        var row = Assert.Single(result.ContractRegister);
        Assert.Equal(1_100m, row.EffectiveApprovedAmount);
        Assert.Equal(1, row.ApprovedAmendmentCount);
    }

    [Fact]
    public void F06C03ReceiptInspectionAndAcceptanceRespectIndependentCutoffs()
    {
        var request = Request(20, ordered: true);
        var order = Order(30, request.PurchaseRequestId);
        var pending = Receipt(40, inspected: true) with
        {
            InspectedAt = Cutoff.AddMinutes(1)
        };
        var createdAfter = Receipt(41, inspected: false) with
        {
            CreatedAt = Cutoff.AddMinutes(1),
            ArrivedAt = Cutoff.AddDays(-1)
        };
        var result = Calculate(Projection(
            parties: [Party()],
            items: [Item()],
            requests: [request],
            orders: [order],
            receipts: [pending, createdAfter]));

        var row = Assert.Single(result.PurchaseOrders);
        Assert.Equal(1, row.GoodsReceiptCount);
        Assert.Equal(1, row.PendingInspectionCount);
        Assert.Equal(0m, row.AcceptedBaseQuantity);
    }

    [Fact]
    public void F06N01InactiveSetupAndSuspendedFeaturesExposeExplicitStatuses()
    {
        var configuration = Configuration() with
        {
            ContractState = ProjectFeatureState.SetupRequired,
            ProcurementState = ProjectFeatureState.Suspended
        };

        var result = Calculate(Projection(configurations: [configuration]));

        Assert.Equal(ProjectCommercialReportingDataStatus.NotConfigured, result.DataStatus);
        Assert.Equal(ProjectCommercialReportingSectionStatus.SetupRequired, result.ContractStatus);
        Assert.Equal(ProjectCommercialReportingSectionStatus.Suspended, result.ProcurementStatus);
        Assert.Contains(ProjectCommercialReportingReasonCode.ContractSetupRequired, result.ReasonCodes);
        Assert.Contains(ProjectCommercialReportingReasonCode.ProcurementSuspended, result.ReasonCodes);
    }

    [Fact]
    public void F06N02ActiveFeaturesWithoutOfficialEvidenceAreNoDataNotSyntheticZero()
    {
        var result = Calculate(Projection());

        Assert.Equal(ProjectCommercialReportingDataStatus.NoData, result.DataStatus);
        Assert.Null(result.ContractSummary);
        Assert.Null(result.ProcurementSummary);
        Assert.Empty(result.PurchaseOrders);
    }

    [Fact]
    public void F06I01IncompleteContractLifecycleFailsClosedWithoutCurrentStateFallback()
    {
        var result = Calculate(Projection(
            parties: [Party()],
            contracts: [Contract()],
            contractCompleteness: ProjectCommercialReportingSourceCompleteness.Incomplete));

        Assert.Equal(ProjectCommercialReportingDataStatus.InsufficientData, result.DataStatus);
        Assert.Equal(ProjectCommercialReportingSectionStatus.InsufficientData, result.ContractStatus);
        Assert.Empty(result.ContractRegister);
        Assert.Contains(ProjectCommercialReportingReasonCode.ContractLifecycleIncomplete, result.ReasonCodes);

        var sequenceGap = Contract(lifecycle:
        [
            ContractEvent(1, ProjectCommercialContractEventType.Submitted, -20),
            ContractEvent(3, ProjectCommercialContractEventType.Activated, -19)
        ]);
        Assert.Equal(
            "commercial.project_commercial_procurement_supply_reporting.contract.lifecycle.invalid",
            Assert.Throws<DomainRuleException>(() => Select(Projection(
                parties: [Party()],
                contracts: [sequenceGap]))).Code);
    }

    [Fact]
    public void F06I02IncompleteProcurementPreservesSafeContractSection()
    {
        var result = Calculate(Projection(
            parties: [Party()],
            contracts: [Contract()],
            procurementCompleteness: ProjectCommercialReportingSourceCompleteness.Incomplete));

        Assert.Equal(ProjectCommercialReportingSectionStatus.Available, result.ContractStatus);
        Assert.Equal(ProjectCommercialReportingSectionStatus.InsufficientData, result.ProcurementStatus);
        Assert.Equal(ProjectCommercialReportingSectionStatus.InsufficientData, result.SupplyStatus);
        Assert.Single(result.ContractRegister);
    }

    [Fact]
    public void F06I03MissingPartyItemAndUnitHistoryRemainExplicitlyInsufficient()
    {
        var request = Request(20, ordered: true);
        var order = Order(30, request.PurchaseRequestId) with
        {
            OrderedBaseQuantity = null,
            BaseUnit = null,
            ConversionVersion = null
        };
        var result = Calculate(Projection(
            requests: [request],
            orders: [order],
            partyCompleteness: ProjectCommercialReportingSourceCompleteness.Incomplete,
            itemCompleteness: ProjectCommercialReportingSourceCompleteness.Incomplete));

        Assert.Equal(ProjectCommercialReportingDataStatus.InsufficientData, result.DataStatus);
        Assert.Contains(ProjectCommercialReportingReasonCode.PartySnapshotUnavailable, result.ReasonCodes);
        Assert.Contains(ProjectCommercialReportingReasonCode.UnitConversionHistoryUnavailable, result.ReasonCodes);
    }

    [Fact]
    public void F06CT01PositiveAndNegativeAmendmentsProduceExactEffectiveCeiling()
    {
        var result = Calculate(Projection(
            parties: [Party()],
            contracts: [Contract(amount: 1_000m)],
            amendments:
            [
                Amendment(11, amountDelta: 150m),
                Amendment(12, amountDelta: -25m)
            ]));

        Assert.Equal(1_125m, Assert.Single(result.ContractRegister).EffectiveApprovedAmount);
        Assert.Equal(1_125m, result.ContractSummary!.EffectiveContractCeilingTotal);
    }

    [Fact]
    public void F06CT02UnknownOriginalAmountKeepsSubtotalSeparateAndTotalNull()
    {
        var result = Calculate(Projection(
            parties: [Party()],
            contracts: [Contract(amount: null)],
            amendments: [Amendment(11, amountDelta: 50m)]));

        Assert.Null(Assert.Single(result.ContractRegister).EffectiveApprovedAmount);
        Assert.Equal(0m, result.ContractSummary!.KnownEffectiveContractCeilingSubtotal);
        Assert.Null(result.ContractSummary.EffectiveContractCeilingTotal);
        Assert.Contains(ProjectCommercialReportingReasonCode.ContractCeilingUnavailable, result.ReasonCodes);
    }

    [Fact]
    public void F06CT03TimeAndMixedExtensionsProduceDeterministicEffectiveEndDate()
    {
        var result = Calculate(Projection(
            parties: [Party()],
            contracts: [Contract(endDate: new DateOnly(2026, 10, 1))],
            amendments:
            [
                Amendment(11, ContractAmendmentType.TimeExtension, null, 10),
                Amendment(12, ContractAmendmentType.Mixed, 5m, 5)
            ]));

        var row = Assert.Single(result.ContractRegister);
        Assert.Equal(15, row.ApprovedExtensionDays);
        Assert.Equal(new DateOnly(2026, 10, 16), row.EffectiveEndDate);
    }

    [Fact]
    public void F06CT04ScopeChangeShapeViolationFailsClosed()
    {
        var invalid = Amendment(11) with
        {
            Type = ContractAmendmentType.ScopeChange,
            AmountDelta = 10m,
            ExtensionDays = null
        };

        var exception = Assert.Throws<DomainRuleException>(() => Select(Projection(
            parties: [Party()],
            contracts: [Contract()],
            amendments: [invalid])));

        Assert.Equal(
            "commercial.project_commercial_procurement_supply_reporting.amendment.invalid",
            exception.Code);
    }

    [Fact]
    public void F06CT05SubmittedWorkflowNeverChangesOfficialRegisterOrCeiling()
    {
        var submittedContract = Contract(lifecycle:
        [
            ContractEvent(1, ProjectCommercialContractEventType.Submitted, -1)
        ]);
        var submittedAmendment = Amendment(11) with
        {
            Lifecycle =
            [
                AmendmentEvent(1, ProjectCommercialAmendmentEventType.Submitted, -1)
            ]
        };
        var result = Calculate(Projection(
            parties: [Party()],
            contracts: [submittedContract],
            amendments: [submittedAmendment]));

        Assert.Equal(ProjectCommercialReportingSectionStatus.NoData, result.ContractStatus);
        Assert.Equal(1, result.SourceCounts.PendingContractCount);
        Assert.Equal(1, result.SourceCounts.PendingAmendmentCount);
        Assert.Empty(result.ContractRegister);
    }

    [Fact]
    public void F06PR01ApprovedRequestAwaitingOrderCreatesNoCommercialCommitment()
    {
        var request = Request(20, ordered: false);

        var result = Calculate(Projection(requests: [request]));

        Assert.Equal(1, result.ProcurementSummary!.ApprovedRequestsAwaitingOrderCount);
        Assert.Equal(0m, result.ProcurementSummary.TotalIssuedOrderAmount);
        Assert.Empty(result.PurchaseOrders);
    }

    [Fact]
    public void F06PR02TwoOrdersForOneRequestFailWithoutLatestTieBreak()
    {
        var request = Request(20, ordered: true);

        var exception = Assert.Throws<DomainRuleException>(() => Select(Projection(
            parties: [Party()],
            items: [Item()],
            requests: [request],
            orders: [Order(30, request.PurchaseRequestId), Order(31, request.PurchaseRequestId)])));

        Assert.Equal(
            "commercial.project_commercial_procurement_supply_reporting.order.request_duplicate",
            exception.Code);
    }

    [Fact]
    public void F06PO01IssuedClosedAndCancelledStatesUseLifecycleAtCutoff()
    {
        var firstRequest = Request(20, ordered: true);
        var secondRequest = Request(21, ordered: true);
        var issuedAtCutoff = Order(30, firstRequest.PurchaseRequestId) with
        {
            Lifecycle =
            [
                OrderEvent(1, ProjectCommercialPurchaseOrderEventType.Issued, -8),
                OrderEvent(2, ProjectCommercialPurchaseOrderEventType.Closed, 1)
            ]
        };
        var cancelled = Order(31, secondRequest.PurchaseRequestId) with
        {
            Lifecycle =
            [
                OrderEvent(1, ProjectCommercialPurchaseOrderEventType.Issued, -8),
                OrderEvent(2, ProjectCommercialPurchaseOrderEventType.Cancelled, -1)
            ]
        };
        var result = Calculate(Projection(
            parties: [Party()],
            items: [Item()],
            requests: [firstRequest, secondRequest],
            orders: [issuedAtCutoff, cancelled]));

        Assert.Equal(100m, result.ProcurementSummary!.TotalIssuedOrderAmount);
        Assert.Equal(100m, result.ProcurementSummary.OpenOrderAmount);
        Assert.Contains(result.PurchaseOrders, item =>
            item.State == ProjectCommercialPurchaseOrderState.Cancelled &&
            item.DeliveryStatus == ProjectCommercialDeliveryStatus.Cancelled);
    }

    [Fact]
    public void F06PO02DueDateEqualCutoffIsPendingWhilePriorDayIsOverdue()
    {
        var firstRequest = Request(20, ordered: true);
        var secondRequest = Request(21, ordered: true);
        var result = Calculate(Projection(
            parties: [Party()],
            items: [Item()],
            requests: [firstRequest, secondRequest],
            orders:
            [
                Order(30, firstRequest.PurchaseRequestId, dueDate: CutoffLocalDate),
                Order(31, secondRequest.PurchaseRequestId, dueDate: CutoffLocalDate.AddDays(-1))
            ]));

        Assert.Equal(
            ProjectCommercialDeliveryStatus.PendingDue,
            result.PurchaseOrders.Single(item => item.PurchaseOrderId == Id(30)).DeliveryStatus);
        Assert.Equal(
            ProjectCommercialDeliveryStatus.OverdueOpen,
            result.PurchaseOrders.Single(item => item.PurchaseOrderId == Id(31)).DeliveryStatus);
    }

    [Fact]
    public void F06PO03ContractAndOrderPartyMismatchFailsClosed()
    {
        var request = Request(20, ordered: true);
        var order = Order(30, request.PurchaseRequestId, contractId: Id(10)) with { PartyId = Id(99) };

        var exception = Assert.Throws<DomainRuleException>(() => Select(Projection(
            parties: [Party(), Party(99)],
            items: [Item()],
            contracts: [Contract()],
            requests: [request],
            orders: [order])));

        Assert.Equal(
            "commercial.project_commercial_procurement_supply_reporting.order.contract_party_mismatch",
            exception.Code);
    }

    [Fact]
    public void F06S01ReceiptInspectionQuantitiesRemainSeparate()
    {
        var request = Request(20, ordered: true);
        var order = Order(30, request.PurchaseRequestId, orderedBaseQuantity: 20m);
        var result = Calculate(Projection(
            parties: [Party()],
            items: [Item()],
            requests: [request],
            orders: [order],
            receipts:
            [
                Receipt(40, 5m, accepted: 5m),
                Receipt(41, 3m, accepted: 1m, rejected: 2m),
                Receipt(42, 2m, accepted: 0m, quarantined: 2m),
                Receipt(43, 1m, inspected: false)
            ]));

        var row = Assert.Single(result.PurchaseOrders);
        Assert.Equal(11m, row.DeliveredBaseQuantity);
        Assert.Equal(6m, row.AcceptedBaseQuantity);
        Assert.Equal(2m, row.RejectedBaseQuantity);
        Assert.Equal(2m, row.QuarantinedBaseQuantity);
        Assert.Equal(1, row.PendingInspectionCount);
    }

    [Fact]
    public void F06S02ServiceAcceptanceOnlyFulfillsServiceOrRentalOrders()
    {
        var serviceItem = Item(kind: SupplyItemKind.Service, baseUnit: "HOUR");
        var request = Request(20, ordered: true);
        var order = Order(30, request.PurchaseRequestId, baseUnit: "HOUR", itemId: serviceItem.ItemId);
        var result = Calculate(Projection(
            parties: [Party()],
            items: [serviceItem],
            requests: [request],
            orders: [order],
            acceptances: [Acceptance(50, accepted: 8m, rejected: 2m, baseUnit: "HOUR") ]));

        var row = Assert.Single(result.PurchaseOrders);
        Assert.Equal(1, row.ServiceAcceptanceCount);
        Assert.Equal(10m, row.DeliveredBaseQuantity);
        Assert.Equal(8m, row.AcceptedBaseQuantity);
    }

    [Fact]
    public void F06S03PartialDeliveriesUseFirstCanonicalCompletionDate()
    {
        var request = Request(20, ordered: true);
        var order = Order(30, request.PurchaseRequestId, dueDate: CutoffLocalDate.AddDays(-1));
        var result = Calculate(Projection(
            parties: [Party()],
            items: [Item()],
            requests: [request],
            orders: [order],
            receipts:
            [
                Receipt(40, 4m, accepted: 4m, arrivalDayOffset: -2),
                Receipt(41, 6m, accepted: 6m, arrivalDayOffset: -1)
            ]));

        var row = Assert.Single(result.PurchaseOrders);
        Assert.Equal(100m, row.FulfillmentPercent);
        Assert.Equal(CutoffLocalDate.AddDays(-1), row.CompletionDate);
        Assert.Equal(ProjectCommercialDeliveryStatus.OnTimeFulfilled, row.DeliveryStatus);
    }

    [Fact]
    public void F06S04ApprovedExcessIsUncappedAndUnapprovedExcessFailsClosed()
    {
        var request = Request(20, ordered: true);
        var order = Order(30, request.PurchaseRequestId);
        var approved = Receipt(40, 12m, accepted: 12m) with
        {
            ExcessApprovalId = Id(400),
            ExcessApprovedAt = Cutoff.AddDays(-1)
        };
        var projection = Projection(
            parties: [Party()],
            items: [Item()],
            requests: [request],
            orders: [order],
            receipts: [approved]);

        Assert.Equal(120m, Assert.Single(Calculate(projection).PurchaseOrders).FulfillmentPercent);
        var exception = Assert.Throws<DomainRuleException>(() => Select(projection with
        {
            GoodsReceipts = [approved with { ExcessApprovalId = null, ExcessApprovedAt = null }]
        }));
        Assert.Equal(
            "commercial.project_commercial_procurement_supply_reporting.supply.excess_approval.missing",
            exception.Code);
    }

    [Fact]
    public void F06S05SupplySummariesNeverAggregateAcrossItemOrBaseUnit()
    {
        var firstRequest = Request(20, ordered: true);
        var secondRequest = Request(21, ordered: true) with { ItemId = Id(5) };
        var itemChangedAt = Cutoff.AddDays(-4);
        var issuedItem = Item(name: "Pump at Issue") with { EffectiveToUtc = itemChangedAt };
        var currentItem = issuedItem with
        {
            Revision = 2,
            Name = "Pump at Cutoff",
            BaseUnit = "BOX",
            EffectiveFromUtc = itemChangedAt,
            EffectiveToUtc = null
        };
        var secondItem = Item(5, baseUnit: "KG");
        var result = Calculate(Projection(
            parties: [Party()],
            items: [issuedItem, currentItem, secondItem],
            requests: [firstRequest, secondRequest],
            orders:
            [
                Order(30, firstRequest.PurchaseRequestId),
                Order(31, secondRequest.PurchaseRequestId, itemId: secondItem.ItemId, baseUnit: "KG")
            ],
            receipts:
            [
                Receipt(40, itemId: ItemId),
                Receipt(41, orderId: Id(31), itemId: secondItem.ItemId, baseUnit: "KG")
            ]));

        Assert.Equal(2, result.SupplySummaries.Count);
        Assert.Contains(result.SupplySummaries, item =>
            item.ItemName == "Pump at Issue" && item.BaseUnit == "EA");
        Assert.Contains(result.SupplySummaries, item => item.BaseUnit == "KG");
    }

    [Fact]
    public void F06S06OrderWithoutQuantityOrDueDatePreservesAmountButIsNotAssessable()
    {
        var request = Request(20, ordered: true);
        var order = Order(30, request.PurchaseRequestId) with
        {
            DeliveryDueDate = null,
            ItemId = null,
            OrderedQuantity = null,
            UnitCode = null,
            OrderedBaseQuantity = null,
            BaseUnit = null,
            ConversionVersion = null
        };
        var result = Calculate(Projection(
            parties: [Party()],
            requests: [request],
            orders: [order]));

        var row = Assert.Single(result.PurchaseOrders);
        Assert.Equal(100m, row.Amount);
        Assert.Null(row.FulfillmentPercent);
        Assert.Equal(ProjectCommercialDeliveryStatus.NotAssessable, row.DeliveryStatus);
        Assert.Contains(ProjectCommercialReportingReasonCode.DeliveryDueDateUnavailable, result.ReasonCodes);
    }

    [Fact]
    public void F06PF01SupplierPerformanceUsesAuditableCountsAndNullableRate()
    {
        var firstRequest = Request(20, ordered: true);
        var secondRequest = Request(21, ordered: true);
        var thirdRequest = Request(22, ordered: true);
        var result = Calculate(Projection(
            parties: [Party()],
            items: [Item()],
            requests: [firstRequest, secondRequest, thirdRequest],
            orders:
            [
                Order(30, firstRequest.PurchaseRequestId, dueDate: CutoffLocalDate.AddDays(-2)),
                Order(31, secondRequest.PurchaseRequestId, dueDate: CutoffLocalDate.AddDays(-3)),
                Order(32, thirdRequest.PurchaseRequestId, dueDate: CutoffLocalDate.AddDays(-1))
            ],
            receipts:
            [
                Receipt(40, orderId: Id(30), arrivalDayOffset: -2),
                Receipt(41, orderId: Id(31), arrivalDayOffset: -1)
            ]));

        var supplier = Assert.Single(result.SupplierPerformance);
        Assert.Equal(1, supplier.OnTimeFulfilledCount);
        Assert.Equal(1, supplier.LateFulfilledCount);
        Assert.Equal(1, supplier.OverdueOpenCount);
        Assert.Equal(50m, supplier.OnTimeFulfillmentRate);
    }

    [Fact]
    public void F06X01CurrencyMismatchFailsClosedWithoutFx()
    {
        var contract = Contract() with { CurrencyCode = "USD" };

        var exception = Assert.Throws<DomainRuleException>(() => Select(Projection(
            parties: [Party()],
            contracts: [contract])));

        Assert.Equal(
            "commercial.project_commercial_procurement_supply_reporting.currency_mismatch",
            exception.Code);
    }

    [Fact]
    public void F06D01TwinRunsIgnoreQueryOrderRunIdentityAndBuildTime()
    {
        var request = Request(20, ordered: true);
        var projection = Projection(
            parties: [Party()],
            items: [Item()],
            contracts: [Contract()],
            requests: [request],
            orders: [Order(30, request.PurchaseRequestId)],
            receipts: [Receipt(40)]);
        var first = Calculate(projection);
        var reversed = Calculate(projection with
        {
            PartySnapshots = projection.PartySnapshots.Reverse().ToArray(),
            Contracts = projection.Contracts.Reverse().ToArray(),
            GoodsReceipts = projection.GoodsReceipts.Reverse().ToArray()
        });

        var firstSnapshot = Build(first, runId: Id(500), builtAt: Cutoff.AddMinutes(2));
        var secondSnapshot = Build(reversed, runId: Id(501), builtAt: Cutoff.AddMinutes(9));
        Assert.Equal(first.SourceManifestSha256, reversed.SourceManifestSha256);
        Assert.Equal(firstSnapshot.Sha256, secondSnapshot.Sha256);
        Assert.Equal(firstSnapshot.SourceManifestSha256, secondSnapshot.SourceManifestSha256);

        var tampered = first with
        {
            SourceManifest = first.SourceManifest with { Contracts = [] }
        };
        Assert.Equal(
            "reporting.project_commercial_procurement_supply.source_manifest.hash_mismatch",
            Assert.Throws<DomainRuleException>(() => Build(tampered)).Code);
    }

    [Fact]
    public void F06PM01CrossTenantEvidenceFailsClosedWithoutLeakage()
    {
        var crossTenant = Contract() with { TenantId = Id(999) };

        var exception = Assert.Throws<DomainRuleException>(() => Select(Projection(
            parties: [Party()],
            contracts: [crossTenant])));

        Assert.Equal(
            "commercial.project_commercial_procurement_supply_reporting.contract.invalid",
            exception.Code);
    }

    [Fact]
    public void F06CL01ClassificationIsAtLeastConfidentialAndRestrictedPropagates()
    {
        var restricted = Contract() with
        {
            Classification = ProjectCommercialReportingClassification.Restricted
        };
        var result = Calculate(Projection(parties: [Party()], contracts: [restricted]));
        var snapshot = Build(result);

        Assert.Equal(ProjectCommercialReportingClassification.Restricted, result.Classification);
        Assert.Equal(ReportClassification.Restricted, snapshot.Classification);
    }

    [Fact]
    public void F06MN01SemanticSnapshotContainsOnlyAllowlistedCommercialMetadata()
    {
        var request = Request(20, ordered: true);
        var result = Calculate(Projection(
            parties: [Party(name: "Supplier One")],
            items: [Item(name: "Pump")],
            contracts: [Contract()],
            requests: [request],
            orders: [Order(30, request.PurchaseRequestId)],
            receipts: [Receipt(40)]));

        var payload = Build(result).PayloadJson;
        Assert.Contains("Supplier One", payload, StringComparison.Ordinal);
        Assert.Contains("Pump", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("contractId", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("purchaseOrderId", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("partyId", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nationalId", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contact", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stock", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("invoice", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void F06SP01RuntimeIdentityUsesStrictEmptyParametersAndVersionedSchemas()
    {
        var parameters = new ProjectCommercialProcurementSupplyReportParameters();
        var element = JsonSerializer.SerializeToElement(parameters, CanonicalJson.SerializerOptions);

        Assert.Equal(JsonValueKind.Object, element.ValueKind);
        Assert.Empty(element.EnumerateObject());
        Assert.Equal(
            "project-commercial-procurement-supply-certified",
            ProjectCommercialProcurementSupplyReportRuntimeContract.DefinitionCode);
        Assert.Equal(
            "pmcs.reporting.project-commercial-procurement-supply.parameters/v1",
            ProjectCommercialProcurementSupplyReportRuntimeContract.ParameterSchemaVersion);
        Assert.Equal(
            "pmcs.reporting.project-commercial-procurement-supply.snapshot/v1",
            ProjectCommercialProcurementSupplyReportRuntimeContract.SnapshotSchemaVersion);
    }

    [Fact]
    public void F06SC01CompatibilityProjectionRejectsLegacyClosedContractHistory()
    {
        var contract = ProjectContract.Create(
            Id(10),
            TenantId,
            ProjectId,
            PartyId,
            "CTR-001",
            "Main Contract",
            ProjectContractType.MainContract,
            1_000m,
            "IRR",
            CutoffLocalDate.AddDays(-100),
            CutoffLocalDate.AddDays(100),
            null,
            Id(90),
            Cutoff.AddDays(-20));
        contract.Submit(contract.Revision, Cutoff.AddDays(-19));
        contract.Activate(contract.Revision, Id(91), Cutoff.AddDays(-18), null);
        contract.Close(contract.Revision, Id(91), Cutoff.AddDays(-1), null);

        var exception = Assert.Throws<DomainRuleException>(() =>
            ProjectCommercialProcurementSupplyReportingCompatibilityProjection.Create(
                Profile(),
                CutoffLocalDate,
                Cutoff,
                [],
                [],
                [contract],
                [],
                [],
                [],
                [],
                []));

        Assert.Equal(
            "commercial.project_commercial_procurement_supply_reporting.contract_history.unavailable",
            exception.Code);
    }

    private static ProjectCommercialProcurementSupplyReportingResult Calculate(
        ProjectCommercialProcurementSupplyReportingProjection projection) =>
        ProjectCommercialProcurementSupplyReportingCalculator.Calculate(Select(projection));

    private static ProjectCommercialProcurementSupplyReportingSelection Select(
        ProjectCommercialProcurementSupplyReportingProjection projection) =>
        ProjectCommercialProcurementSupplyReportingSelector.Select(projection);

    private static ProjectCommercialProcurementSupplyReportingProjection Projection(
        IReadOnlyCollection<ProjectCommercialConfigurationVersion>? configurations = null,
        IReadOnlyCollection<ProjectCommercialPartySnapshotVersion>? parties = null,
        IReadOnlyCollection<ProjectCommercialItemSnapshotVersion>? items = null,
        IReadOnlyCollection<ProjectCommercialContractVersion>? contracts = null,
        IReadOnlyCollection<ProjectCommercialAmendmentVersion>? amendments = null,
        IReadOnlyCollection<ProjectCommercialPurchaseRequestVersion>? requests = null,
        IReadOnlyCollection<ProjectCommercialPurchaseOrderVersion>? orders = null,
        IReadOnlyCollection<ProjectCommercialGoodsReceiptVersion>? receipts = null,
        IReadOnlyCollection<ProjectCommercialServiceAcceptanceVersion>? acceptances = null,
        ProjectCommercialReportingSourceCompleteness contractCompleteness =
            ProjectCommercialReportingSourceCompleteness.Complete,
        ProjectCommercialReportingSourceCompleteness amendmentCompleteness =
            ProjectCommercialReportingSourceCompleteness.Complete,
        ProjectCommercialReportingSourceCompleteness procurementCompleteness =
            ProjectCommercialReportingSourceCompleteness.Complete,
        ProjectCommercialReportingSourceCompleteness partyCompleteness =
            ProjectCommercialReportingSourceCompleteness.Complete,
        ProjectCommercialReportingSourceCompleteness itemCompleteness =
            ProjectCommercialReportingSourceCompleteness.Complete,
        ProjectCommercialReportingSourceCompleteness receiptCompleteness =
            ProjectCommercialReportingSourceCompleteness.Complete,
        ProjectCommercialReportingSourceCompleteness acceptanceCompleteness =
            ProjectCommercialReportingSourceCompleteness.Complete) => new(
        ProjectCommercialProcurementSupplyReportingContract.Version,
        TenantId,
        ProjectId,
        CutoffLocalDate,
        Cutoff,
        configurations ?? [Configuration()],
        parties ?? [],
        items ?? [],
        contracts ?? [],
        amendments ?? [],
        requests ?? [],
        orders ?? [],
        receipts ?? [],
        acceptances ?? [],
        contractCompleteness,
        amendmentCompleteness,
        procurementCompleteness,
        partyCompleteness,
        itemCompleteness,
        receiptCompleteness,
        acceptanceCompleteness,
        ProjectCommercialReportingClassification.Confidential);

    private static ProjectCommercialConfigurationVersion Configuration() => new(
        7,
        12,
        ContractModel.ConstructionManagement,
        ProjectFeatureState.Active,
        ProjectFeatureState.Active,
        "Etc/UTC",
        "IRR",
        Cutoff.AddDays(-100),
        null,
        ProjectCommercialReportingClassification.Confidential);

    private static ProjectCommercialPartySnapshotVersion Party(
        int id = 3,
        string name = "Supplier One") => new(
        Id(id),
        TenantId,
        ProjectId,
        1,
        $"SUP-{id:000}",
        name,
        PartyType.Supplier,
        Cutoff.AddDays(-100),
        null,
        ProjectCommercialReportingClassification.Confidential);

    private static ProjectCommercialItemSnapshotVersion Item(
        int id = 4,
        string name = "Pump",
        SupplyItemKind kind = SupplyItemKind.Material,
        string baseUnit = "EA") => new(
        Id(id),
        TenantId,
        ProjectId,
        1,
        $"ITM-{id:000}",
        name,
        kind,
        baseUnit,
        Cutoff.AddDays(-100),
        null,
        ProjectCommercialReportingClassification.Confidential);

    private static ProjectCommercialContractVersion Contract(
        int id = 10,
        decimal? amount = 1_000m,
        DateOnly? endDate = null,
        Guid? partyId = null,
        IReadOnlyCollection<ProjectCommercialContractLifecycleEvent>? lifecycle = null) => new(
        Id(id),
        TenantId,
        ProjectId,
        3,
        partyId ?? PartyId,
        $"CTR-{id:000}",
        "Main Contract",
        ProjectContractType.MainContract,
        amount,
        "IRR",
        CutoffLocalDate.AddDays(-100),
        endDate ?? CutoffLocalDate.AddDays(100),
        Cutoff.AddDays(-30),
        lifecycle ??
        [
            ContractEvent(1, ProjectCommercialContractEventType.Submitted, -20),
            ContractEvent(2, ProjectCommercialContractEventType.Activated, -19)
        ],
        ProjectCommercialReportingClassification.Confidential);

    private static ProjectCommercialAmendmentVersion Amendment(
        int id,
        ContractAmendmentType type = ContractAmendmentType.ValueChange,
        decimal? amountDelta = 100m,
        int? extensionDays = null,
        int approvedDayOffset = -1) => new(
        Id(id),
        TenantId,
        ProjectId,
        2,
        Id(10),
        $"AMD-{id:000}",
        "Approved Amendment",
        type,
        amountDelta,
        "IRR",
        extensionDays,
        Cutoff.AddDays(-10),
        [
            AmendmentEvent(1, ProjectCommercialAmendmentEventType.Submitted, -2),
            AmendmentEvent(2, ProjectCommercialAmendmentEventType.Approved, approvedDayOffset)
        ],
        ProjectCommercialReportingClassification.Confidential);

    private static ProjectCommercialPurchaseRequestVersion Request(
        int id,
        bool ordered,
        Guid? itemId = null) => new(
        Id(id),
        TenantId,
        ProjectId,
        ordered ? 4 : 3,
        $"REQ-{id:000}",
        "Purchase Request",
        100m,
        "IRR",
        itemId ?? ItemId,
        Cutoff.AddDays(-12),
        ordered
            ?
            [
                RequestEvent(1, ProjectCommercialPurchaseRequestEventType.Submitted, -10),
                RequestEvent(2, ProjectCommercialPurchaseRequestEventType.Approved, -9),
                RequestEvent(3, ProjectCommercialPurchaseRequestEventType.Ordered, -8)
            ]
            :
            [
                RequestEvent(1, ProjectCommercialPurchaseRequestEventType.Submitted, -10),
                RequestEvent(2, ProjectCommercialPurchaseRequestEventType.Approved, -9)
            ],
        ProjectCommercialReportingClassification.Confidential);

    private static ProjectCommercialPurchaseOrderVersion Order(
        int id,
        Guid requestId,
        DateOnly? dueDate = null,
        Guid? contractId = null,
        Guid? itemId = null,
        string baseUnit = "EA",
        decimal orderedBaseQuantity = 10m) => new(
        Id(id),
        TenantId,
        ProjectId,
        1,
        requestId,
        PartyId,
        contractId,
        $"PO-{id:000}",
        "Purchase Order",
        100m,
        "IRR",
        dueDate ?? CutoffLocalDate.AddDays(5),
        itemId ?? ItemId,
        orderedBaseQuantity,
        baseUnit,
        orderedBaseQuantity,
        baseUnit,
        1,
        [OrderEvent(1, ProjectCommercialPurchaseOrderEventType.Issued, -8)],
        ProjectCommercialReportingClassification.Confidential);

    private static ProjectCommercialGoodsReceiptVersion Receipt(
        int id,
        decimal received = 10m,
        decimal? accepted = null,
        decimal rejected = 0m,
        decimal quarantined = 0m,
        bool inspected = true,
        int arrivalDayOffset = -1,
        Guid? orderId = null,
        Guid? itemId = null,
        string baseUnit = "EA") => new(
        Id(id),
        TenantId,
        ProjectId,
        inspected ? 2 : 1,
        $"REC-{id:000}",
        orderId ?? Id(30),
        PartyId,
        itemId ?? ItemId,
        Cutoff.AddDays(arrivalDayOffset).AddHours(-1),
        Cutoff.AddDays(arrivalDayOffset),
        received,
        baseUnit,
        1,
        inspected
            ? ReceiptStatus(accepted ?? received - rejected - quarantined, received, quarantined)
            : GoodsReceiptStatus.PendingInspection,
        inspected ? Cutoff.AddDays(arrivalDayOffset).AddHours(1) : null,
        inspected ? accepted ?? received - rejected - quarantined : 0,
        inspected ? rejected : 0,
        inspected ? quarantined : 0,
        null,
        null,
        ProjectCommercialReportingClassification.Confidential);

    private static ProjectCommercialServiceAcceptanceVersion Acceptance(
        int id,
        decimal accepted,
        decimal rejected,
        string baseUnit) => new(
        Id(id),
        TenantId,
        ProjectId,
        1,
        $"SAC-{id:000}",
        Id(30),
        PartyId,
        ItemId,
        CutoffLocalDate.AddDays(-5),
        CutoffLocalDate.AddDays(-1),
        accepted + rejected,
        accepted,
        rejected,
        baseUnit,
        Cutoff.AddDays(-1),
        null,
        null,
        ProjectCommercialReportingClassification.Confidential);

    private static GoodsReceiptStatus ReceiptStatus(
        decimal accepted,
        decimal received,
        decimal quarantined) => accepted == received
        ? GoodsReceiptStatus.Accepted
        : accepted > 0
            ? GoodsReceiptStatus.PartiallyAccepted
            : quarantined > 0
                ? GoodsReceiptStatus.Quarantined
                : GoodsReceiptStatus.Rejected;

    private static ProjectCommercialContractLifecycleEvent ContractEvent(
        long sequence,
        ProjectCommercialContractEventType type,
        int dayOffset) => new(sequence, type, Cutoff.AddDays(dayOffset));

    private static ProjectCommercialAmendmentLifecycleEvent AmendmentEvent(
        long sequence,
        ProjectCommercialAmendmentEventType type,
        int dayOffset) => new(sequence, type, Cutoff.AddDays(dayOffset));

    private static ProjectCommercialPurchaseRequestLifecycleEvent RequestEvent(
        long sequence,
        ProjectCommercialPurchaseRequestEventType type,
        int dayOffset) => new(sequence, type, Cutoff.AddDays(dayOffset));

    private static ProjectCommercialPurchaseOrderLifecycleEvent OrderEvent(
        long sequence,
        ProjectCommercialPurchaseOrderEventType type,
        int dayOffset) => new(sequence, type, Cutoff.AddDays(dayOffset));

    private static ReportSnapshot Build(
        ProjectCommercialProcurementSupplyReportingResult source,
        Guid? runId = null,
        DateTimeOffset? builtAt = null) =>
        ProjectCommercialProcurementSupplyReportSnapshotBuilder.Build(
            runId ?? Id(500),
            TenantId,
            ProjectCommercialProcurementSupplyPinnedProjectProfile.Capture(
                Profile(),
                Cutoff.AddMinutes(1)),
            Cutoff,
            source,
            Cutoff.AddMinutes(1),
            builtAt ?? Cutoff.AddMinutes(2));

    private static ProjectControlProfile Profile() => new(
        ProjectId,
        TenantId,
        "PRJ-01",
        "Project One",
        "Etc/UTC",
        "IRR",
        12,
        Cutoff.AddDays(-100),
        ProjectStatus.Active,
        ContractModel.ConstructionManagement,
        PlanningMode.SimpleWorkList,
        ProjectFeatureState.Active,
        ProjectFeatureState.Active,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotEnabled,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.Active,
        new ProjectCalendarProfile(ProjectCalendarConfigurationState.Configured, 62),
        ConfigurationVersion: 7);

    private static Guid Id(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
}

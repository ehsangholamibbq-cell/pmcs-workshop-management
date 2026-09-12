using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Commercial.Domain;

namespace Pmcs.Domain.Tests;

public sealed class SupplyRealityWorkflowTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProjectId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void UnitConversionIsVersionedAndPreservesOriginalBasis()
    {
        var item = Material();
        item.AddUnitConversion(1, "BUNDLE", 12m, 1, Now.AddMinutes(1));
        item.AddUnitConversion(2, "BUNDLE", 10m, 2, Now.AddMinutes(2));

        var oldBasis = item.ConvertToBase(2m, "BUNDLE", 1);
        var currentBasis = item.ConvertToBase(2m, "BUNDLE");

        Assert.Equal(24m, oldBasis.BaseQuantity);
        Assert.Equal(1, oldBasis.ConversionVersion);
        Assert.Equal(20m, currentBasis.BaseQuantity);
        Assert.Equal(2, currentBasis.ConversionVersion);
    }

    [Fact]
    public void ServiceCannotPretendToBeTrackedInventory()
    {
        Assert.Throws<DomainRuleException>(() => SupplyItem.Create(
            Guid.NewGuid(), TenantId, ProjectId, "SVC-1", "جرثقیل", SupplyItemKind.Service,
            "خدمات", "HOUR", null, SupplyTrackingPolicy.Quantity, false, null, null, UserId, Now));
        Assert.Throws<DomainRuleException>(() => SupplyItem.Create(
            Guid.NewGuid(), TenantId, ProjectId, "MAT-SERIAL", "شیرآلات سریالی", SupplyItemKind.Material,
            "مکانیک", "EA", null, SupplyTrackingPolicy.Serial, true, null, null, UserId, Now));
    }

    [Fact]
    public void QuarantineLocationCannotExposeAvailableStock()
    {
        Assert.Throws<DomainRuleException>(() => InventoryLocation.Create(
            Guid.NewGuid(), TenantId, ProjectId, "Q-1", "قرنطینه", InventoryLocationType.QuarantineArea,
            true, UserId, Now));
    }

    [Fact]
    public void PhysicalReceiptIsNeitherAcceptanceNorAvailableStock()
    {
        var receipt = Receipt(10m);

        Assert.Equal(GoodsReceiptStatus.Received, receipt.Status);
        Assert.Equal(0m, receipt.AcceptedBaseQuantity);
        Assert.Null(receipt.InspectedAt);
        Assert.Null(receipt.StockPostedAt);
        Assert.StartsWith("REC-14050620-", receipt.Number);
    }

    [Fact]
    public void InspectionMustReconcileAndCanRecordPartialAcceptance()
    {
        var receipt = Receipt(10m);
        receipt.SubmitForInspection(1);

        Assert.Throws<DomainRuleException>(() => receipt.RecordInspection(
            2, 8m, 1m, 0m, "کمی", null, "کسری تصمیم", UserId, Now.AddMinutes(1)));

        receipt.RecordInspection(2, 8m, 1m, 1m, "کمی و ظاهری", "INS-1", "یک واحد مردود و یک واحد قرنطینه", UserId, Now.AddMinutes(2));

        Assert.Equal(GoodsReceiptStatus.PartiallyAccepted, receipt.Status);
        Assert.Equal(8m, receipt.AcceptedBaseQuantity);
        Assert.Null(receipt.StockPostedAt);
    }

    [Fact]
    public void OnlyAcceptedQuantityCanBeExplicitlyPosted()
    {
        var receipt = Receipt(10m);
        Assert.Throws<DomainRuleException>(() => receipt.MarkStockPosted(1, Now));
        receipt.SubmitForInspection(1);
        receipt.RecordInspection(2, 10m, 0m, 0m, "کمی", null, null, UserId, Now);
        receipt.MarkStockPosted(3, Now.AddMinutes(1));

        Assert.Equal(10m, receipt.AcceptedBaseQuantity);
        Assert.NotNull(receipt.StockPostedAt);
    }

    [Fact]
    public void LedgerEnforcesMovementDirection()
    {
        Assert.Throws<DomainRuleException>(() => Ledger(InventoryEventType.AcceptedReceipt, -1m));
        Assert.Throws<DomainRuleException>(() => Ledger(InventoryEventType.MaterialIssue, 1m));
        Assert.Throws<DomainRuleException>(() => Ledger(InventoryEventType.Reversal, -1m));

        var incoming = Ledger(InventoryEventType.AcceptedReceipt, 4m);
        var outgoing = Ledger(InventoryEventType.MaterialIssue, -2m);
        Assert.Equal(2m, incoming.BaseQuantityDelta + outgoing.BaseQuantityDelta);
    }

    [Fact]
    public void IssueAndConsumptionRemainSeparateFacts()
    {
        var issue = Issue(10m);

        Assert.Equal(MaterialIssueStatus.Issued, issue.Status);
        Assert.Equal(0m, issue.ConsumedBaseQuantity);
        Assert.Equal(10m, issue.UnreconciledBaseQuantity);
        Assert.Null(issue.WbsReference);

        issue.Reconcile(1, 6m, 2m, 0m, null, ["evidence:usage"], Now.AddMinutes(1));
        Assert.Equal(MaterialIssueStatus.PartiallyReconciled, issue.Status);
        Assert.Equal(2m, issue.UnreconciledBaseQuantity);
    }

    [Fact]
    public void ReconciliationCannotExceedCustodyAndWasteNeedsEvidence()
    {
        var issue = Issue(5m);

        Assert.Throws<DomainRuleException>(() => issue.Reconcile(1, 6m, 0m, 0m, null, null, Now));
        Assert.Throws<DomainRuleException>(() => issue.Reconcile(1, 0m, 0m, 1m, "شکستگی", [], Now));

        issue.Reconcile(1, 3m, 1m, 1m, "شکستگی", ["evidence:waste-photo"], Now);
        Assert.Equal(MaterialIssueStatus.Reconciled, issue.Status);
        Assert.Equal(1m, issue.WasteBaseQuantity);
    }

    [Fact]
    public void CountVarianceRequiresApprovalBeforeAppendOnlyPosting()
    {
        var adjustment = InventoryAdjustment.Propose(
            Guid.NewGuid(), TenantId, ProjectId, Guid.NewGuid(), Guid.NewGuid(), Now,
            10m, 8m, "EA", "شمارش دوره‌ای", ["evidence:count-sheet"], UserId, Now);

        Assert.Equal(-2m, adjustment.DeltaBaseQuantity);
        Assert.Throws<DomainRuleException>(() => adjustment.MarkPosted(1, Now));
        adjustment.Review(1, true, UserId, Now.AddMinutes(1), "کنترل شد");
        adjustment.MarkPosted(2, Now.AddMinutes(2));

        Assert.Equal(InventoryAdjustmentStatus.Posted, adjustment.Status);
        Assert.Equal(10m, adjustment.SystemBaseQuantity);
        Assert.Equal(8m, adjustment.CountedBaseQuantity);
    }

    [Fact]
    public void ServiceAcceptanceBalancesWithoutCreatingStockSemantics()
    {
        var acceptance = ServiceAcceptance.Create(
            Guid.NewGuid(), TenantId, ProjectId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 11), 40m, 36m, 4m, "HOUR",
            "صورت‌جلسه کارکرد", ["evidence:service-sheet"], "چهار ساعت مردود", UserId, Now);

        Assert.Equal(40m, acceptance.AcceptedBaseQuantity + acceptance.RejectedBaseQuantity);
        Assert.StartsWith("SAC-14050620-", acceptance.Number);
        Assert.Throws<DomainRuleException>(() => ServiceAcceptance.Create(
            Guid.NewGuid(), TenantId, ProjectId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 11), 40m, 39m, 0m, "HOUR",
            "صورت‌جلسه", ["evidence:service-sheet"], null, UserId, Now));
    }

    [Fact]
    public void ProcurementDemandKeepsWbsAndBudgetOptionalButQuantityBasisAtomic()
    {
        var itemId = Guid.NewGuid();
        var request = PurchaseRequest.Create(
            Guid.NewGuid(), TenantId, ProjectId, "PR-15", "میلگرد", "نیاز واقعی کارگاه",
            null, "IRR", null, UserId, Now, itemId, 20m, "TON", "کارگاه", null, null, null,
            BudgetCheckStatus.NotConfigured, ProcurementCriticality.Critical);

        Assert.Null(request.WbsReference);
        Assert.Null(request.BudgetLineReference);
        Assert.Equal(BudgetCheckStatus.NotConfigured, request.BudgetCheckStatus);
        Assert.Equal(20m, request.RequestedQuantity);

        Assert.Throws<DomainRuleException>(() => PurchaseRequest.Create(
            Guid.NewGuid(), TenantId, ProjectId, "PR-16", "کابل", "نیاز کارگاه",
            null, "IRR", null, UserId, Now, null, 10m, "M", "کارگاه"));
        Assert.Throws<DomainRuleException>(() => PurchaseOrder.Issue(
            Guid.NewGuid(), TenantId, ProjectId, Guid.NewGuid(), Guid.NewGuid(), null,
            "PO-16", "سفارش ناقص", 100m, "IRR", null, UserId, Now,
            itemId, null, null, null));
    }

    private static SupplyItem Material() => SupplyItem.Create(
        Guid.NewGuid(), TenantId, ProjectId, "MAT-1", "میلگرد", SupplyItemKind.Material,
        "فلزات", "EA", null, SupplyTrackingPolicy.Quantity, true, null, "تأیید آزمایشگاه", UserId, Now);

    private static GoodsReceipt Receipt(decimal quantity)
    {
        var basis = new ConvertedQuantity(quantity, "EA", quantity, "EA", 1);
        return GoodsReceipt.Create(
            Guid.NewGuid(), TenantId, ProjectId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "DN-1", Now, "انبار کارگاه", basis, basis, 0m, null, "سالم", null, null,
            ["evidence:delivery"], UserId, Now);
    }

    private static MaterialIssue Issue(decimal quantity) => MaterialIssue.Create(
        Guid.NewGuid(), TenantId, ProjectId, Guid.NewGuid(), Guid.NewGuid(), quantity, "EA",
        "اکیپ نصب", "طبقه دوم", null, null, null, ["evidence:handover"], UserId, Now);

    private static InventoryLedgerEntry Ledger(InventoryEventType type, decimal quantity) => InventoryLedgerEntry.Create(
        Guid.NewGuid(), TenantId, ProjectId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), type,
        quantity, "EA", "Test", Guid.NewGuid(), null, null, Now, UserId, Now);
}

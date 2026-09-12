using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Commercial.Domain;

namespace Pmcs.Domain.Tests;

public sealed class ProcurementWorkflowTests
{
    [Fact]
    public void RequestWithoutEstimateCanStillBeApprovedAndOrdered()
    {
        var request = CreateRequest(null);
        request.Submit(1, DateTimeOffset.UtcNow);
        request.Approve(2, Guid.NewGuid(), DateTimeOffset.UtcNow, null);

        var order = PurchaseOrder.Issue(
            Guid.NewGuid(), request.TenantId, request.ProjectId, request.Id, Guid.NewGuid(), null,
            "PO-001", "Approved steel purchase", 5_000m, "IRR", null, Guid.NewGuid(), DateTimeOffset.UtcNow);
        request.MarkOrdered(3, DateTimeOffset.UtcNow);

        Assert.Null(request.EstimatedAmount);
        Assert.Equal(PurchaseRequestStatus.Ordered, request.Status);
        Assert.Equal(PurchaseOrderStatus.Issued, order.Status);
    }

    [Fact]
    public void DraftRequestCannotBecomeAnOrder()
    {
        var request = CreateRequest(1_000m);

        Assert.Throws<DomainRuleException>(() => request.MarkOrdered(1, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void IssuedOrderCanBeClosedButNotChangedAgain()
    {
        var order = PurchaseOrder.Issue(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            "PO-002", "Equipment rental", 2_000m, "IRR", null, Guid.NewGuid(), DateTimeOffset.UtcNow);
        order.Close(1, Guid.NewGuid(), DateTimeOffset.UtcNow, "delivered");

        Assert.Equal(PurchaseOrderStatus.Closed, order.Status);
        Assert.Throws<DomainRuleException>(() =>
            order.Cancel(2, Guid.NewGuid(), DateTimeOffset.UtcNow, "late"));
    }

    private static PurchaseRequest CreateRequest(decimal? estimatedAmount) => PurchaseRequest.Create(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PR-001", "Steel", "Rebar for level two",
        estimatedAmount, "IRR", null, Guid.NewGuid(), DateTimeOffset.UtcNow);
}

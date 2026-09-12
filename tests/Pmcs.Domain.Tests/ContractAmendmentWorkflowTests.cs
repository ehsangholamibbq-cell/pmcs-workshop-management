using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Commercial.Domain;

namespace Pmcs.Domain.Tests;

public sealed class ContractAmendmentWorkflowTests
{
    [Fact]
    public void ScopeChangeDoesNotInventAMonetaryDelta()
    {
        var amendment = ContractAmendment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "AMD-001", "Scope clarification", ContractAmendmentType.ScopeChange,
            null, "IRR", null, "Approved drawing revision", Guid.NewGuid(), DateTimeOffset.UtcNow);

        amendment.Submit(1, DateTimeOffset.UtcNow);
        amendment.Approve(2, Guid.NewGuid(), DateTimeOffset.UtcNow, null);

        Assert.Null(amendment.AmountDelta);
        Assert.Equal(ContractAmendmentStatus.Approved, amendment.Status);
    }

    [Fact]
    public void ValueAmendmentRequiresANonZeroDelta()
    {
        Assert.Throws<DomainRuleException>(() => ContractAmendment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "AMD-002", "Value change", ContractAmendmentType.ValueChange,
            0m, "IRR", null, null, Guid.NewGuid(), DateTimeOffset.UtcNow));
    }
}

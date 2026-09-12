using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Commercial.Domain;

namespace Pmcs.Domain.Tests;

public sealed class ProjectContractWorkflowTests
{
    [Fact]
    public void ContractWithoutInitialCeilingCanBeActivated()
    {
        var contract = CreateContract(null);

        contract.Submit(1, DateTimeOffset.UtcNow);
        contract.Activate(2, Guid.NewGuid(), DateTimeOffset.UtcNow, "verified");

        Assert.Null(contract.OriginalApprovedAmount);
        Assert.Equal(ProjectContractStatus.Active, contract.Status);
        Assert.Equal(3, contract.Revision);
    }

    [Fact]
    public void ReturnedContractCanBeCorrectedAndResubmitted()
    {
        var contract = CreateContract(1_000m);
        contract.Submit(1, DateTimeOffset.UtcNow);
        contract.Return(2, Guid.NewGuid(), DateTimeOffset.UtcNow, "correct dates");

        contract.Amend(
            3,
            contract.PartyId,
            contract.Number,
            "Corrected title",
            contract.Type,
            1_200m,
            contract.CurrencyCode,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 12, 1),
            null,
            DateTimeOffset.UtcNow);
        contract.Submit(4, DateTimeOffset.UtcNow);

        Assert.Equal(ProjectContractStatus.Submitted, contract.Status);
        Assert.Equal(1_200m, contract.OriginalApprovedAmount);
        Assert.Equal(5, contract.Revision);
    }

    [Fact]
    public void ActiveContractCannotBeSilentlyEdited()
    {
        var contract = CreateContract(1_000m);
        contract.Submit(1, DateTimeOffset.UtcNow);
        contract.Activate(2, Guid.NewGuid(), DateTimeOffset.UtcNow, null);

        Assert.Throws<DomainRuleException>(() => contract.Amend(
            3,
            contract.PartyId,
            contract.Number,
            contract.Title,
            contract.Type,
            2_000m,
            contract.CurrencyCode,
            null,
            null,
            null,
            DateTimeOffset.UtcNow));
    }

    private static ProjectContract CreateContract(decimal? amount) => ProjectContract.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        "CTR-001",
        "Concrete supply",
        ProjectContractType.Supply,
        amount,
        "IRR",
        null,
        null,
        null,
        Guid.NewGuid(),
        DateTimeOffset.UtcNow);
}

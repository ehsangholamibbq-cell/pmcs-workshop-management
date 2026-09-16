using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Finance.Domain;

namespace Pmcs.Domain.Tests;

public sealed class FinancialRecordWorkflowTests
{
    [Fact]
    public void RecordMustBeSubmittedBeforeItCanBePosted()
    {
        var record = CreateRecord();

        Assert.Throws<DomainRuleException>(() =>
            record.Post(1, null, Guid.NewGuid(), DateTimeOffset.UtcNow));

        record.Submit(1, DateTimeOffset.UtcNow);
        record.Post(2, "verified", Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Equal(FinancialRecordStatus.Posted, record.Status);
        Assert.Equal(3, record.Revision);
    }

    [Fact]
    public void ReturnRequiresAReasonAndPreservesRevisionControl()
    {
        var record = CreateRecord();
        record.Submit(1, DateTimeOffset.UtcNow);

        Assert.Throws<DomainRuleException>(() =>
            record.ReturnForCorrection(2, " ", Guid.NewGuid(), DateTimeOffset.UtcNow));
        Assert.Throws<DomainRuleException>(() =>
            record.ReturnForCorrection(1, "missing document", Guid.NewGuid(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ReturnedRecordCanBeAmendedAndResubmitted()
    {
        var record = CreateRecord();
        record.Submit(1, DateTimeOffset.UtcNow);
        record.ReturnForCorrection(2, "correct amount", Guid.NewGuid(), DateTimeOffset.UtcNow);

        record.Amend(
            3,
            record.Type,
            record.TransactionDate,
            1_500_000m,
            record.CurrencyCode,
            "Corrected supplier payment",
            record.Counterparty,
            record.DocumentNumber,
            record.ContractReference,
            record.ContractId,
            record.CommitmentId,
            record.CostCenterCode);
        record.Submit(4, DateTimeOffset.UtcNow);

        Assert.Equal(1_500_000m, record.Amount);
        Assert.Equal(FinancialRecordStatus.Submitted, record.Status);
        Assert.Equal(5, record.Revision);
    }

    [Fact]
    public void RecordPreservesStablePartyLocationAndWbsLineage()
    {
        var partyId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var record = FinancialRecord.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), FinancialRecordType.Payment,
            new DateOnly(2026, 9, 9), 100m, "IRR", "Location-linked payment", null, null,
            null, null, null, "CC-1", Guid.NewGuid(), DateTimeOffset.UtcNow,
            partyId, locationId, "ZONE-A", "WBS-1");

        Assert.Equal(partyId, record.PartyId);
        Assert.Equal(locationId, record.LocationId);
        Assert.Equal("ZONE-A", record.LocationCode);
        Assert.Equal("WBS-1", record.WbsReference);
    }

    private static FinancialRecord CreateRecord() => FinancialRecord.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        FinancialRecordType.Payment,
        new DateOnly(2026, 9, 9),
        1_250_000m,
        "IRR",
        "Concrete supplier payment",
        "Supplier",
        "PAY-001",
        null,
        null,
        null,
        null,
        Guid.NewGuid(),
        DateTimeOffset.UtcNow);
}

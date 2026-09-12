using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.TechnicalOffice.Domain;

namespace Pmcs.Domain.Tests;

public sealed class TechnicalOfficeWorkflowTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ProjectId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);
    private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void DocumentNumberIsIssuedByServerAndOptionalLinksStayOptional()
    {
        var document = CreateDocument();

        Assert.StartsWith("DOC-14050620-", document.Number);
        Assert.Null(document.ContractId);
        Assert.Null(document.WbsReference);
        Assert.Null(document.CurrentOfficialRevisionId);
    }

    [Fact]
    public void RevisionContentRemainsStableAcrossReviewAndIssue()
    {
        var document = CreateDocument();
        var revision = CreateRevision(document.Id);
        var fileReference = revision.FileReference;
        var sha256 = revision.Sha256;

        revision.Submit(1, Now.AddMinutes(1));
        revision.Approve(2, UserId, Now.AddMinutes(2), "تأیید فنی");
        revision.Issue(Guid.NewGuid(), Now.AddMinutes(3));
        document.MakeCurrent(revision.Id);
        var issuedAt = revision.IssuedAt;
        revision.Supersede(Now.AddMinutes(4));

        Assert.Equal(DocumentRevisionStatus.Superseded, revision.Status);
        Assert.Equal(revision.Id, document.CurrentOfficialRevisionId);
        Assert.Equal(fileReference, revision.FileReference);
        Assert.Equal(sha256, revision.Sha256);
        Assert.Equal(issuedAt, revision.IssuedAt);
        Assert.Equal(Now.AddMinutes(4), revision.SupersededAt);
        Assert.Equal(5, revision.Revision);
    }

    [Fact]
    public void UnapprovedRevisionCannotBeIssued()
    {
        var revision = CreateRevision(CreateDocument().Id);
        Assert.Throws<DomainRuleException>(() => revision.Issue(Guid.NewGuid(), Now));
    }

    [Fact]
    public void RevisionRejectsInvalidHash()
    {
        Assert.Throws<DomainRuleException>(() => TechnicalDocumentRevision.Create(
            Guid.NewGuid(), TenantId, ProjectId, CreateDocument().Id, "A", DateOnly.FromDateTime(Now.Date),
            DocumentRevisionPurpose.ForReview, "drawing.pdf", "evidence:1", "bad-hash", null, UserId, Now));
    }

    [Fact]
    public void TransmittalIsSeparateFromUploadAndHasAcknowledgmentLifecycle()
    {
        var transmittal = TechnicalTransmittal.Create(
            Guid.NewGuid(), TenantId, ProjectId, "دفتر فنی", ["مشاور"], [Guid.NewGuid()],
            "صدور جهت اجرا", "سامانه رسمی", null, UserId, Now);

        Assert.Equal(TransmittalStatus.Draft, transmittal.Status);
        transmittal.Issue(1, UserId, Now.AddMinutes(1));
        Assert.Equal(TransmittalStatus.Issued, transmittal.Status);
        transmittal.Acknowledge(2, UserId, Now.AddMinutes(2), "رسید-۱");
        Assert.Equal(TransmittalStatus.Acknowledged, transmittal.Status);
    }

    [Fact]
    public void RfiNeedsEvidenceBeforeInternalReview()
    {
        var rfi = CreateRfi([]);
        Assert.Throws<DomainRuleException>(() => rfi.SubmitForInternalReview(1));
    }

    [Fact]
    public void AnsweredRfiCannotCloseBeforeHumanAcceptance()
    {
        var rfi = IssuedRfi();
        rfi.RecordResponse(
            3, "مسیر مطابق نقشه اصلاحی اجرا شود.", "مشاور", Now.AddHours(1),
            RfiResponseClassification.NewRevisionRequired, true, null, UserId);

        Assert.Equal(RfiStatus.Answered, rfi.Status);
        Assert.Throws<DomainRuleException>(() => rfi.Close(4, Now.AddHours(2)));
        rfi.AcceptResponse(4, UserId, Now.AddHours(2), "پاسخ کافی است");
        rfi.Close(5, Now.AddHours(3));
        Assert.Equal(RfiStatus.Closed, rfi.Status);
    }

    [Fact]
    public void ExternalResponseCapturesSourceAndInternalReceiver()
    {
        var rfi = IssuedRfi();
        rfi.RecordResponse(
            3, "نیازمند جزئیات بیشتر", "مشاور", Now.AddHours(1),
            RfiResponseClassification.DesignClarification, false, null, UserId);
        rfi.RequireClarification(4, UserId, Now.AddHours(2), "جزئیات رایزر مشخص نیست");
        rfi.RecordResponse(
            5, "جزئیات رایزر در پیوست آمده است", "مشاور", Now.AddHours(3),
            RfiResponseClassification.NewRevisionRequired, false, [Guid.NewGuid()], UserId);

        Assert.Equal(2, rfi.Responses.Count(response => response.Source == "ExternalResponse"));
        Assert.All(rfi.Responses.Where(response => response.Source == "ExternalResponse"),
            response => Assert.Equal(UserId, response.ReceivedBy));
        Assert.Equal(RfiStatus.Answered, rfi.Status);
    }

    [Fact]
    public void RfiAcceptsIndependentImpactDimensions()
    {
        var rfi = CreateRfi(["evidence:photo-1"], PotentialImpact.Time | PotentialImpact.Cost | PotentialImpact.Scope);
        Assert.True(rfi.PotentialImpact.HasFlag(PotentialImpact.Time));
        Assert.True(rfi.PotentialImpact.HasFlag(PotentialImpact.Cost));
        Assert.False(rfi.PotentialImpact.HasFlag(PotentialImpact.Safety));
    }

    [Fact]
    public void SubmittalApprovalDoesNotImplyMaterialOrSiteAcceptance()
    {
        var submittal = CreateSubmittal();
        submittal.Submit(1, Now.AddMinutes(1));
        submittal.BeginReview(2);
        submittal.RecordReview(3, SubmittalReviewOutcome.Approved, UserId, Now.AddMinutes(2), null);

        Assert.Equal(SubmittalStatus.Approved, submittal.Status);
        Assert.Null(submittal.ClosedAt);
        submittal.Close(4, Now.AddMinutes(3));
        Assert.Equal(SubmittalStatus.Closed, submittal.Status);
    }

    [Fact]
    public void RejectedOrReviseOutcomeRequiresReason()
    {
        var submittal = CreateSubmittal();
        submittal.Submit(1, Now);
        submittal.BeginReview(2);
        Assert.Throws<DomainRuleException>(() => submittal.RecordReview(
            3, SubmittalReviewOutcome.ReviseAndResubmit, UserId, Now, null));
    }

    [Fact]
    public void ResubmissionMustReferencePrecedingPackage()
    {
        Assert.Throws<DomainRuleException>(() => TechnicalSubmittal.Create(
            Guid.NewGuid(), TenantId, ProjectId, "مصالح نما", TechnicalSubmittalType.MaterialOrProductData,
            "معماری", "پیمانکار", "مشاور", null, null, null, null, null, null, null, null,
            1, null, [Guid.NewGuid()], null, UserId, Now));
    }

    private static TechnicalDocument CreateDocument() => TechnicalDocument.Create(
        Guid.NewGuid(), TenantId, ProjectId, "نقشه مسیر کابل", TechnicalDocumentType.Drawing,
        "برق", "مشاور", null, "طبقه چهارم", null, null, null, UserId, Now);

    private static TechnicalDocumentRevision CreateRevision(Guid documentId) => TechnicalDocumentRevision.Create(
        Guid.NewGuid(), TenantId, ProjectId, documentId, "A", DateOnly.FromDateTime(Now.Date),
        DocumentRevisionPurpose.ForApproval, "electrical-a.pdf", "evidence:electrical-a", Hash,
        null, UserId, Now);

    private static TechnicalRfi CreateRfi(
        IReadOnlyCollection<string> evidence,
        PotentialImpact impact = PotentialImpact.Time) => TechnicalRfi.Create(
            Guid.NewGuid(), TenantId, ProjectId, "ابهام مسیر کابل", "مسیر کابل طبقه چهارم کدام است؟",
            "مشاور", "برق", null, "طبقه چهارم", null, null, null,
            DateOnly.FromDateTime(Now.Date), DateOnly.FromDateTime(Now.AddDays(2).Date), impact,
            true, null, evidence, null, UserId, Now);

    private static TechnicalRfi IssuedRfi()
    {
        var rfi = CreateRfi(["evidence:photo-1"]);
        rfi.SubmitForInternalReview(1);
        rfi.Issue(2, Now.AddMinutes(1));
        return rfi;
    }

    private static TechnicalSubmittal CreateSubmittal() => TechnicalSubmittal.Create(
        Guid.NewGuid(), TenantId, ProjectId, "مصالح نما", TechnicalSubmittalType.MaterialOrProductData,
        "معماری", "پیمانکار", "مشاور", null, null, null, null, null,
        DateOnly.FromDateTime(Now.AddDays(3).Date), DateOnly.FromDateTime(Now.Date),
        DateOnly.FromDateTime(Now.AddDays(2).Date), 0, null, [Guid.NewGuid()], null, UserId, Now);
}

using System.Text.Json;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Services;
using Pmcs.Modules.TechnicalOffice.Contracts;
using Pmcs.Modules.TechnicalOffice.Domain;
using Pmcs.Modules.TechnicalOffice.Services;

namespace Pmcs.Domain.Tests;

public sealed class ProjectTechnicalOfficeReportingTests
{
    private static readonly Guid Tenant = Id(1);
    private static readonly Guid Project = Id(2);
    private static readonly DateTimeOffset Cutoff = new(2026, 9, 21, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public void F07DraftAndApprovedWithoutIssueDoNotBecomeOfficial()
    {
        var revision = Revision(11, [Event(1, TechnicalReportingEventType.Submitted, -4),
            Event(2, TechnicalReportingEventType.Approved, -3)]) with
        { IssuedThroughTransmittalId = null };
        var result = Calculate(Projection(documents: [Document()], revisions: [revision]));
        Assert.Equal(TechnicalReportingStatus.NoData, result.Documents.Status);
        Assert.Equal(0, result.Documents.OfficialCount);
        Assert.Empty(result.Documents.Rows);
    }

    [Fact]
    public void F07IssueAndAcknowledgeRespectIndependentCutoffsAndDueDate()
    {
        var transmittal = Transmittal(events: [Event(1, TechnicalReportingEventType.Issued, -1),
            Event(2, TechnicalReportingEventType.Acknowledged, 1)]) with
        { DueResponseDate = new DateOnly(2026, 9, 20) };
        var result = Calculate(Projection(documents: [Document()], revisions: [Revision()],
            transmittals: [transmittal]));
        Assert.Equal("A", Assert.Single(result.Documents.Rows).RevisionCode);
        Assert.Equal(TechnicalReportingDueState.Overdue, Assert.Single(result.Transmittals.Rows).DueState);
        Assert.Null(Assert.Single(result.Transmittals.Rows).AcknowledgedAtUtc);
        var later = Calculate(Projection(documents: [Document()], revisions: [Revision()],
            transmittals: [transmittal], cutoff: Cutoff.AddDays(2)));
        Assert.Equal(TechnicalReportingDueState.Acknowledged, Assert.Single(later.Transmittals.Rows).DueState);
    }

    [Fact]
    public void F07SupersessionChangesOfficialRevisionOnlyAtNewIssue()
    {
        var original = Revision() with { Events = [
            Event(1, TechnicalReportingEventType.Submitted, -5),
            Event(2, TechnicalReportingEventType.Approved, -4),
            Event(3, TechnicalReportingEventType.Issued, -3),
            Event(4, TechnicalReportingEventType.Superseded, 1)] };
        var replacement = Revision(12, [
            Event(1, TechnicalReportingEventType.Submitted, -2),
            Event(2, TechnicalReportingEventType.Approved, -1),
            Event(3, TechnicalReportingEventType.Issued, 1)], 21) with
        { Code = "B", SupersedesRevisionId = original.Id };
        var a = Transmittal() with
        {
            CreatedAtUtc = Cutoff.AddDays(-4),
            Events = [Event(1, TechnicalReportingEventType.Issued, -3)]
        };
        var b = Transmittal(21, [replacement.Id], [Event(1, TechnicalReportingEventType.Issued, 1)]);
        var initial = Calculate(Projection(documents: [Document()], revisions: [original, replacement],
            transmittals: [a, b]));
        Assert.Equal("A", Assert.Single(initial.Documents.Rows).RevisionCode);
        var later = Calculate(Projection(documents: [Document()], revisions: [original, replacement],
            transmittals: [a, b], cutoff: Cutoff.AddDays(2)));
        Assert.Equal("B", Assert.Single(later.Documents.Rows).RevisionCode);
    }

    [Fact]
    public void F07ContradictoryOfficialIssueFailsRatherThanSelectingLatest()
    {
        var other = Revision(12, [Event(1, TechnicalReportingEventType.Submitted, -3),
            Event(2, TechnicalReportingEventType.Approved, -2),
            Event(3, TechnicalReportingEventType.Issued, -1)]) with { Code = "B" };
        var transaction = Transmittal() with { RevisionIds = [Id(11), Id(12)] };
        Assert.Equal("technical.project_reporting.transmittal.document.duplicate",
            Assert.Throws<DomainRuleException>(() => Calculate(Projection(
                documents: [Document()], revisions: [Revision(), other],
                transmittals: [transaction]))).Code);
    }

    [Fact]
    public void F07IncompleteHistoricalSectionsRemainInsufficientWhileSafeDocumentsSurvive()
    {
        var result = Calculate(Projection(documents: [Document()], revisions: [Revision()],
            transmittals: [Transmittal()], rfis: [Rfi()], submittals: [Submittal()],
            rfiCoverage: TechnicalReportingCompleteness.Incomplete,
            submittalCoverage: TechnicalReportingCompleteness.Incomplete));
        Assert.Equal(TechnicalReportingStatus.Available, result.Documents.Status);
        Assert.Equal(TechnicalReportingStatus.Available, result.Transmittals.Status);
        Assert.Equal(TechnicalReportingStatus.InsufficientData, result.Rfis.Status);
        Assert.Equal(TechnicalReportingStatus.InsufficientData, result.Submittals.Status);
        Assert.Null(result.Rfis.OfficialCount);
        Assert.Empty(result.Rfis.Rows);
        Assert.Equal(TechnicalReportingStatus.InsufficientData, result.DataStatus);
    }

    [Fact]
    public void F07EmptyCompleteSourceIsNoDataWithProvenZero()
    {
        var result = Calculate(Projection());
        Assert.Equal(TechnicalReportingStatus.NoData, result.DataStatus);
        Assert.All(new[] { result.Documents.OfficialCount, result.Transmittals.OfficialCount,
            result.Rfis.OfficialCount, result.Submittals.OfficialCount }, count => Assert.Equal(0, count));
    }

    [Fact]
    public void F07RfiIssueResponseAcceptanceAndCloseRemainSeparate()
    {
        var events = new[] { Event(1, TechnicalReportingEventType.InternalReview, -5),
            Event(2, TechnicalReportingEventType.Issued, -4),
            Event(3, TechnicalReportingEventType.ResponseReceived, -3) with
            { ResponseClassification = RfiResponseClassification.ChangePotential, ChangePotential = true },
            Event(4, TechnicalReportingEventType.ResponseAccepted, -2),
            Event(5, TechnicalReportingEventType.Closed, 1) };
        var result = Calculate(Projection(rfis: [Rfi() with { Events = events }]));
        var row = Assert.Single(result.Rfis.Rows);
        Assert.Equal(TechnicalReportingEventType.ResponseAccepted, row.State);
        Assert.Equal(1, row.ResponseCount);
        Assert.Equal(RfiResponseClassification.ChangePotential, row.LastResponseClassification);
        Assert.True(row.IsBlocking);
        var later = Calculate(Projection(rfis: [Rfi() with { Events = events }], cutoff: Cutoff.AddDays(2)));
        Assert.Equal(TechnicalReportingEventType.Closed, Assert.Single(later.Rfis.Rows).State);
    }

    [Fact]
    public void F07RfiDraftAndInternalReviewDoNotCountAsIssued()
    {
        var result = Calculate(Projection(rfis: [Rfi() with
        { Events = [Event(1, TechnicalReportingEventType.InternalReview, -2)] }]));
        Assert.Equal(TechnicalReportingStatus.NoData, result.Rfis.Status);
        Assert.Equal(0, result.Rfis.OfficialCount);
    }

    [Fact]
    public void F07ForInformationOutcomeDoesNotIssueDocument()
    {
        var submittal = Submittal() with { Events = [
            Event(1, TechnicalReportingEventType.Submitted, -3),
            Event(2, TechnicalReportingEventType.UnderReview, -2),
            Event(3, TechnicalReportingEventType.Reviewed, -1) with
            { ReviewOutcome = SubmittalReviewOutcome.ForInformation }] };
        var result = Calculate(Projection(documents: [Document()],
            revisions: [Revision(11, [Event(1, TechnicalReportingEventType.Submitted, -4),
                Event(2, TechnicalReportingEventType.Approved, -3)]) with
                { IssuedThroughTransmittalId = null }], submittals: [submittal]));
        Assert.Equal(SubmittalReviewOutcome.ForInformation, Assert.Single(result.Submittals.Rows).ReviewOutcome);
        Assert.Equal(TechnicalReportingSubmittalState.ForInformation, Assert.Single(result.Submittals.Rows).State);
        Assert.Equal(TechnicalReportingStatus.NoData, result.Documents.Status);
    }

    [Fact]
    public void F07ReviewDueDateEqualityIsNotOverdue()
    {
        var item = Submittal() with
        {
            ReviewDueDate = new DateOnly(2026, 9, 21),
            Events = [Event(1, TechnicalReportingEventType.Submitted, -1)]
        };
        var result = Calculate(Projection(documents: [Document()],
            revisions: [Revision()], transmittals: [Transmittal()], submittals: [item]));
        Assert.Equal(false, Assert.Single(result.Submittals.Rows).ReviewOverdue);
    }

    [Fact]
    public void F07ScopeAndLineageErrorsAreProcessingFailures()
    {
        var crossProject = Document() with { ProjectId = Id(999) };
        Assert.Throws<DomainRuleException>(() => Calculate(Projection(documents: [crossProject])));
        var gap = Revision() with { Events = [Event(1, TechnicalReportingEventType.Submitted, -3),
            Event(3, TechnicalReportingEventType.Approved, -2)] };
        Assert.Throws<DomainRuleException>(() => Calculate(Projection(documents: [Document()], revisions: [gap])));
        var cycle = Revision() with { SupersedesRevisionId = Id(11) };
        Assert.Throws<DomainRuleException>(() => Calculate(Projection(documents: [Document()], revisions: [cycle])));
    }

    [Fact]
    public void F07RestrictedRequiresPolicyAndUnknownClassificationFails()
    {
        Assert.Equal("technical.project_reporting.classification.restricted",
            Assert.Throws<DomainRuleException>(() => Calculate(Projection(
                documents: [Document() with { Classification = TechnicalReportingClassification.Restricted }]))).Code);
        Assert.Throws<DomainRuleException>(() => Calculate(Projection(
            documents: [Document() with { Classification = (TechnicalReportingClassification)99 }])));
        var approved = Calculate(Projection(
            documents: [Document() with { Classification = TechnicalReportingClassification.Restricted }],
            restrictedApproved: true));
        Assert.Equal(TechnicalReportingClassification.Restricted, approved.Classification);
    }

    [Fact]
    public void F07BudgetAndDuplicateIdentityFailClosed()
    {
        var duplicated = Projection(documents: [Document(), Document()]);
        Assert.Throws<DomainRuleException>(() => Calculate(duplicated));
        var overflow = Projection() with
        { Documents = Enumerable.Range(0, ProjectTechnicalOfficeReportingContract.MaximumDocuments + 1)
            .Select(i => Document() with { Id = Id(1000 + i) }).ToArray() };
        Assert.Equal("technical.project_reporting.documents.budget",
            Assert.Throws<DomainRuleException>(() => Calculate(overflow)).Code);
    }

    [Fact]
    public void F07ManifestAndSemanticHashDoNotDependOnQueryOrderOrRunIdentity()
    {
        var one = Calculate(Projection(documents: [Document(), Document(12)],
            revisions: [Revision(), Revision(13, [], 0) with { DocumentId = Id(12) }],
            transmittals: [Transmittal()]));
        var two = Calculate(Projection(documents: [Document(12), Document()],
            revisions: [Revision(13, [], 0) with { DocumentId = Id(12) }, Revision()],
            transmittals: [Transmittal()]));
        Assert.Equal(one.SourceManifestSha256, two.SourceManifestSha256);
        Assert.Equal(one.SemanticSha256, two.SemanticSha256);
    }

    [Fact]
    public void F07SnapshotIsMinimizedAndTamperingIsRejected()
    {
        var result = Calculate(Projection(documents: [Document()], revisions: [Revision()],
            transmittals: [Transmittal()]));
        var profile = ProjectTechnicalOfficePinnedProjectProfile.Capture(Profile(), Cutoff.AddMinutes(1));
        var snapshot = ProjectTechnicalOfficeReportSnapshotBuilder.Build(
            Id(900), Tenant, profile, Cutoff, result, Cutoff.AddMinutes(1), Cutoff.AddMinutes(2));
        Assert.Equal(ReportDataStatus.Available, snapshot.DataStatus);
        Assert.Equal(ReportClassification.Confidential, snapshot.Classification);
        Assert.DoesNotContain("fileReference", snapshot.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("respondingParty", snapshot.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(result.SourceManifestSha256, snapshot.SourceManifestSha256);
        Assert.Equal(2, ProjectTechnicalOfficeReportRuntimeContract.RequiredSourcePermissions.Count);
        Assert.Throws<DomainRuleException>(() => ProjectTechnicalOfficeReportSnapshotBuilder.Build(
            Id(900), Tenant, profile, Cutoff, result with { SemanticSha256 = new string('0', 64) },
            Cutoff.AddMinutes(1), Cutoff.AddMinutes(2)));
    }

    private static ProjectTechnicalOfficeReportingResult Calculate(ProjectTechnicalOfficeReportingProjection source) =>
        ProjectTechnicalOfficeReportingCalculator.Calculate(source);

    private static ProjectTechnicalOfficeReportingProjection Projection(
        IReadOnlyCollection<TechnicalReportingDocument>? documents = null,
        IReadOnlyCollection<TechnicalReportingRevision>? revisions = null,
        IReadOnlyCollection<TechnicalReportingTransmittal>? transmittals = null,
        IReadOnlyCollection<TechnicalReportingRfi>? rfis = null,
        IReadOnlyCollection<TechnicalReportingSubmittal>? submittals = null,
        DateTimeOffset? cutoff = null,
        TechnicalReportingCompleteness rfiCoverage = TechnicalReportingCompleteness.Complete,
        TechnicalReportingCompleteness submittalCoverage = TechnicalReportingCompleteness.Complete,
        bool restrictedApproved = false)
    {
        var at = cutoff ?? Cutoff;
        return new ProjectTechnicalOfficeReportingProjection(
            ProjectTechnicalOfficeReportingContract.Version, Tenant, Project,
            DateOnly.FromDateTime(at.UtcDateTime), at, 1, Cutoff.AddDays(-30), true,
            TechnicalReportingClassification.Confidential, restrictedApproved,
            documents ?? [], revisions ?? [], transmittals ?? [], rfis ?? [], submittals ?? [],
            TechnicalReportingCompleteness.Complete, TechnicalReportingCompleteness.Complete,
            rfiCoverage, submittalCoverage);
    }

    private static TechnicalReportingDocument Document(int id = 10) => new(
        Id(id), Tenant, Project, $"DOC-{id}", TechnicalDocumentType.Drawing,
        "CIVIL", Cutoff.AddDays(-10), TechnicalReportingClassification.Confidential);

    private static TechnicalReportingRevision Revision(
        int id = 11, IReadOnlyCollection<TechnicalReportingEvent>? events = null, int transmittalId = 20) => new(
        Id(id), Tenant, Project, Id(10), "A", new DateOnly(2026, 9, 18),
        DocumentRevisionPurpose.ForConstruction, null,
        transmittalId == 0 ? null : Id(transmittalId), Cutoff.AddDays(-6),
        events ?? [Event(1, TechnicalReportingEventType.Submitted, -5),
            Event(2, TechnicalReportingEventType.Approved, -4),
            Event(3, TechnicalReportingEventType.Issued, -1)]);

    private static TechnicalReportingTransmittal Transmittal(
        int id = 20, IReadOnlyCollection<Guid>? revisionIds = null,
        IReadOnlyCollection<TechnicalReportingEvent>? events = null) => new(
        Id(id), Tenant, Project, $"TRN-{id}", revisionIds ?? [Id(11)], null,
        Cutoff.AddDays(-2), events ?? [Event(1, TechnicalReportingEventType.Issued, -1)]);

    private static TechnicalReportingRfi Rfi() => new(
        Id(30), Tenant, Project, "RFI-30", new DateOnly(2026, 9, 17),
        new DateOnly(2026, 9, 20), true, PotentialImpact.Cost,
        [], Cutoff.AddDays(-6), []);

    private static TechnicalReportingSubmittal Submittal() => new(
        Id(40), Tenant, Project, "SUB-40", TechnicalSubmittalType.ShopDrawing,
        "CIVIL", null, 0, null, [Id(11)], Cutoff.AddDays(-6), []);

    private static TechnicalReportingEvent Event(long sequence, TechnicalReportingEventType type,
        int days) => new(sequence, type, Cutoff.AddDays(days));

    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");

    private static ProjectControlProfile Profile() => new(
        Project, Tenant, "P-001", "Test Project", "UTC", "IRR", 1,
        Cutoff.AddDays(-30), ProjectStatus.Active, ContractModel.NotConfigured,
        PlanningMode.None, ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured, ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured, ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured, ProjectFeatureState.NotConfigured,
        new ProjectCalendarProfile(ProjectCalendarConfigurationState.NotConfigured, null), 1);
}

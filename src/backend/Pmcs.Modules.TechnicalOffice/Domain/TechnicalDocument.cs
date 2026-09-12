using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.TechnicalOffice.Domain;

public sealed class TechnicalDocument : AggregateRoot
{
    private TechnicalDocument() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public TechnicalDocumentType Type { get; private set; }
    public string Discipline { get; private set; } = string.Empty;
    public string? Originator { get; private set; }
    public Guid? ContractId { get; private set; }
    public string? LocationReference { get; private set; }
    public string? WorkItemReference { get; private set; }
    public string? WbsReference { get; private set; }
    public string? Confidentiality { get; private set; }
    public Guid? CurrentOfficialRevisionId { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static TechnicalDocument Create(
        Guid id, Guid tenantId, Guid projectId, string title, TechnicalDocumentType type,
        string discipline, string? originator, Guid? contractId, string? locationReference,
        string? workItemReference, string? wbsReference, string? confidentiality,
        Guid createdBy, DateTimeOffset createdAt)
    {
        TechnicalOfficeRules.Identity(id, tenantId, projectId, createdBy);
        if (!Enum.IsDefined(type))
        {
            throw new DomainRuleException("technical.document.type.invalid", "Document type is invalid.");
        }

        return new TechnicalDocument
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            Number = TechnicalOfficeRules.OfficialNumber("DOC", createdAt, id),
            Title = TechnicalOfficeRules.Required(title, 240, "technical.document.title.invalid"),
            Type = type,
            Discipline = TechnicalOfficeRules.Required(discipline, 120, "technical.document.discipline.invalid"),
            Originator = TechnicalOfficeRules.Optional(originator, 240, "technical.document.originator.too_long"),
            ContractId = contractId,
            LocationReference = TechnicalOfficeRules.Optional(locationReference, 240, "technical.document.location.too_long"),
            WorkItemReference = TechnicalOfficeRules.Optional(workItemReference, 240, "technical.document.work_item.too_long"),
            WbsReference = TechnicalOfficeRules.Optional(wbsReference, 240, "technical.document.wbs.too_long"),
            Confidentiality = TechnicalOfficeRules.Optional(confidentiality, 80, "technical.document.confidentiality.too_long"),
            CreatedBy = createdBy,
            CreatedAt = createdAt
        };
    }

    public void MakeCurrent(Guid revisionId)
    {
        TechnicalOfficeRules.Identity(revisionId);
        if (CurrentOfficialRevisionId == revisionId)
        {
            return;
        }

        CurrentOfficialRevisionId = revisionId;
        AdvanceRevision();
    }
}

public sealed class TechnicalDocumentRevision : AggregateRoot
{
    private TechnicalDocumentRevision() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid DocumentId { get; private set; }
    public string RevisionCode { get; private set; } = string.Empty;
    public DateOnly RevisionDate { get; private set; }
    public DocumentRevisionPurpose Purpose { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string FileReference { get; private set; } = string.Empty;
    public string Sha256 { get; private set; } = string.Empty;
    public Guid? SupersedesRevisionId { get; private set; }
    public DocumentRevisionStatus Status { get; private set; }
    public Guid PreparedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? ReviewComment { get; private set; }
    public Guid? IssuedThroughTransmittalId { get; private set; }
    public DateTimeOffset? IssuedAt { get; private set; }
    public DateTimeOffset? SupersededAt { get; private set; }

    public static TechnicalDocumentRevision Create(
        Guid id, Guid tenantId, Guid projectId, Guid documentId, string revisionCode,
        DateOnly revisionDate, DocumentRevisionPurpose purpose, string fileName,
        string fileReference, string sha256, Guid? supersedesRevisionId,
        Guid preparedBy, DateTimeOffset createdAt)
    {
        TechnicalOfficeRules.Identity(id, tenantId, projectId, documentId, preparedBy);
        if (!Enum.IsDefined(purpose))
        {
            throw new DomainRuleException("technical.revision.purpose.invalid", "Revision purpose is invalid.");
        }

        return new TechnicalDocumentRevision
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            DocumentId = documentId,
            RevisionCode = TechnicalOfficeRules.Required(revisionCode, 80, "technical.revision.code.invalid").ToUpperInvariant(),
            RevisionDate = revisionDate,
            Purpose = purpose,
            FileName = TechnicalOfficeRules.Required(fileName, 255, "technical.file.name.invalid"),
            FileReference = TechnicalOfficeRules.Required(fileReference, 700, "technical.file.reference.invalid"),
            Sha256 = TechnicalOfficeRules.Sha256(sha256),
            SupersedesRevisionId = supersedesRevisionId,
            Status = DocumentRevisionStatus.Draft,
            PreparedBy = preparedBy,
            CreatedAt = createdAt
        };
    }

    public void Submit(long baseRevision, DateTimeOffset at)
    {
        TechnicalOfficeRules.Revision(Revision, baseRevision, "technical.revision.revision.conflict");
        if (Status != DocumentRevisionStatus.Draft)
        {
            throw new DomainRuleException("technical.revision.submit.invalid_state", "Only a draft revision can be submitted.");
        }

        Status = DocumentRevisionStatus.Submitted;
        SubmittedAt = at;
        AdvanceRevision();
    }

    public void Approve(long baseRevision, Guid reviewer, DateTimeOffset at, string? comment)
    {
        TechnicalOfficeRules.Revision(Revision, baseRevision, "technical.revision.revision.conflict");
        TechnicalOfficeRules.Identity(reviewer);
        if (Status != DocumentRevisionStatus.Submitted)
        {
            throw new DomainRuleException("technical.revision.review.invalid_state", "Only a submitted revision can be approved.");
        }

        Status = DocumentRevisionStatus.Approved;
        ReviewedBy = reviewer;
        ReviewedAt = at;
        ReviewComment = TechnicalOfficeRules.Optional(comment, 1_000, "technical.review.comment.too_long");
        AdvanceRevision();
    }

    public void Return(long baseRevision, Guid reviewer, DateTimeOffset at, string reason)
    {
        TechnicalOfficeRules.Revision(Revision, baseRevision, "technical.revision.revision.conflict");
        TechnicalOfficeRules.Identity(reviewer);
        if (Status != DocumentRevisionStatus.Submitted)
        {
            throw new DomainRuleException("technical.revision.review.invalid_state", "Only a submitted revision can be returned.");
        }

        Status = DocumentRevisionStatus.Returned;
        ReviewedBy = reviewer;
        ReviewedAt = at;
        ReviewComment = TechnicalOfficeRules.Required(reason, 1_000, "technical.review.reason.invalid");
        AdvanceRevision();
    }

    public void Issue(Guid transmittalId, DateTimeOffset at)
    {
        TechnicalOfficeRules.Identity(transmittalId);
        if (Status != DocumentRevisionStatus.Approved)
        {
            throw new DomainRuleException("technical.revision.issue.invalid_state", "Only an approved revision can be formally issued.");
        }

        Status = DocumentRevisionStatus.Issued;
        IssuedThroughTransmittalId = transmittalId;
        IssuedAt = at;
        AdvanceRevision();
    }

    public void Supersede(DateTimeOffset at)
    {
        if (Status != DocumentRevisionStatus.Issued)
        {
            throw new DomainRuleException("technical.revision.supersede.invalid_state", "Only an issued revision can be superseded.");
        }

        Status = DocumentRevisionStatus.Superseded;
        SupersededAt = at;
        AdvanceRevision();
    }
}

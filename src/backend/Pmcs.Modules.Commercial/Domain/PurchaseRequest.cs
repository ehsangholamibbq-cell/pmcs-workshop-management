using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Commercial.Domain;

public sealed class PurchaseRequest : AggregateRoot
{
    private PurchaseRequest()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ProjectId { get; private set; }

    public string Number { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public decimal? EstimatedAmount { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    public DateOnly? NeededByDate { get; private set; }

    public Guid? SupplyItemId { get; private set; }

    public decimal? RequestedQuantity { get; private set; }

    public string? UnitCode { get; private set; }

    public string? DeliveryLocation { get; private set; }

    public string? WorkItemReference { get; private set; }

    public string? WbsReference { get; private set; }

    public string? BudgetLineReference { get; private set; }

    public BudgetCheckStatus BudgetCheckStatus { get; private set; }

    public ProcurementCriticality Criticality { get; private set; }

    public PurchaseRequestStatus Status { get; private set; }

    public Guid RequestedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public Guid? ReviewedBy { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public string? ReviewComment { get; private set; }

    public static PurchaseRequest Create(
        Guid id,
        Guid tenantId,
        Guid projectId,
        string number,
        string title,
        string description,
        decimal? estimatedAmount,
        string currencyCode,
        DateOnly? neededByDate,
        Guid requestedBy,
        DateTimeOffset createdAt,
        Guid? supplyItemId = null,
        decimal? requestedQuantity = null,
        string? unitCode = null,
        string? deliveryLocation = null,
        string? workItemReference = null,
        string? wbsReference = null,
        string? budgetLineReference = null,
        BudgetCheckStatus budgetCheckStatus = BudgetCheckStatus.NotConfigured,
        ProcurementCriticality criticality = ProcurementCriticality.Normal)
    {
        CommercialRules.Identity(id, tenantId, projectId, requestedBy);
        ValidateSupplyFields(supplyItemId, requestedQuantity, unitCode, deliveryLocation, budgetCheckStatus, criticality);
        return new PurchaseRequest
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            Number = CommercialRules.Required(number, 80, "commercial.request.number.invalid"),
            Title = CommercialRules.Required(title, 240, "commercial.request.title.invalid"),
            Description = CommercialRules.Required(description, 2_000, "commercial.request.description.invalid"),
            EstimatedAmount = CommercialRules.OptionalPositiveAmount(estimatedAmount, "commercial.request.amount.invalid"),
            CurrencyCode = CommercialRules.Currency(currencyCode),
            NeededByDate = neededByDate,
            SupplyItemId = supplyItemId,
            RequestedQuantity = requestedQuantity,
            UnitCode = string.IsNullOrWhiteSpace(unitCode) ? null : SupplyRules.Unit(unitCode),
            DeliveryLocation = CommercialRules.Optional(deliveryLocation, 240, "commercial.request.delivery_location.too_long"),
            WorkItemReference = CommercialRules.Optional(workItemReference, 240, "commercial.request.work_item.too_long"),
            WbsReference = CommercialRules.Optional(wbsReference, 240, "commercial.request.wbs.too_long"),
            BudgetLineReference = CommercialRules.Optional(budgetLineReference, 240, "commercial.request.budget_line.too_long"),
            BudgetCheckStatus = budgetCheckStatus,
            Criticality = criticality,
            Status = PurchaseRequestStatus.Draft,
            RequestedBy = requestedBy,
            CreatedAt = createdAt,
            ChangedAt = createdAt
        };
    }

    public void Amend(
        long baseRevision,
        string number,
        string title,
        string description,
        decimal? estimatedAmount,
        string currencyCode,
        DateOnly? neededByDate,
        DateTimeOffset changedAt,
        Guid? supplyItemId = null,
        decimal? requestedQuantity = null,
        string? unitCode = null,
        string? deliveryLocation = null,
        string? workItemReference = null,
        string? wbsReference = null,
        string? budgetLineReference = null,
        BudgetCheckStatus budgetCheckStatus = BudgetCheckStatus.NotConfigured,
        ProcurementCriticality criticality = ProcurementCriticality.Normal)
    {
        EnsureRevision(baseRevision);
        EnsureEditable();
        ValidateSupplyFields(supplyItemId, requestedQuantity, unitCode, deliveryLocation, budgetCheckStatus, criticality);
        Number = CommercialRules.Required(number, 80, "commercial.request.number.invalid");
        Title = CommercialRules.Required(title, 240, "commercial.request.title.invalid");
        Description = CommercialRules.Required(description, 2_000, "commercial.request.description.invalid");
        EstimatedAmount = CommercialRules.OptionalPositiveAmount(estimatedAmount, "commercial.request.amount.invalid");
        CurrencyCode = CommercialRules.Currency(currencyCode);
        NeededByDate = neededByDate;
        SupplyItemId = supplyItemId;
        RequestedQuantity = requestedQuantity;
        UnitCode = string.IsNullOrWhiteSpace(unitCode) ? null : SupplyRules.Unit(unitCode);
        DeliveryLocation = CommercialRules.Optional(deliveryLocation, 240, "commercial.request.delivery_location.too_long");
        WorkItemReference = CommercialRules.Optional(workItemReference, 240, "commercial.request.work_item.too_long");
        WbsReference = CommercialRules.Optional(wbsReference, 240, "commercial.request.wbs.too_long");
        BudgetLineReference = CommercialRules.Optional(budgetLineReference, 240, "commercial.request.budget_line.too_long");
        BudgetCheckStatus = budgetCheckStatus;
        Criticality = criticality;
        ChangedAt = changedAt;
        AdvanceRevision();
    }

    public void Submit(long baseRevision, DateTimeOffset submittedAt)
    {
        EnsureRevision(baseRevision);
        EnsureEditable();
        Status = PurchaseRequestStatus.Submitted;
        SubmittedAt = submittedAt;
        ReviewedBy = null;
        ReviewedAt = null;
        ReviewComment = null;
        ChangedAt = submittedAt;
        AdvanceRevision();
    }

    public void Approve(long baseRevision, Guid reviewedBy, DateTimeOffset reviewedAt, string? comment)
    {
        EnsureRevision(baseRevision);
        EnsureSubmitted();
        Status = PurchaseRequestStatus.Approved;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
        ReviewComment = CommercialRules.Optional(comment, 1_000, "commercial.request.review_comment.too_long");
        ChangedAt = reviewedAt;
        AdvanceRevision();
    }

    public void Return(long baseRevision, Guid reviewedBy, DateTimeOffset reviewedAt, string? comment)
    {
        EnsureRevision(baseRevision);
        EnsureSubmitted();
        Status = PurchaseRequestStatus.Returned;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
        ReviewComment = CommercialRules.Optional(comment, 1_000, "commercial.request.review_comment.too_long");
        ChangedAt = reviewedAt;
        AdvanceRevision();
    }

    public void MarkOrdered(long baseRevision, DateTimeOffset changedAt)
    {
        EnsureRevision(baseRevision);
        if (Status != PurchaseRequestStatus.Approved)
        {
            throw new DomainRuleException("commercial.request.order.invalid_state", "Only an approved request can become an order.");
        }

        Status = PurchaseRequestStatus.Ordered;
        ChangedAt = changedAt;
        AdvanceRevision();
    }

    private void EnsureRevision(long baseRevision) =>
        CommercialRules.Revision(Revision, baseRevision, "commercial.request.revision.conflict");

    private void EnsureEditable()
    {
        if (Status is not PurchaseRequestStatus.Draft and not PurchaseRequestStatus.Returned)
        {
            throw new DomainRuleException("commercial.request.edit.invalid_state", "Only a draft or returned request can be edited.");
        }
    }

    private void EnsureSubmitted()
    {
        if (Status != PurchaseRequestStatus.Submitted)
        {
            throw new DomainRuleException("commercial.request.review.invalid_state", "Only a submitted request can be reviewed.");
        }
    }

    private static void ValidateSupplyFields(
        Guid? supplyItemId, decimal? requestedQuantity, string? unitCode, string? deliveryLocation, BudgetCheckStatus budgetCheckStatus,
        ProcurementCriticality criticality)
    {
        if (supplyItemId.HasValue != requestedQuantity.HasValue)
            throw new DomainRuleException("commercial.request.supply_basis.invalid", "Supply item and quantified demand must be supplied together.");
        if (requestedQuantity.HasValue != !string.IsNullOrWhiteSpace(unitCode))
            throw new DomainRuleException("commercial.request.quantity_basis.invalid", "Quantity and unit must be supplied together.");
        if (requestedQuantity.HasValue)
            SupplyRules.PositiveQuantity(requestedQuantity.Value, "commercial.request.quantity.invalid");
        if (requestedQuantity.HasValue && string.IsNullOrWhiteSpace(deliveryLocation))
            throw new DomainRuleException("commercial.request.delivery_location.required", "Quantified demand requires a delivery location.");
        if (!Enum.IsDefined(budgetCheckStatus) || !Enum.IsDefined(criticality))
            throw new DomainRuleException("commercial.request.classification.invalid", "Request classification is invalid.");
    }
}

public enum PurchaseRequestStatus
{
    Draft = 1,
    Submitted = 2,
    Returned = 3,
    Approved = 4,
    Ordered = 5,
    Cancelled = 6
}

public enum BudgetCheckStatus { NotConfigured = 1, NotChecked = 2, WithinBudget = 3, ExceptionRequired = 4, ExceptionApproved = 5 }
public enum ProcurementCriticality { Normal = 1, Priority = 2, Critical = 3, Emergency = 4 }

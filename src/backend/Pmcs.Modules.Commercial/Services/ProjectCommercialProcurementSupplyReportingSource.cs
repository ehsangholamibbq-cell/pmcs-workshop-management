using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Commercial.Contracts;
using Pmcs.Modules.Commercial.Domain;
using Pmcs.Modules.Commercial.Persistence;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Commercial.Services;

internal sealed class ProjectCommercialProcurementSupplyReportingSource(
    CommercialDbContext dbContext,
    IProjectDirectory projectDirectory) : IProjectCommercialProcurementSupplyReportingSource
{
    public async Task<ProjectCommercialProcurementSupplyReportingResult> LoadAsync(
        Guid tenantId,
        Guid projectId,
        DateOnly cutoffLocalDate,
        DateTimeOffset sourceCutoffUtc,
        CancellationToken cancellationToken = default)
    {
        var cutoff = sourceCutoffUtc.ToUniversalTime();
        var project = await projectDirectory.FindProfileAsync(tenantId, projectId, cancellationToken);
        if (project is null)
        {
            throw new DomainRuleException(
                "commercial.project_commercial_procurement_supply_reporting.project.not_found",
                "The Project Commercial reporting scope does not exist.");
        }

        var parties = await dbContext.Parties.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId &&
                item.CreatedAt <= cutoff)
            .Take(ProjectCommercialProcurementSupplyReportingContract.MaximumPartyAndItemSnapshots + 1)
            .ToArrayAsync(cancellationToken);
        var items = await dbContext.SupplyItems.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId &&
                item.CreatedAt <= cutoff)
            .Take(ProjectCommercialProcurementSupplyReportingContract.MaximumPartyAndItemSnapshots + 1)
            .ToArrayAsync(cancellationToken);
        var contracts = await dbContext.Contracts.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId &&
                item.CreatedAt <= cutoff)
            .Take(ProjectCommercialProcurementSupplyReportingContract.MaximumContracts + 1)
            .ToArrayAsync(cancellationToken);
        var amendments = await dbContext.ContractAmendments.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId &&
                item.CreatedAt <= cutoff)
            .Take(ProjectCommercialProcurementSupplyReportingContract.MaximumAmendments + 1)
            .ToArrayAsync(cancellationToken);
        var requests = await dbContext.PurchaseRequests.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId &&
                item.CreatedAt <= cutoff)
            .Take(ProjectCommercialProcurementSupplyReportingContract.MaximumPurchaseRequests + 1)
            .ToArrayAsync(cancellationToken);
        var orders = await dbContext.PurchaseOrders.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId &&
                item.IssuedAt <= cutoff)
            .Take(ProjectCommercialProcurementSupplyReportingContract.MaximumPurchaseOrders + 1)
            .ToArrayAsync(cancellationToken);
        var receipts = await dbContext.GoodsReceipts.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId &&
                item.CreatedAt <= cutoff)
            .Take(ProjectCommercialProcurementSupplyReportingContract.MaximumSupplyEvidence + 1)
            .ToArrayAsync(cancellationToken);
        var acceptances = await dbContext.ServiceAcceptances.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId &&
                item.VerifiedAt <= cutoff)
            .Take(ProjectCommercialProcurementSupplyReportingContract.MaximumSupplyEvidence + 1)
            .ToArrayAsync(cancellationToken);

        var projection = ProjectCommercialProcurementSupplyReportingCompatibilityProjection.Create(
            project,
            cutoffLocalDate,
            cutoff,
            parties,
            items,
            contracts,
            amendments,
            requests,
            orders,
            receipts,
            acceptances);
        return ProjectCommercialProcurementSupplyReportingCalculator.Calculate(
            ProjectCommercialProcurementSupplyReportingSelector.Select(projection));
    }
}

internal static class ProjectCommercialProcurementSupplyReportingCompatibilityProjection
{
    public static ProjectCommercialProcurementSupplyReportingProjection Create(
        ProjectControlProfile project,
        DateOnly cutoffLocalDate,
        DateTimeOffset sourceCutoffUtc,
        IReadOnlyCollection<Party> parties,
        IReadOnlyCollection<SupplyItem> items,
        IReadOnlyCollection<ProjectContract> contracts,
        IReadOnlyCollection<ContractAmendment> amendments,
        IReadOnlyCollection<PurchaseRequest> requests,
        IReadOnlyCollection<PurchaseOrder> orders,
        IReadOnlyCollection<GoodsReceipt> receipts,
        IReadOnlyCollection<ServiceAcceptance> acceptances)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(parties);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(contracts);
        ArgumentNullException.ThrowIfNull(amendments);
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(receipts);
        ArgumentNullException.ThrowIfNull(acceptances);
        var cutoff = sourceCutoffUtc.ToUniversalTime();
        if (project.Id == Guid.Empty || project.TenantId == Guid.Empty || cutoff == default ||
            cutoffLocalDate == default || !project.ConfigurationChangedAt.HasValue ||
            project.ConfigurationVersion <= 0 || project.Revision <= 0 ||
            project.ConfigurationChangedAt.Value.ToUniversalTime() > cutoff)
        {
            throw HistoryUnavailable(
                "configuration_history.unavailable",
                "The current Project profile cannot safely reconstruct Commercial configuration at this cutoff.");
        }

        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(project.TimeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            throw HistoryUnavailable("time_zone.invalid", "The Project time zone is unknown or invalid.");
        }
        catch (InvalidTimeZoneException)
        {
            throw HistoryUnavailable("time_zone.invalid", "The Project time zone is unknown or invalid.");
        }
        var derivedLocalDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(cutoff, timeZone).DateTime);
        if (derivedLocalDate != cutoffLocalDate)
        {
            throw HistoryUnavailable(
                "cutoff.invalid",
                "The local cutoff date does not match the versioned Project time zone.");
        }

        EnsureCurrentAtCutoff(parties.Select(item => item.ChangedAt), cutoff, "party_history.unavailable");
        EnsureCurrentAtCutoff(items.Select(item => item.ChangedAt), cutoff, "item_history.unavailable");
        EnsureCurrentAtCutoff(contracts.Select(item => item.ChangedAt), cutoff, "contract_history.unavailable");
        EnsureCurrentAtCutoff(amendments.Select(item => item.ChangedAt), cutoff, "amendment_history.unavailable");
        EnsureCurrentAtCutoff(requests.Select(item => item.ChangedAt), cutoff, "request_history.unavailable");
        if (contracts.Any(item => item.Status is ProjectContractStatus.Closed or
                ProjectContractStatus.Suspended or ProjectContractStatus.Terminated))
        {
            throw HistoryUnavailable(
                "contract_history.unavailable",
                "A legacy closed, suspended or terminated Contract does not retain independent activation history.");
        }
        if (requests.Any(item => item.Status == PurchaseRequestStatus.Cancelled))
        {
            throw HistoryUnavailable(
                "request_history.unavailable",
                "A legacy cancelled Purchase Request does not retain independent cancellation history.");
        }
        if (receipts.Any(item => item.ExcessApprovedBy.HasValue))
        {
            throw HistoryUnavailable(
                "excess_approval_history.unavailable",
                "A legacy Goods Receipt does not retain a versioned excess approval timestamp.");
        }

        const ProjectCommercialReportingClassification classification =
            ProjectCommercialReportingClassification.Confidential;
        var changedAt = project.ConfigurationChangedAt.Value.ToUniversalTime();
        return new ProjectCommercialProcurementSupplyReportingProjection(
            ProjectCommercialProcurementSupplyReportingContract.Version,
            project.TenantId,
            project.Id,
            cutoffLocalDate,
            cutoff,
            [new ProjectCommercialConfigurationVersion(
                project.ConfigurationVersion,
                project.Revision,
                project.ContractModel,
                project.Contract,
                project.Procurement,
                project.TimeZone,
                project.BaseCurrencyCode,
                changedAt,
                null,
                classification)],
            parties.Select(item => Map(item, classification)).ToArray(),
            items.Select(item => Map(item, classification)).ToArray(),
            contracts.Select(item => Map(item, classification)).ToArray(),
            amendments.Select(item => Map(item, classification)).ToArray(),
            requests.Select(item => Map(item, classification)).ToArray(),
            orders.Select(item => Map(item, classification)).ToArray(),
            receipts.Select(item => Map(item, classification)).ToArray(),
            acceptances.Select(item => Map(item, classification)).ToArray(),
            ProjectCommercialReportingSourceCompleteness.Complete,
            ProjectCommercialReportingSourceCompleteness.Complete,
            ProjectCommercialReportingSourceCompleteness.Complete,
            ProjectCommercialReportingSourceCompleteness.Complete,
            ProjectCommercialReportingSourceCompleteness.Complete,
            ProjectCommercialReportingSourceCompleteness.Complete,
            ProjectCommercialReportingSourceCompleteness.Complete,
            classification);
    }

    private static ProjectCommercialPartySnapshotVersion Map(
        Party item,
        ProjectCommercialReportingClassification classification) => new(
        item.Id,
        item.TenantId,
        item.ProjectId,
        item.Revision,
        item.Code,
        item.LegalName,
        item.Type,
        item.ChangedAt.ToUniversalTime(),
        null,
        classification);

    private static ProjectCommercialItemSnapshotVersion Map(
        SupplyItem item,
        ProjectCommercialReportingClassification classification) => new(
        item.Id,
        item.TenantId,
        item.ProjectId,
        item.Revision,
        item.Code,
        item.Name,
        item.Kind,
        item.BaseUnit,
        item.ChangedAt.ToUniversalTime(),
        null,
        classification);

    private static ProjectCommercialContractVersion Map(
        ProjectContract item,
        ProjectCommercialReportingClassification classification) => new(
        item.Id,
        item.TenantId,
        item.ProjectId,
        item.Revision,
        item.PartyId,
        item.Number,
        item.Title,
        item.Type,
        item.OriginalApprovedAmount,
        item.CurrencyCode,
        item.StartDate,
        item.EndDate,
        item.CreatedAt.ToUniversalTime(),
        ContractLifecycle(item),
        classification);

    private static ProjectCommercialContractLifecycleEvent[] ContractLifecycle(ProjectContract item)
    {
        if (item.Status == ProjectContractStatus.Draft)
        {
            return [];
        }
        if (!item.SubmittedAt.HasValue)
        {
            throw HistoryUnavailable(
                "contract_history.unavailable",
                "A legacy Contract does not retain its submission timestamp.");
        }

        var events = new List<ProjectCommercialContractLifecycleEvent>
        {
            new(1, ProjectCommercialContractEventType.Submitted, item.SubmittedAt.Value.ToUniversalTime())
        };
        if (item.Status == ProjectContractStatus.Submitted)
        {
            return events.ToArray();
        }
        if (!item.ReviewedAt.HasValue)
        {
            throw HistoryUnavailable(
                "contract_history.unavailable",
                "A legacy reviewed Contract does not retain its review timestamp.");
        }
        events.Add(new ProjectCommercialContractLifecycleEvent(
            2,
            item.Status == ProjectContractStatus.Returned
                ? ProjectCommercialContractEventType.Returned
                : ProjectCommercialContractEventType.Activated,
            item.ReviewedAt.Value.ToUniversalTime()));
        return events.ToArray();
    }

    private static ProjectCommercialAmendmentVersion Map(
        ContractAmendment item,
        ProjectCommercialReportingClassification classification) => new(
        item.Id,
        item.TenantId,
        item.ProjectId,
        item.Revision,
        item.ContractId,
        item.Number,
        item.Title,
        item.Type,
        item.AmountDelta,
        item.CurrencyCode,
        item.ExtensionDays,
        item.CreatedAt.ToUniversalTime(),
        AmendmentLifecycle(item),
        classification);

    private static ProjectCommercialAmendmentLifecycleEvent[] AmendmentLifecycle(ContractAmendment item)
    {
        if (item.Status == ContractAmendmentStatus.Draft)
        {
            return [];
        }
        if (!item.SubmittedAt.HasValue)
        {
            throw HistoryUnavailable(
                "amendment_history.unavailable",
                "A legacy Amendment does not retain its submission timestamp.");
        }
        var events = new List<ProjectCommercialAmendmentLifecycleEvent>
        {
            new(1, ProjectCommercialAmendmentEventType.Submitted, item.SubmittedAt.Value.ToUniversalTime())
        };
        if (item.Status == ContractAmendmentStatus.Submitted)
        {
            return events.ToArray();
        }
        if (!item.ReviewedAt.HasValue)
        {
            throw HistoryUnavailable(
                "amendment_history.unavailable",
                "A legacy reviewed Amendment does not retain its review timestamp.");
        }
        events.Add(new ProjectCommercialAmendmentLifecycleEvent(
            2,
            item.Status == ContractAmendmentStatus.Returned
                ? ProjectCommercialAmendmentEventType.Returned
                : ProjectCommercialAmendmentEventType.Approved,
            item.ReviewedAt.Value.ToUniversalTime()));
        return events.ToArray();
    }

    private static ProjectCommercialPurchaseRequestVersion Map(
        PurchaseRequest item,
        ProjectCommercialReportingClassification classification) => new(
        item.Id,
        item.TenantId,
        item.ProjectId,
        item.Revision,
        item.Number,
        item.Title,
        item.EstimatedAmount,
        item.CurrencyCode,
        item.SupplyItemId,
        item.CreatedAt.ToUniversalTime(),
        RequestLifecycle(item),
        classification);

    private static ProjectCommercialPurchaseRequestLifecycleEvent[] RequestLifecycle(PurchaseRequest item)
    {
        if (item.Status == PurchaseRequestStatus.Draft)
        {
            return [];
        }
        if (!item.SubmittedAt.HasValue)
        {
            throw HistoryUnavailable(
                "request_history.unavailable",
                "A legacy Purchase Request does not retain its submission timestamp.");
        }
        var events = new List<ProjectCommercialPurchaseRequestLifecycleEvent>
        {
            new(1, ProjectCommercialPurchaseRequestEventType.Submitted, item.SubmittedAt.Value.ToUniversalTime())
        };
        if (item.Status == PurchaseRequestStatus.Submitted)
        {
            return events.ToArray();
        }
        if (!item.ReviewedAt.HasValue)
        {
            throw HistoryUnavailable(
                "request_history.unavailable",
                "A legacy reviewed Purchase Request does not retain its review timestamp.");
        }
        if (item.Status == PurchaseRequestStatus.Returned)
        {
            events.Add(new ProjectCommercialPurchaseRequestLifecycleEvent(
                2,
                ProjectCommercialPurchaseRequestEventType.Returned,
                item.ReviewedAt.Value.ToUniversalTime()));
            return events.ToArray();
        }
        events.Add(new ProjectCommercialPurchaseRequestLifecycleEvent(
            2,
            ProjectCommercialPurchaseRequestEventType.Approved,
            item.ReviewedAt.Value.ToUniversalTime()));
        if (item.Status == PurchaseRequestStatus.Ordered)
        {
            events.Add(new ProjectCommercialPurchaseRequestLifecycleEvent(
                3,
                ProjectCommercialPurchaseRequestEventType.Ordered,
                item.ChangedAt.ToUniversalTime()));
        }
        return events.ToArray();
    }

    private static ProjectCommercialPurchaseOrderVersion Map(
        PurchaseOrder item,
        ProjectCommercialReportingClassification classification)
    {
        var lifecycle = new List<ProjectCommercialPurchaseOrderLifecycleEvent>
        {
            new(1, ProjectCommercialPurchaseOrderEventType.Issued, item.IssuedAt.ToUniversalTime())
        };
        if (item.Status is PurchaseOrderStatus.Closed or PurchaseOrderStatus.Cancelled)
        {
            if (!item.ClosedAt.HasValue)
            {
                throw HistoryUnavailable(
                    "order_history.unavailable",
                    "A legacy closed Purchase Order does not retain its closure timestamp.");
            }
            lifecycle.Add(new ProjectCommercialPurchaseOrderLifecycleEvent(
                2,
                item.Status == PurchaseOrderStatus.Closed
                    ? ProjectCommercialPurchaseOrderEventType.Closed
                    : ProjectCommercialPurchaseOrderEventType.Cancelled,
                item.ClosedAt.Value.ToUniversalTime()));
        }
        return new ProjectCommercialPurchaseOrderVersion(
            item.Id,
            item.TenantId,
            item.ProjectId,
            item.Revision,
            item.PurchaseRequestId,
            item.PartyId,
            item.ContractId,
            item.Number,
            item.Title,
            item.Amount,
            item.CurrencyCode,
            item.DeliveryDueDate,
            item.SupplyItemId,
            item.OrderedQuantity,
            item.UnitCode,
            null,
            null,
            null,
            lifecycle,
            classification);
    }

    private static ProjectCommercialGoodsReceiptVersion Map(
        GoodsReceipt item,
        ProjectCommercialReportingClassification classification) => new(
        item.Id,
        item.TenantId,
        item.ProjectId,
        item.Revision,
        item.Number,
        item.PurchaseOrderId,
        item.PartyId,
        item.ItemId,
        item.CreatedAt.ToUniversalTime(),
        item.ArrivedAt.ToUniversalTime(),
        item.BaseReceivedQuantity,
        item.BaseUnit,
        item.ConversionVersion,
        item.Status,
        item.InspectedAt?.ToUniversalTime(),
        item.AcceptedBaseQuantity,
        item.RejectedBaseQuantity,
        item.QuarantinedBaseQuantity,
        null,
        null,
        classification);

    private static ProjectCommercialServiceAcceptanceVersion Map(
        ServiceAcceptance item,
        ProjectCommercialReportingClassification classification) => new(
        item.Id,
        item.TenantId,
        item.ProjectId,
        item.Revision,
        item.Number,
        item.PurchaseOrderId,
        item.PartyId,
        item.ItemId,
        item.PeriodStart,
        item.PeriodEnd,
        item.DeliveredBaseQuantity,
        item.AcceptedBaseQuantity,
        item.RejectedBaseQuantity,
        item.BaseUnit,
        item.VerifiedAt.ToUniversalTime(),
        null,
        null,
        classification);

    private static void EnsureCurrentAtCutoff(
        IEnumerable<DateTimeOffset> changedAtValues,
        DateTimeOffset cutoff,
        string suffix)
    {
        if (changedAtValues.Any(item => item.ToUniversalTime() > cutoff))
        {
            throw HistoryUnavailable(
                suffix,
                "A current Commercial row changed after the requested cutoff and has no immutable history.");
        }
    }

    private static DomainRuleException HistoryUnavailable(string suffix, string message) =>
        new($"commercial.project_commercial_procurement_supply_reporting.{suffix}", message);
}

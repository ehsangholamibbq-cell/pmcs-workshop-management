using System.Text.Json;
using Pmcs.Modules.Commercial.Contracts;
using Pmcs.Modules.Commercial.Domain;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Rendering;

internal static class ProjectCommercialProcurementSupplyReportRenderSnapshot
{
    public static ProjectCommercialProcurementSupplyReportSemanticSnapshot Parse(string payloadJson)
    {
        try
        {
            var snapshot = JsonSerializer.Deserialize<
                ProjectCommercialProcurementSupplyReportSemanticSnapshot>(
                payloadJson,
                CanonicalJson.SerializerOptions)
                ?? throw Invalid("Commercial procurement and supply snapshot payload is empty.");
            ProjectCommercialProcurementSupplyReportRenderingContract.ValidateSnapshot(snapshot);
            return snapshot;
        }
        catch (ReportRenderingException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw Invalid(
                "Commercial procurement and supply snapshot payload cannot be rendered.",
                exception);
        }
        catch (NotSupportedException exception)
        {
            throw Invalid(
                "Commercial procurement and supply snapshot payload uses an unsupported value.",
                exception);
        }
    }

    private static ReportRenderingException Invalid(
        string message,
        Exception? innerException = null) => new(
        "reporting.project_commercial_procurement_supply.snapshot.payload_invalid",
        transient: false,
        message,
        innerException);
}

internal sealed record ProjectCommercialProcurementSupplyReportRenderRequest(
    Guid RunId,
    Guid OutputId,
    Guid SnapshotId,
    Guid TemplateVersionId,
    string DefinitionCode,
    string DefinitionVersion,
    string TemplateVersion,
    string TemplateContentDigest,
    string RendererContractVersion,
    string LayoutContractVersion,
    ReportFormat Format,
    string FileName,
    string VerificationCode,
    string ManifestSha256,
    string SnapshotSha256,
    string SourceManifestSha256,
    DateTimeOffset SourceCutoffUtc,
    ProjectCommercialProcurementSupplyReportSemanticSnapshot Snapshot);

internal interface IProjectCommercialProcurementSupplyReportRenderer
{
    ReportFormat Format { get; }

    RenderedReportArtifact Render(ProjectCommercialProcurementSupplyReportRenderRequest request);
}

internal sealed class ProjectCommercialProcurementSupplyReportRenderModel
{
    private ProjectCommercialProcurementSupplyReportRenderModel(
        ProjectCommercialProcurementSupplyReportRenderRequest request)
    {
        Request = request;
        Snapshot = request.Snapshot;
        ReasonCodes = Snapshot.ReasonCodes.OrderBy(item => item).ToArray();
        Contracts = Snapshot.ContractRegister.ToArray();
        Amendments = Snapshot.ApprovedAmendments.ToArray();
        PurchaseOrders = Snapshot.PurchaseOrders.ToArray();
        SupplySummaries = Snapshot.SupplySummaries.ToArray();
        SupplierPerformance = Snapshot.SupplierPerformance.ToArray();
    }

    public ProjectCommercialProcurementSupplyReportRenderRequest Request { get; }

    public ProjectCommercialProcurementSupplyReportSemanticSnapshot Snapshot { get; }

    public IReadOnlyList<ProjectCommercialReportingReasonCode> ReasonCodes { get; }

    public IReadOnlyList<ProjectCommercialProcurementSupplyReportContractRow> Contracts { get; }

    public IReadOnlyList<ProjectCommercialProcurementSupplyReportAmendmentRow> Amendments { get; }

    public IReadOnlyList<ProjectCommercialProcurementSupplyReportOrderRow> PurchaseOrders { get; }

    public IReadOnlyList<ProjectCommercialSupplySummary> SupplySummaries { get; }

    public IReadOnlyList<ProjectCommercialProcurementSupplyReportSupplierPerformance>
        SupplierPerformance { get; }

    public static ProjectCommercialProcurementSupplyReportRenderModel Create(
        ProjectCommercialProcurementSupplyReportRenderRequest request)
    {
        ProjectCommercialProcurementSupplyReportRenderingContract.ValidateRequest(request);
        return new ProjectCommercialProcurementSupplyReportRenderModel(request);
    }
}

internal static class ProjectCommercialProcurementSupplyReportRenderingContract
{
    public const int MaximumProjectCodeLength = 160;
    public const int MaximumProjectNameLength = 400;
    public const int MaximumPartyCodeLength = 32;
    public const int MaximumPartyNameLength = 200;
    public const int MaximumItemCodeLength = 48;
    public const int MaximumItemNameLength = 240;
    public const int MaximumNumberLength = 80;
    public const int MaximumTitleLength = 240;
    public const int MaximumUnitLength = 24;

    public static void ValidateRequest(
        ProjectCommercialProcurementSupplyReportRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSnapshot(request.Snapshot);
        var extension = request.Format switch
        {
            ReportFormat.Pdf => ".pdf",
            ReportFormat.Xlsx => ".xlsx",
            _ => throw InvalidRequest(
                "The Commercial procurement and supply output format is unsupported.")
        };
        if (request.RunId == Guid.Empty || request.OutputId == Guid.Empty ||
            request.SnapshotId == Guid.Empty || request.TemplateVersionId == Guid.Empty ||
            !string.Equals(
                request.DefinitionCode,
                ProjectCommercialProcurementSupplyReportRuntimeContract.DefinitionCode,
                StringComparison.Ordinal) ||
            !string.Equals(
                request.DefinitionVersion,
                ProjectCommercialProcurementSupplyReportRuntimeContract.DefinitionVersion,
                StringComparison.Ordinal) ||
            !string.Equals(
                request.TemplateVersion,
                ProjectCommercialProcurementSupplyReportRuntimeContract.TemplateVersion,
                StringComparison.Ordinal) ||
            !string.Equals(
                request.TemplateContentDigest,
                ProjectCommercialProcurementSupplyReportRuntimeContract.TemplateContentDigest,
                StringComparison.Ordinal) ||
            !string.Equals(
                request.RendererContractVersion,
                ProjectCommercialProcurementSupplyReportRuntimeContract.RendererContractVersion,
                StringComparison.Ordinal) ||
            !string.Equals(
                request.LayoutContractVersion,
                ProjectCommercialProcurementSupplyReportRuntimeContract.LayoutContractVersion,
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(request.FileName) ||
            !request.FileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                request.FileName,
                ReportArtifactIdentity.FileName(request.Snapshot, request.Format),
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(request.VerificationCode) ||
            !IsSha256(request.TemplateContentDigest) || !IsSha256(request.ManifestSha256) ||
            !IsSha256(request.SnapshotSha256) || !IsSha256(request.SourceManifestSha256))
        {
            throw InvalidRequest(
                "The Commercial procurement and supply render request violates its pinned contract.");
        }

        var canonicalSnapshot = CanonicalJson.Serialize(request.Snapshot);
        if (!string.Equals(
                CanonicalJson.Sha256(canonicalSnapshot),
                request.SnapshotSha256,
                StringComparison.Ordinal) ||
            !string.Equals(
                request.SourceManifestSha256,
                request.Snapshot.SourceManifestSha256,
                StringComparison.Ordinal) ||
            request.SourceCutoffUtc.ToUniversalTime() !=
                request.Snapshot.Cutoff.SourceCutoffUtc.ToUniversalTime())
        {
            throw InvalidRequest(
                "The render request does not match its immutable Commercial snapshot identity.");
        }
    }

    public static void ValidateSnapshot(
        ProjectCommercialProcurementSupplyReportSemanticSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!string.Equals(
                snapshot.SchemaVersion,
                ProjectCommercialProcurementSupplyReportRuntimeContract.SnapshotSchemaVersion,
                StringComparison.Ordinal) ||
            !string.Equals(
                snapshot.SemanticContractId,
                ProjectCommercialProcurementSupplyReportRuntimeContract.SemanticContractId,
                StringComparison.Ordinal) ||
            !string.Equals(
                snapshot.DefinitionCode,
                ProjectCommercialProcurementSupplyReportRuntimeContract.DefinitionCode,
                StringComparison.Ordinal) ||
            !string.Equals(
                snapshot.DefinitionVersion,
                ProjectCommercialProcurementSupplyReportRuntimeContract.DefinitionVersion,
                StringComparison.Ordinal) ||
            !string.Equals(
                snapshot.PolicyVersion,
                ProjectCommercialProcurementSupplyReportingContract.PolicyVersion,
                StringComparison.Ordinal) ||
            !Enum.IsDefined(snapshot.DataStatus) || snapshot.DataStatus == ReportDataStatus.Pending ||
            !Enum.IsDefined(snapshot.Classification) ||
            snapshot.Classification < ReportClassification.Confidential ||
            snapshot.ReasonCodes is null || snapshot.Parameters is null || snapshot.Project is null ||
            snapshot.Cutoff is null || snapshot.ContractRegister is null ||
            snapshot.ApprovedAmendments is null || snapshot.PurchaseOrders is null ||
            snapshot.SupplySummaries is null || snapshot.SupplierPerformance is null ||
            snapshot.SourceCounts is null || !Enum.IsDefined(snapshot.ContractStatus) ||
            !Enum.IsDefined(snapshot.ProcurementStatus) || !Enum.IsDefined(snapshot.SupplyStatus) ||
            !IsSha256(snapshot.SourceManifestSha256))
        {
            throw InvalidSnapshot(
                "Commercial procurement and supply snapshot identity or collections are invalid.");
        }

        ValidateProject(snapshot.Project, snapshot.Cutoff);
        ValidateCutoff(snapshot.Cutoff);
        ValidateReasons(snapshot.ReasonCodes);
        ValidateConfiguration(snapshot.Configuration, snapshot);
        ValidateContracts(snapshot);
        ValidateProcurement(snapshot);
        ValidateSupply(snapshot);
        ValidateSuppliers(snapshot);
        ValidateStatus(snapshot);
        ValidateCounts(snapshot);
        if (snapshot.SourceMaxChangedAt.HasValue &&
            (snapshot.SourceMaxChangedAt.Value.Offset != TimeSpan.Zero ||
                snapshot.SourceMaxChangedAt.Value > snapshot.Cutoff.SourceCutoffUtc))
        {
            throw InvalidSnapshot("Commercial source change time is invalid.");
        }
    }

    private static void ValidateProject(
        ProjectCommercialProcurementSupplyReportProjectIdentity project,
        ProjectCommercialProcurementSupplyReportCutoffIdentity cutoff)
    {
        if (project.Id == Guid.Empty || project.TenantId == Guid.Empty ||
            !HasBoundedText(project.Code, MaximumProjectCodeLength) ||
            !HasBoundedText(project.Name, MaximumProjectNameLength) ||
            string.IsNullOrWhiteSpace(project.TimeZone) ||
            !IsCurrency(project.CapturedBaseCurrencyCode) || project.Revision <= 0 ||
            project.ConfigurationVersion <= 0 || project.ConfigurationChangedAt == default ||
            project.ConfigurationChangedAt.Offset != TimeSpan.Zero ||
            project.ProfileCapturedAtUtc == default ||
            project.ProfileCapturedAtUtc.Offset != TimeSpan.Zero ||
            project.ConfigurationChangedAt > project.ProfileCapturedAtUtc ||
            cutoff.SourceCutoffUtc > project.ProfileCapturedAtUtc)
        {
            throw InvalidSnapshot("Commercial project identity is invalid.");
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(project.TimeZone);
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw InvalidSnapshot("Commercial project time zone is invalid.", exception);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw InvalidSnapshot("Commercial project time zone is invalid.", exception);
        }
    }

    private static void ValidateCutoff(
        ProjectCommercialProcurementSupplyReportCutoffIdentity cutoff)
    {
        if (cutoff.SourceCutoffUtc == default || cutoff.SourceCutoffUtc.Offset != TimeSpan.Zero ||
            cutoff.CutoffLocalDate == default)
        {
            throw InvalidSnapshot("Commercial cutoff identity is invalid.");
        }
    }

    private static void ValidateReasons(
        IReadOnlyCollection<ProjectCommercialReportingReasonCode> reasons)
    {
        var canonical = reasons.OrderBy(item => item).ToArray();
        if (reasons.Any(reason => !Enum.IsDefined(reason)) ||
            reasons.Distinct().Count() != reasons.Count || !reasons.SequenceEqual(canonical))
        {
            throw InvalidSnapshot("Commercial reason codes are invalid or non-canonical.");
        }
    }

    private static void ValidateConfiguration(
        ProjectCommercialProcurementSupplyReportConfiguration? configuration,
        ProjectCommercialProcurementSupplyReportSemanticSnapshot snapshot)
    {
        if (configuration is null)
        {
            return;
        }
        if (configuration.ConfigurationVersion <= 0 || configuration.ProjectRevision <= 0 ||
            configuration.ConfigurationVersion != snapshot.Project.ConfigurationVersion ||
            configuration.ProjectRevision != snapshot.Project.Revision ||
            !Enum.IsDefined(configuration.ContractModel) ||
            !Enum.IsDefined(configuration.ContractState) ||
            !Enum.IsDefined(configuration.ProcurementState) ||
            !string.Equals(configuration.TimeZone, snapshot.Project.TimeZone, StringComparison.Ordinal) ||
            !IsCurrency(configuration.BaseCurrencyCode) ||
            !string.Equals(
                configuration.BaseCurrencyCode,
                snapshot.Project.CapturedBaseCurrencyCode,
                StringComparison.Ordinal) ||
            configuration.EffectiveFromUtc == default ||
            configuration.EffectiveFromUtc.Offset != TimeSpan.Zero ||
            configuration.EffectiveFromUtc > snapshot.Cutoff.SourceCutoffUtc ||
            configuration.EffectiveToUtc.HasValue &&
                (configuration.EffectiveToUtc.Value.Offset != TimeSpan.Zero ||
                    configuration.EffectiveToUtc.Value <= snapshot.Cutoff.SourceCutoffUtc))
        {
            throw InvalidSnapshot("Commercial effective configuration is invalid.");
        }
    }

    private static void ValidateContracts(
        ProjectCommercialProcurementSupplyReportSemanticSnapshot snapshot)
    {
        if (snapshot.ContractStatus != ProjectCommercialReportingSectionStatus.Available)
        {
            if (snapshot.ContractSummary is not null || snapshot.ContractRegister.Count > 0 ||
                snapshot.ApprovedAmendments.Count > 0)
            {
                throw InvalidSnapshot(
                    "An unavailable Contract section cannot expose register data.");
            }
            return;
        }

        var summary = snapshot.ContractSummary;
        var rows = snapshot.ContractRegister.ToArray();
        var amendments = snapshot.ApprovedAmendments.ToArray();
        if (summary is null || rows.Length == 0 || !IsOrderedByText(rows, item => item.Number) ||
            !IsOrderedAmendments(amendments))
        {
            throw InvalidSnapshot("Contract summary or canonical ordering is invalid.");
        }

        foreach (var row in rows)
        {
            if (!HasBoundedText(row.Number, MaximumNumberLength) ||
                !HasBoundedText(row.Title, MaximumTitleLength) ||
                !HasBoundedText(row.PartyCode, MaximumPartyCodeLength) ||
                !HasBoundedText(row.PartyName, MaximumPartyNameLength) ||
                !Enum.IsDefined(row.PartyType) || !Enum.IsDefined(row.Type) ||
                !Enum.IsDefined(row.State) || row.ApprovedAmendmentCount < 0 ||
                row.ApprovedExtensionDays < 0 ||
                row.OriginalApprovedAmount.HasValue && !IsMoneyOrZero(row.OriginalApprovedAmount.Value) ||
                !IsMoney(row.ApprovedAmountDelta) ||
                row.EffectiveApprovedAmount.HasValue && !IsMoneyOrZero(row.EffectiveApprovedAmount.Value) ||
                row.StartDate.HasValue && row.OriginalEndDate.HasValue &&
                    row.StartDate.Value > row.OriginalEndDate.Value ||
                row.OriginalEndDate.HasValue != row.EffectiveEndDate.HasValue ||
                row.OriginalApprovedAmount.HasValue != row.EffectiveApprovedAmount.HasValue ||
                row.OriginalApprovedAmount.HasValue &&
                    row.EffectiveApprovedAmount !=
                        row.OriginalApprovedAmount.Value + row.ApprovedAmountDelta ||
                row.OriginalEndDate.HasValue &&
                    row.EffectiveEndDate != row.OriginalEndDate.Value.AddDays(row.ApprovedExtensionDays))
            {
                throw InvalidSnapshot("A Contract render row violates its semantic contract.");
            }
        }

        var contractNumbers = rows.Select(item => item.Number).ToHashSet(StringComparer.Ordinal);
        foreach (var amendment in amendments)
        {
            var shapeValid = amendment.Type switch
            {
                ContractAmendmentType.ScopeChange =>
                    !amendment.AmountDelta.HasValue && !amendment.ExtensionDays.HasValue,
                ContractAmendmentType.ValueChange =>
                    amendment.AmountDelta is not null and not 0 &&
                    !amendment.ExtensionDays.HasValue,
                ContractAmendmentType.TimeExtension =>
                    !amendment.AmountDelta.HasValue && amendment.ExtensionDays > 0,
                ContractAmendmentType.Mixed =>
                    amendment.AmountDelta is not null and not 0 && amendment.ExtensionDays > 0,
                _ => false
            };
            if (!HasBoundedText(amendment.ContractNumber, MaximumNumberLength) ||
                !contractNumbers.Contains(amendment.ContractNumber) ||
                !HasBoundedText(amendment.Number, MaximumNumberLength) ||
                !HasBoundedText(amendment.Title, MaximumTitleLength) || !shapeValid ||
                amendment.AmountDelta.HasValue && !IsMoney(amendment.AmountDelta.Value) ||
                amendment.ApprovedAt == default || amendment.ApprovedAt.Offset != TimeSpan.Zero ||
                amendment.ApprovedAt > snapshot.Cutoff.SourceCutoffUtc)
            {
                throw InvalidSnapshot("An approved Amendment render row is invalid.");
            }
        }

        var knownSubtotal = rows.Where(item => item.EffectiveApprovedAmount.HasValue)
            .Sum(item => item.EffectiveApprovedAmount!.Value);
        var completeTotal = rows.All(item => item.EffectiveApprovedAmount.HasValue)
            ? knownSubtotal
            : (decimal?)null;
        var nonNegativeCounts = new[]
        {
            summary.OfficialContractCount,
            summary.ActiveContractCount,
            summary.SuspendedContractCount,
            summary.ClosedContractCount,
            summary.TerminatedContractCount,
            summary.ExpiredActiveContractCount,
            summary.PendingContractWorkflowCount,
            summary.ApprovedAmendmentCount,
            summary.PendingAmendmentWorkflowCount,
            summary.ApprovedExtensionDays
        };
        if (nonNegativeCounts.Any(item => item < 0) ||
            summary.OfficialContractCount != rows.Length ||
            summary.ActiveContractCount != rows.Count(item =>
                item.State == ProjectCommercialContractState.Active) ||
            summary.SuspendedContractCount != rows.Count(item =>
                item.State == ProjectCommercialContractState.Suspended) ||
            summary.ClosedContractCount != rows.Count(item =>
                item.State == ProjectCommercialContractState.Closed) ||
            summary.TerminatedContractCount != rows.Count(item =>
                item.State == ProjectCommercialContractState.Terminated) ||
            summary.ExpiredActiveContractCount != rows.Count(item =>
                item.State == ProjectCommercialContractState.Expired) ||
            summary.ApprovedAmendmentCount != amendments.Length ||
            summary.ApprovedAmendmentCount != rows.Sum(item => item.ApprovedAmendmentCount) ||
            summary.ApprovedAmountDelta != amendments.Where(item => item.AmountDelta.HasValue)
                .Sum(item => item.AmountDelta!.Value) ||
            summary.ApprovedExtensionDays != amendments.Where(item => item.ExtensionDays.HasValue)
                .Sum(item => item.ExtensionDays!.Value) ||
            summary.ApprovedAmountDelta != rows.Sum(item => item.ApprovedAmountDelta) ||
            summary.ApprovedExtensionDays != rows.Sum(item => item.ApprovedExtensionDays) ||
            summary.KnownEffectiveContractCeilingSubtotal != knownSubtotal ||
            summary.EffectiveContractCeilingTotal != completeTotal)
        {
            throw InvalidSnapshot("Contract summary does not match its canonical rows.");
        }
    }

    private static void ValidateProcurement(
        ProjectCommercialProcurementSupplyReportSemanticSnapshot snapshot)
    {
        if (snapshot.ProcurementStatus != ProjectCommercialReportingSectionStatus.Available)
        {
            if (snapshot.ProcurementSummary is not null || snapshot.PurchaseOrders.Count > 0 ||
                snapshot.SupplierPerformance.Count > 0)
            {
                throw InvalidSnapshot(
                    "An unavailable Procurement section cannot expose Order data.");
            }
            return;
        }

        var summary = snapshot.ProcurementSummary;
        var orders = snapshot.PurchaseOrders.ToArray();
        if (summary is null || !IsOrderedByText(orders, item => item.Number))
        {
            throw InvalidSnapshot("Procurement summary or Order ordering is invalid.");
        }
        foreach (var order in orders)
        {
            ValidateOrder(order, snapshot.SupplyStatus);
        }

        var counts = new[]
        {
            summary.DraftRequestCount,
            summary.SubmittedRequestCount,
            summary.ReturnedRequestCount,
            summary.ApprovedRequestCount,
            summary.OrderedRequestCount,
            summary.CancelledRequestCount,
            summary.ApprovedRequestsAwaitingOrderCount,
            summary.IssuedOrderCount,
            summary.ClosedOrderCount,
            summary.CancelledOrderCount
        };
        if (counts.Any(item => item < 0) ||
            summary.ApprovedRequestsAwaitingOrderCount > summary.ApprovedRequestCount ||
            summary.IssuedOrderCount != orders.Count(item =>
                item.State == ProjectCommercialPurchaseOrderState.Issued) ||
            summary.ClosedOrderCount != orders.Count(item =>
                item.State == ProjectCommercialPurchaseOrderState.Closed) ||
            summary.CancelledOrderCount != orders.Count(item =>
                item.State == ProjectCommercialPurchaseOrderState.Cancelled) ||
            !IsMoneyOrZero(summary.TotalIssuedOrderAmount) ||
            !IsMoneyOrZero(summary.OpenOrderAmount) ||
            summary.TotalIssuedOrderAmount != orders.Where(item =>
                    item.State is ProjectCommercialPurchaseOrderState.Issued or
                        ProjectCommercialPurchaseOrderState.Closed)
                .Sum(item => item.Amount) ||
            summary.OpenOrderAmount != orders.Where(item =>
                    item.State == ProjectCommercialPurchaseOrderState.Issued)
                .Sum(item => item.Amount))
        {
            throw InvalidSnapshot("Procurement summary does not match its Order rows.");
        }
    }

    private static void ValidateOrder(
        ProjectCommercialProcurementSupplyReportOrderRow order,
        ProjectCommercialReportingSectionStatus supplyStatus)
    {
        var quantityValues = new decimal?[]
        {
            order.OrderedBaseQuantity,
            order.DeliveredBaseQuantity,
            order.AcceptedBaseQuantity,
            order.RejectedBaseQuantity,
            order.QuarantinedBaseQuantity,
            order.RemainingOrderedQuantity,
            order.AcceptedExcessQuantity
        };
        if (!HasBoundedText(order.Number, MaximumNumberLength) ||
            !HasBoundedText(order.Title, MaximumTitleLength) ||
            !HasBoundedText(order.PurchaseRequestNumber, MaximumNumberLength) ||
            order.ContractNumber is not null &&
                !HasBoundedText(order.ContractNumber, MaximumNumberLength) ||
            !HasBoundedText(order.PartyCode, MaximumPartyCodeLength) ||
            !HasBoundedText(order.PartyName, MaximumPartyNameLength) ||
            !Enum.IsDefined(order.PartyType) || !Enum.IsDefined(order.State) ||
            !Enum.IsDefined(order.DeliveryStatus) || !IsPositiveMoney(order.Amount) ||
            order.GoodsReceiptCount < 0 || order.PendingInspectionCount < 0 ||
            order.PendingInspectionCount > order.GoodsReceiptCount ||
            order.ServiceAcceptanceCount < 0 ||
            order.RejectedOrQuarantinedEvidenceCount < 0 ||
            order.ItemCode is not null && !HasBoundedText(order.ItemCode, MaximumItemCodeLength) ||
            order.ItemName is not null && !HasBoundedText(order.ItemName, MaximumItemNameLength) ||
            order.ItemKind.HasValue && !Enum.IsDefined(order.ItemKind.Value) ||
            order.UnitCode is not null && !HasBoundedText(order.UnitCode, MaximumUnitLength) ||
            order.BaseUnit is not null && !HasBoundedText(order.BaseUnit, MaximumUnitLength) ||
            order.OrderedQuantity.HasValue && !IsPositiveQuantity(order.OrderedQuantity.Value) ||
            order.ConversionVersion.HasValue && order.ConversionVersion.Value <= 0 ||
            quantityValues.Where(item => item.HasValue)
                .Any(item => !IsQuantityOrZero(item!.Value)) ||
            order.FulfillmentPercent.HasValue &&
                (order.FulfillmentPercent.Value < 0 ||
                    decimal.Round(order.FulfillmentPercent.Value, 1, MidpointRounding.AwayFromZero) !=
                        order.FulfillmentPercent.Value))
        {
            throw InvalidSnapshot("A Purchase Order render row is invalid.");
        }

        if (supplyStatus == ProjectCommercialReportingSectionStatus.InsufficientData)
        {
            if (quantityValues.Skip(1).Any(item => item.HasValue) ||
                order.FulfillmentPercent.HasValue || order.CompletionDate.HasValue ||
                order.DeliveryStatus is not (ProjectCommercialDeliveryStatus.NotAssessable or
                    ProjectCommercialDeliveryStatus.Cancelled))
            {
                throw InvalidSnapshot(
                    "An insufficient Supply section cannot expose derived fulfillment metrics.");
            }
            return;
        }

        var basisPresent = order.OrderedBaseQuantity.HasValue;
        var basisFieldsPresent = order.ItemCode is not null && order.ItemName is not null &&
            order.ItemKind.HasValue && order.OrderedQuantity.HasValue && order.UnitCode is not null &&
            order.BaseUnit is not null && order.ConversionVersion.HasValue;
        if (basisPresent != basisFieldsPresent ||
            !basisPresent && (quantityValues.Skip(1).Any(item => item.HasValue) ||
                order.FulfillmentPercent.HasValue || order.CompletionDate.HasValue ||
                order.DeliveryStatus != ProjectCommercialDeliveryStatus.NotAssessable &&
                    order.DeliveryStatus != ProjectCommercialDeliveryStatus.Cancelled))
        {
            throw InvalidSnapshot("Purchase Order quantity basis is internally inconsistent.");
        }
        if (!basisPresent)
        {
            return;
        }

        var ordered = order.OrderedBaseQuantity!.Value;
        var accepted = order.AcceptedBaseQuantity ?? throw InvalidSnapshot(
            "Purchase Order accepted quantity is missing.");
        var remaining = Math.Max(ordered - accepted, 0);
        var excess = Math.Max(accepted - ordered, 0);
        var fulfillment = decimal.Round(
            accepted * 100m / ordered,
            1,
            MidpointRounding.AwayFromZero);
        if (!order.DeliveredBaseQuantity.HasValue || !order.RejectedBaseQuantity.HasValue ||
            !order.QuarantinedBaseQuantity.HasValue || order.RemainingOrderedQuantity != remaining ||
            order.AcceptedExcessQuantity != excess || order.FulfillmentPercent != fulfillment ||
            order.State == ProjectCommercialPurchaseOrderState.Cancelled &&
                order.DeliveryStatus != ProjectCommercialDeliveryStatus.Cancelled ||
            order.DeliveryDueDate is null &&
                order.DeliveryStatus is not (ProjectCommercialDeliveryStatus.NotAssessable or
                    ProjectCommercialDeliveryStatus.Cancelled) ||
            order.DeliveryStatus is (ProjectCommercialDeliveryStatus.OnTimeFulfilled or
                ProjectCommercialDeliveryStatus.LateFulfilled) && !order.CompletionDate.HasValue)
        {
            throw InvalidSnapshot("Purchase Order fulfillment metrics are invalid.");
        }
    }

    private static void ValidateSupply(
        ProjectCommercialProcurementSupplyReportSemanticSnapshot snapshot)
    {
        var rows = snapshot.SupplySummaries.ToArray();
        if (snapshot.SupplyStatus is ProjectCommercialReportingSectionStatus.NotConfigured or
            ProjectCommercialReportingSectionStatus.SetupRequired or
            ProjectCommercialReportingSectionStatus.Suspended or
            ProjectCommercialReportingSectionStatus.InsufficientData)
        {
            if (rows.Length > 0)
            {
                throw InvalidSnapshot("An unavailable Supply section cannot expose summaries.");
            }
            return;
        }
        if (!IsOrderedSupply(rows))
        {
            throw InvalidSnapshot("Supply summaries are non-canonical.");
        }
        foreach (var item in rows)
        {
            if (!HasBoundedText(item.ItemCode, MaximumItemCodeLength) ||
                !HasBoundedText(item.ItemName, MaximumItemNameLength) ||
                !Enum.IsDefined(item.ItemKind) || !HasBoundedText(item.BaseUnit, MaximumUnitLength) ||
                item.OrderCount <= 0 || !IsPositiveQuantity(item.OrderedBaseQuantity) ||
                !IsQuantityOrZero(item.DeliveredBaseQuantity) ||
                !IsQuantityOrZero(item.AcceptedBaseQuantity) ||
                !IsQuantityOrZero(item.RejectedBaseQuantity) ||
                !IsQuantityOrZero(item.QuarantinedBaseQuantity))
            {
                throw InvalidSnapshot("A Supply summary row is invalid.");
            }
        }
    }

    private static void ValidateSuppliers(
        ProjectCommercialProcurementSupplyReportSemanticSnapshot snapshot)
    {
        var suppliers = snapshot.SupplierPerformance.ToArray();
        if (snapshot.ProcurementStatus != ProjectCommercialReportingSectionStatus.Available)
        {
            if (suppliers.Length > 0)
            {
                throw InvalidSnapshot("Unavailable Procurement cannot expose supplier data.");
            }
            return;
        }
        if (!IsOrderedByText(suppliers, item => item.PartyCode))
        {
            throw InvalidSnapshot("Supplier performance rows are non-canonical.");
        }
        foreach (var item in suppliers)
        {
            var counts = new[]
            {
                item.IssuedOrderCount,
                item.OpenOrderCount,
                item.ClosedOrderCount,
                item.CancelledOrderCount,
                item.AssessableCompletedOrderCount,
                item.OnTimeFulfilledCount,
                item.LateFulfilledCount,
                item.OverdueOpenCount,
                item.ClosedShortCount,
                item.NotAssessableCount,
                item.GoodsReceiptCount,
                item.PendingInspectionCount,
                item.ReceiptWithRejectedOrQuarantinedCount,
                item.ServiceAcceptanceCount,
                item.ServiceAcceptanceWithRejectedCount
            };
            var expectedRate = item.AssessableCompletedOrderCount == 0
                ? (decimal?)null
                : decimal.Round(
                    item.OnTimeFulfilledCount * 100m / item.AssessableCompletedOrderCount,
                    1,
                    MidpointRounding.AwayFromZero);
            if (!HasBoundedText(item.PartyCode, MaximumPartyCodeLength) ||
                !HasBoundedText(item.PartyName, MaximumPartyNameLength) ||
                !Enum.IsDefined(item.PartyType) || counts.Any(value => value < 0) ||
                item.OpenOrderCount + item.ClosedOrderCount + item.CancelledOrderCount !=
                    item.IssuedOrderCount ||
                item.OnTimeFulfilledCount + item.LateFulfilledCount + item.ClosedShortCount !=
                    item.AssessableCompletedOrderCount ||
                item.OnTimeFulfillmentRate != expectedRate ||
                item.PendingInspectionCount > item.GoodsReceiptCount ||
                item.ReceiptWithRejectedOrQuarantinedCount > item.GoodsReceiptCount ||
                item.ServiceAcceptanceWithRejectedCount > item.ServiceAcceptanceCount)
            {
                throw InvalidSnapshot("A Supplier performance row is invalid.");
            }
        }

        foreach (var group in snapshot.PurchaseOrders.GroupBy(item => item.PartyCode))
        {
            var supplier = suppliers.SingleOrDefault(item =>
                string.Equals(item.PartyCode, group.Key, StringComparison.Ordinal));
            if (supplier is null || supplier.IssuedOrderCount != group.Count() ||
                supplier.OpenOrderCount != group.Count(item =>
                    item.State == ProjectCommercialPurchaseOrderState.Issued) ||
                supplier.ClosedOrderCount != group.Count(item =>
                    item.State == ProjectCommercialPurchaseOrderState.Closed) ||
                supplier.CancelledOrderCount != group.Count(item =>
                    item.State == ProjectCommercialPurchaseOrderState.Cancelled))
            {
                throw InvalidSnapshot("Supplier performance does not match Purchase Orders.");
            }
        }
    }

    private static void ValidateStatus(
        ProjectCommercialProcurementSupplyReportSemanticSnapshot snapshot)
    {
        var statuses = new[]
        {
            snapshot.ContractStatus,
            snapshot.ProcurementStatus,
            snapshot.SupplyStatus
        };
        var active = statuses.Where(item => item is ProjectCommercialReportingSectionStatus.NoData or
            ProjectCommercialReportingSectionStatus.InsufficientData or
            ProjectCommercialReportingSectionStatus.Available).ToArray();
        var valid = snapshot.DataStatus switch
        {
            ReportDataStatus.NotConfigured => active.Length == 0,
            ReportDataStatus.NoData => active.Length > 0 &&
                active.All(item => item == ProjectCommercialReportingSectionStatus.NoData),
            ReportDataStatus.InsufficientData =>
                active.Contains(ProjectCommercialReportingSectionStatus.InsufficientData),
            ReportDataStatus.Available =>
                !active.Contains(ProjectCommercialReportingSectionStatus.InsufficientData) &&
                active.Contains(ProjectCommercialReportingSectionStatus.Available),
            _ => false
        };
        if (!valid)
        {
            throw InvalidSnapshot("Commercial data status conflicts with section statuses.");
        }
    }

    private static void ValidateCounts(
        ProjectCommercialProcurementSupplyReportSemanticSnapshot snapshot)
    {
        var counts = snapshot.SourceCounts;
        var values = new[]
        {
            counts.PartySnapshotSourceCount,
            counts.EffectivePartySnapshotCount,
            counts.ItemSnapshotSourceCount,
            counts.EffectiveItemSnapshotCount,
            counts.ContractSourceCount,
            counts.OfficialContractCount,
            counts.PendingContractCount,
            counts.AmendmentSourceCount,
            counts.ApprovedAmendmentCount,
            counts.PendingAmendmentCount,
            counts.PurchaseRequestSourceCount,
            counts.PurchaseRequestAtCutoffCount,
            counts.PurchaseOrderSourceCount,
            counts.OfficialPurchaseOrderCount,
            counts.GoodsReceiptSourceCount,
            counts.EligibleGoodsReceiptCount,
            counts.ServiceAcceptanceSourceCount,
            counts.EligibleServiceAcceptanceCount,
            counts.IncompleteCollectionCount
        };
        if (values.Any(item => item < 0) || counts.IncompleteCollectionCount > 7 ||
            counts.EffectivePartySnapshotCount > counts.PartySnapshotSourceCount ||
            counts.EffectiveItemSnapshotCount > counts.ItemSnapshotSourceCount ||
            counts.OfficialContractCount > counts.ContractSourceCount ||
            counts.ApprovedAmendmentCount > counts.AmendmentSourceCount ||
            counts.PurchaseRequestAtCutoffCount > counts.PurchaseRequestSourceCount ||
            counts.OfficialPurchaseOrderCount > counts.PurchaseOrderSourceCount ||
            counts.EligibleGoodsReceiptCount > counts.GoodsReceiptSourceCount ||
            counts.EligibleServiceAcceptanceCount > counts.ServiceAcceptanceSourceCount ||
            snapshot.ContractStatus == ProjectCommercialReportingSectionStatus.Available &&
                (counts.OfficialContractCount != snapshot.ContractRegister.Count ||
                    counts.ApprovedAmendmentCount != snapshot.ApprovedAmendments.Count) ||
            snapshot.ProcurementStatus == ProjectCommercialReportingSectionStatus.Available &&
                (counts.OfficialPurchaseOrderCount != snapshot.PurchaseOrders.Count ||
                    counts.EligibleGoodsReceiptCount !=
                        snapshot.PurchaseOrders.Sum(item => item.GoodsReceiptCount) ||
                    counts.EligibleServiceAcceptanceCount !=
                        snapshot.PurchaseOrders.Sum(item => item.ServiceAcceptanceCount)))
        {
            throw InvalidSnapshot("Commercial source counts are inconsistent.");
        }
    }

    private static bool IsOrderedByText<T>(
        IReadOnlyList<T> items,
        Func<T, string> key)
    {
        for (var index = 1; index < items.Count; index++)
        {
            if (StringComparer.Ordinal.Compare(key(items[index - 1]), key(items[index])) > 0)
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsOrderedAmendments(
        ProjectCommercialProcurementSupplyReportAmendmentRow[] items)
    {
        for (var index = 1; index < items.Length; index++)
        {
            var dateComparison = items[index - 1].ApprovedAt.CompareTo(items[index].ApprovedAt);
            if (dateComparison > 0 || dateComparison == 0 &&
                StringComparer.Ordinal.Compare(
                    items[index - 1].Number,
                    items[index].Number) > 0)
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsOrderedSupply(ProjectCommercialSupplySummary[] items)
    {
        for (var index = 1; index < items.Length; index++)
        {
            var code = StringComparer.Ordinal.Compare(
                items[index - 1].ItemCode,
                items[index].ItemCode);
            if (code > 0 || code == 0 && StringComparer.Ordinal.Compare(
                    items[index - 1].BaseUnit,
                    items[index].BaseUnit) > 0)
            {
                return false;
            }
        }
        return true;
    }

    private static bool HasBoundedText(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maximumLength;

    private static bool IsCurrency(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length == 3 &&
        value.All(char.IsAsciiLetterUpper);

    private static bool IsPositiveMoney(decimal value) => value > 0 && IsMoney(value);

    private static bool IsMoneyOrZero(decimal value) => value >= 0 && IsMoney(value);

    private static bool IsMoney(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero) == value;

    private static bool IsPositiveQuantity(decimal value) =>
        value > 0 && IsQuantityOrZero(value);

    private static bool IsQuantityOrZero(decimal value) =>
        value >= 0 && decimal.Round(value, 6, MidpointRounding.AwayFromZero) == value;

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static ReportRenderingException InvalidSnapshot(
        string message,
        Exception? innerException = null) => new(
        "reporting.project_commercial_procurement_supply.snapshot.payload_invalid",
        transient: false,
        message,
        innerException);

    private static ReportRenderingException InvalidRequest(string message) => new(
        "reporting.project_commercial_procurement_supply.render_request.invalid",
        transient: false,
        message);
}

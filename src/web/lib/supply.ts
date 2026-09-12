import { ensureApiSuccess } from "./localization.ts";

export interface SupplyIdentity { readonly tenantId: string; readonly userId: string }
export type SupplyItemKind = "Material" | "Service" | "EquipmentRental";
export type SupplyTrackingPolicy = "None" | "Quantity" | "Batch" | "Serial";
export type SupplyItemStatus = "Active" | "Inactive";
export type InventoryLocationType = "CentralStore" | "SiteStore" | "FloorOrZoneStore" | "LaydownArea" | "ContractorCustody" | "QuarantineArea" | "RejectedArea";
export type InventoryLocationStatus = "Active" | "Inactive";
export type GoodsReceiptStatus = "Received" | "PendingInspection" | "Accepted" | "PartiallyAccepted" | "Rejected" | "Quarantined";
export type MaterialIssueStatus = "Issued" | "PendingReconciliation" | "PartiallyReconciled" | "Reconciled";
export type InventoryAdjustmentStatus = "Proposed" | "Approved" | "Rejected" | "Posted";
export type InventoryEventType = "AcceptedReceipt" | "MaterialIssue" | "TransferOut" | "TransferIn" | "ReturnFromSite" | "Adjustment" | "Reversal";

export interface UnitConversionModel { readonly unitCode: string; readonly factorToBase: number; readonly version: number }
export interface SupplyItemModel {
  readonly id: string; readonly code: string; readonly name: string; readonly kind: SupplyItemKind;
  readonly category: string; readonly baseUnit: string; readonly unitConversions: readonly UnitConversionModel[];
  readonly technicalSpecificationReference: string | null; readonly trackingPolicy: SupplyTrackingPolicy;
  readonly inspectionRequired: boolean; readonly storageCondition: string | null;
  readonly acceptanceCriteria: string | null; readonly status: SupplyItemStatus; readonly revision: number;
}
export interface InventoryLocationModel {
  readonly id: string; readonly code: string; readonly name: string; readonly type: InventoryLocationType;
  readonly allowsAvailableStock: boolean; readonly status: InventoryLocationStatus; readonly revision: number;
}
export interface GoodsReceiptModel {
  readonly id: string; readonly number: string; readonly purchaseOrderId: string; readonly partyId: string;
  readonly itemId: string; readonly stockLocationId: string | null; readonly dispatchNote: string;
  readonly arrivedAt: string; readonly deliveryLocation: string; readonly shippedQuantity: number;
  readonly receivedQuantity: number; readonly baseReceivedQuantity: number; readonly damagedQuantity: number;
  readonly unitCode: string; readonly baseUnit: string; readonly conversionVersion: number;
  readonly batchOrLotReference: string | null; readonly packageCondition: string | null;
  readonly excessApprovalReason: string | null; readonly evidenceReferences: readonly string[];
  readonly status: GoodsReceiptStatus; readonly acceptedBaseQuantity: number;
  readonly rejectedBaseQuantity: number; readonly quarantinedBaseQuantity: number;
  readonly inspectedAt: string | null; readonly inspectionType: string | null;
  readonly inspectionReference: string | null; readonly inspectionComment: string | null;
  readonly stockPostedAt: string | null; readonly revision: number;
}
export interface MaterialIssueModel {
  readonly id: string; readonly number: string; readonly itemId: string; readonly sourceLocationId: string;
  readonly issuedBaseQuantity: number; readonly baseUnit: string; readonly issuedTo: string;
  readonly destinationLocation: string; readonly workItemReference: string | null; readonly wbsReference: string | null;
  readonly purchaseRequestId: string | null; readonly evidenceReferences: readonly string[];
  readonly consumedBaseQuantity: number; readonly returnedBaseQuantity: number; readonly wasteBaseQuantity: number;
  readonly unreconciledBaseQuantity: number; readonly status: MaterialIssueStatus; readonly issuedAt: string;
  readonly acknowledgedAt: string | null; readonly acknowledgmentReference: string | null;
  readonly reconciledAt: string | null; readonly revision: number;
}
export interface MaterialReconciliationModel {
  readonly id: string; readonly materialIssueId: string; readonly consumedBaseQuantity: number;
  readonly returnedBaseQuantity: number; readonly wasteBaseQuantity: number; readonly baseUnit: string;
  readonly wasteReason: string | null; readonly evidenceReferences: readonly string[]; readonly recordedAt: string;
}
export interface InventoryAdjustmentModel {
  readonly id: string; readonly number: string; readonly itemId: string; readonly locationId: string;
  readonly cutoffAt: string; readonly systemBaseQuantity: number; readonly countedBaseQuantity: number;
  readonly deltaBaseQuantity: number; readonly baseUnit: string; readonly reason: string;
  readonly evidenceReferences: readonly string[]; readonly status: InventoryAdjustmentStatus;
  readonly proposedAt: string; readonly reviewedAt: string | null; readonly reviewComment: string | null;
  readonly postedAt: string | null; readonly revision: number;
}
export interface ServiceAcceptanceModel {
  readonly id: string; readonly number: string; readonly purchaseOrderId: string; readonly partyId: string;
  readonly itemId: string; readonly periodStart: string; readonly periodEnd: string;
  readonly deliveredBaseQuantity: number; readonly acceptedBaseQuantity: number;
  readonly rejectedBaseQuantity: number; readonly baseUnit: string; readonly acceptanceCriteria: string;
  readonly evidenceReferences: readonly string[]; readonly comment: string | null;
  readonly verifiedAt: string; readonly revision: number;
}
export interface InventoryLedgerModel {
  readonly id: string; readonly transactionId: string; readonly itemId: string; readonly locationId: string;
  readonly eventType: InventoryEventType; readonly baseQuantityDelta: number; readonly baseUnit: string;
  readonly sourceType: string; readonly sourceId: string; readonly reason: string | null; readonly occurredAt: string;
}
export interface StockPositionModel {
  readonly itemId: string; readonly locationId: string; readonly baseUnit: string;
  readonly onHandBaseQuantity: number; readonly reservedBaseQuantity: number;
  readonly availableBaseQuantity: number; readonly lastMovementAt: string | null;
}
export interface SupplierDeliveryPerformanceModel {
  readonly partyId: string; readonly receiptCount: number; readonly onTimeReceiptCount: number;
  readonly fullyAcceptedReceiptCount: number; readonly receiptWithRejectionCount: number;
}
export interface SupplyStateModel {
  readonly calculationVersion: string; readonly calculatedAt: string;
  readonly items: readonly SupplyItemModel[]; readonly locations: readonly InventoryLocationModel[];
  readonly receipts: readonly GoodsReceiptModel[]; readonly materialIssues: readonly MaterialIssueModel[];
  readonly reconciliations: readonly MaterialReconciliationModel[]; readonly adjustments: readonly InventoryAdjustmentModel[];
  readonly serviceAcceptances: readonly ServiceAcceptanceModel[]; readonly ledger: readonly InventoryLedgerModel[];
  readonly stockPositions: readonly StockPositionModel[]; readonly supplierPerformance: readonly SupplierDeliveryPerformanceModel[];
  readonly pendingInspectionCount: number; readonly quarantinedReceiptCount: number;
  readonly rejectedReceiptCount: number; readonly unreconciledMaterialIssueCount: number;
  readonly pendingAdjustmentApprovalCount: number;
}

export async function getSupplyState(baseUrl: string, identity: SupplyIdentity, projectId: string) {
  return requestJson<SupplyStateModel>(path(baseUrl, projectId, "/state"), identity);
}
export async function createSupplyItem(baseUrl: string, identity: SupplyIdentity, projectId: string, input: {
  code: string; name: string; kind: SupplyItemKind; category: string; baseUnit: string;
  technicalSpecificationReference?: string; trackingPolicy: SupplyTrackingPolicy;
  inspectionRequired: boolean; storageCondition?: string; acceptanceCriteria?: string;
}) {
  return command<SupplyItemModel>(path(baseUrl, projectId, "/items"), identity, {
    clientGeneratedId: crypto.randomUUID(), ...input,
    technicalSpecificationReference: optional(input.technicalSpecificationReference),
    storageCondition: optional(input.storageCondition), acceptanceCriteria: optional(input.acceptanceCriteria),
  });
}
export async function addUnitConversion(baseUrl: string, identity: SupplyIdentity, projectId: string, item: SupplyItemModel, unitCode: string, factorToBase: number, version: number) {
  return command<SupplyItemModel>(path(baseUrl, projectId, `/items/${item.id}/unit-conversions`), identity,
    { baseRevision: item.revision, unitCode, factorToBase, version });
}
export async function createInventoryLocation(baseUrl: string, identity: SupplyIdentity, projectId: string, input: {
  code: string; name: string; type: InventoryLocationType; allowsAvailableStock: boolean;
}) {
  return command<InventoryLocationModel>(path(baseUrl, projectId, "/locations"), identity,
    { clientGeneratedId: crypto.randomUUID(), ...input });
}
export async function createGoodsReceipt(baseUrl: string, identity: SupplyIdentity, projectId: string, input: {
  purchaseOrderId: string; itemId: string; stockLocationId?: string; dispatchNote: string;
  arrivedAt: string; deliveryLocation: string; shippedQuantity: number; receivedQuantity: number;
  damagedQuantity: number; unitCode?: string; conversionVersion?: number;
  batchOrLotReference?: string; packageCondition?: string; excessApprovalReason?: string;
  evidenceReferences: readonly string[];
}) {
  return command<GoodsReceiptModel>(path(baseUrl, projectId, "/receipts"), identity, {
    clientGeneratedId: crypto.randomUUID(), ...input, stockLocationId: optional(input.stockLocationId),
    unitCode: optional(input.unitCode), conversionVersion: input.conversionVersion ?? null,
    batchOrLotReference: optional(input.batchOrLotReference), packageCondition: optional(input.packageCondition),
    excessApprovalReason: optional(input.excessApprovalReason),
  });
}
export async function transitionReceipt(baseUrl: string, identity: SupplyIdentity, projectId: string, receipt: GoodsReceiptModel, action: "submit-inspection" | "post-stock", comment?: string) {
  return command<GoodsReceiptModel>(path(baseUrl, projectId, `/receipts/${receipt.id}/${action}`), identity,
    { baseRevision: receipt.revision, comment: optional(comment) });
}
export async function inspectReceipt(baseUrl: string, identity: SupplyIdentity, projectId: string, receipt: GoodsReceiptModel, input: {
  acceptedBaseQuantity: number; rejectedBaseQuantity: number; quarantinedBaseQuantity: number;
  inspectionType: string; inspectionReference?: string; comment?: string;
}) {
  return command<GoodsReceiptModel>(path(baseUrl, projectId, `/receipts/${receipt.id}/inspect`), identity, {
    baseRevision: receipt.revision, ...input,
    inspectionReference: optional(input.inspectionReference), comment: optional(input.comment),
  });
}
export async function createMaterialIssue(baseUrl: string, identity: SupplyIdentity, projectId: string, input: {
  itemId: string; sourceLocationId: string; quantity: number; unitCode?: string;
  issuedTo: string; destinationLocation: string; workItemReference?: string; wbsReference?: string;
  purchaseRequestId?: string; evidenceReferences: readonly string[];
}) {
  return command<MaterialIssueModel>(path(baseUrl, projectId, "/material-issues"), identity, {
    clientGeneratedId: crypto.randomUUID(), ...input, unitCode: optional(input.unitCode), conversionVersion: null,
    workItemReference: optional(input.workItemReference), wbsReference: optional(input.wbsReference),
    purchaseRequestId: optional(input.purchaseRequestId),
  });
}
export async function acknowledgeMaterialIssue(baseUrl: string, identity: SupplyIdentity, projectId: string, issue: MaterialIssueModel, reference: string) {
  return command<MaterialIssueModel>(path(baseUrl, projectId, `/material-issues/${issue.id}/acknowledge`), identity,
    { baseRevision: issue.revision, reference });
}
export async function reconcileMaterialIssue(baseUrl: string, identity: SupplyIdentity, projectId: string, issue: MaterialIssueModel, input: {
  consumedBaseQuantity: number; returnedBaseQuantity: number; wasteBaseQuantity: number;
  wasteReason?: string; evidenceReferences?: readonly string[];
}) {
  return command<MaterialIssueModel>(path(baseUrl, projectId, `/material-issues/${issue.id}/reconcile`), identity, {
    baseRevision: issue.revision, ...input, wasteReason: optional(input.wasteReason),
    evidenceReferences: input.evidenceReferences ?? [],
  });
}
export async function transferInventory(baseUrl: string, identity: SupplyIdentity, projectId: string, input: {
  itemId: string; sourceLocationId: string; destinationLocationId: string; quantity: number;
  unitCode?: string; reason: string;
}) {
  return command(path(baseUrl, projectId, "/transfers"), identity, {
    clientGeneratedId: crypto.randomUUID(), ...input, unitCode: optional(input.unitCode), conversionVersion: null,
  });
}
export async function proposeInventoryAdjustment(baseUrl: string, identity: SupplyIdentity, projectId: string, input: {
  itemId: string; locationId: string; cutoffAt: string; countedBaseQuantity: number;
  reason: string; evidenceReferences: readonly string[];
}) {
  return command<InventoryAdjustmentModel>(path(baseUrl, projectId, "/adjustments"), identity,
    { clientGeneratedId: crypto.randomUUID(), ...input });
}
export async function reviewInventoryAdjustment(baseUrl: string, identity: SupplyIdentity, projectId: string, adjustment: InventoryAdjustmentModel, approved: boolean, comment?: string) {
  return command<InventoryAdjustmentModel>(path(baseUrl, projectId, `/adjustments/${adjustment.id}/review`), identity,
    { baseRevision: adjustment.revision, approved, comment: optional(comment) });
}
export async function postInventoryAdjustment(baseUrl: string, identity: SupplyIdentity, projectId: string, adjustment: InventoryAdjustmentModel) {
  return command<InventoryAdjustmentModel>(path(baseUrl, projectId, `/adjustments/${adjustment.id}/post`), identity,
    { baseRevision: adjustment.revision });
}
export async function createServiceAcceptance(baseUrl: string, identity: SupplyIdentity, projectId: string, input: {
  purchaseOrderId: string; itemId: string; periodStart: string; periodEnd: string;
  deliveredQuantity: number; acceptedQuantity: number; rejectedQuantity: number;
  unitCode?: string; acceptanceCriteria: string; evidenceReferences: readonly string[]; comment?: string;
}) {
  return command<ServiceAcceptanceModel>(path(baseUrl, projectId, "/service-acceptances"), identity, {
    clientGeneratedId: crypto.randomUUID(), ...input, unitCode: optional(input.unitCode), conversionVersion: null,
    comment: optional(input.comment),
  });
}

async function command<T>(url: string, identity: SupplyIdentity, body: unknown): Promise<T> {
  return requestJson(url, identity, { method: "POST", headers: {
    "Content-Type": "application/json", "Idempotency-Key": crypto.randomUUID(),
  }, body: JSON.stringify(body) });
}
async function requestJson<T>(url: string, identity: SupplyIdentity, init: RequestInit = {}): Promise<T> {
  const response = await fetch(url, { ...init, headers: { "X-Tenant-Id": identity.tenantId,
    "X-User-Id": identity.userId, ...init.headers } });
  await ensureApiSuccess(response);
  return response.json() as Promise<T>;
}
function path(baseUrl: string, projectId: string, suffix: string) {
  return `${baseUrl.replace(/\/$/, "")}/api/v1/projects/${projectId}/commercial/supply${suffix}`;
}
function optional(value?: string) { return value?.trim() ? value.trim() : null; }

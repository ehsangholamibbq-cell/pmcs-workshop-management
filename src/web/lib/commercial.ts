import { ensureApiSuccess } from "./localization.ts";

export type PartyType = "Supplier" | "Subcontractor" | "Consultant" | "LaborCrew" | "Client" | "Other";
export type PartyStatus = "Active" | "Inactive";
export type ProjectContractType = "MainContract" | "Subcontract" | "Supply" | "ProfessionalService" | "Labor" | "Other";
export type ProjectContractStatus = "Draft" | "Submitted" | "Returned" | "Active" | "Suspended" | "Closed" | "Terminated";
export type ContractAmendmentType = "ScopeChange" | "ValueChange" | "TimeExtension" | "Mixed";
export type ContractAmendmentStatus = "Draft" | "Submitted" | "Returned" | "Approved";
export type PurchaseRequestStatus = "Draft" | "Submitted" | "Returned" | "Approved" | "Ordered" | "Cancelled";
export type PurchaseOrderStatus = "Issued" | "Closed" | "Cancelled";
export type BudgetCheckStatus = "NotConfigured" | "NotChecked" | "WithinBudget" | "ExceptionRequired" | "ExceptionApproved";
export type ProcurementCriticality = "Normal" | "Priority" | "Critical" | "Emergency";
export type CommercialMetricState = "NotConfigured" | "SetupRequired" | "NoData" | "Available" | "Suspended";
export type CommercialDataQualityStatus = "NoData" | "Adequate" | "NeedsAttention";

export interface PartyModel {
  readonly id: string;
  readonly projectId: string;
  readonly code: string;
  readonly legalName: string;
  readonly type: PartyType;
  readonly nationalId: string | null;
  readonly contactName: string | null;
  readonly phone: string | null;
  readonly status: PartyStatus;
  readonly changedAt: string;
  readonly revision: number;
}

export interface ProjectContractModel {
  readonly id: string;
  readonly projectId: string;
  readonly partyId: string;
  readonly number: string;
  readonly title: string;
  readonly type: ProjectContractType;
  readonly originalApprovedAmount: number | null;
  readonly currencyCode: string;
  readonly startDate: string | null;
  readonly endDate: string | null;
  readonly notes: string | null;
  readonly status: ProjectContractStatus;
  readonly changedAt: string;
  readonly reviewComment: string | null;
  readonly revision: number;
}

export interface ContractAmendmentModel {
  readonly id: string;
  readonly projectId: string;
  readonly contractId: string;
  readonly number: string;
  readonly title: string;
  readonly type: ContractAmendmentType;
  readonly amountDelta: number | null;
  readonly currencyCode: string;
  readonly extensionDays: number | null;
  readonly notes: string | null;
  readonly status: ContractAmendmentStatus;
  readonly changedAt: string;
  readonly reviewComment: string | null;
  readonly revision: number;
}

export interface PurchaseRequestModel {
  readonly id: string;
  readonly projectId: string;
  readonly number: string;
  readonly title: string;
  readonly description: string;
  readonly estimatedAmount: number | null;
  readonly currencyCode: string;
  readonly neededByDate: string | null;
  readonly supplyItemId: string | null;
  readonly requestedQuantity: number | null;
  readonly unitCode: string | null;
  readonly deliveryLocation: string | null;
  readonly workItemReference: string | null;
  readonly wbsReference: string | null;
  readonly budgetLineReference: string | null;
  readonly budgetCheckStatus: BudgetCheckStatus;
  readonly criticality: ProcurementCriticality;
  readonly status: PurchaseRequestStatus;
  readonly changedAt: string;
  readonly reviewComment: string | null;
  readonly revision: number;
}

export interface PurchaseOrderModel {
  readonly id: string;
  readonly projectId: string;
  readonly purchaseRequestId: string;
  readonly partyId: string;
  readonly contractId: string | null;
  readonly number: string;
  readonly title: string;
  readonly amount: number;
  readonly currencyCode: string;
  readonly deliveryDueDate: string | null;
  readonly supplyItemId: string | null;
  readonly orderedQuantity: number | null;
  readonly unitCode: string | null;
  readonly deliveryLocation: string | null;
  readonly status: PurchaseOrderStatus;
  readonly issuedAt: string;
  readonly changedAt: string;
  readonly closureComment: string | null;
  readonly revision: number;
}

export interface CommercialStateModel {
  readonly snapshotId: string | null;
  readonly calculationVersion: string;
  readonly asOfDate: string;
  readonly calculatedAt: string;
  readonly currencyCode: string;
  readonly contractState: CommercialMetricState;
  readonly contractDataQualityStatus: CommercialDataQualityStatus;
  readonly procurementState: CommercialMetricState;
  readonly procurementDataQualityStatus: CommercialDataQualityStatus;
  readonly activePartyCount: number;
  readonly registeredContractCount: number;
  readonly activeContractCount: number;
  readonly contractsWithoutCeilingCount: number;
  readonly pendingContractApprovalCount: number;
  readonly expiredActiveContractCount: number;
  readonly approvedAmendmentCount: number;
  readonly approvedAmendmentDelta: number;
  readonly approvedContractCeilingAmount: number | null;
  readonly purchaseRequestCount: number;
  readonly pendingProcurementApprovalCount: number;
  readonly approvedRequestsAwaitingOrderCount: number;
  readonly openCommitmentCount: number;
  readonly overdueCommitmentCount: number;
  readonly totalCommittedAmount: number;
  readonly openCommitmentAmount: number;
  readonly sourceMaxChangedAt: string | null;
}

interface ApiIdentity {
  readonly tenantId: string;
  readonly userId: string;
}

export async function listParties(baseUrl: string, identity: ApiIdentity, projectId: string) {
  return requestJson<readonly PartyModel[]>(`${path(baseUrl, projectId)}/parties`, identity);
}

export async function createParty(
  baseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  input: { code: string; legalName: string; type: PartyType; nationalId?: string; contactName?: string; phone?: string },
) {
  return command<PartyModel>(`${path(baseUrl, projectId)}/parties`, identity, {
    clientGeneratedId: crypto.randomUUID(),
    ...input,
    nationalId: emptyToNull(input.nationalId),
    contactName: emptyToNull(input.contactName),
    phone: emptyToNull(input.phone),
  });
}

export async function listContracts(baseUrl: string, identity: ApiIdentity, projectId: string) {
  return requestJson<readonly ProjectContractModel[]>(`${path(baseUrl, projectId)}/contracts`, identity);
}

export async function createContract(
  baseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  input: {
    partyId: string;
    number: string;
    title: string;
    type: ProjectContractType;
    originalApprovedAmount?: number;
    startDate?: string;
    endDate?: string;
    notes?: string;
  },
) {
  return command<ProjectContractModel>(`${path(baseUrl, projectId)}/contracts`, identity, {
    clientGeneratedId: crypto.randomUUID(),
    currencyCode: null,
    ...input,
    originalApprovedAmount: input.originalApprovedAmount ?? null,
    startDate: emptyToNull(input.startDate),
    endDate: emptyToNull(input.endDate),
    notes: emptyToNull(input.notes),
  });
}

export async function transitionContract(
  baseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  item: Pick<ProjectContractModel, "id" | "revision">,
  action: "submit" | "activate" | "return" | "close",
  comment?: string,
) {
  return command<ProjectContractModel>(`${path(baseUrl, projectId)}/contracts/${item.id}/${action}`, identity, {
    baseRevision: item.revision,
    comment: emptyToNull(comment),
  });
}

export async function listAmendments(baseUrl: string, identity: ApiIdentity, projectId: string) {
  return requestJson<readonly ContractAmendmentModel[]>(`${path(baseUrl, projectId)}/contract-amendments`, identity);
}

export async function createAmendment(
  baseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  input: {
    contractId: string;
    number: string;
    title: string;
    type: ContractAmendmentType;
    amountDelta?: number;
    extensionDays?: number;
    notes?: string;
  },
) {
  return command<ContractAmendmentModel>(`${path(baseUrl, projectId)}/contract-amendments`, identity, {
    clientGeneratedId: crypto.randomUUID(),
    currencyCode: null,
    ...input,
    amountDelta: input.amountDelta ?? null,
    extensionDays: input.extensionDays ?? null,
    notes: emptyToNull(input.notes),
  });
}

export async function transitionAmendment(
  baseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  item: Pick<ContractAmendmentModel, "id" | "revision">,
  action: "submit" | "approve" | "return",
  comment?: string,
) {
  return command<ContractAmendmentModel>(`${path(baseUrl, projectId)}/contract-amendments/${item.id}/${action}`, identity, {
    baseRevision: item.revision,
    comment: emptyToNull(comment),
  });
}

export async function listPurchaseRequests(baseUrl: string, identity: ApiIdentity, projectId: string) {
  return requestJson<readonly PurchaseRequestModel[]>(`${path(baseUrl, projectId)}/purchase-requests`, identity);
}

export async function createPurchaseRequest(
  baseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  input: {
    number: string; title: string; description: string; estimatedAmount?: number; neededByDate?: string;
    supplyItemId?: string; requestedQuantity?: number; unitCode?: string; deliveryLocation?: string;
    workItemReference?: string; wbsReference?: string; budgetLineReference?: string;
    budgetCheckStatus?: BudgetCheckStatus; criticality?: ProcurementCriticality;
  },
) {
  return command<PurchaseRequestModel>(`${path(baseUrl, projectId)}/purchase-requests`, identity, {
    clientGeneratedId: crypto.randomUUID(),
    currencyCode: null,
    ...input,
    estimatedAmount: input.estimatedAmount ?? null,
    neededByDate: emptyToNull(input.neededByDate),
    supplyItemId: emptyToNull(input.supplyItemId),
    requestedQuantity: input.requestedQuantity ?? null,
    unitCode: emptyToNull(input.unitCode),
    deliveryLocation: emptyToNull(input.deliveryLocation),
    workItemReference: emptyToNull(input.workItemReference),
    wbsReference: emptyToNull(input.wbsReference),
    budgetLineReference: emptyToNull(input.budgetLineReference),
    budgetCheckStatus: input.budgetCheckStatus ?? "NotConfigured",
    criticality: input.criticality ?? "Normal",
  });
}

export async function transitionPurchaseRequest(
  baseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  item: Pick<PurchaseRequestModel, "id" | "revision">,
  action: "submit" | "approve" | "return",
  comment?: string,
) {
  return command<PurchaseRequestModel>(`${path(baseUrl, projectId)}/purchase-requests/${item.id}/${action}`, identity, {
    baseRevision: item.revision,
    comment: emptyToNull(comment),
  });
}

export async function listPurchaseOrders(baseUrl: string, identity: ApiIdentity, projectId: string) {
  return requestJson<readonly PurchaseOrderModel[]>(`${path(baseUrl, projectId)}/purchase-orders`, identity);
}

export async function issuePurchaseOrder(
  baseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  input: {
    purchaseRequestId: string;
    purchaseRequestRevision: number;
    partyId: string;
    contractId?: string;
    number: string;
    title: string;
    amount: number;
    deliveryDueDate?: string;
  },
) {
  return command<PurchaseOrderModel>(`${path(baseUrl, projectId)}/purchase-orders`, identity, {
    clientGeneratedId: crypto.randomUUID(),
    currencyCode: null,
    ...input,
    contractId: emptyToNull(input.contractId),
    deliveryDueDate: emptyToNull(input.deliveryDueDate),
  });
}

export async function transitionPurchaseOrder(
  baseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  item: Pick<PurchaseOrderModel, "id" | "revision">,
  action: "close" | "cancel",
  comment?: string,
) {
  return command<PurchaseOrderModel>(`${path(baseUrl, projectId)}/purchase-orders/${item.id}/${action}`, identity, {
    baseRevision: item.revision,
    comment: emptyToNull(comment),
  });
}

export async function getCommercialState(baseUrl: string, identity: ApiIdentity, projectId: string) {
  return requestJson<CommercialStateModel>(`${path(baseUrl, projectId)}/state`, identity);
}

async function command<T>(url: string, identity: ApiIdentity, body: unknown): Promise<T> {
  return requestJson<T>(url, identity, {
    method: "POST",
    headers: { "Content-Type": "application/json", "Idempotency-Key": crypto.randomUUID() },
    body: JSON.stringify(body),
  });
}

async function requestJson<T>(url: string, identity: ApiIdentity, init: RequestInit = {}): Promise<T> {
  const response = await fetch(url, {
    ...init,
    headers: { ...identityHeaders(identity), ...init.headers },
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<T>;
}

function identityHeaders(identity: ApiIdentity): Record<string, string> {
  return { "X-Tenant-Id": identity.tenantId, "X-User-Id": identity.userId };
}

function path(baseUrl: string, projectId: string): string {
  return `${baseUrl.replace(/\/$/, "")}/api/v1/projects/${projectId}/commercial`;
}

function emptyToNull(value?: string): string | null {
  return value?.trim() ? value.trim() : null;
}

import { ensureApiSuccess } from "./localization.ts";
import type { FinancialStateModel } from "./finance.ts";

export type FinancialObligationType = "Payable" | "Receivable";
export type FinancialObligationStatus = "Draft" | "Submitted" | "Returned" | "Approved" | "PartiallySettled" | "Settled";
export type PettyCashRequestStatus = "Draft" | "Submitted" | "Returned" | "Approved" | "Advanced" | "ReconciliationSubmitted" | "Reconciled";
export type ManagementFeePolicyStatus = "Draft" | "Submitted" | "Returned" | "Approved" | "Superseded";

export interface ObligationAgingModel {
  readonly openPayableCount: number;
  readonly openReceivableCount: number;
  readonly overduePayableCount: number;
  readonly overdueReceivableCount: number;
  readonly openPayableAmount: number;
  readonly openReceivableAmount: number;
  readonly overduePayableAmount: number;
  readonly overdueReceivableAmount: number;
  readonly agingOneToThirtyAmount: number;
  readonly agingThirtyOneToSixtyAmount: number;
  readonly agingOverSixtyAmount: number;
}

export interface PettyCashControlModel {
  readonly activeRequestCount: number;
  readonly advancedRequestCount: number;
  readonly overdueReconciliationCount: number;
  readonly outstandingAdvanceAmount: number;
  readonly overdueAdvanceAmount: number;
}

export interface FinanceControlStateModel {
  readonly financialState: FinancialStateModel;
  readonly control: {
    readonly calculationVersion: string;
    readonly asOfDate: string;
    readonly aging: ObligationAgingModel;
    readonly pettyCash: PettyCashControlModel;
    readonly managementFeePolicyId: string | null;
    readonly managementFeeRatePercent: number | null;
    readonly managementFeeAmount: number | null;
  };
}

export interface FinancialObligationModel {
  readonly id: string;
  readonly projectId: string;
  readonly type: FinancialObligationType;
  readonly number: string;
  readonly description: string;
  readonly issueDate: string;
  readonly dueDate: string;
  readonly amount: number;
  readonly settledAmount: number;
  readonly outstandingAmount: number;
  readonly currencyCode: string;
  readonly partyId: string | null;
  readonly counterparty: string | null;
  readonly contractId: string | null;
  readonly commitmentId: string | null;
  readonly costCenterCode: string | null;
  readonly wbsReference: string | null;
  readonly locationId: string | null;
  readonly locationCode: string | null;
  readonly status: FinancialObligationStatus;
  readonly reviewComment: string | null;
  readonly revision: number;
}

export interface PettyCashRequestModel {
  readonly id: string;
  readonly projectId: string;
  readonly number: string;
  readonly purpose: string;
  readonly custodian: string;
  readonly requestDate: string;
  readonly reconciliationDueDate: string;
  readonly requestedAmount: number;
  readonly approvedAmount: number | null;
  readonly reconciledExpenseAmount: number | null;
  readonly returnedAmount: number | null;
  readonly currencyCode: string;
  readonly locationId: string | null;
  readonly locationCode: string | null;
  readonly costCenterCode: string | null;
  readonly wbsReference: string | null;
  readonly advanceRecordId: string | null;
  readonly expenseRecordId: string | null;
  readonly returnRecordId: string | null;
  readonly status: PettyCashRequestStatus;
  readonly reviewComment: string | null;
  readonly revision: number;
}

export interface ManagementFeePolicyModel {
  readonly id: string;
  readonly projectId: string;
  readonly title: string;
  readonly ratePercent: number;
  readonly calculationBase: "RecognizedSpend";
  readonly effectiveFrom: string;
  readonly notes: string | null;
  readonly status: ManagementFeePolicyStatus;
  readonly reviewComment: string | null;
  readonly revision: number;
}

interface ApiIdentity {
  readonly tenantId: string;
  readonly userId: string;
}

export async function getFinanceControlState(baseUrl: string, identity: ApiIdentity, projectId: string) {
  return requestJson<FinanceControlStateModel>(`${path(baseUrl, projectId)}/control-state`, identity);
}

export async function listFinancialObligations(baseUrl: string, identity: ApiIdentity, projectId: string) {
  return requestJson<readonly FinancialObligationModel[]>(`${path(baseUrl, projectId)}/obligations`, identity);
}

export async function createFinancialObligation(
  baseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  input: {
    readonly type: FinancialObligationType;
    readonly number: string;
    readonly description: string;
    readonly issueDate: string;
    readonly dueDate: string;
    readonly amount: number;
    readonly partyId?: string;
    readonly counterparty?: string;
    readonly contractId?: string;
    readonly commitmentId?: string;
    readonly costCenterCode?: string;
    readonly wbsReference?: string;
    readonly locationId?: string;
  },
) {
  return command<FinancialObligationModel>(`${path(baseUrl, projectId)}/obligations`, identity, {
    clientGeneratedId: crypto.randomUUID(),
    currencyCode: null,
    ...input,
    partyId: emptyToNull(input.partyId),
    counterparty: emptyToNull(input.counterparty),
    contractId: emptyToNull(input.contractId),
    commitmentId: emptyToNull(input.commitmentId),
    costCenterCode: emptyToNull(input.costCenterCode),
    wbsReference: emptyToNull(input.wbsReference),
    locationId: emptyToNull(input.locationId),
  });
}

export async function transitionFinancialObligation(
  baseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  item: Pick<FinancialObligationModel, "id" | "revision">,
  action: "submit" | "approve" | "return",
  comment?: string,
) {
  const body = action === "submit"
    ? { baseRevision: item.revision }
    : { baseRevision: item.revision, comment: emptyToNull(comment) };
  return command<FinancialObligationModel>(
    `${path(baseUrl, projectId)}/obligations/${item.id}/${action}`,
    identity,
    body,
  );
}

export async function settleFinancialObligation(
  baseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  item: Pick<FinancialObligationModel, "id" | "revision">,
  financialRecordId: string,
  amount: number,
) {
  return command<FinancialObligationModel>(
    `${path(baseUrl, projectId)}/obligations/${item.id}/settlements`,
    identity,
    { baseRevision: item.revision, financialRecordId, amount },
  );
}

export async function listPettyCashRequests(baseUrl: string, identity: ApiIdentity, projectId: string) {
  return requestJson<readonly PettyCashRequestModel[]>(`${path(baseUrl, projectId)}/petty-cash-requests`, identity);
}

export async function createPettyCashRequest(
  baseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  input: {
    readonly number: string;
    readonly purpose: string;
    readonly custodian: string;
    readonly requestDate: string;
    readonly reconciliationDueDate: string;
    readonly requestedAmount: number;
    readonly locationId?: string;
    readonly costCenterCode?: string;
    readonly wbsReference?: string;
  },
) {
  return command<PettyCashRequestModel>(`${path(baseUrl, projectId)}/petty-cash-requests`, identity, {
    clientGeneratedId: crypto.randomUUID(),
    currencyCode: null,
    ...input,
    locationId: emptyToNull(input.locationId),
    costCenterCode: emptyToNull(input.costCenterCode),
    wbsReference: emptyToNull(input.wbsReference),
  });
}

export async function transitionPettyCashRequest(
  baseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  item: Pick<PettyCashRequestModel, "id" | "revision" | "requestedAmount">,
  action: "submit" | "approve" | "return" | "reconciliation/approve",
  comment?: string,
) {
  const body = action === "submit"
    ? { baseRevision: item.revision }
    : action === "approve"
      ? { baseRevision: item.revision, approvedAmount: item.requestedAmount, comment: emptyToNull(comment) }
      : { baseRevision: item.revision, comment: emptyToNull(comment) };
  return command<PettyCashRequestModel>(
    `${path(baseUrl, projectId)}/petty-cash-requests/${item.id}/${action}`,
    identity,
    body,
  );
}

export async function recordPettyCashAdvance(
  baseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  item: Pick<PettyCashRequestModel, "id" | "revision">,
  advanceRecordId: string,
) {
  return command<PettyCashRequestModel>(
    `${path(baseUrl, projectId)}/petty-cash-requests/${item.id}/advance`,
    identity,
    { baseRevision: item.revision, advanceRecordId },
  );
}

export async function submitPettyCashReconciliation(
  baseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  item: Pick<PettyCashRequestModel, "id" | "revision">,
  expenseAmount: number,
  returnedAmount: number,
  expenseRecordId: string,
  returnRecordId?: string,
) {
  return command<PettyCashRequestModel>(
    `${path(baseUrl, projectId)}/petty-cash-requests/${item.id}/reconciliation`,
    identity,
    {
      baseRevision: item.revision,
      expenseAmount,
      returnedAmount,
      expenseRecordId,
      returnRecordId: emptyToNull(returnRecordId),
    },
  );
}

export async function listManagementFeePolicies(baseUrl: string, identity: ApiIdentity, projectId: string) {
  return requestJson<readonly ManagementFeePolicyModel[]>(`${path(baseUrl, projectId)}/management-fee-policies`, identity);
}

export async function createManagementFeePolicy(
  baseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  input: { readonly title: string; readonly ratePercent: number; readonly effectiveFrom: string; readonly notes?: string },
) {
  return command<ManagementFeePolicyModel>(`${path(baseUrl, projectId)}/management-fee-policies`, identity, {
    clientGeneratedId: crypto.randomUUID(),
    calculationBase: "RecognizedSpend",
    ...input,
    notes: emptyToNull(input.notes),
  });
}

export async function transitionManagementFeePolicy(
  baseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  item: Pick<ManagementFeePolicyModel, "id" | "revision">,
  action: "submit" | "approve" | "return",
  comment?: string,
) {
  const body = action === "submit"
    ? { baseRevision: item.revision }
    : { baseRevision: item.revision, comment: emptyToNull(comment) };
  return command<ManagementFeePolicyModel>(
    `${path(baseUrl, projectId)}/management-fee-policies/${item.id}/${action}`,
    identity,
    body,
  );
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
    headers: { "X-Tenant-Id": identity.tenantId, "X-User-Id": identity.userId, ...init.headers },
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<T>;
}

function path(baseUrl: string, projectId: string): string {
  return `${baseUrl.replace(/\/$/, "")}/api/v1/projects/${projectId}/finance`;
}

function emptyToNull(value?: string): string | null {
  return value?.trim() ? value.trim() : null;
}

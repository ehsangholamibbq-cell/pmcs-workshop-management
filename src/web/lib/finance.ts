import { ensureApiSuccess } from "./localization.ts";

export type FinancialRecordType = "Receipt" | "Payment" | "PettyCashFunding" | "PettyCashExpense";
export type FinancialRecordStatus = "Draft" | "Submitted" | "Returned" | "Posted";
export type BudgetBaselineStatus = "Draft" | "Submitted" | "Returned" | "Approved" | "Superseded";
export type FinancialStateStatus = "NotConfigured" | "NoData" | "Available";
export type FinancialDataQualityStatus = "NoData" | "Adequate" | "NeedsAttention";
export type BudgetComparisonState = "NotConfigured" | "SetupRequired" | "NoData" | "Available" | "Suspended";

export interface FinancialRecordModel {
  readonly id: string;
  readonly projectId: string;
  readonly type: FinancialRecordType;
  readonly transactionDate: string;
  readonly amount: number;
  readonly currencyCode: string;
  readonly description: string;
  readonly counterparty: string | null;
  readonly documentNumber: string | null;
  readonly contractReference: string | null;
  readonly contractId: string | null;
  readonly commitmentId: string | null;
  readonly costCenterCode: string | null;
  readonly status: FinancialRecordStatus;
  readonly reviewComment: string | null;
  readonly revision: number;
}

export interface FinancialStateModel {
  readonly snapshotId: string | null;
  readonly calculationVersion: string;
  readonly asOfDate: string;
  readonly calculatedAt: string;
  readonly currencyCode: string;
  readonly status: FinancialStateStatus;
  readonly dataQualityStatus: FinancialDataQualityStatus;
  readonly budgetComparisonState: BudgetComparisonState;
  readonly postedRecordCount: number;
  readonly totalReceipts: number;
  readonly directPayments: number;
  readonly pettyCashFunding: number;
  readonly pettyCashExpenses: number;
  readonly externalNetCash: number;
  readonly recognizedSpend: number;
  readonly pettyCashBalance: number;
  readonly approvedBudgetAmount: number | null;
  readonly budgetRemainingAmount: number | null;
  readonly budgetConsumedPercent: number | null;
  readonly sourceMaxChangedAt: string | null;
}

export interface BudgetBaselineModel {
  readonly id: string;
  readonly projectId: string;
  readonly title: string;
  readonly amount: number;
  readonly currencyCode: string;
  readonly notes: string | null;
  readonly status: BudgetBaselineStatus;
  readonly reviewComment: string | null;
  readonly revision: number;
}

export interface CreateFinancialRecordInput {
  readonly type: FinancialRecordType;
  readonly transactionDate: string;
  readonly amount: number;
  readonly description: string;
  readonly counterparty?: string;
  readonly documentNumber?: string;
  readonly contractReference?: string;
  readonly contractId?: string;
  readonly commitmentId?: string;
  readonly costCenterCode?: string;
}

interface ApiIdentity {
  readonly tenantId: string;
  readonly userId: string;
}

export async function listFinancialRecords(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
): Promise<readonly FinancialRecordModel[]> {
  return requestJson(`${path(apiBaseUrl, projectId)}/records`, identity);
}

export async function getFinancialState(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
): Promise<FinancialStateModel> {
  return requestJson(`${path(apiBaseUrl, projectId)}/state`, identity);
}

export async function createFinancialRecord(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  input: CreateFinancialRecordInput,
): Promise<FinancialRecordModel> {
  return command(`${path(apiBaseUrl, projectId)}/records`, identity, {
    clientGeneratedId: crypto.randomUUID(),
    currencyCode: null,
    ...input,
    counterparty: emptyToNull(input.counterparty),
    documentNumber: emptyToNull(input.documentNumber),
    contractReference: emptyToNull(input.contractReference),
    contractId: emptyToNull(input.contractId),
    commitmentId: emptyToNull(input.commitmentId),
    costCenterCode: emptyToNull(input.costCenterCode),
  });
}

export async function transitionFinancialRecord(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  record: Pick<FinancialRecordModel, "id" | "revision">,
  action: "submit" | "post" | "return",
  comment?: string,
): Promise<FinancialRecordModel> {
  const body = action === "submit"
    ? { baseRevision: record.revision }
    : { baseRevision: record.revision, comment: emptyToNull(comment) };
  return command(`${path(apiBaseUrl, projectId)}/records/${record.id}/${action}`, identity, body);
}

export async function amendFinancialRecord(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  record: FinancialRecordModel,
  amount: number,
  description: string,
): Promise<FinancialRecordModel> {
  return requestJson(`${path(apiBaseUrl, projectId)}/records/${record.id}`, identity, {
    method: "PUT",
    headers: {
      "Content-Type": "application/json",
      "Idempotency-Key": crypto.randomUUID(),
    },
    body: JSON.stringify({
      baseRevision: record.revision,
      type: record.type,
      transactionDate: record.transactionDate,
      amount,
      currencyCode: record.currencyCode,
      description,
      counterparty: record.counterparty,
      documentNumber: record.documentNumber,
      contractReference: record.contractReference,
      contractId: record.contractId,
      commitmentId: record.commitmentId,
      costCenterCode: record.costCenterCode,
    }),
  });
}

export async function listBudgetBaselines(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
): Promise<readonly BudgetBaselineModel[]> {
  return requestJson(`${path(apiBaseUrl, projectId)}/budget-baselines`, identity);
}

export async function createBudgetBaseline(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  title: string,
  amount: number,
  notes?: string,
): Promise<BudgetBaselineModel> {
  return command(`${path(apiBaseUrl, projectId)}/budget-baselines`, identity, {
    clientGeneratedId: crypto.randomUUID(),
    title,
    amount,
    currencyCode: null,
    notes: emptyToNull(notes),
  });
}

export async function transitionBudgetBaseline(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  baseline: Pick<BudgetBaselineModel, "id" | "revision">,
  action: "submit" | "approve" | "return",
  comment?: string,
): Promise<BudgetBaselineModel> {
  const body = action === "submit"
    ? { baseRevision: baseline.revision }
    : { baseRevision: baseline.revision, comment: emptyToNull(comment) };
  return command(`${path(apiBaseUrl, projectId)}/budget-baselines/${baseline.id}/${action}`, identity, body);
}

export async function amendBudgetBaseline(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  baseline: BudgetBaselineModel,
  amount: number,
  title: string,
): Promise<BudgetBaselineModel> {
  return requestJson(`${path(apiBaseUrl, projectId)}/budget-baselines/${baseline.id}`, identity, {
    method: "PUT",
    headers: {
      "Content-Type": "application/json",
      "Idempotency-Key": crypto.randomUUID(),
    },
    body: JSON.stringify({
      baseRevision: baseline.revision,
      title,
      amount,
      currencyCode: baseline.currencyCode,
      notes: baseline.notes,
    }),
  });
}

async function command<T>(url: string, identity: ApiIdentity, body: unknown): Promise<T> {
  return requestJson<T>(url, identity, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "Idempotency-Key": crypto.randomUUID(),
    },
    body: JSON.stringify(body),
  });
}

async function requestJson<T>(
  url: string,
  identity: ApiIdentity,
  init: RequestInit = {},
): Promise<T> {
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

function path(apiBaseUrl: string, projectId: string): string {
  return `${apiBaseUrl.replace(/\/$/, "")}/api/v1/projects/${projectId}/finance`;
}

function emptyToNull(value?: string): string | null {
  return value?.trim() ? value.trim() : null;
}

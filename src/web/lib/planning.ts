import { ensureApiSuccess } from "./localization.ts";

export type PlanningMode = "None" | "SimpleWorkList" | "Milestones" | "WbsBaseline" | "ExternalSchedule";
export type MeasurementBasisState = "NotConfigured" | "ActualOnly" | "TargetsAvailable";
export type OfficialProgressBasisState = "NotConfigured" | "Approved" | "IncompleteActualData" | "ModeMismatch";
export type ScheduleBasisState = "NotConfigured" | "Approved" | "ModeMismatch";
export type MeasurementItemStatus = "Active" | "Inactive";
export type PlanningBaselineKind = "MeasurementWeights" | "MilestonePlan" | "WbsBaseline" | "ExternalSchedule";
export type PlanningEntryKind = "Summary" | "MeasurementItem" | "Activity" | "Milestone";
export type ProgressMeasurementMethod = "None" | "QuantityBased" | "ManualPercent";
export type PlanningWorkflowStatus = "Draft" | "Submitted" | "Returned" | "Approved" | "Superseded";
export type MilestoneScheduleState = "Upcoming" | "Due" | "Late" | "Completed";

export interface ApiIdentity {
  readonly tenantId: string;
  readonly userId: string;
}

export interface MeasurementItemModel {
  readonly id: string;
  readonly code: string;
  readonly title: string;
  readonly unit: string;
  readonly targetQuantity: number | null;
  readonly notes: string | null;
  readonly status: MeasurementItemStatus;
  readonly revision: number;
  readonly lastModifiedAt: string;
}

export interface ProgressItemModel {
  readonly measurementItemId: string;
  readonly code: string;
  readonly title: string;
  readonly unit: string;
  readonly targetQuantity: number | null;
  readonly isActive: boolean;
  readonly approvedQuantity: number | null;
  readonly provisionalQuantity: number | null;
  readonly approvedCompletionPercent: number | null;
  readonly latestApprovedReportDate: string | null;
}

export interface ProgressLedgerModel {
  readonly planningMode: PlanningMode;
  readonly measurementBasisState: MeasurementBasisState;
  readonly officialProgressBasisState: OfficialProgressBasisState;
  readonly scheduleBasisState: ScheduleBasisState;
  readonly approvedBaselineId: string | null;
  readonly approvedBaselineVersion: string | null;
  readonly approvedBaselineKind: PlanningBaselineKind | null;
  readonly officialOverallPhysicalPercent: number | null;
  readonly plannedOverallPhysicalPercent: number | null;
  readonly scheduleVariancePercent: number | null;
  readonly forecastCompletionDate: string | null;
  readonly missingActualEntryCount: number;
  readonly approvedFactCount: number;
  readonly provisionalFactCount: number;
  readonly unlinkedApprovedFactCount: number;
  readonly unlinkedProvisionalFactCount: number;
  readonly items: readonly ProgressItemModel[];
  readonly milestones: readonly MilestoneProgressSummaryModel[];
}

export interface MilestoneProgressSummaryModel {
  readonly baselineEntryId: string;
  readonly code: string;
  readonly title: string;
  readonly plannedDate: string;
  readonly weightPercent: number;
  readonly approvedProgressPercent: number | null;
  readonly latestApprovedStatusDate: string | null;
  readonly scheduleState: MilestoneScheduleState;
}

export interface PlanningBaselineEntryModel {
  readonly id: string;
  readonly parentEntryId: string | null;
  readonly code: string;
  readonly title: string;
  readonly kind: PlanningEntryKind;
  readonly measurementMethod: ProgressMeasurementMethod;
  readonly measurementItemId: string | null;
  readonly plannedStart: string | null;
  readonly plannedFinish: string | null;
  readonly weightPercent: number | null;
  readonly externalId: string | null;
  readonly sortOrder: number;
}

export interface PlanningBaselineModel {
  readonly id: string;
  readonly versionCode: string;
  readonly title: string;
  readonly kind: PlanningBaselineKind;
  readonly sourceSystem: string | null;
  readonly sourceReference: string | null;
  readonly status: PlanningWorkflowStatus;
  readonly entries: readonly PlanningBaselineEntryModel[];
  readonly createdBy: string;
  readonly createdAt: string;
  readonly submittedAt: string | null;
  readonly reviewedBy: string | null;
  readonly reviewedAt: string | null;
  readonly reviewComment: string | null;
  readonly revision: number;
}

export interface PlanningBaselineEntryInput {
  readonly clientGeneratedId: string;
  readonly parentEntryId: string | null;
  readonly code: string;
  readonly title: string;
  readonly kind: PlanningEntryKind;
  readonly measurementMethod: ProgressMeasurementMethod;
  readonly measurementItemId: string | null;
  readonly plannedStart: string | null;
  readonly plannedFinish: string | null;
  readonly weightPercent: number | null;
  readonly externalId: string | null;
  readonly sortOrder: number;
}

export interface MilestoneProgressUpdateModel {
  readonly id: string;
  readonly baselineId: string;
  readonly baselineEntryId: string;
  readonly statusDate: string;
  readonly progressPercent: number;
  readonly evidenceReference: string;
  readonly note: string | null;
  readonly status: PlanningWorkflowStatus;
  readonly createdBy: string;
  readonly createdAt: string;
  readonly submittedAt: string | null;
  readonly reviewedBy: string | null;
  readonly reviewedAt: string | null;
  readonly reviewComment: string | null;
  readonly revision: number;
}

export async function listMeasurementItems(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
): Promise<readonly MeasurementItemModel[]> {
  return requestJson(`${path(apiBaseUrl, projectId)}/measurement-items`, identity);
}

export async function getProgressLedger(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
): Promise<ProgressLedgerModel> {
  return requestJson(`${path(apiBaseUrl, projectId)}/progress`, identity);
}

export async function createMeasurementItem(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  input: {
    readonly code: string;
    readonly title: string;
    readonly unit: string;
    readonly targetQuantity: number | null;
    readonly notes: string;
  },
): Promise<MeasurementItemModel> {
  return requestJson(`${path(apiBaseUrl, projectId)}/measurement-items`, identity, {
    method: "POST",
    headers: commandHeaders(),
    body: JSON.stringify({
      clientGeneratedId: crypto.randomUUID(),
      ...input,
      notes: input.notes.trim() || null,
    }),
  });
}

export async function deactivateMeasurementItem(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  item: MeasurementItemModel,
): Promise<MeasurementItemModel> {
  return requestJson(`${path(apiBaseUrl, projectId)}/measurement-items/${item.id}/deactivate`, identity, {
    method: "POST",
    headers: commandHeaders(),
    body: JSON.stringify({ baseRevision: item.revision }),
  });
}

export async function listPlanningBaselines(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
): Promise<readonly PlanningBaselineModel[]> {
  return requestJson(`${path(apiBaseUrl, projectId)}/baselines`, identity);
}

export async function createPlanningBaseline(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  input: {
    readonly versionCode: string;
    readonly title: string;
    readonly kind: PlanningBaselineKind;
    readonly sourceSystem: string | null;
    readonly sourceReference: string | null;
    readonly entries: readonly PlanningBaselineEntryInput[];
  },
): Promise<PlanningBaselineModel> {
  return requestJson(`${path(apiBaseUrl, projectId)}/baselines`, identity, {
    method: "POST",
    headers: commandHeaders(),
    body: JSON.stringify({ clientGeneratedId: crypto.randomUUID(), ...input }),
  });
}

export async function amendPlanningBaseline(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  baseline: PlanningBaselineModel,
  input: Pick<PlanningBaselineModel, "title" | "sourceSystem" | "sourceReference"> & {
    readonly entries: readonly PlanningBaselineEntryInput[];
  },
): Promise<PlanningBaselineModel> {
  return requestJson(`${path(apiBaseUrl, projectId)}/baselines/${baseline.id}`, identity, {
    method: "PUT",
    headers: commandHeaders(),
    body: JSON.stringify({ ...input, baseRevision: baseline.revision }),
  });
}

export async function submitPlanningBaseline(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  baseline: PlanningBaselineModel,
): Promise<PlanningBaselineModel> {
  return planningTransition(apiBaseUrl, identity, projectId, "baselines", baseline.id, "submit", baseline.revision);
}

export async function approvePlanningBaseline(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  baseline: PlanningBaselineModel,
  comment: string,
): Promise<PlanningBaselineModel> {
  return planningTransition(apiBaseUrl, identity, projectId, "baselines", baseline.id, "approve", baseline.revision, { comment: comment.trim() || null });
}

export async function returnPlanningBaseline(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  baseline: PlanningBaselineModel,
  reason: string,
): Promise<PlanningBaselineModel> {
  return planningTransition(apiBaseUrl, identity, projectId, "baselines", baseline.id, "return", baseline.revision, { reason });
}

export async function listMilestoneProgressUpdates(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  baselineId?: string,
): Promise<readonly MilestoneProgressUpdateModel[]> {
  const query = baselineId ? `?baselineId=${encodeURIComponent(baselineId)}` : "";
  return requestJson(`${path(apiBaseUrl, projectId)}/milestone-updates${query}`, identity);
}

export async function createMilestoneProgressUpdate(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  input: {
    readonly baselineId: string;
    readonly baselineEntryId: string;
    readonly statusDate: string;
    readonly progressPercent: number;
    readonly evidenceReference: string;
    readonly note: string;
  },
): Promise<MilestoneProgressUpdateModel> {
  return requestJson(`${path(apiBaseUrl, projectId)}/milestone-updates`, identity, {
    method: "POST",
    headers: commandHeaders(),
    body: JSON.stringify({
      clientGeneratedId: crypto.randomUUID(),
      ...input,
      note: input.note.trim() || null,
    }),
  });
}

export async function amendMilestoneProgressUpdate(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  update: MilestoneProgressUpdateModel,
  input: {
    readonly statusDate: string;
    readonly progressPercent: number;
    readonly evidenceReference: string;
    readonly note: string;
  },
): Promise<MilestoneProgressUpdateModel> {
  return requestJson(`${path(apiBaseUrl, projectId)}/milestone-updates/${update.id}`, identity, {
    method: "PUT",
    headers: commandHeaders(),
    body: JSON.stringify({
      ...input,
      note: input.note.trim() || null,
      baseRevision: update.revision,
    }),
  });
}

export async function submitMilestoneProgressUpdate(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  update: MilestoneProgressUpdateModel,
): Promise<MilestoneProgressUpdateModel> {
  return planningTransition(apiBaseUrl, identity, projectId, "milestone-updates", update.id, "submit", update.revision);
}

export async function approveMilestoneProgressUpdate(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  update: MilestoneProgressUpdateModel,
  comment: string,
): Promise<MilestoneProgressUpdateModel> {
  return planningTransition(apiBaseUrl, identity, projectId, "milestone-updates", update.id, "approve", update.revision, { comment: comment.trim() || null });
}

export async function returnMilestoneProgressUpdate(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  update: MilestoneProgressUpdateModel,
  reason: string,
): Promise<MilestoneProgressUpdateModel> {
  return planningTransition(apiBaseUrl, identity, projectId, "milestone-updates", update.id, "return", update.revision, { reason });
}

async function planningTransition<T>(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  collection: "baselines" | "milestone-updates",
  itemId: string,
  transition: "submit" | "approve" | "return",
  baseRevision: number,
  body: Record<string, unknown> = {},
): Promise<T> {
  return requestJson(`${path(apiBaseUrl, projectId)}/${collection}/${itemId}/${transition}`, identity, {
    method: "POST",
    headers: commandHeaders(),
    body: JSON.stringify({ baseRevision, ...body }),
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

function commandHeaders(): Record<string, string> {
  return {
    "Content-Type": "application/json",
    "Idempotency-Key": crypto.randomUUID(),
  };
}

function identityHeaders(identity: ApiIdentity): Record<string, string> {
  return { "X-Tenant-Id": identity.tenantId, "X-User-Id": identity.userId };
}

function path(apiBaseUrl: string, projectId: string): string {
  return `${apiBaseUrl.replace(/\/$/, "")}/api/v1/projects/${projectId}/planning`;
}

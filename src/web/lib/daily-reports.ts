import { ensureApiSuccess } from "./localization.ts";
import type { DailyFactPayload } from "./field-facts.ts";

export type DailyReportStatus =
  | "Draft"
  | "Submitted"
  | "Returned"
  | "Approved"
  | "Rejected"
  | "Superseded";

export interface DailyReportSummary {
  readonly id: string;
  readonly projectId: string;
  readonly reportDate: string;
  readonly locationName: string | null;
  readonly narrative: string | null;
  readonly status: DailyReportStatus;
  readonly revision: number;
  readonly createdBy: string;
  readonly factCount: number;
  readonly reviewedBy: string | null;
  readonly reviewedAt: string | null;
  readonly reviewComment: string | null;
  readonly rootReportId: string;
  readonly versionNumber: number;
  readonly supersedesReportId: string | null;
  readonly supersededByReportId: string | null;
  readonly supersededAt: string | null;
  readonly correctionReason: string | null;
  readonly correctionInitiatedBy: string | null;
  readonly facts?: readonly DailyReportFactModel[] | null;
}

export interface DailyReportFactModel {
  readonly id: string;
  readonly kind: DailyFactPayload["kind"];
  readonly description: string;
  readonly category: string | null;
  readonly locationName: string | null;
  readonly locationId: string | null;
  readonly quantity: number | null;
  readonly unit: string | null;
  readonly resourceCount: number | null;
  readonly hours: number | null;
  readonly impactLevel: DailyFactPayload["impactLevel"];
  readonly referenceCode: string | null;
  readonly createdBy: string;
  readonly createdAt: string;
  readonly measurementItemId: string | null;
  readonly copiedFromFactId: string | null;
}

interface ApiIdentity {
  readonly tenantId: string;
  readonly userId: string;
}

export async function getDailyReport(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  reportId: string,
): Promise<DailyReportSummary | null> {
  const response = await fetch(buildReportUrl(apiBaseUrl, projectId, reportId), {
    headers: identityHeaders(identity),
  });
  if (response.status === 404) {
    return null;
  }
  await ensureApiSuccess(response);
  return response.json() as Promise<DailyReportSummary>;
}

export async function listDailyReports(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
): Promise<readonly DailyReportSummary[]> {
  const response = await fetch(
    `${normalizedBaseUrl(apiBaseUrl)}/api/v1/projects/${projectId}/daily-reports`,
    { headers: identityHeaders(identity) },
  );
  await ensureApiSuccess(response);
  return response.json() as Promise<readonly DailyReportSummary[]>;
}

export async function listReviewInbox(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
): Promise<readonly DailyReportSummary[]> {
  const response = await fetch(
    `${normalizedBaseUrl(apiBaseUrl)}/api/v1/projects/${projectId}/daily-reports/inbox`,
    { headers: identityHeaders(identity) },
  );
  await ensureApiSuccess(response);
  return response.json() as Promise<readonly DailyReportSummary[]>;
}

export async function submitDailyReport(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  reportId: string,
  baseRevision: number,
): Promise<DailyReportSummary> {
  return postWorkflow(
    `${buildReportUrl(apiBaseUrl, projectId, reportId)}/submit`,
    identity,
    { baseRevision },
  );
}

export async function reviewDailyReport(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  reportId: string,
  action: "approve" | "return",
  baseRevision: number,
  comment: string,
): Promise<DailyReportSummary> {
  return postWorkflow(
    `${buildReportUrl(apiBaseUrl, projectId, reportId)}/${action}`,
    identity,
    { baseRevision, comment: comment.trim() || null },
  );
}

export async function startDailyReportCorrection(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  reportId: string,
  baseRevision: number,
  reason: string,
): Promise<DailyReportSummary> {
  return postWorkflow(
    `${buildReportUrl(apiBaseUrl, projectId, reportId)}/corrections`,
    identity,
    { clientGeneratedId: crypto.randomUUID(), baseRevision, reason: reason.trim() },
  );
}

export async function reviseDailyReportDetails(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  reportId: string,
  baseRevision: number,
  locationName: string,
  narrative: string,
): Promise<DailyReportSummary> {
  return postWorkflow(
    `${buildReportUrl(apiBaseUrl, projectId, reportId)}/details`,
    identity,
    { baseRevision, locationName: locationName.trim() || null, narrative: narrative.trim() || null },
  );
}

export async function addDailyReportFact(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  reportId: string,
  baseRevision: number,
  payload: DailyFactPayload,
): Promise<DailyReportSummary> {
  return postWorkflow(
    `${buildReportUrl(apiBaseUrl, projectId, reportId)}/facts`,
    identity,
    {
      clientGeneratedId: payload.factId,
      kind: payload.kind,
      description: payload.description,
      category: payload.category,
      locationName: payload.factLocationName,
      locationId: payload.locationId,
      quantity: payload.quantity,
      unit: payload.unit,
      resourceCount: payload.resourceCount,
      hours: payload.hours,
      impactLevel: payload.impactLevel,
      referenceCode: payload.referenceCode,
      measurementItemId: payload.measurementItemId,
      baseRevision,
    },
  );
}

export async function removeDailyReportFact(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  reportId: string,
  factId: string,
  baseRevision: number,
): Promise<DailyReportSummary> {
  return postWorkflow(
    `${buildReportUrl(apiBaseUrl, projectId, reportId)}/facts/${factId}/remove`,
    identity,
    { baseRevision },
  );
}

async function postWorkflow(
  url: string,
  identity: ApiIdentity,
  body: object,
): Promise<DailyReportSummary> {
  const response = await fetch(url, {
    method: "POST",
    headers: {
      ...identityHeaders(identity),
      "Content-Type": "application/json",
      "Idempotency-Key": crypto.randomUUID(),
    },
    body: JSON.stringify(body),
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<DailyReportSummary>;
}

function identityHeaders(identity: ApiIdentity): Record<string, string> {
  return {
    "X-Tenant-Id": identity.tenantId,
    "X-User-Id": identity.userId,
  };
}

function buildReportUrl(apiBaseUrl: string, projectId: string, reportId: string): string {
  return `${normalizedBaseUrl(apiBaseUrl)}/api/v1/projects/${projectId}/daily-reports/${reportId}`;
}

function normalizedBaseUrl(apiBaseUrl: string): string {
  return apiBaseUrl.replace(/\/$/, "");
}

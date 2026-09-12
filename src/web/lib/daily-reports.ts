import { ensureApiSuccess } from "./localization.ts";

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
  readonly factCount: number;
  readonly reviewedBy: string | null;
  readonly reviewedAt: string | null;
  readonly reviewComment: string | null;
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

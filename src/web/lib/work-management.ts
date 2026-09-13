import { ensureApiSuccess } from "./localization.ts";

export interface MyWorkItemModel {
  readonly id: string;
  readonly kind: "DailyReportReview" | "DailyReportCorrection" | "ManagementAction";
  readonly title: string;
  readonly description: string | null;
  readonly dueDate: string | null;
  readonly referenceDate: string | null;
  readonly priority: "Low" | "Medium" | "High" | "Critical";
  readonly status: string;
  readonly targetType: "DailyReport" | "ManagementAction";
  readonly targetId: string;
  readonly changedAt: string;
  readonly revision: number;
  readonly isOverdue: boolean;
}

export interface MyWorkModel {
  readonly calculatedAt: string;
  readonly unreadNotificationCount: number;
  readonly items: readonly MyWorkItemModel[];
}

export interface InAppNotificationModel {
  readonly id: string;
  readonly projectId: string;
  readonly category: string;
  readonly title: string;
  readonly body: string;
  readonly targetType: "DailyReport" | "ManagementAction";
  readonly targetId: string;
  readonly occurredAt: string;
  readonly readAt: string | null;
  readonly acknowledgedAt: string | null;
  readonly revision: number;
}

interface ApiIdentity {
  readonly tenantId: string;
  readonly userId: string;
}

export async function getMyWork(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
): Promise<MyWorkModel> {
  const response = await fetch(`${base(apiBaseUrl, projectId)}/my-work`, {
    headers: identityHeaders(identity),
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<MyWorkModel>;
}

export async function listNotifications(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
): Promise<readonly InAppNotificationModel[]> {
  const response = await fetch(`${base(apiBaseUrl, projectId)}/notifications`, {
    headers: identityHeaders(identity),
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<readonly InAppNotificationModel[]>;
}

export async function changeNotificationReceipt(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  notificationId: string,
  baseRevision: number,
  action: "read" | "acknowledge",
): Promise<InAppNotificationModel> {
  const response = await fetch(
    `${base(apiBaseUrl, projectId)}/notifications/${notificationId}/${action}`,
    {
      method: "POST",
      headers: {
        ...identityHeaders(identity),
        "Content-Type": "application/json",
        "Idempotency-Key": crypto.randomUUID(),
      },
      body: JSON.stringify({ baseRevision }),
    },
  );
  await ensureApiSuccess(response);
  return response.json() as Promise<InAppNotificationModel>;
}

function identityHeaders(identity: ApiIdentity): Record<string, string> {
  return { "X-Tenant-Id": identity.tenantId, "X-User-Id": identity.userId };
}

function base(apiBaseUrl: string, projectId: string): string {
  return `${apiBaseUrl.replace(/\/$/, "")}/api/v1/projects/${projectId}`;
}

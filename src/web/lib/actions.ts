import { ensureApiSuccess } from "./localization.ts";

export type ActionPriority = "Low" | "Medium" | "High" | "Critical";
export type ManagementActionStatus = "Open" | "InProgress" | "Blocked" | "Done" | "Cancelled";

export interface ManagementActionModel {
  readonly id: string;
  readonly projectId: string;
  readonly sourceFactId: string;
  readonly title: string;
  readonly description: string | null;
  readonly assigneeUserId: string;
  readonly assigneeDisplayName: string;
  readonly dueDate: string;
  readonly priority: ActionPriority;
  readonly status: ManagementActionStatus;
  readonly createdAt: string;
  readonly lastChangedAt: string | null;
  readonly completedAt: string | null;
  readonly revision: number;
}

export interface CreateActionInput {
  readonly assigneeUserId: string;
  readonly dueDate: string;
  readonly priority: ActionPriority;
  readonly title?: string | null;
  readonly description?: string | null;
}

interface ApiIdentity {
  readonly tenantId: string;
  readonly userId: string;
}

export async function listActions(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
): Promise<readonly ManagementActionModel[]> {
  const response = await fetch(`${normalize(apiBaseUrl)}/api/v1/projects/${projectId}/actions`, {
    headers: identityHeaders(identity),
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<readonly ManagementActionModel[]>;
}

export async function createActionFromAttention(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  sourceFactId: string,
  input: CreateActionInput,
): Promise<ManagementActionModel> {
  const response = await fetch(
    `${normalize(apiBaseUrl)}/api/v1/projects/${projectId}/attention/${sourceFactId}/actions`,
    {
      method: "POST",
      headers: commandHeaders(identity),
      body: JSON.stringify({ clientGeneratedId: crypto.randomUUID(), ...input }),
    },
  );
  await ensureApiSuccess(response);
  return response.json() as Promise<ManagementActionModel>;
}

export async function dismissAttention(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  sourceFactId: string,
  reason: string,
): Promise<void> {
  const response = await fetch(
    `${normalize(apiBaseUrl)}/api/v1/projects/${projectId}/attention/${sourceFactId}/dismiss`,
    {
      method: "POST",
      headers: commandHeaders(identity),
      body: JSON.stringify({ reason }),
    },
  );
  await ensureApiSuccess(response);
}

export async function transitionAction(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  actionId: string,
  baseRevision: number,
  targetStatus: ManagementActionStatus,
): Promise<ManagementActionModel> {
  const response = await fetch(
    `${normalize(apiBaseUrl)}/api/v1/projects/${projectId}/actions/${actionId}/transition`,
    {
      method: "POST",
      headers: commandHeaders(identity),
      body: JSON.stringify({ baseRevision, targetStatus }),
    },
  );
  await ensureApiSuccess(response);
  return response.json() as Promise<ManagementActionModel>;
}

function commandHeaders(identity: ApiIdentity): Record<string, string> {
  return {
    ...identityHeaders(identity),
    "Content-Type": "application/json",
    "Idempotency-Key": crypto.randomUUID(),
  };
}

function identityHeaders(identity: ApiIdentity): Record<string, string> {
  return { "X-Tenant-Id": identity.tenantId, "X-User-Id": identity.userId };
}

function normalize(apiBaseUrl: string): string {
  return apiBaseUrl.replace(/\/$/, "");
}

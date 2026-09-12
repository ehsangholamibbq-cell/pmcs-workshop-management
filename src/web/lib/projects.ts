import { ensureApiSuccess } from "./localization.ts";

export type ProjectCalendarMode = "NotConfigured" | "WorkingWeek";
export type PlanningMode = "None" | "SimpleWorkList" | "Milestones" | "WbsBaseline" | "ExternalSchedule";
export type Weekday = "Sunday" | "Monday" | "Tuesday" | "Wednesday" | "Thursday" | "Friday" | "Saturday";

export interface ProjectModel {
  readonly id: string;
  readonly code: string;
  readonly name: string;
  readonly planningMode: PlanningMode;
  readonly financeMode: "NotEnabled" | "SetupRequired" | "Active" | "Suspended";
  readonly procurementMode: "NotEnabled" | "SetupRequired" | "Active" | "Suspended";
  readonly baseCurrencyCode: string;
  readonly calendarMode: ProjectCalendarMode;
  readonly workingDays: readonly Weekday[];
  readonly configurationChangedAt: string | null;
  readonly timeZone: string;
  readonly revision: number;
}

interface ApiIdentity {
  readonly tenantId: string;
  readonly userId: string;
}

export async function getProject(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
): Promise<ProjectModel> {
  const response = await fetch(`${normalize(apiBaseUrl)}/api/v1/projects/${projectId}`, {
    headers: identityHeaders(identity),
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<ProjectModel>;
}

export async function listProjects(
  apiBaseUrl: string,
  identity: ApiIdentity,
): Promise<readonly ProjectModel[]> {
  const response = await fetch(`${normalize(apiBaseUrl)}/api/v1/projects`, {
    headers: identityHeaders(identity),
    cache: "no-store",
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<readonly ProjectModel[]>;
}

export async function configureProjectCalendar(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  baseRevision: number,
  mode: ProjectCalendarMode,
  workingDays: readonly Weekday[],
): Promise<ProjectModel> {
  const response = await fetch(`${normalize(apiBaseUrl)}/api/v1/projects/${projectId}/calendar`, {
    method: "PUT",
    headers: {
      ...identityHeaders(identity),
      "Content-Type": "application/json",
      "Idempotency-Key": crypto.randomUUID(),
    },
    body: JSON.stringify({
      baseRevision,
      mode,
      workingDays: mode === "WorkingWeek" ? workingDays : null,
    }),
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<ProjectModel>;
}

export async function configureProjectPlanningMode(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  baseRevision: number,
  mode: PlanningMode,
): Promise<ProjectModel> {
  const response = await fetch(`${normalize(apiBaseUrl)}/api/v1/projects/${projectId}/planning-mode`, {
    method: "PUT",
    headers: {
      ...identityHeaders(identity),
      "Content-Type": "application/json",
      "Idempotency-Key": crypto.randomUUID(),
    },
    body: JSON.stringify({ baseRevision, mode }),
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<ProjectModel>;
}

function identityHeaders(identity: ApiIdentity): Record<string, string> {
  return { "X-Tenant-Id": identity.tenantId, "X-User-Id": identity.userId };
}

function normalize(value: string): string {
  return value.replace(/\/$/, "");
}

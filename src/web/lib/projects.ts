import { ensureApiSuccess } from "./localization.ts";

export type ProjectCalendarMode = "NotConfigured" | "WorkingWeek";
export type PlanningMode = "None" | "SimpleWorkList" | "Milestones" | "WbsBaseline" | "ExternalSchedule";
export type ContractModel = "NotConfigured" | "GeneralContracting" | "ConstructionManagement" | "LaborOnly" | "Hybrid";
export type CapabilityMode = "NotEnabled" | "SetupRequired" | "Active" | "Suspended";
export type ProjectStatus = "Draft" | "Active" | "OnHold" | "Closing" | "Closed";
export type ProjectLocationStatus = "Active" | "Retired";
export type Weekday = "Sunday" | "Monday" | "Tuesday" | "Wednesday" | "Thursday" | "Friday" | "Saturday";

export interface ProjectModel {
  readonly id: string;
  readonly code: string;
  readonly name: string;
  readonly contractModel: ContractModel;
  readonly planningMode: PlanningMode;
  readonly budgetMode: CapabilityMode;
  readonly qualityMode: CapabilityMode;
  readonly hseMode: CapabilityMode;
  readonly financeMode: CapabilityMode;
  readonly procurementMode: CapabilityMode;
  readonly baseCurrencyCode: string;
  readonly calendarMode: ProjectCalendarMode;
  readonly workingDays: readonly Weekday[];
  readonly configurationChangedAt: string | null;
  readonly timeZone: string;
  readonly status: ProjectStatus;
  readonly activatedBy: string | null;
  readonly activatedAt: string | null;
  readonly revision: number;
}

export interface CreateProjectInput {
  readonly code: string;
  readonly name: string;
  readonly contractModel: ContractModel;
  readonly planningMode: PlanningMode;
  readonly budgetMode: CapabilityMode;
  readonly qualityMode: CapabilityMode;
  readonly hseMode: CapabilityMode;
  readonly financeMode: CapabilityMode;
  readonly procurementMode: CapabilityMode;
  readonly baseCurrencyCode: string;
  readonly timeZone: string;
}

export interface ProjectLocationModel {
  readonly id: string;
  readonly projectId: string;
  readonly code: string;
  readonly name: string;
  readonly parentLocationId: string | null;
  readonly status: ProjectLocationStatus;
  readonly changedAt: string;
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

export async function createProject(
  apiBaseUrl: string,
  identity: ApiIdentity,
  input: CreateProjectInput,
): Promise<ProjectModel> {
  const response = await fetch(`${normalize(apiBaseUrl)}/api/v1/projects`, {
    method: "POST",
    headers: {
      ...identityHeaders(identity),
      "Content-Type": "application/json",
      "Idempotency-Key": crypto.randomUUID(),
    },
    body: JSON.stringify(input),
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<ProjectModel>;
}

export async function activateProject(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  baseRevision: number,
): Promise<ProjectModel> {
  const response = await fetch(`${normalize(apiBaseUrl)}/api/v1/projects/${projectId}/activate`, {
    method: "POST",
    headers: {
      ...identityHeaders(identity),
      "Content-Type": "application/json",
      "Idempotency-Key": crypto.randomUUID(),
    },
    body: JSON.stringify({ baseRevision }),
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<ProjectModel>;
}

export async function listProjectLocations(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
): Promise<readonly ProjectLocationModel[]> {
  const response = await fetch(`${normalize(apiBaseUrl)}/api/v1/projects/${projectId}/locations`, {
    headers: identityHeaders(identity),
    cache: "no-store",
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<readonly ProjectLocationModel[]>;
}

export async function createProjectLocation(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  input: { readonly code: string; readonly name: string; readonly parentLocationId: string },
): Promise<ProjectLocationModel> {
  const response = await fetch(`${normalize(apiBaseUrl)}/api/v1/projects/${projectId}/locations`, {
    method: "POST",
    headers: {
      ...identityHeaders(identity),
      "Content-Type": "application/json",
      "Idempotency-Key": crypto.randomUUID(),
    },
    body: JSON.stringify(input),
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<ProjectLocationModel>;
}

export async function retireProjectLocation(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  location: Pick<ProjectLocationModel, "id" | "revision">,
): Promise<ProjectLocationModel> {
  const response = await fetch(
    `${normalize(apiBaseUrl)}/api/v1/projects/${projectId}/locations/${location.id}/retire`,
    {
      method: "POST",
      headers: {
        ...identityHeaders(identity),
        "Content-Type": "application/json",
        "Idempotency-Key": crypto.randomUUID(),
      },
      body: JSON.stringify({ baseRevision: location.revision }),
    },
  );
  await ensureApiSuccess(response);
  return response.json() as Promise<ProjectLocationModel>;
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

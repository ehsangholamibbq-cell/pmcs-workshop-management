import { ensureApiSuccess } from "./localization.ts";

export type ProjectCalendarMode = "NotConfigured" | "WorkingWeek";
export type PlanningMode = "None" | "SimpleWorkList" | "Milestones" | "WbsBaseline" | "ExternalSchedule";
export type ContractModel = "NotConfigured" | "GeneralContracting" | "ConstructionManagement" | "LaborOnly" | "Hybrid";
export type CapabilityMode = "NotEnabled" | "SetupRequired" | "Active" | "Suspended";
export type ProjectStatus = "Draft" | "Active" | "OnHold" | "Closing" | "Closed";
export type ProjectLocationStatus = "Active" | "Retired";
export type ProjectType = "NotConfigured" | "Building" | "Industrial" | "Infrastructure" | "Renovation" | "Landscaping" | "Mixed";
export type ProjectExecutionPhase = "NotConfigured" | "PreConstruction" | "ActiveExecution" | "OnHold" | "Closing";
export type ProjectUnitSystem = "NotConfigured" | "Metric";
export type ReportingFrequency = "NotConfigured" | "Daily" | "WorkingDays" | "Weekly";
export type DailyReportWorkflow = "NotConfigured" | "OneStepApproval";
export type ProjectReadinessStatus = "Passed" | "Warning" | "Blocked";
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
  readonly projectType: ProjectType;
  readonly executionPhase: ProjectExecutionPhase;
  readonly countryCode: string;
  readonly region: string;
  readonly startDate: string | null;
  readonly plannedFinishDate: string | null;
  readonly shortDescription: string;
  readonly unitSystem: ProjectUnitSystem;
  readonly dailyCutoffLocalTime: string | null;
  readonly reportingFrequency: ReportingFrequency;
  readonly dailyReportWorkflow: DailyReportWorkflow;
  readonly offlinePolicyAccepted: boolean;
  readonly configurationVersion: number;
  readonly activatedConfigurationVersion: number | null;
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
  readonly projectType: ProjectType;
  readonly executionPhase: ProjectExecutionPhase;
  readonly countryCode: string;
  readonly region: string;
  readonly startDate: string;
  readonly plannedFinishDate: string;
  readonly shortDescription: string;
  readonly unitSystem: ProjectUnitSystem;
  readonly dailyCutoffLocalTime: string;
  readonly reportingFrequency: ReportingFrequency;
  readonly dailyReportWorkflow: DailyReportWorkflow;
  readonly offlinePolicyAccepted: boolean;
  readonly calendarMode: ProjectCalendarMode;
  readonly workingDays: readonly Weekday[];
}

export interface ProjectReadinessItem {
  readonly code: string;
  readonly title: string;
  readonly status: ProjectReadinessStatus;
  readonly detail: string;
}

export interface ProjectReadinessModel {
  readonly projectId: string;
  readonly configurationVersion: number;
  readonly isReady: boolean;
  readonly completionPercent: number;
  readonly items: readonly ProjectReadinessItem[];
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
    body: JSON.stringify({
      ...input,
      startDate: input.startDate || null,
      plannedFinishDate: input.plannedFinishDate || null,
      dailyCutoffLocalTime: normalizeTime(input.dailyCutoffLocalTime),
    }),
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<ProjectModel>;
}

export async function configureProjectSetup(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  baseRevision: number,
  input: CreateProjectInput,
  reason: string | null = null,
): Promise<ProjectModel> {
  const response = await fetch(`${normalize(apiBaseUrl)}/api/v1/projects/${projectId}/setup`, {
    method: "PUT",
    headers: {
      ...identityHeaders(identity),
      "Content-Type": "application/json",
      "Idempotency-Key": crypto.randomUUID(),
    },
    body: JSON.stringify({
      baseRevision,
      contractModel: input.contractModel,
      planningMode: input.planningMode,
      budgetMode: input.budgetMode,
      qualityMode: input.qualityMode,
      hseMode: input.hseMode,
      financeMode: input.financeMode,
      procurementMode: input.procurementMode,
      calendarMode: input.calendarMode,
      workingDays: input.workingDays,
      projectType: input.projectType,
      executionPhase: input.executionPhase,
      countryCode: input.countryCode,
      region: input.region,
      startDate: input.startDate || null,
      plannedFinishDate: input.plannedFinishDate || null,
      shortDescription: input.shortDescription,
      timeZone: input.timeZone,
      baseCurrencyCode: input.baseCurrencyCode,
      unitSystem: input.unitSystem,
      dailyCutoffLocalTime: normalizeTime(input.dailyCutoffLocalTime),
      reportingFrequency: input.reportingFrequency,
      dailyReportWorkflow: input.dailyReportWorkflow,
      offlinePolicyAccepted: input.offlinePolicyAccepted,
      reason,
    }),
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

export async function getProjectReadiness(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
): Promise<ProjectReadinessModel> {
  const response = await fetch(`${normalize(apiBaseUrl)}/api/v1/projects/${projectId}/readiness`, {
    headers: identityHeaders(identity),
    cache: "no-store",
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<ProjectReadinessModel>;
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

function normalizeTime(value: string): string | null {
  return /^\d{2}:\d{2}$/.test(value) ? `${value}:00` : value || null;
}

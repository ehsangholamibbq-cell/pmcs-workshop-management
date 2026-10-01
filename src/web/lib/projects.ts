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
export type ProjectBootstrapCategory =
  | "BaseSettings"
  | "Calendar"
  | "Locations"
  | "RoleTemplates"
  | "WorkflowTemplates"
  | "FormTemplates"
  | "ReportTemplates"
  | "Lookups"
  | "Members"
  | "NotificationDefaults"
  | "GroupDefaults";
export type ProjectBootstrapDisposition = "Added" | "Skipped" | "Conflict" | "Blocked";
export type ProjectBootstrapStatus = "Draft" | "PreviewReady" | "Completed" | "Activated";
export type ProjectBootstrapConflictPolicy = "FailOnConflict" | "SkipConflicts";

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

export interface ProjectBootstrapTargetInput {
  readonly code: string;
  readonly name: string;
  readonly projectType: ProjectType;
  readonly executionPhase: ProjectExecutionPhase;
  readonly countryCode: string;
  readonly region: string;
  readonly startDate: string;
  readonly plannedFinishDate: string;
  readonly shortDescription: string;
  readonly timeZone: string;
  readonly baseCurrencyCode: string;
  readonly unitSystem: ProjectUnitSystem;
  readonly offlinePolicyAccepted: boolean;
}

export interface ProjectBootstrapMemberSelectionInput {
  readonly userId: string;
  readonly roleCode: string;
  readonly accessScope: "Project";
}

export interface CreateProjectBootstrapInput {
  readonly sourceProjectId: string;
  readonly target: ProjectBootstrapTargetInput;
  readonly categories: readonly ProjectBootstrapCategory[];
  readonly members: readonly ProjectBootstrapMemberSelectionInput[];
  readonly conflictPolicy: ProjectBootstrapConflictPolicy;
}

export interface ProjectBootstrapContributorModel {
  readonly contributorId: string;
  readonly schemaVersion: string;
  readonly category: ProjectBootstrapCategory;
  readonly permission: string;
  readonly order: number;
  readonly dependencies: readonly string[];
  readonly allowlist: readonly string[];
  readonly conflictPolicy: string;
  readonly failSafeBehavior: string;
  readonly postValidation: string;
}

export interface ProjectBootstrapItemModel {
  readonly contributorId: string;
  readonly category: ProjectBootstrapCategory;
  readonly disposition: ProjectBootstrapDisposition;
  readonly code: string;
  readonly title: string;
  readonly detail: string;
  readonly sourceReference: string | null;
  readonly targetReference: string | null;
}

export interface ProjectBootstrapSummaryModel {
  readonly added: number;
  readonly skipped: number;
  readonly conflicts: number;
  readonly blocked: number;
}

export interface ProjectBootstrapPreviewModel {
  readonly planId: string;
  readonly sourceProjectId: string;
  readonly sourceProjectCode: string;
  readonly targetProjectId: string;
  readonly targetProject: ProjectModel;
  readonly status: ProjectBootstrapStatus;
  readonly conflictPolicy: ProjectBootstrapConflictPolicy;
  readonly selectedCategories: readonly ProjectBootstrapCategory[];
  readonly contributorCatalogVersion: string;
  readonly previewDigest: string;
  readonly previewedAt: string;
  readonly previewExpiresAt: string;
  readonly contributors: readonly ProjectBootstrapContributorModel[];
  readonly items: readonly ProjectBootstrapItemModel[];
  readonly summary: ProjectBootstrapSummaryModel;
  readonly alwaysExcluded: readonly string[];
  readonly planRevision: number;
}

export interface ProjectBootstrapValidationModel {
  readonly code: string;
  readonly passed: boolean;
  readonly detail: string;
}

export interface ProjectBootstrapResultModel {
  readonly planId: string;
  readonly sourceProjectId: string;
  readonly targetProjectId: string;
  readonly targetProject: ProjectModel;
  readonly status: ProjectBootstrapStatus;
  readonly contributorCatalogVersion: string;
  readonly previewDigest: string;
  readonly items: readonly ProjectBootstrapItemModel[];
  readonly summary: ProjectBootstrapSummaryModel;
  readonly validation: readonly ProjectBootstrapValidationModel[];
  readonly executedAt: string;
  readonly planRevision: number;
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

export async function createProjectBootstrap(
  apiBaseUrl: string,
  identity: ApiIdentity,
  input: CreateProjectBootstrapInput,
): Promise<ProjectBootstrapPreviewModel> {
  return bootstrapMutation<ProjectBootstrapPreviewModel>(
    `${normalize(apiBaseUrl)}/api/v1/project-bootstraps`,
    identity,
    {
      ...input,
      target: {
        ...input.target,
        startDate: input.target.startDate || null,
        plannedFinishDate: input.target.plannedFinishDate || null,
      },
    },
  );
}

export async function getProjectBootstrap(
  apiBaseUrl: string,
  identity: ApiIdentity,
  planId: string,
): Promise<ProjectBootstrapPreviewModel> {
  const response = await fetch(`${normalize(apiBaseUrl)}/api/v1/project-bootstraps/${planId}`, {
    headers: identityHeaders(identity),
    cache: "no-store",
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<ProjectBootstrapPreviewModel>;
}

export async function refreshProjectBootstrapPreview(
  apiBaseUrl: string,
  identity: ApiIdentity,
  planId: string,
  baseRevision: number,
): Promise<ProjectBootstrapPreviewModel> {
  return bootstrapMutation<ProjectBootstrapPreviewModel>(
    `${normalize(apiBaseUrl)}/api/v1/project-bootstraps/${planId}/preview`,
    identity,
    { baseRevision },
  );
}

export async function executeProjectBootstrap(
  apiBaseUrl: string,
  identity: ApiIdentity,
  preview: Pick<ProjectBootstrapPreviewModel, "planId" | "planRevision" | "previewDigest">,
): Promise<ProjectBootstrapResultModel> {
  return bootstrapMutation<ProjectBootstrapResultModel>(
    `${normalize(apiBaseUrl)}/api/v1/project-bootstraps/${preview.planId}/execute`,
    identity,
    { baseRevision: preview.planRevision, previewDigest: preview.previewDigest },
  );
}

export async function getProjectBootstrapResult(
  apiBaseUrl: string,
  identity: ApiIdentity,
  planId: string,
): Promise<ProjectBootstrapResultModel> {
  const response = await fetch(`${normalize(apiBaseUrl)}/api/v1/project-bootstraps/${planId}/result`, {
    headers: identityHeaders(identity),
    cache: "no-store",
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<ProjectBootstrapResultModel>;
}

export async function activateProjectBootstrap(
  apiBaseUrl: string,
  identity: ApiIdentity,
  result: Pick<ProjectBootstrapResultModel, "planId" | "planRevision" | "targetProject">,
): Promise<ProjectBootstrapResultModel> {
  return bootstrapMutation<ProjectBootstrapResultModel>(
    `${normalize(apiBaseUrl)}/api/v1/project-bootstraps/${result.planId}/activate`,
    identity,
    { baseRevision: result.planRevision, targetBaseRevision: result.targetProject.revision },
  );
}

async function bootstrapMutation<T>(
  url: string,
  identity: ApiIdentity,
  body: unknown,
): Promise<T> {
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
  return response.json() as Promise<T>;
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

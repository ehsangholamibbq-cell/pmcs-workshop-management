import { ensureApiSuccess } from "./localization.ts";

export interface QualitySafetyIdentity { readonly tenantId: string; readonly userId: string }
export type QualityOperatingMode = "Disabled" | "DefectPunchOnly" | "InspectionAndNcrLite" | "FullV1";
export type HseOperatingMode = "Disabled" | "ObservationIntakeOnly" | "HseLite" | "FullV1";
export type ControlArea = "Quality" | "Hse";
export type IntakeKind = "QualityObservation" | "Defect" | "UnsafeCondition" | "UnsafeAct" | "NearMiss" | "IncidentIntake" | "EnvironmentalObservation" | "SafeObservation" | "PositiveIntervention";
export type InitialSeverity = "Unassessed" | "Low" | "Medium" | "High" | "Critical";
export type DataClassification = "GeneralProject" | "RestrictedQuality" | "ConfidentialHse" | "PersonalMedical" | "LegalInvestigation";
export type IntakeStatus = "Captured" | "UnderTriage" | "Converted" | "RetainedAsGeneralIssue" | "Dismissed";
export type InspectionReadiness = "NotAssessed" | "Ready" | "NotReady";
export type InspectionResult = "Pass" | "PassWithObservation" | "Fail" | "NotReadyOrNotInspected" | "HoldOrDeferred";
export type NcrStatus = "Draft" | "Issued" | "Containment" | "InvestigationDisposition" | "ActionImplementation" | "ReinspectionVerification" | "Closed" | "Reopened";
export type NcrDisposition = "NotDecided" | "Rework" | "Repair" | "Replace" | "UseAsIsWithConcession" | "RejectOrRemove" | "FurtherEvaluation";
export type RootCauseStatus = "NotStarted" | "Proposed" | "UnderReview" | "Confirmed" | "Disputed";
export type DefectStatus = "Open" | "Assigned" | "Rectified" | "ReadyForVerification" | "Accepted" | "Rejected" | "Closed" | "Reopened";
export type IncidentStatus = "Reported" | "Triage" | "Contained" | "UnderInvestigation" | "ActionsOpen" | "FinalReview" | "Closed" | "Reopened";
export type CorrectiveActionStatus = "Open" | "InProgress" | "Completed" | "ReadyForVerification" | "Verified" | "Closed" | "Reopened" | "Cancelled";
export type PermitStatus = "Draft" | "Submitted" | "Approved" | "Active" | "Suspended" | "Closed" | "Cancelled";

export interface QualitySafetyConfigurationModel {
  readonly id: string; readonly qualityMode: QualityOperatingMode; readonly hseMode: HseOperatingMode;
  readonly qualityOwnerUserId: string | null; readonly hseOwnerUserId: string | null;
  readonly qualityMatrixVersionId: string | null; readonly hseMatrixVersionId: string | null;
  readonly workflowAndSlaDefined: boolean; readonly templatesDefined: boolean;
  readonly evidenceAndClosureRulesDefined: boolean; readonly qualityReady: boolean; readonly hseReady: boolean;
  readonly changedAt: string; readonly revision: number;
}
export interface RiskMatrixModel { readonly id: string; readonly area: ControlArea; readonly version: number; readonly title: string; readonly definitionJson: string; readonly effectiveFrom: string; readonly createdAt: string; readonly revision: number }
export interface IntakeModel { readonly id: string; readonly number: string; readonly kind: IntakeKind; readonly observedAt: string; readonly location: string; readonly facts: string; readonly initialSeverity: InitialSeverity; readonly immediateAction: string | null; readonly evidenceReferences: readonly string[]; readonly classification: DataClassification; readonly status: IntakeStatus; readonly conversionType: string; readonly convertedRecordId: string | null; readonly triageNote: string | null; readonly createdAt: string; readonly revision: number }
export interface InspectionModel { readonly id: string; readonly number: string; readonly sourceIntakeId: string | null; readonly inspectionTestPlanVersionId: string | null; readonly inspectionType: string; readonly location: string; readonly acceptanceCriteria: string; readonly checklistTemplateReference: string | null; readonly checklistTemplateVersion: number | null; readonly requestedFor: string; readonly readiness: InspectionReadiness; readonly readinessNote: string | null; readonly status: string; readonly result: InspectionResult | null; readonly resultNote: string | null; readonly evidenceReferences: readonly string[]; readonly inspectedAt: string | null; readonly revision: number }
export interface NcrModel { readonly id: string; readonly number: string; readonly title: string; readonly nonConformity: string; readonly status: NcrStatus; readonly disposition: string; readonly vendorPartyId: string | null; readonly lotReference: string | null; readonly createdAt: string; readonly closedAt: string | null; readonly revision: number }
export interface DefectModel { readonly id: string; readonly number: string; readonly title: string; readonly location: string; readonly assigneeUserId: string | null; readonly dueDate: string | null; readonly status: DefectStatus; readonly createdAt: string; readonly closedAt: string | null; readonly revision: number }
export interface IncidentModel { readonly id: string; readonly number: string; readonly occurredAt: string; readonly location: string; readonly facts: string; readonly preliminarySeverity: InitialSeverity; readonly finalSeverity: InitialSeverity | null; readonly classification: DataClassification; readonly status: IncidentStatus; readonly createdAt: string; readonly closedAt: string | null; readonly revision: number }
export interface CorrectiveActionModel { readonly id: string; readonly number: string; readonly sourceArea: ControlArea; readonly sourceRecordType: string; readonly sourceRecordId: string; readonly kind: string; readonly title: string; readonly ownerUserId: string; readonly responsibleParty: string; readonly dueDate: string; readonly extendedDueDate: string | null; readonly successCriteria: string; readonly status: CorrectiveActionStatus; readonly verifiedAt: string | null; readonly closedAt: string | null; readonly revision: number }
export interface PermitModel { readonly id: string; readonly number: string; readonly workDescription: string; readonly location: string; readonly validFrom: string; readonly validTo: string; readonly status: PermitStatus; readonly approvedAt: string | null; readonly closedAt: string | null; readonly revision: number }
export interface ToolboxTalkModel { readonly id: string; readonly number: string; readonly topic: string; readonly heldAt: string; readonly location: string; readonly createdAt: string; readonly revision: number }
export interface InspectionTestPlanModel { readonly id: string; readonly code: string; readonly version: number; readonly title: string; readonly acceptanceCriteria: string; readonly inspectorRole: string; readonly pointType: "Hold" | "Witness" | "Review"; readonly effectiveFrom: string; readonly revision: number }
export interface ChecklistTemplateModel { readonly id: string; readonly code: string; readonly version: number; readonly title: string; readonly effectiveFrom: string; readonly revision: number }
export interface QualityTestModel { readonly id: string; readonly number: string; readonly inspectionId: string | null; readonly testType: string; readonly testedAt: string; readonly sampleReference: string; readonly result: "Pass" | "Fail" | "Inconclusive"; readonly resultDetails: string; readonly createdAt: string; readonly revision: number }
export interface CompetencyModel { readonly id: string; readonly personReference: string; readonly inductionDate: string; readonly validUntil: string | null; readonly classification: DataClassification; readonly createdAt: string; readonly revision: number }
export interface ExposureHoursModel { readonly id: string; readonly periodStart: string; readonly periodEnd: string; readonly hours: number; readonly sourceReference: string; readonly approvedAt: string; readonly revision: number }
export interface QualitySafetyStateModel {
  readonly qualityState: "Hidden" | "NotEnabled" | "Suspended" | "SetupRequired" | "NoData" | "Available";
  readonly hseState: "Hidden" | "NotEnabled" | "Suspended" | "SetupRequired" | "NoData" | "Available";
  readonly configuration: QualitySafetyConfigurationModel | null; readonly matrices: readonly RiskMatrixModel[];
  readonly intakes: readonly IntakeModel[]; readonly inspections: readonly InspectionModel[];
  readonly nonConformances: readonly NcrModel[]; readonly defects: readonly DefectModel[];
  readonly incidents: readonly IncidentModel[]; readonly correctiveActions: readonly CorrectiveActionModel[];
  readonly permits: readonly PermitModel[]; readonly toolboxTalks: readonly ToolboxTalkModel[];
  readonly inspectionTestPlans: readonly InspectionTestPlanModel[]; readonly checklistTemplates: readonly ChecklistTemplateModel[];
  readonly testRecords: readonly QualityTestModel[]; readonly competencyRecords: readonly CompetencyModel[];
  readonly exposureHours: readonly ExposureHoursModel[];
  readonly openQualityCount: number; readonly openHseCount: number | null; readonly overdueCorrectiveActionCount: number;
  readonly incidentRatesAvailable: boolean; readonly incidentRateUnavailableReason: string | null;
  readonly reportedIncidentFrequencyPerTwoHundredThousandHours: number | null;
}

export const getQualitySafetyState = (baseUrl: string, identity: QualitySafetyIdentity, projectId: string) =>
  request<QualitySafetyStateModel>(path(baseUrl, projectId, "/state"), identity);
export const createRiskMatrix = (baseUrl: string, identity: QualitySafetyIdentity, projectId: string, input: { area: ControlArea; version: number; title: string; definitionJson: string; effectiveFrom: string }) =>
  command<RiskMatrixModel>(path(baseUrl, projectId, "/matrices"), identity, { clientGeneratedId: crypto.randomUUID(), ...input });
export const configureQualitySafety = (baseUrl: string, identity: QualitySafetyIdentity, projectId: string, input: Omit<QualitySafetyConfigurationModel, "id" | "qualityReady" | "hseReady" | "changedAt">) =>
  command<QualitySafetyConfigurationModel>(path(baseUrl, projectId, "/configuration"), identity, { ...input, baseRevision: input.revision || null }, "PUT");
export const captureQualitySafetyIntake = (baseUrl: string, identity: QualitySafetyIdentity, projectId: string, input: { kind: IntakeKind; observedAt: string; location: string; facts: string; initialSeverity: InitialSeverity; immediateAction?: string; evidenceReferences: readonly string[]; classification: DataClassification }) =>
  command<IntakeModel>(path(baseUrl, projectId, "/intakes"), identity, { clientGeneratedId: crypto.randomUUID(), ...input, immediateAction: optional(input.immediateAction) });
export const beginIntakeTriage = (baseUrl: string, identity: QualitySafetyIdentity, projectId: string, intake: IntakeModel) =>
  command<IntakeModel>(path(baseUrl, projectId, `/intakes/${intake.id}/triage`), identity, { baseRevision: intake.revision });
export const resolveIntake = (baseUrl: string, identity: QualitySafetyIdentity, projectId: string, intake: IntakeModel, retainAsGeneralIssue: boolean, note: string) =>
  command<IntakeModel>(path(baseUrl, projectId, `/intakes/${intake.id}/resolve`), identity, { baseRevision: intake.revision, retainAsGeneralIssue, note });
export const convertIntake = (baseUrl: string, identity: QualitySafetyIdentity, projectId: string, intake: IntakeModel, conversionType: "QualityObservation" | "Defect" | "NonConformance" | "HseObservation" | "Incident", note: string, title?: string, requirement?: string) =>
  command<IntakeModel>(path(baseUrl, projectId, `/intakes/${intake.id}/convert`), identity, { baseRevision: intake.revision, conversionType, clientGeneratedRecordId: crypto.randomUUID(), note, title: optional(title), requirement: optional(requirement) });
export const requestInspection = (baseUrl: string, identity: QualitySafetyIdentity, projectId: string, input: { sourceIntakeId?: string; inspectionTestPlanVersionId?: string; inspectionType: string; location: string; acceptanceCriteria: string; checklistTemplateReference?: string; checklistTemplateVersion?: number; requestedFor: string }) =>
  command<InspectionModel>(path(baseUrl, projectId, "/inspections"), identity, { clientGeneratedId: crypto.randomUUID(), ...input, sourceIntakeId: optional(input.sourceIntakeId), inspectionTestPlanVersionId: optional(input.inspectionTestPlanVersionId), checklistTemplateReference: optional(input.checklistTemplateReference), checklistTemplateVersion: input.checklistTemplateVersion ?? null });
export const recordInspectionReadiness = (baseUrl: string, identity: QualitySafetyIdentity, projectId: string, item: InspectionModel, readiness: Exclude<InspectionReadiness, "NotAssessed">, note?: string) =>
  command<InspectionModel>(path(baseUrl, projectId, `/inspections/${item.id}/readiness`), identity, { baseRevision: item.revision, readiness, note: optional(note) });
export const recordInspectionResult = (baseUrl: string, identity: QualitySafetyIdentity, projectId: string, item: InspectionModel, result: InspectionResult, note: string, evidenceReferences: readonly string[]) =>
  command<InspectionModel>(path(baseUrl, projectId, `/inspections/${item.id}/result`), identity, { baseRevision: item.revision, result, note: optional(note), evidenceReferences });
export const createNcr = (baseUrl: string, identity: QualitySafetyIdentity, projectId: string, input: { sourceIntakeId?: string; inspectionId?: string; goodsReceiptId?: string; purchaseOrderId?: string; vendorPartyId?: string; supplyItemId?: string; lotReference?: string; title: string; requirement: string; nonConformity: string }) =>
  command<NcrModel>(path(baseUrl, projectId, "/ncrs"), identity, nullable({ clientGeneratedId: crypto.randomUUID(), ...input }));
export const transitionNcr = (baseUrl: string, identity: QualitySafetyIdentity, projectId: string, item: NcrModel, input: { targetStatus: NcrStatus; disposition: NcrDisposition; dispositionNote?: string; concessionApprovedBy?: string; rootCauseStatus: RootCauseStatus; rootCause?: string; closureEvidence: readonly string[]; closureWaiverReason?: string }) =>
  command<NcrModel>(path(baseUrl, projectId, `/ncrs/${item.id}/transition`), identity, nullable({ baseRevision: item.revision, ...input }));
export const createDefect = (baseUrl: string, identity: QualitySafetyIdentity, projectId: string, input: { sourceIntakeId?: string; title: string; location: string }) =>
  command<DefectModel>(path(baseUrl, projectId, "/defects"), identity, nullable({ clientGeneratedId: crypto.randomUUID(), ...input }));
export const assignDefect = (baseUrl: string, identity: QualitySafetyIdentity, projectId: string, item: DefectModel, assigneeUserId: string, dueDate: string) =>
  command<DefectModel>(path(baseUrl, projectId, `/defects/${item.id}/assign`), identity, { baseRevision: item.revision, assigneeUserId, dueDate });
export const transitionDefect = (baseUrl: string, identity: QualitySafetyIdentity, projectId: string, item: DefectModel, targetStatus: DefectStatus, evidenceReferences: readonly string[] = []) =>
  command<DefectModel>(path(baseUrl, projectId, `/defects/${item.id}/transition`), identity, { baseRevision: item.revision, targetStatus, evidenceReferences });
export const reportIncident = (baseUrl: string, identity: QualitySafetyIdentity, projectId: string, input: { sourceIntakeId?: string; occurredAt: string; location: string; facts: string; preliminarySeverity: Exclude<InitialSeverity, "Unassessed">; matrixVersionId: string; classification: Exclude<DataClassification, "GeneralProject" | "RestrictedQuality"> }) =>
  command<IncidentModel>(path(baseUrl, projectId, "/incidents"), identity, nullable({ clientGeneratedId: crypto.randomUUID(), ...input }));
export const transitionIncident = (baseUrl: string, identity: QualitySafetyIdentity, projectId: string, item: IncidentModel, input: { targetStatus: IncidentStatus; finalSeverity?: Exclude<InitialSeverity, "Unassessed">; rootCauseStatus: RootCauseStatus; rootCause?: string; closureEvidence: readonly string[] }) =>
  command<IncidentModel>(path(baseUrl, projectId, `/incidents/${item.id}/transition`), identity, nullable({ baseRevision: item.revision, ...input }));
export const createCorrectiveAction = (baseUrl: string, identity: QualitySafetyIdentity, projectId: string, input: { sourceArea: ControlArea; sourceRecordType: string; sourceRecordId: string; kind: "Immediate" | "Corrective" | "Preventive"; title: string; ownerUserId: string; responsibleParty: string; dueDate: string; successCriteria: string }) =>
  command<CorrectiveActionModel>(path(baseUrl, projectId, "/corrective-actions"), identity, { clientGeneratedId: crypto.randomUUID(), ...input });
export const transitionCorrectiveAction = (baseUrl: string, identity: QualitySafetyIdentity, projectId: string, item: CorrectiveActionModel, targetStatus: CorrectiveActionStatus, evidenceReferences: readonly string[] = []) =>
  command<CorrectiveActionModel>(path(baseUrl, projectId, `/corrective-actions/${item.id}/transition`), identity, { baseRevision: item.revision, targetStatus, evidenceReferences });
export const extendCorrectiveAction = (baseUrl: string, identity: QualitySafetyIdentity, projectId: string, item: CorrectiveActionModel, extendedDueDate: string, reason: string) =>
  command<CorrectiveActionModel>(path(baseUrl, projectId, `/corrective-actions/${item.id}/extend`), identity, { baseRevision: item.revision, extendedDueDate, reason });
export const createPermit = (baseUrl: string, identity: QualitySafetyIdentity, projectId: string, input: { workDescription: string; location: string; validFrom: string; validTo: string; hazards: readonly string[]; controls: readonly string[] }) =>
  command<PermitModel>(path(baseUrl, projectId, "/permits"), identity, { clientGeneratedId: crypto.randomUUID(), ...input });
export const transitionPermit = (baseUrl: string, identity: QualitySafetyIdentity, projectId: string, item: PermitModel, targetStatus: PermitStatus) =>
  command<PermitModel>(path(baseUrl, projectId, `/permits/${item.id}/transition`), identity, { baseRevision: item.revision, targetStatus });
export const recordToolboxTalk = (baseUrl: string, identity: QualitySafetyIdentity, projectId: string, input: { topic: string; heldAt: string; location: string; attendees: readonly string[]; evidenceReferences: readonly string[] }) =>
  command<ToolboxTalkModel>(path(baseUrl, projectId, "/toolbox-talks"), identity, { clientGeneratedId: crypto.randomUUID(), ...input });
export const createInspectionTestPlan = (baseUrl: string, identity: QualitySafetyIdentity, projectId: string, input: { code: string; version: number; title: string; stages: readonly string[]; acceptanceCriteria: string; inspectorRole: string; pointType: "Hold" | "Witness" | "Review"; effectiveFrom: string }) =>
  command<InspectionTestPlanModel>(path(baseUrl, projectId, "/inspection-test-plans"), identity, { clientGeneratedId: crypto.randomUUID(), ...input });
export const createChecklistTemplate = (baseUrl: string, identity: QualitySafetyIdentity, projectId: string, input: { code: string; version: number; title: string; items: readonly string[]; effectiveFrom: string }) =>
  command<ChecklistTemplateModel>(path(baseUrl, projectId, "/checklist-templates"), identity, { clientGeneratedId: crypto.randomUUID(), ...input });
export const recordQualityTest = (baseUrl: string, identity: QualitySafetyIdentity, projectId: string, input: { inspectionId?: string; testType: string; testedAt: string; sampleReference: string; acceptanceCriteria: string; result: "Pass" | "Fail" | "Inconclusive"; resultDetails: string; evidenceReferences: readonly string[] }) =>
  command<QualityTestModel>(path(baseUrl, projectId, "/test-records"), identity, nullable({ clientGeneratedId: crypto.randomUUID(), ...input }));
export const recordCompetency = (baseUrl: string, identity: QualitySafetyIdentity, projectId: string, input: { personReference: string; inductionDate: string; validUntil?: string; competencies: readonly string[]; evidenceReferences: readonly string[]; classification: "ConfidentialHse" | "PersonalMedical" | "LegalInvestigation" }) =>
  command<CompetencyModel>(path(baseUrl, projectId, "/competencies"), identity, nullable({ clientGeneratedId: crypto.randomUUID(), ...input }));
export const recordExposureHours = (baseUrl: string, identity: QualitySafetyIdentity, projectId: string, input: { periodStart: string; periodEnd: string; hours: number; sourceReference: string; evidenceReferences: readonly string[] }) =>
  command<ExposureHoursModel>(path(baseUrl, projectId, "/exposure-hours"), identity, { clientGeneratedId: crypto.randomUUID(), ...input });

async function command<T>(url: string, identity: QualitySafetyIdentity, body: unknown, method = "POST"): Promise<T> {
  return request<T>(url, identity, { method, headers: { "Content-Type": "application/json", "Idempotency-Key": crypto.randomUUID() }, body: JSON.stringify(body) });
}
async function request<T>(url: string, identity: QualitySafetyIdentity, init: RequestInit = {}): Promise<T> {
  const response = await fetch(url, { ...init, headers: { "X-Tenant-Id": identity.tenantId, "X-User-Id": identity.userId, ...init.headers } });
  await ensureApiSuccess(response); return response.json() as Promise<T>;
}
function path(baseUrl: string, projectId: string, suffix: string) { return `${baseUrl.replace(/\/$/, "")}/api/v1/projects/${projectId}/quality-safety${suffix}`; }
function optional(value?: string) { return value?.trim() ? value.trim() : null; }
function nullable<T extends Record<string, unknown>>(value: T) { return Object.fromEntries(Object.entries(value).map(([key, item]) => [key, typeof item === "string" && !item.trim() ? null : item ?? null])); }

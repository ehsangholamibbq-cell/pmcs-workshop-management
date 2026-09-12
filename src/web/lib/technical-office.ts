import { ensureApiSuccess } from "./localization.ts";

export interface TechnicalIdentity {
  readonly tenantId: string;
  readonly userId: string;
}

export type TechnicalDocumentType =
  | "Drawing" | "Specification" | "MethodStatement" | "MaterialSubmittal" | "ShopDrawing"
  | "CalculationOrReport" | "MeetingMinute" | "Correspondence" | "Instruction"
  | "MeasurementSheet" | "HandoverOrTestRecord" | "Other";
export type DocumentRevisionPurpose =
  | "WorkInProgress" | "ForReview" | "ForApproval" | "Approved"
  | "ApprovedWithComments" | "ForConstruction" | "AsBuilt" | "Rejected";
export type DocumentRevisionStatus = "Draft" | "Submitted" | "Returned" | "Approved" | "Issued" | "Superseded";
export type TransmittalStatus = "Draft" | "Issued" | "Acknowledged";
export type RfiStatus = "Draft" | "InternalReview" | "Submitted" | "Answered" | "ResponseAccepted" | "ClarificationRequired" | "Closed";
export type RfiResponseClassification = "InformationOnly" | "DesignClarification" | "NewRevisionRequired" | "InstructionPotential" | "ChangePotential";
export type TechnicalSubmittalType =
  | "MaterialOrProductData" | "ShopDrawing" | "MethodStatement" | "SampleOrMockup"
  | "TechnicalCalculation" | "VendorDocument" | "TestOrCertificate" | "AsBuiltOrHandover";
export type SubmittalStatus = "Draft" | "Submitted" | "UnderReview" | "Approved" | "ApprovedAsNoted" | "ReviseAndResubmit" | "Rejected" | "Closed";
export type SubmittalReviewOutcome = "Approved" | "ApprovedAsNoted" | "ReviseAndResubmit" | "Rejected" | "ForInformation";

export interface TechnicalDocumentModel {
  readonly id: string;
  readonly number: string;
  readonly title: string;
  readonly type: TechnicalDocumentType;
  readonly discipline: string;
  readonly originator: string | null;
  readonly contractId: string | null;
  readonly locationReference: string | null;
  readonly workItemReference: string | null;
  readonly wbsReference: string | null;
  readonly confidentiality: string | null;
  readonly currentOfficialRevisionId: string | null;
  readonly createdAt: string;
  readonly revision: number;
}

export interface DocumentRevisionModel {
  readonly id: string;
  readonly documentId: string;
  readonly revisionCode: string;
  readonly revisionDate: string;
  readonly purpose: DocumentRevisionPurpose;
  readonly fileName: string;
  readonly fileReference: string;
  readonly sha256: string;
  readonly supersedesRevisionId: string | null;
  readonly status: DocumentRevisionStatus;
  readonly reviewComment: string | null;
  readonly issuedThroughTransmittalId: string | null;
  readonly issuedAt: string | null;
  readonly supersededAt: string | null;
  readonly revision: number;
}

export interface TransmittalModel {
  readonly id: string;
  readonly number: string;
  readonly sender: string;
  readonly recipients: readonly string[];
  readonly revisionIds: readonly string[];
  readonly purpose: string;
  readonly deliveryChannel: string;
  readonly dueResponseDate: string | null;
  readonly status: TransmittalStatus;
  readonly issuedAt: string | null;
  readonly acknowledgedAt: string | null;
  readonly acknowledgmentReference: string | null;
  readonly revision: number;
}

export interface RfiResponseRecordModel {
  readonly sequence: number;
  readonly source: string;
  readonly text: string;
  readonly respondingParty: string;
  readonly respondedAt: string;
  readonly classification: RfiResponseClassification;
  readonly changePotential: boolean;
  readonly referencedRevisionIds: readonly string[];
  readonly receivedBy: string;
  readonly accepted: boolean | null;
  readonly reviewComment: string | null;
}

export interface RfiModel {
  readonly id: string;
  readonly number: string;
  readonly title: string;
  readonly question: string;
  readonly requestedFrom: string;
  readonly discipline: string;
  readonly contractId: string | null;
  readonly locationReference: string | null;
  readonly workItemReference: string | null;
  readonly wbsReference: string | null;
  readonly sourceIssueId: string | null;
  readonly raisedDate: string;
  readonly requiredByDate: string | null;
  readonly potentialImpact: string | number;
  readonly isBlocking: boolean;
  readonly proposedSolution: string | null;
  readonly evidenceReferences: readonly string[];
  readonly relatedRevisionIds: readonly string[];
  readonly responses: readonly RfiResponseRecordModel[];
  readonly status: RfiStatus;
  readonly submittedAt: string | null;
  readonly closedAt: string | null;
  readonly revision: number;
}

export interface SubmittalModel {
  readonly id: string;
  readonly number: string;
  readonly title: string;
  readonly type: TechnicalSubmittalType;
  readonly discipline: string;
  readonly submitter: string;
  readonly reviewer: string;
  readonly contractId: string | null;
  readonly commitmentId: string | null;
  readonly locationReference: string | null;
  readonly workItemReference: string | null;
  readonly wbsReference: string | null;
  readonly requiredByDate: string | null;
  readonly plannedSubmissionDate: string | null;
  readonly reviewDueDate: string | null;
  readonly resubmissionNumber: number;
  readonly supersedesSubmittalId: string | null;
  readonly revisionIds: readonly string[];
  readonly requiredDeliverableReference: string | null;
  readonly status: SubmittalStatus;
  readonly reviewOutcome: SubmittalReviewOutcome | null;
  readonly reviewComment: string | null;
  readonly closedAt: string | null;
  readonly revision: number;
}

export interface TechnicalOfficeStateModel {
  readonly documents: readonly TechnicalDocumentModel[];
  readonly documentRevisions: readonly DocumentRevisionModel[];
  readonly transmittals: readonly TransmittalModel[];
  readonly rfis: readonly RfiModel[];
  readonly submittals: readonly SubmittalModel[];
  readonly blockingRfiCount: number;
  readonly overdueRfiCount: number;
  readonly overdueSubmittalCount: number;
  readonly supersededRevisionCount: number;
}

export async function getTechnicalOfficeState(
  baseUrl: string, identity: TechnicalIdentity, projectId: string,
): Promise<TechnicalOfficeStateModel> {
  return requestJson(path(baseUrl, projectId, "/state"), identity);
}

export async function createTechnicalDocument(
  baseUrl: string, identity: TechnicalIdentity, projectId: string,
  input: {
    title: string; type: TechnicalDocumentType; discipline: string; originator?: string;
    contractId?: string; locationReference?: string; workItemReference?: string;
    wbsReference?: string; confidentiality?: string;
  },
): Promise<TechnicalDocumentModel> {
  return command(path(baseUrl, projectId, "/documents"), identity, {
    clientGeneratedId: crypto.randomUUID(), ...input,
    originator: emptyToNull(input.originator), contractId: emptyToNull(input.contractId),
    locationReference: emptyToNull(input.locationReference), workItemReference: emptyToNull(input.workItemReference),
    wbsReference: emptyToNull(input.wbsReference), confidentiality: emptyToNull(input.confidentiality),
  });
}

export async function createDocumentRevision(
  baseUrl: string, identity: TechnicalIdentity, projectId: string, documentId: string,
  input: {
    revisionCode: string; revisionDate: string; purpose: DocumentRevisionPurpose;
    fileName: string; fileReference: string; sha256: string; supersedesRevisionId?: string;
  },
): Promise<DocumentRevisionModel> {
  return command(path(baseUrl, projectId, `/documents/${documentId}/revisions`), identity, {
    clientGeneratedId: crypto.randomUUID(), ...input,
    supersedesRevisionId: emptyToNull(input.supersedesRevisionId),
  });
}

export async function transitionDocumentRevision(
  baseUrl: string, identity: TechnicalIdentity, projectId: string,
  item: Pick<DocumentRevisionModel, "id" | "revision">, action: "submit" | "approve" | "return", comment?: string,
): Promise<DocumentRevisionModel> {
  return command(path(baseUrl, projectId, `/document-revisions/${item.id}/${action}`), identity,
    action === "return"
      ? { baseRevision: item.revision, reason: comment?.trim() ?? "" }
      : { baseRevision: item.revision, comment: emptyToNull(comment) });
}

export async function createTransmittal(
  baseUrl: string, identity: TechnicalIdentity, projectId: string,
  input: { sender: string; recipients: readonly string[]; revisionIds: readonly string[]; purpose: string; deliveryChannel: string; dueResponseDate?: string },
): Promise<TransmittalModel> {
  return command(path(baseUrl, projectId, "/transmittals"), identity, {
    clientGeneratedId: crypto.randomUUID(), ...input, dueResponseDate: emptyToNull(input.dueResponseDate),
  });
}

export async function transitionTransmittal(
  baseUrl: string, identity: TechnicalIdentity, projectId: string,
  item: Pick<TransmittalModel, "id" | "revision">, action: "issue" | "acknowledge", reference?: string,
): Promise<TransmittalModel> {
  return command(path(baseUrl, projectId, `/transmittals/${item.id}/${action}`), identity,
    action === "acknowledge" ? { baseRevision: item.revision, reference: reference?.trim() ?? "" } : { baseRevision: item.revision });
}

export async function createRfi(
  baseUrl: string, identity: TechnicalIdentity, projectId: string,
  input: {
    title: string; question: string; requestedFrom: string; discipline: string; raisedDate: string;
    requiredByDate?: string; potentialImpact: number; isBlocking: boolean; proposedSolution?: string;
    evidenceReferences: readonly string[]; relatedRevisionIds?: readonly string[];
    contractId?: string; locationReference?: string; workItemReference?: string; wbsReference?: string; sourceIssueId?: string;
  },
): Promise<RfiModel> {
  return command(path(baseUrl, projectId, "/rfis"), identity, {
    clientGeneratedId: crypto.randomUUID(), ...input,
    requiredByDate: emptyToNull(input.requiredByDate), proposedSolution: emptyToNull(input.proposedSolution),
    contractId: emptyToNull(input.contractId), locationReference: emptyToNull(input.locationReference),
    workItemReference: emptyToNull(input.workItemReference), wbsReference: emptyToNull(input.wbsReference),
    sourceIssueId: emptyToNull(input.sourceIssueId), relatedRevisionIds: input.relatedRevisionIds ?? [],
  });
}

export async function transitionRfi(
  baseUrl: string, identity: TechnicalIdentity, projectId: string,
  item: Pick<RfiModel, "id" | "revision">,
  action: "internal-review" | "issue" | "accept" | "clarification" | "close" | "return",
  comment?: string,
): Promise<RfiModel> {
  const body = action === "clarification" || action === "return"
    ? { baseRevision: item.revision, reason: comment?.trim() ?? "" }
    : action === "accept"
      ? { baseRevision: item.revision, comment: emptyToNull(comment) }
      : { baseRevision: item.revision };
  return command(path(baseUrl, projectId, `/rfis/${item.id}/${action}`), identity, body);
}

export async function recordRfiResponse(
  baseUrl: string, identity: TechnicalIdentity, projectId: string, item: RfiModel,
  input: { responseText: string; respondingParty: string; responseAt: string; classification: RfiResponseClassification; changePotential: boolean; referencedRevisionIds?: readonly string[] },
): Promise<RfiModel> {
  return command(path(baseUrl, projectId, `/rfis/${item.id}/responses`), identity, {
    baseRevision: item.revision, ...input, referencedRevisionIds: input.referencedRevisionIds ?? [],
  });
}

export async function createSubmittal(
  baseUrl: string, identity: TechnicalIdentity, projectId: string,
  input: {
    title: string; type: TechnicalSubmittalType; discipline: string; submitter: string; reviewer: string;
    revisionIds: readonly string[]; requiredByDate?: string; plannedSubmissionDate?: string; reviewDueDate?: string;
    contractId?: string; commitmentId?: string; locationReference?: string; workItemReference?: string;
    wbsReference?: string; requiredDeliverableReference?: string; resubmissionNumber?: number; supersedesSubmittalId?: string;
  },
): Promise<SubmittalModel> {
  return command(path(baseUrl, projectId, "/submittals"), identity, {
    clientGeneratedId: crypto.randomUUID(), ...input,
    contractId: emptyToNull(input.contractId), commitmentId: emptyToNull(input.commitmentId),
    locationReference: emptyToNull(input.locationReference), workItemReference: emptyToNull(input.workItemReference),
    wbsReference: emptyToNull(input.wbsReference), requiredByDate: emptyToNull(input.requiredByDate),
    plannedSubmissionDate: emptyToNull(input.plannedSubmissionDate), reviewDueDate: emptyToNull(input.reviewDueDate),
    requiredDeliverableReference: emptyToNull(input.requiredDeliverableReference),
    resubmissionNumber: input.resubmissionNumber ?? 0,
    supersedesSubmittalId: emptyToNull(input.supersedesSubmittalId),
  });
}

export async function transitionSubmittal(
  baseUrl: string, identity: TechnicalIdentity, projectId: string,
  item: Pick<SubmittalModel, "id" | "revision">, action: "submit" | "begin-review" | "close",
): Promise<SubmittalModel> {
  return command(path(baseUrl, projectId, `/submittals/${item.id}/${action}`), identity, { baseRevision: item.revision });
}

export async function reviewSubmittal(
  baseUrl: string, identity: TechnicalIdentity, projectId: string, item: Pick<SubmittalModel, "id" | "revision">,
  outcome: SubmittalReviewOutcome, comment?: string,
): Promise<SubmittalModel> {
  return command(path(baseUrl, projectId, `/submittals/${item.id}/review`), identity, {
    baseRevision: item.revision, outcome, comment: emptyToNull(comment),
  });
}

async function command<T>(url: string, identity: TechnicalIdentity, body: unknown): Promise<T> {
  return requestJson(url, identity, {
    method: "POST",
    headers: { "Content-Type": "application/json", "Idempotency-Key": crypto.randomUUID() },
    body: JSON.stringify(body),
  });
}

async function requestJson<T>(url: string, identity: TechnicalIdentity, init: RequestInit = {}): Promise<T> {
  const response = await fetch(url, { ...init, headers: { ...identityHeaders(identity), ...init.headers } });
  await ensureApiSuccess(response);
  return response.json() as Promise<T>;
}

function identityHeaders(identity: TechnicalIdentity): Record<string, string> {
  return { "X-Tenant-Id": identity.tenantId, "X-User-Id": identity.userId };
}

function path(baseUrl: string, projectId: string, suffix: string): string {
  return `${baseUrl.replace(/\/$/, "")}/api/v1/projects/${projectId}/technical-office${suffix}`;
}

function emptyToNull(value?: string): string | null {
  return value?.trim() ? value.trim() : null;
}

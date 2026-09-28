import { CollaborationAccessError } from "./collaboration-events.ts";
import { CollaborationRevisionConflict } from "./collaboration-interactions.ts";
import type { ProjectConversationMessage } from "./collaboration-room.ts";
import type { DailyFactKind, DailyImpactLevel } from "./field-facts.ts";
import type { ProjectMessageAttachment } from "./collaboration-attachments.ts";
import type { TechnicalDocumentType } from "./technical-office.ts";

export interface ProjectMessageConversionLineage {
  readonly id: string;
  readonly messageId: string;
  readonly messageRevision: number;
  readonly destinationType: "Action" | "Issue" | "RFI" | "DailyFact" | "Evidence" | "TechnicalDocument";
  readonly destinationId: string;
  readonly destinationReference: string;
  readonly documents: readonly {
    readonly id: string; readonly sha256: string; readonly versionNumber: number;
    readonly fileName: string; readonly contentType: string; readonly sizeBytes: number;
  }[];
  readonly confirmedBy: string;
  readonly confirmedAt: string;
}

const guid = (value: unknown): value is string => typeof value === "string" &&
  /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/iu.test(value);

function validLineage(item: ProjectMessageConversionLineage, messageId: string): boolean {
  return Boolean(item && guid(item.id) && item.messageId?.toLowerCase() === messageId.toLowerCase() &&
    Number.isSafeInteger(item.messageRevision) && item.messageRevision >= 1 &&
    ["Action", "Issue", "RFI", "DailyFact", "Evidence", "TechnicalDocument"].includes(item.destinationType) &&
    guid(item.destinationId) && typeof item.destinationReference === "string" &&
    item.destinationReference.trim() && item.destinationReference.length <= 250 &&
    guid(item.confirmedBy) && typeof item.confirmedAt === "string" &&
    Number.isFinite(Date.parse(item.confirmedAt)) &&
    Array.isArray(item.documents) && item.documents.length <= 10 &&
    item.documents.every((document) => document && guid(document.id) &&
      /^[a-f0-9]{64}$/iu.test(document.sha256) &&
      Number.isSafeInteger(document.versionNumber) && document.versionNumber >= 1 &&
      typeof document.fileName === "string" && Boolean(document.fileName) &&
      document.fileName.length <= 255 && typeof document.contentType === "string" &&
      Boolean(document.contentType) && document.contentType.length <= 150 &&
      Number.isSafeInteger(document.sizeBytes) && document.sizeBytes >= 1));
}

function validSelectedAttachments(messageId: string, attachments: readonly ProjectMessageAttachment[]): boolean {
  return Array.isArray(attachments) && attachments.length <= 10 &&
    new Set(attachments.map((item) => item?.documentId?.toLowerCase())).size === attachments.length &&
    attachments.every((item) => item && item.messageId?.toLowerCase() === messageId.toLowerCase() &&
      guid(item.documentId) && /^[0-9a-f]{64}$/iu.test(item.sha256) &&
      Number.isSafeInteger(item.versionNumber) && item.versionNumber > 0 &&
      Number.isSafeInteger(item.sizeBytes) && item.sizeBytes > 0 && item.sizeBytes <= 25 * 1024 * 1024 &&
      typeof item.originalFileName === "string" && item.originalFileName.length > 0 &&
      typeof item.contentType === "string" && item.contentType.length > 0 &&
      typeof item.releasedAt === "string" && Number.isFinite(Date.parse(item.releasedAt)));
}

function matchingLineageDocuments(documents: ProjectMessageConversionLineage["documents"],
  selected: readonly ProjectMessageAttachment[]): boolean {
  return documents.length === selected.length && selected.every((attachment) => {
    const document = documents.find((item) => item.id.toLowerCase() === attachment.documentId.toLowerCase());
    return document && document.sha256.toLowerCase() === attachment.sha256.toLowerCase() &&
      document.versionNumber === attachment.versionNumber &&
      document.fileName === attachment.originalFileName &&
      document.contentType === attachment.contentType && document.sizeBytes === attachment.sizeBytes;
  });
}

/** Authorized, bounded lineage read; no official record is created here. */
export async function loadProjectMessageConversions(apiBaseUrl: string, projectId: string,
  messageId: string, signal?: AbortSignal): Promise<readonly ProjectMessageConversionLineage[]> {
  if (!guid(projectId) || !guid(messageId)) throw new Error("شناسهٔ تبدیل معتبر نیست.");
  const url = `${apiBaseUrl.replace(/\/$/u, "")}/api/v1/projects/${encodeURIComponent(projectId)}` +
    `/collaboration/messages/${encodeURIComponent(messageId)}/conversions`;
  const response = await fetch(url, { cache: "no-store", signal });
  if ([401, 403, 404].includes(response.status)) throw new CollaborationAccessError(response.status);
  if (!response.ok) throw new Error("دریافت تبدیل‌های رسمی کامل نشد.");
  const entries = await response.json() as ProjectMessageConversionLineage[];
  if (!Array.isArray(entries) || entries.length > 100 ||
      entries.some((item) => !validLineage(item, messageId)) ||
      new Set(entries.map((item) => item.id.toLowerCase())).size !== entries.length) {
    throw new Error("تبار تبدیل‌های رسمی معتبر نیست.");
  }
  return entries;
}

export class CollaborationActionAlreadyExists extends Error {
  constructor() { super("از این پیام قبلاً اقدام رسمی ساخته شده است؛ تبار تبدیل را تازه‌سازی کنید."); }
}

export class CollaborationActionValidationError extends Error {
  constructor() { super("مقصد اقدام، مسئول یا مهلت انتخاب‌شده پذیرفته نشد."); }
}

export interface ProjectActionConversionDetails {
  readonly assigneeUserId: string;
  readonly dueDate: string;
  readonly priority: "Low" | "Medium" | "High" | "Critical";
  readonly title: string;
  readonly description: string;
}

/** Creates one confirmed Action through the owner command; no Chat-to-truth shortcut. */
export async function convertProjectMessageToAction(apiBaseUrl: string, projectId: string,
  message: ProjectConversationMessage, actorUserId: string, details: ProjectActionConversionDetails,
  destinationId: string, idempotencyKey: string,
  selectedAttachments: readonly ProjectMessageAttachment[] = []): Promise<ProjectMessageConversionLineage> {
  const title = details.title.trim();
  const description = details.description.trim();
  if (!guid(projectId) || !guid(message.id) || !guid(actorUserId) ||
      !guid(destinationId) || !guid(idempotencyKey) ||
      message.projectId.toLowerCase() !== projectId.toLowerCase() ||
      !Number.isSafeInteger(message.revision) || message.revision < 1 ||
      message.deletedAt || message.redactedAt ||
      details.assigneeUserId.toLowerCase() !== actorUserId.toLowerCase() ||
      !/^\d{4}-\d{2}-\d{2}$/u.test(details.dueDate) ||
      Number.isNaN(Date.parse(`${details.dueDate}T00:00:00Z`)) ||
      new Date(`${details.dueDate}T00:00:00Z`).toISOString().slice(0, 10) !== details.dueDate ||
      !["Low", "Medium", "High", "Critical"].includes(details.priority) ||
      !title || title.length > 240 || description.length > 2000 ||
      !validSelectedAttachments(message.id, selectedAttachments) ||
      /[\x00-\x08\x0b-\x1f\x7f]/u.test(title + description)) {
    throw new Error("مشخصات تبدیل به اقدام معتبر نیست.");
  }
  const url = `${apiBaseUrl.replace(/\/$/u, "")}/api/v1/projects/${encodeURIComponent(projectId)}` +
    `/collaboration/messages/${encodeURIComponent(message.id)}/conversions`;
  const response = await fetch(url, {
    method: "POST", cache: "no-store",
    headers: { "Content-Type": "application/json", "Idempotency-Key": idempotencyKey },
    body: JSON.stringify({ destinationId, destinationType: "Action", baseRevision: message.revision,
      confirmed: true, details: { assigneeUserId: actorUserId, dueDate: details.dueDate,
        priority: details.priority, title, description: description || null },
      documentIds: selectedAttachments.map((item) => item.documentId) }),
  });
  if ([401, 403, 404].includes(response.status)) throw new CollaborationAccessError(response.status);
  if (response.status === 409) {
    const conflict = await response.json().catch(() => null) as {
      code?: string; currentRevision?: number;
    } | null;
    if (conflict?.code === "collaboration.message.revision.conflict") {
      const revision = conflict.currentRevision;
      throw new CollaborationRevisionConflict(Number.isSafeInteger(revision) && revision! > 0 ? revision! : null);
    }
    if (conflict?.code === "collaboration.conversion.action.already_created" ||
        conflict?.code === "collaboration.conversion.destination_id.reused") {
      throw new CollaborationActionAlreadyExists();
    }
    throw new Error("تعارض در تبدیل رسمی؛ تبار پیام را بازخوانی کنید.");
  }
  if (response.status === 422) throw new CollaborationActionValidationError();
  if (![200, 201].includes(response.status)) throw new Error("ثبت اقدام رسمی کامل نشد.");
  const result = await response.json() as ProjectMessageConversionLineage;
  if (!validLineage(result, message.id) || result.destinationType !== "Action" ||
      result.destinationId.toLowerCase() !== destinationId.toLowerCase() ||
      result.messageRevision !== message.revision ||
      result.confirmedBy.toLowerCase() !== actorUserId.toLowerCase() ||
      !matchingLineageDocuments(result.documents, selectedAttachments)) {
    throw new Error("تأیید تبدیل رسمی معتبر نیست.");
  }
  return result;
}

export class CollaborationIssueAlreadyExists extends Error {
  constructor() { super("از این پیام قبلاً مسئلهٔ رسمی ساخته شده است؛ تبار تبدیل را تازه‌سازی کنید."); }
}

export class CollaborationIssueValidationError extends Error {
  constructor() { super("مشخصات مسئله، مسئول یا مهلت انتخاب‌شده پذیرفته نشد."); }
}

export interface ProjectIssueConversionDetails {
  readonly ownerUserId: string;
  readonly targetResolutionDate: string;
  readonly title: string;
  readonly observedFact: string;
  readonly category: string;
  readonly severity: "Low" | "Medium" | "High" | "Critical";
  readonly urgency: "Routine" | "Soon" | "Immediate";
}

/** Creates one general-project Issue through the governance owner command. */
export async function convertProjectMessageToIssue(apiBaseUrl: string, projectId: string,
  message: ProjectConversationMessage, actorUserId: string, details: ProjectIssueConversionDetails,
  destinationId: string, idempotencyKey: string,
  selectedAttachments: readonly ProjectMessageAttachment[] = []): Promise<ProjectMessageConversionLineage> {
  const title = details.title.trim();
  const observedFact = details.observedFact.trim();
  const category = details.category.trim();
  const date = details.targetResolutionDate;
  if (!guid(projectId) || !guid(message.id) || !guid(actorUserId) ||
      !guid(destinationId) || !guid(idempotencyKey) ||
      message.projectId.toLowerCase() !== projectId.toLowerCase() ||
      !Number.isSafeInteger(message.revision) || message.revision < 1 ||
      message.deletedAt || message.redactedAt ||
      details.ownerUserId.toLowerCase() !== actorUserId.toLowerCase() ||
      !/^\d{4}-\d{2}-\d{2}$/u.test(date) ||
      Number.isNaN(Date.parse(`${date}T00:00:00Z`)) ||
      new Date(`${date}T00:00:00Z`).toISOString().slice(0, 10) !== date ||
      !title || title.length > 240 || !observedFact || observedFact.length > 5000 ||
      !category || category.length > 120 ||
      !["Low", "Medium", "High", "Critical"].includes(details.severity) ||
      !["Routine", "Soon", "Immediate"].includes(details.urgency) ||
      !validSelectedAttachments(message.id, selectedAttachments) ||
      /[\x00-\x08\x0b-\x1f\x7f]/u.test(title + observedFact + category)) {
    throw new Error("مشخصات تبدیل به مسئله معتبر نیست.");
  }
  const url = `${apiBaseUrl.replace(/\/$/u, "")}/api/v1/projects/${encodeURIComponent(projectId)}` +
    `/collaboration/messages/${encodeURIComponent(message.id)}/conversions`;
  const response = await fetch(url, {
    method: "POST", cache: "no-store",
    headers: { "Content-Type": "application/json", "Idempotency-Key": idempotencyKey },
    body: JSON.stringify({ destinationId, destinationType: "Issue", baseRevision: message.revision,
      confirmed: true, details: { ownerUserId: actorUserId, targetResolutionDate: date,
        title, observedFact, category, severity: details.severity, urgency: details.urgency,
        confidentiality: "GeneralProject" },
      documentIds: selectedAttachments.map((item) => item.documentId) }),
  });
  if ([401, 403, 404].includes(response.status)) throw new CollaborationAccessError(response.status);
  if (response.status === 409) {
    const conflict = await response.json().catch(() => null) as {
      code?: string; currentRevision?: number;
    } | null;
    if (conflict?.code === "collaboration.message.revision.conflict") {
      const revision = conflict.currentRevision;
      throw new CollaborationRevisionConflict(Number.isSafeInteger(revision) && revision! > 0 ? revision! : null);
    }
    if (conflict?.code === "collaboration.conversion.issue.already_created" ||
        conflict?.code === "collaboration.conversion.destination_id.reused") {
      throw new CollaborationIssueAlreadyExists();
    }
    throw new Error("تعارض در تبدیل رسمی؛ تبار پیام را بازخوانی کنید.");
  }
  if (response.status === 422) throw new CollaborationIssueValidationError();
  if (![200, 201].includes(response.status)) throw new Error("ثبت مسئلهٔ رسمی کامل نشد.");
  const result = await response.json() as ProjectMessageConversionLineage;
  if (!validLineage(result, message.id) || result.destinationType !== "Issue" ||
      result.destinationId.toLowerCase() !== destinationId.toLowerCase() ||
      result.messageRevision !== message.revision ||
      result.confirmedBy.toLowerCase() !== actorUserId.toLowerCase() ||
      !matchingLineageDocuments(result.documents, selectedAttachments)) {
    throw new Error("تأیید تبدیل مسئلهٔ رسمی معتبر نیست.");
  }
  return result;
}

export class CollaborationRfiAlreadyExists extends Error {
  constructor() { super("از این پیام قبلاً RFI رسمی ساخته شده است؛ تبار تبدیل را تازه‌سازی کنید."); }
}

export class CollaborationRfiValidationError extends Error {
  constructor() { super("مشخصات پرسش فنی، مخاطب یا مهلت انتخاب‌شده پذیرفته نشد."); }
}

export type RfiPotentialImpact = "Time" | "Cost" | "Quality" | "Scope" | "Safety";

export interface ProjectRfiConversionDetails {
  readonly title: string;
  readonly question: string;
  readonly requestedFrom: string;
  readonly discipline: string;
  readonly requiredByDate: string;
  readonly potentialImpacts: readonly RfiPotentialImpact[];
  readonly isBlocking: boolean;
  readonly proposedSolution: string;
}

/** Creates a Draft RFI through Technical Office, leaving later review/issue transitions untouched. */
export async function convertProjectMessageToRfi(apiBaseUrl: string, projectId: string,
  message: ProjectConversationMessage, actorUserId: string, details: ProjectRfiConversionDetails,
  destinationId: string, idempotencyKey: string): Promise<ProjectMessageConversionLineage> {
  const title = details.title.trim();
  const question = details.question.trim();
  const requestedFrom = details.requestedFrom.trim();
  const discipline = details.discipline.trim();
  const proposedSolution = details.proposedSolution.trim();
  const date = details.requiredByDate;
  const impacts = details.potentialImpacts;
  if (!guid(projectId) || !guid(message.id) || !guid(actorUserId) ||
      !guid(destinationId) || !guid(idempotencyKey) ||
      message.projectId.toLowerCase() !== projectId.toLowerCase() ||
      !Number.isSafeInteger(message.revision) || message.revision < 1 ||
      message.deletedAt || message.redactedAt ||
      !title || title.length > 240 || !question || question.length > 6000 ||
      !requestedFrom || requestedFrom.length > 240 ||
      !discipline || discipline.length > 120 || proposedSolution.length > 4000 ||
      (date !== "" && (!/^\d{4}-\d{2}-\d{2}$/u.test(date) ||
        Number.isNaN(Date.parse(`${date}T00:00:00Z`)) ||
        new Date(`${date}T00:00:00Z`).toISOString().slice(0, 10) !== date)) ||
      !Array.isArray(impacts) || impacts.length > 5 ||
      new Set(impacts).size !== impacts.length ||
      impacts.some((item) => !["Time", "Cost", "Quality", "Scope", "Safety"].includes(item)) ||
      typeof details.isBlocking !== "boolean" ||
      /[\x00-\x08\x0b-\x1f\x7f]/u.test(title + question + requestedFrom + discipline + proposedSolution)) {
    throw new Error("مشخصات تبدیل به RFI معتبر نیست.");
  }
  const url = `${apiBaseUrl.replace(/\/$/u, "")}/api/v1/projects/${encodeURIComponent(projectId)}` +
    `/collaboration/messages/${encodeURIComponent(message.id)}/conversions`;
  const response = await fetch(url, {
    method: "POST", cache: "no-store",
    headers: { "Content-Type": "application/json", "Idempotency-Key": idempotencyKey },
    body: JSON.stringify({ destinationId, destinationType: "RFI", baseRevision: message.revision,
      confirmed: true, details: { title, question, requestedFrom, discipline,
        requiredByDate: date || null, potentialImpact: impacts.join(", ") || "None",
        isBlocking: details.isBlocking, proposedSolution: proposedSolution || null }, documentIds: [] }),
  });
  if ([401, 403, 404].includes(response.status)) throw new CollaborationAccessError(response.status);
  if (response.status === 409) {
    const conflict = await response.json().catch(() => null) as {
      code?: string; currentRevision?: number;
    } | null;
    if (conflict?.code === "collaboration.message.revision.conflict") {
      const revision = conflict.currentRevision;
      throw new CollaborationRevisionConflict(Number.isSafeInteger(revision) && revision! > 0 ? revision! : null);
    }
    if (conflict?.code === "collaboration.conversion.rfi.already_created" ||
        conflict?.code === "collaboration.conversion.destination_id.reused") {
      throw new CollaborationRfiAlreadyExists();
    }
    throw new Error("تعارض در تبدیل رسمی؛ تبار پیام را بازخوانی کنید.");
  }
  if (response.status === 422) throw new CollaborationRfiValidationError();
  if (![200, 201].includes(response.status)) throw new Error("ثبت RFI رسمی کامل نشد.");
  const result = await response.json() as ProjectMessageConversionLineage;
  if (!validLineage(result, message.id) || result.destinationType !== "RFI" ||
      result.destinationId.toLowerCase() !== destinationId.toLowerCase() ||
      result.messageRevision !== message.revision ||
      result.confirmedBy.toLowerCase() !== actorUserId.toLowerCase() ||
      result.documents.length !== 0) {
    throw new Error("تأیید تبدیل RFI رسمی معتبر نیست.");
  }
  return result;
}

export class CollaborationDailyFactAlreadyExists extends Error {
  constructor() { super("از این پیام قبلاً واقعیت روزانهٔ رسمی ساخته شده است؛ تبار تبدیل را تازه‌سازی کنید."); }
}

export class CollaborationDailyFactTargetConflict extends Error {
  constructor() { super("نسخه یا وضعیت گزارش روزانه تغییر کرده است؛ گزارش را دوباره بررسی و تأیید کنید."); }
}

export class CollaborationDailyFactValidationError extends Error {
  constructor() { super("گزارش، محل یا مشخصات واقعیت روزانه پذیرفته نشد."); }
}

export interface ProjectDailyFactConversionDetails {
  readonly reportId: string;
  readonly baseReportRevision: number;
  readonly kind: DailyFactKind;
  readonly description: string;
  readonly locationId: string;
  readonly category: string | null;
  readonly quantity: number | null;
  readonly unit: string | null;
  readonly resourceCount: number | null;
  readonly hours: number | null;
  readonly impactLevel: DailyImpactLevel | null;
}

/** Appends a confirmed fact to an existing Draft daily report through Field Operations. */
export async function convertProjectMessageToDailyFact(apiBaseUrl: string, projectId: string,
  message: ProjectConversationMessage, actorUserId: string, details: ProjectDailyFactConversionDetails,
  destinationId: string, idempotencyKey: string): Promise<ProjectMessageConversionLineage> {
  const description = details.description.trim();
  const category = details.category?.trim() || null;
  const unit = details.unit?.trim() || null;
  const quantity = details.quantity;
  const count = details.resourceCount;
  const hours = details.hours;
  if (!guid(projectId) || !guid(message.id) || !guid(actorUserId) ||
      !guid(destinationId) || !guid(idempotencyKey) ||
      message.projectId.toLowerCase() !== projectId.toLowerCase() ||
      !Number.isSafeInteger(message.revision) || message.revision < 1 ||
      message.deletedAt || message.redactedAt ||
      !guid(details.reportId) || !guid(details.locationId) ||
      !Number.isSafeInteger(details.baseReportRevision) || details.baseReportRevision < 1 ||
      !["WorkProgress", "Labor", "Equipment", "Material", "Issue", "Stoppage", "SiteCondition", "Note"].includes(details.kind) ||
      !description || description.length > 1000 || category !== null && category.length > 120 ||
      unit !== null && unit.length > 40 ||
      quantity !== null && (!Number.isFinite(quantity) || quantity < 0) ||
      count !== null && (!Number.isSafeInteger(count) || count <= 0) ||
      hours !== null && (!Number.isFinite(hours) || hours < 0 || hours > 100_000) ||
      quantity !== null && !unit ||
      ["Labor", "Equipment"].includes(details.kind) && (!category || count === null) ||
      details.kind === "Material" && (!category || quantity === null || !unit) ||
      details.kind === "WorkProgress" && !category ||
      details.impactLevel !== null && !["Low", "Medium", "High", "Critical"].includes(details.impactLevel) ||
      /[\x00-\x08\x0b-\x1f\x7f]/u.test(description + (category ?? "") + (unit ?? ""))) {
    throw new Error("مشخصات تبدیل به واقعیت روزانه معتبر نیست.");
  }
  const url = `${apiBaseUrl.replace(/\/$/u, "")}/api/v1/projects/${encodeURIComponent(projectId)}` +
    `/collaboration/messages/${encodeURIComponent(message.id)}/conversions`;
  const response = await fetch(url, {
    method: "POST", cache: "no-store",
    headers: { "Content-Type": "application/json", "Idempotency-Key": idempotencyKey },
    body: JSON.stringify({ destinationId, destinationType: "DailyFact", baseRevision: message.revision,
      confirmed: true, details: { ...details, description, category, unit }, documentIds: [] }),
  });
  if ([401, 403, 404].includes(response.status)) throw new CollaborationAccessError(response.status);
  if (response.status === 409) {
    const conflict = await response.json().catch(() => null) as {
      code?: string; currentRevision?: number;
    } | null;
    if (conflict?.code === "collaboration.message.revision.conflict") {
      const revision = conflict.currentRevision;
      throw new CollaborationRevisionConflict(Number.isSafeInteger(revision) && revision! > 0 ? revision! : null);
    }
    if (conflict?.code === "collaboration.conversion.fact.report.conflict")
      throw new CollaborationDailyFactTargetConflict();
    if (conflict?.code === "collaboration.conversion.fact.already_created" ||
        conflict?.code === "collaboration.conversion.destination_id.reused")
      throw new CollaborationDailyFactAlreadyExists();
    throw new Error("تعارض در تبدیل رسمی؛ تبار پیام را بازخوانی کنید.");
  }
  if (response.status === 422) throw new CollaborationDailyFactValidationError();
  if (![200, 201].includes(response.status)) throw new Error("ثبت واقعیت روزانهٔ رسمی کامل نشد.");
  const result = await response.json() as ProjectMessageConversionLineage;
  if (!validLineage(result, message.id) || result.destinationType !== "DailyFact" ||
      result.destinationId.toLowerCase() !== destinationId.toLowerCase() ||
      result.messageRevision !== message.revision ||
      result.confirmedBy.toLowerCase() !== actorUserId.toLowerCase() ||
      result.documents.length !== 0) {
    throw new Error("تأیید تبدیل واقعیت روزانهٔ رسمی معتبر نیست.");
  }
  return result;
}

export class CollaborationEvidenceSourceAlreadyExists extends Error {
  constructor() { super("از این فایل پیام قبلاً Evidence رسمی ساخته شده است؛ تبار را تازه‌سازی کنید."); }
}

export class CollaborationEvidenceValidationError extends Error {
  constructor() { super("فایل Released یا مقصد گزارش/واقعیت پذیرفته نشد."); }
}

const evidenceMimeTypes = new Set(["image/jpeg", "image/png", "image/webp", "image/heic",
  "image/heif", "application/pdf"]);

/** Converts exactly one attached Released file to official Evidence with preserved hash lineage. */
export async function convertProjectMessageToEvidence(apiBaseUrl: string, projectId: string,
  message: ProjectConversationMessage, actorUserId: string, attachment: ProjectMessageAttachment,
  dailyReportId: string, dailyFactId: string | null,
  destinationId: string, idempotencyKey: string): Promise<ProjectMessageConversionLineage> {
  if (!guid(projectId) || !guid(message.id) || !guid(actorUserId) ||
      !guid(destinationId) || !guid(idempotencyKey) || !guid(dailyReportId) ||
      (dailyFactId !== null && !guid(dailyFactId)) ||
      message.projectId.toLowerCase() !== projectId.toLowerCase() ||
      !Number.isSafeInteger(message.revision) || message.revision < 1 ||
      message.deletedAt || message.redactedAt ||
      attachment.messageId?.toLowerCase() !== message.id.toLowerCase() ||
      !guid(attachment.documentId) ||
      !/^[0-9a-f]{64}$/iu.test(attachment.sha256) ||
      !Number.isSafeInteger(attachment.versionNumber) || attachment.versionNumber < 1 ||
      !Number.isSafeInteger(attachment.sizeBytes) || attachment.sizeBytes < 1 ||
      attachment.sizeBytes > 25 * 1024 * 1024 ||
      !evidenceMimeTypes.has(attachment.contentType) ||
      typeof attachment.releasedAt !== "string" ||
      !Number.isFinite(Date.parse(attachment.releasedAt))) {
    throw new Error("مشخصات تبدیل فایل به Evidence معتبر نیست.");
  }
  const url = `${apiBaseUrl.replace(/\/$/u, "")}/api/v1/projects/${encodeURIComponent(projectId)}` +
    `/collaboration/messages/${encodeURIComponent(message.id)}/conversions`;
  const response = await fetch(url, {
    method: "POST", cache: "no-store",
    headers: { "Content-Type": "application/json", "Idempotency-Key": idempotencyKey },
    body: JSON.stringify({ destinationId, destinationType: "Evidence", baseRevision: message.revision,
      confirmed: true, details: { dailyReportId, dailyFactId }, documentIds: [attachment.documentId] }),
  });
  if ([401, 403, 404].includes(response.status)) throw new CollaborationAccessError(response.status);
  if (response.status === 409) {
    const conflict = await response.json().catch(() => null) as { code?: string; currentRevision?: number } | null;
    if (conflict?.code === "collaboration.message.revision.conflict") {
      const revision = conflict.currentRevision;
      throw new CollaborationRevisionConflict(Number.isSafeInteger(revision) && revision! > 0 ? revision! : null);
    }
    if (conflict?.code === "collaboration.conversion.evidence.source.already_created" ||
        conflict?.code === "collaboration.conversion.destination_id.reused")
      throw new CollaborationEvidenceSourceAlreadyExists();
    throw new Error("تعارض در تبدیل رسمی؛ تبار پیام را بازخوانی کنید.");
  }
  if (response.status === 422) throw new CollaborationEvidenceValidationError();
  if (![200, 201].includes(response.status)) throw new Error("ثبت Evidence رسمی کامل نشد.");
  const result = await response.json() as ProjectMessageConversionLineage;
  const document = result?.documents?.[0];
  if (!validLineage(result, message.id) || result.destinationType !== "Evidence" ||
      result.destinationId.toLowerCase() !== destinationId.toLowerCase() ||
      result.messageRevision !== message.revision ||
      result.confirmedBy.toLowerCase() !== actorUserId.toLowerCase() ||
      result.documents.length !== 1 ||
      document?.id.toLowerCase() !== attachment.documentId.toLowerCase() ||
      document?.sha256.toLowerCase() !== attachment.sha256.toLowerCase() ||
      document?.versionNumber !== attachment.versionNumber ||
      document?.fileName !== attachment.originalFileName ||
      document?.contentType !== attachment.contentType ||
      document?.sizeBytes !== attachment.sizeBytes) {
    throw new Error("تأیید Evidence و تبار فایل معتبر نیست.");
  }
  return result;
}

export class CollaborationTechnicalDocumentSourceAlreadyExists extends Error {
  constructor() { super("از این فایل پیام قبلاً سند فنی رسمی ساخته شده است؛ تبار را تازه‌سازی کنید."); }
}

export class CollaborationTechnicalDocumentValidationError extends Error {
  constructor() { super("نوع سند، مشخصات Revision یا فایل آزادشده پذیرفته نشد."); }
}

export interface ProjectTechnicalDocumentConversionDetails {
  readonly title: string;
  readonly type: TechnicalDocumentType;
  readonly discipline: string;
  readonly originator: string;
  readonly revisionCode: string;
}

const technicalDocumentTypes: readonly TechnicalDocumentType[] = ["Drawing", "Specification",
  "MethodStatement", "MaterialSubmittal", "ShopDrawing", "CalculationOrReport",
  "MeetingMinute", "Correspondence", "Instruction", "MeasurementSheet",
  "HandoverOrTestRecord", "Other"];

/** Creates one Draft Technical Document and WIP revision from a confirmed Released chat file. */
export async function convertProjectMessageToTechnicalDocument(apiBaseUrl: string, projectId: string,
  message: ProjectConversationMessage, actorUserId: string, attachment: ProjectMessageAttachment,
  details: ProjectTechnicalDocumentConversionDetails,
  destinationId: string, idempotencyKey: string): Promise<ProjectMessageConversionLineage> {
  const title = details.title.trim();
  const discipline = details.discipline.trim();
  const originator = details.originator.trim();
  const revisionCode = details.revisionCode.trim().toUpperCase();
  if (!guid(projectId) || !guid(message.id) || !guid(actorUserId) ||
      !guid(destinationId) || !guid(idempotencyKey) ||
      message.projectId.toLowerCase() !== projectId.toLowerCase() ||
      !Number.isSafeInteger(message.revision) || message.revision < 1 ||
      message.deletedAt || message.redactedAt ||
      attachment.messageId?.toLowerCase() !== message.id.toLowerCase() ||
      !guid(attachment.documentId) || !/^[0-9a-f]{64}$/iu.test(attachment.sha256) ||
      !Number.isSafeInteger(attachment.versionNumber) || attachment.versionNumber < 1 ||
      !Number.isSafeInteger(attachment.sizeBytes) || attachment.sizeBytes < 1 ||
      attachment.sizeBytes > 25 * 1024 * 1024 ||
      !title || title.length > 240 || !technicalDocumentTypes.includes(details.type) ||
      !discipline || discipline.length > 120 || originator.length > 240 ||
      !revisionCode || revisionCode.length > 80 ||
      /[\x00-\x08\x0b-\x1f\x7f]/u.test(title + discipline + originator + revisionCode) ||
      typeof attachment.releasedAt !== "string" ||
      !Number.isFinite(Date.parse(attachment.releasedAt))) {
    throw new Error("مشخصات تبدیل به سند فنی معتبر نیست.");
  }
  const url = `${apiBaseUrl.replace(/\/$/u, "")}/api/v1/projects/${encodeURIComponent(projectId)}` +
    `/collaboration/messages/${encodeURIComponent(message.id)}/conversions`;
  const response = await fetch(url, {
    method: "POST", cache: "no-store",
    headers: { "Content-Type": "application/json", "Idempotency-Key": idempotencyKey },
    body: JSON.stringify({ destinationId, destinationType: "TechnicalDocument",
      baseRevision: message.revision, confirmed: true,
      details: { title, type: details.type, discipline, originator: originator || null, revisionCode },
      documentIds: [attachment.documentId] }),
  });
  if ([401, 403, 404].includes(response.status)) throw new CollaborationAccessError(response.status);
  if (response.status === 409) {
    const conflict = await response.json().catch(() => null) as { code?: string; currentRevision?: number } | null;
    if (conflict?.code === "collaboration.message.revision.conflict") {
      const revision = conflict.currentRevision;
      throw new CollaborationRevisionConflict(Number.isSafeInteger(revision) && revision! > 0 ? revision! : null);
    }
    if (conflict?.code === "collaboration.conversion.technical_document.source.already_created" ||
        conflict?.code === "collaboration.conversion.destination_id.reused")
      throw new CollaborationTechnicalDocumentSourceAlreadyExists();
    throw new Error("تعارض در تبدیل رسمی؛ تبار پیام را بازخوانی کنید.");
  }
  if (response.status === 422) throw new CollaborationTechnicalDocumentValidationError();
  if (![200, 201].includes(response.status)) throw new Error("ثبت سند فنی رسمی کامل نشد.");
  const result = await response.json() as ProjectMessageConversionLineage;
  const document = result?.documents?.[0];
  if (!validLineage(result, message.id) || result.destinationType !== "TechnicalDocument" ||
      result.destinationId.toLowerCase() !== destinationId.toLowerCase() ||
      result.messageRevision !== message.revision ||
      result.confirmedBy.toLowerCase() !== actorUserId.toLowerCase() ||
      result.documents.length !== 1 ||
      document?.id.toLowerCase() !== attachment.documentId.toLowerCase() ||
      document?.sha256.toLowerCase() !== attachment.sha256.toLowerCase() ||
      document?.versionNumber !== attachment.versionNumber ||
      document?.fileName !== attachment.originalFileName ||
      document?.contentType !== attachment.contentType ||
      document?.sizeBytes !== attachment.sizeBytes) {
    throw new Error("تأیید سند فنی و تبار فایل معتبر نیست.");
  }
  return result;
}

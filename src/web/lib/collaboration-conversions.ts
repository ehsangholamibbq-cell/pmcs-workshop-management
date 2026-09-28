import { CollaborationAccessError } from "./collaboration-events.ts";
import { CollaborationRevisionConflict } from "./collaboration-interactions.ts";
import type { ProjectConversationMessage } from "./collaboration-room.ts";

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
  destinationId: string, idempotencyKey: string): Promise<ProjectMessageConversionLineage> {
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
        priority: details.priority, title, description: description || null }, documentIds: [] }),
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
      result.documents.length !== 0) {
    throw new Error("تأیید تبدیل رسمی معتبر نیست.");
  }
  return result;
}

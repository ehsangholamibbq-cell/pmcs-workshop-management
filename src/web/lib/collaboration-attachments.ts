import { CollaborationAccessError } from "./collaboration-events.ts";

export interface ProjectMessageAttachment {
  readonly messageId: string;
  readonly documentId: string;
  readonly originalFileName: string;
  readonly contentType: string;
  readonly sizeBytes: number;
  readonly sha256: string;
  readonly classification: "Internal" | "Confidential" | "Restricted";
  readonly retentionPolicy: "Standard" | "LongTerm" | "Permanent";
  readonly legalHold: boolean;
  readonly releasedAt: string;
  readonly versionNumber: number;
  readonly contentUrl: string;
}

export interface ProjectChatUploadState {
  readonly id: string;
  readonly messageId: string;
  readonly originalFileName: string;
  readonly contentType: string;
  readonly sizeBytes: number;
  readonly sha256: string;
  readonly status: "PendingUpload" | "Quarantined" | "Released" | "Rejected";
  readonly versionNumber: number;
  readonly releasedAt: string | null;
}

export interface ProjectChatUploadExpectation {
  readonly assetId: string;
  readonly originalFileName: string;
  readonly contentType: string;
  readonly sizeBytes: number;
  readonly sha256: string;
}

const uuid = /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/iu;
const digest = /^[0-9a-f]{64}$/iu;
const maximumBytes = 25 * 1024 * 1024;
const supportedTypes = new Set([
  "image/jpeg", "image/png", "image/webp", "image/heic", "image/heif",
  "application/pdf", "text/plain", "text/csv",
  "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
  "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
  "application/vnd.openxmlformats-officedocument.presentationml.presentation",
]);

function attachmentPath(projectId: string, messageId: string, documentId?: string): string {
  if (!uuid.test(projectId) || !uuid.test(messageId) || (documentId && !uuid.test(documentId))) {
    throw new Error("شناسهٔ پیوست گفت‌وگو معتبر نیست.");
  }
  const path = `/api/v1/projects/${encodeURIComponent(projectId)}/collaboration/messages/${encodeURIComponent(messageId)}/attachments`;
  return documentId ? `${path}/${encodeURIComponent(documentId)}/content` : path;
}

async function checked(response: Response): Promise<Response> {
  if ([401, 403, 404].includes(response.status)) throw new CollaborationAccessError(response.status);
  if (!response.ok) throw new Error("دریافت پیوست گفت‌وگو کامل نشد.");
  return response;
}

/** Author-only status read; the generic Documents route intentionally hides ProjectChat assets. */
export async function loadProjectChatUploadState(apiBaseUrl: string, projectId: string,
  messageId: string, expected: ProjectChatUploadExpectation): Promise<ProjectChatUploadState> {
  attachmentPath(projectId, messageId);
  if (!uuid.test(expected.assetId)) throw new Error("شناسهٔ آپلود پیام معتبر نیست.");
  const path = `/api/v1/projects/${encodeURIComponent(projectId)}/collaboration/messages/${encodeURIComponent(messageId)}/uploads/${encodeURIComponent(expected.assetId)}`;
  const response = await checked(await fetch(`${apiBaseUrl.replace(/\/$/u, "")}${path}`,
    { cache: "no-store" }));
  const state = await response.json() as ProjectChatUploadState;
  if (!state || state.id?.toLowerCase() !== expected.assetId.toLowerCase() ||
      state.messageId?.toLowerCase() !== messageId.toLowerCase() ||
      state.originalFileName !== expected.originalFileName ||
      state.contentType !== expected.contentType || state.sizeBytes !== expected.sizeBytes ||
      state.sha256?.toLowerCase() !== expected.sha256.toLowerCase() ||
      !["PendingUpload", "Quarantined", "Released", "Rejected"].includes(state.status) ||
      !Number.isSafeInteger(state.versionNumber) || state.versionNumber < 1 ||
      (state.status === "Released"
        ? typeof state.releasedAt !== "string" || !Number.isFinite(Date.parse(state.releasedAt))
        : state.releasedAt !== null)) {
    throw new Error("وضعیت آپلود پیام معتبر نیست.");
  }
  return state;
}

/** Associates only the author-owned Released document after scoped server confirmation. */
export async function attachProjectChatDocument(apiBaseUrl: string, projectId: string,
  messageId: string, upload: ProjectChatUploadExpectation): Promise<ProjectMessageAttachment> {
  const path = attachmentPath(projectId, messageId, upload.assetId).replace(/\/content$/u, "");
  const response = await checked(await fetch(`${apiBaseUrl.replace(/\/$/u, "")}${path}`,
    { method: "PUT", cache: "no-store" }));
  const attached = await response.json() as ProjectMessageAttachment;
  if (!validAttachment(attached, projectId, messageId) ||
      attached.documentId.toLowerCase() !== upload.assetId.toLowerCase() ||
      attached.originalFileName !== upload.originalFileName ||
      attached.contentType !== upload.contentType ||
      attached.sizeBytes !== upload.sizeBytes ||
      attached.sha256.toLowerCase() !== upload.sha256.toLowerCase()) {
    throw new Error("تأیید اتصال پیوست معتبر نیست.");
  }
  return attached;
}

function validAttachment(item: ProjectMessageAttachment, projectId: string, messageId: string): boolean {
  return item && item.messageId?.toLowerCase() === messageId.toLowerCase() &&
    uuid.test(item.documentId ?? "") &&
    typeof item.originalFileName === "string" &&
    item.originalFileName.trim().length > 0 && item.originalFileName.length <= 255 &&
    !/[/\\\x00-\x1f\x7f]/u.test(item.originalFileName) &&
    supportedTypes.has(item.contentType) &&
    Number.isSafeInteger(item.sizeBytes) && item.sizeBytes > 0 && item.sizeBytes <= maximumBytes &&
    digest.test(item.sha256 ?? "") &&
    ["Internal", "Confidential", "Restricted"].includes(item.classification) &&
    ["Standard", "LongTerm", "Permanent"].includes(item.retentionPolicy) &&
    typeof item.legalHold === "boolean" &&
    typeof item.releasedAt === "string" && Number.isFinite(Date.parse(item.releasedAt)) &&
    Number.isSafeInteger(item.versionNumber) && item.versionNumber > 0 &&
    item.contentUrl === attachmentPath(projectId, messageId, item.documentId);
}

/** Reads only Released documents associated with a live message in this project. */
export async function loadProjectMessageAttachments(apiBaseUrl: string, projectId: string,
  messageId: string, signal?: AbortSignal): Promise<readonly ProjectMessageAttachment[]> {
  const path = attachmentPath(projectId, messageId);
  const response = await checked(await fetch(`${apiBaseUrl.replace(/\/$/u, "")}${path}`,
    { cache: "no-store", signal }));
  const attachments = await response.json() as ProjectMessageAttachment[];
  if (!Array.isArray(attachments) || attachments.length > 50 ||
      attachments.some((item) => !validAttachment(item, projectId, messageId)) ||
      new Set(attachments.map((item) => item.documentId.toLowerCase())).size !== attachments.length) {
    throw new Error("فهرست پیوست‌های پیام معتبر نیست.");
  }
  return attachments;
}

/** Downloads through the scoped server gate and verifies the Released document bytes. */
export async function downloadProjectMessageAttachment(apiBaseUrl: string, projectId: string,
  messageId: string, attachment: ProjectMessageAttachment): Promise<Blob> {
  if (!validAttachment(attachment, projectId, messageId)) {
    throw new Error("مشخصات پیوست پیام معتبر نیست.");
  }
  const path = attachmentPath(projectId, messageId, attachment.documentId);
  const response = await checked(await fetch(`${apiBaseUrl.replace(/\/$/u, "")}${path}`,
    { cache: "no-store" }));
  const contentType = response.headers.get("content-type")?.split(";")[0]?.trim().toLowerCase();
  const contentLength = response.headers.get("content-length");
  if (contentType !== attachment.contentType ||
      (contentLength !== null && (!/^\d+$/u.test(contentLength) || Number(contentLength) > attachment.sizeBytes))) {
    throw new Error("محتوای پیوست با مشخصات ثبت‌شده سازگار نیست.");
  }
  const bytes = await response.arrayBuffer();
  if (bytes.byteLength !== attachment.sizeBytes) {
    throw new Error("اندازهٔ پیوست با مشخصات ثبت‌شده سازگار نیست.");
  }
  const actual = Array.from(new Uint8Array(await crypto.subtle.digest("SHA-256", bytes)),
    (byte) => byte.toString(16).padStart(2, "0")).join("");
  if (actual !== attachment.sha256.toLowerCase()) {
    throw new Error("صحت پیوست تأیید نشد.");
  }
  return new Blob([bytes], { type: attachment.contentType });
}

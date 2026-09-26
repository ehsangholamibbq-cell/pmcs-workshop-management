import {
  currentLocalIdentityScope,
  documentUploadStoreName,
  openFieldDatabase,
} from "./field-database.ts";
import { apiProblemMessage, toUserMessage, type ApiProblem } from "./localization.ts";

export type DocumentOwnerType =
  | "ProjectGeneral"
  | "ProjectChat"
  | "ReportOutput"
  | "TechnicalDocument"
  | "MemberProfile"
  | "LoginExperience";
export type DocumentClassification = "Internal" | "Confidential" | "Restricted";
export type DocumentRetentionPolicy = "Standard" | "LongTerm" | "Permanent";
export type DocumentUploadStatus = "queued" | "uploading" | "quarantined" | "released" | "rejected";

export interface QueuedDocumentUpload {
  readonly assetId: string;
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string | null;
  readonly scopeKey: string;
  readonly ownerType: DocumentOwnerType;
  readonly ownerId: string;
  readonly originalFileName: string;
  readonly contentType: string;
  readonly sizeBytes: number;
  readonly sha256: string;
  readonly classification: DocumentClassification;
  readonly retentionPolicy: DocumentRetentionPolicy;
  readonly retainUntil: string | null;
  readonly legalHold: boolean;
  readonly createdAtDevice: string;
  readonly blob: Blob;
  readonly status: DocumentUploadStatus;
  readonly attemptCount: number;
  readonly lastAttemptAt?: string;
  readonly lastError?: string;
  readonly acceptedAt?: string;
}

export interface EnqueueDocumentUploadInput {
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId?: string | null;
  readonly ownerType: DocumentOwnerType;
  readonly ownerId: string;
  readonly file: File;
  readonly classification?: DocumentClassification;
  readonly retentionPolicy?: DocumentRetentionPolicy;
  readonly retainUntil?: string | null;
  readonly legalHold?: boolean;
}

export interface DocumentUploadSessionPayload {
  readonly clientGeneratedId: string;
  readonly projectId: string | null;
  readonly ownerType: DocumentOwnerType;
  readonly ownerId: string;
  readonly originalFileName: string;
  readonly contentType: string;
  readonly sizeBytes: number;
  readonly sha256: string;
  readonly classification: DocumentClassification;
  readonly retentionPolicy: DocumentRetentionPolicy;
  readonly retainUntil: string | null;
  readonly legalHold: boolean;
}

interface DocumentUploadSessionResponse {
  readonly document: {
    readonly id: string;
    readonly status: "PendingUpload" | "Quarantined" | "Released" | "Rejected";
  };
  readonly uploadMethod: "PUT" | null;
  readonly uploadUrl: string | null;
  readonly expiresAt: string | null;
}

export interface DocumentUploadSummary {
  readonly sent: number;
  readonly quarantined: number;
  readonly rejected: number;
  readonly deferred: number;
  readonly remaining: number;
}

const activeSyncs = new Map<string, Promise<DocumentUploadSummary>>();
const maximumSizeBytes = 25 * 1024 * 1024;
const allowedExtensions = new Map<string, readonly string[]>([
  ["image/jpeg", [".jpg", ".jpeg"]],
  ["image/png", [".png"]],
  ["image/webp", [".webp"]],
  ["image/heic", [".heic"]],
  ["image/heif", [".heif"]],
  ["application/pdf", [".pdf"]],
  ["text/plain", [".txt", ".log"]],
  ["text/csv", [".csv"]],
  ["application/vnd.openxmlformats-officedocument.wordprocessingml.document", [".docx"]],
  ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [".xlsx"]],
  ["application/vnd.openxmlformats-officedocument.presentationml.presentation", [".pptx"]],
]);

export async function enqueueDocumentUpload(
  input: EnqueueDocumentUploadInput,
): Promise<QueuedDocumentUpload> {
  const identity = currentLocalIdentityScope();
  if (identity.tenantId !== input.tenantId || identity.userId !== input.userId) {
    throw new Error("صف فایل با هویت نشست فعلی هم‌خوان نیست.");
  }

  validateFile(input.file);
  const projectId = input.projectId ?? null;
  validateOwnerScope(input.ownerType, projectId);
  const item: QueuedDocumentUpload = {
    assetId: crypto.randomUUID(),
    tenantId: input.tenantId,
    userId: input.userId,
    projectId,
    scopeKey: toScopeKey(projectId),
    ownerType: input.ownerType,
    ownerId: input.ownerId,
    originalFileName: input.file.name,
    contentType: input.file.type.toLowerCase(),
    sizeBytes: input.file.size,
    sha256: await calculateSha256(input.file),
    classification: input.classification ?? "Internal",
    retentionPolicy: input.retentionPolicy ?? "Standard",
    retainUntil: input.retainUntil ?? null,
    legalHold: input.legalHold ?? false,
    createdAtDevice: new Date().toISOString(),
    blob: input.file,
    status: "queued",
    attemptCount: 0,
  };
  const database = await openFieldDatabase();
  await writeItems(database, [item]);
  database.close();
  return item;
}

export function buildDocumentSessionPayload(item: QueuedDocumentUpload): DocumentUploadSessionPayload {
  return {
    clientGeneratedId: item.assetId,
    projectId: item.projectId,
    ownerType: item.ownerType,
    ownerId: item.ownerId,
    originalFileName: item.originalFileName,
    contentType: item.contentType,
    sizeBytes: item.sizeBytes,
    sha256: item.sha256,
    classification: item.classification,
    retentionPolicy: item.retentionPolicy,
    retainUntil: item.retainUntil,
    legalHold: item.legalHold,
  };
}

export async function recoverInterruptedDocumentUploads(projectId?: string | null): Promise<number> {
  const database = await openFieldDatabase();
  const interrupted = (await readByScope(database, toScopeKey(projectId ?? null), 100_000))
    .filter((item) => item.status === "uploading");
  if (interrupted.length > 0) {
    await writeItems(database, interrupted.map((item) => ({
      ...item,
      status: "queued" as const,
      lastError: "آپلود قطع‌شده با همان شناسه و بدون ساخت نسخه تکراری از سر گرفته می‌شود.",
    })));
  }

  database.close();
  return interrupted.length;
}

export function syncPendingDocumentUploads(
  apiBaseUrl: string,
  projectId?: string | null,
): Promise<DocumentUploadSummary> {
  const identity = currentLocalIdentityScope();
  const scopeKey = toScopeKey(projectId ?? null);
  const key = `${identity.tenantId}:${identity.userId}:${scopeKey}`;
  const current = activeSyncs.get(key);
  if (current) return current;

  const sync = drainQueue(apiBaseUrl, scopeKey).finally(() => activeSyncs.delete(key));
  activeSyncs.set(key, sync);
  return sync;
}

async function drainQueue(apiBaseUrl: string, scopeKey: string): Promise<DocumentUploadSummary> {
  let sent = 0;
  let quarantined = 0;
  let rejected = 0;
  let deferred = 0;
  let remaining = 0;
  for (let batchNumber = 0; batchNumber < 50; batchNumber += 1) {
    const batch = await performBatch(apiBaseUrl, scopeKey);
    sent += batch.sent;
    quarantined += batch.quarantined;
    rejected += batch.rejected;
    deferred += batch.deferred;
    remaining = batch.remaining;
    if (remaining === 0 || batch.deferred > 0 || batch.sent < 20) break;
  }

  return { sent, quarantined, rejected, deferred, remaining };
}

async function performBatch(apiBaseUrl: string, scopeKey: string): Promise<DocumentUploadSummary> {
  const database = await openFieldDatabase();
  const queued = (await readByScope(database, scopeKey, 100_000))
    .filter((item) => item.status === "queued")
    .slice(0, 20);
  if (queued.length === 0) {
    database.close();
    return { sent: 0, quarantined: 0, rejected: 0, deferred: 0, remaining: 0 };
  }

  const identity = currentLocalIdentityScope();
  if (queued.some((item) => item.tenantId !== identity.tenantId || item.userId !== identity.userId)) {
    database.close();
    throw new Error("صف فایل با هویت نشست فعلی هم‌خوان نیست و ارسال نشد.");
  }

  let quarantined = 0;
  let rejected = 0;
  let deferred = 0;
  for (const item of queued) {
    const uploading: QueuedDocumentUpload = {
      ...item,
      status: "uploading",
      attemptCount: item.attemptCount + 1,
      lastAttemptAt: new Date().toISOString(),
      lastError: undefined,
    };
    await writeItems(database, [uploading]);

    try {
      const session = await createSession(apiBaseUrl, uploading);
      let finalStatus = session.document.status;
      if (finalStatus === "PendingUpload") {
        if (session.uploadMethod !== "PUT" || !session.uploadUrl) {
          throw new DocumentUploadError("سرور آدرس معتبر آپلود برنگرداند.", false);
        }

        finalStatus = await uploadContent(apiBaseUrl, session.uploadUrl, uploading);
      }

      if (finalStatus === "Rejected") {
        throw new DocumentUploadError("فایل در کنترل امنیتی رد شد.", true);
      }

      await writeItems(database, [{
        ...uploading,
        status: finalStatus === "Released" ? "released" : "quarantined",
        blob: new Blob(),
        acceptedAt: new Date().toISOString(),
      }]);
      quarantined += 1;
    } catch (error) {
      const uploadError = error instanceof DocumentUploadError
        ? error
        : new DocumentUploadError(toUserMessage(error, "بارگذاری فایل ناموفق بود؛ دوباره تلاش کنید."), false);
      await writeItems(database, [{
        ...uploading,
        status: uploadError.permanent ? "rejected" : "queued",
        lastError: uploadError.message,
      }]);
      if (uploadError.permanent) rejected += 1;
      else deferred += 1;
    }
  }

  const remaining = (await readByScope(database, scopeKey, 100_000))
    .filter((item) => item.status === "queued").length;
  database.close();
  return { sent: queued.length, quarantined, rejected, deferred, remaining };
}

async function createSession(
  apiBaseUrl: string,
  item: QueuedDocumentUpload,
): Promise<DocumentUploadSessionResponse> {
  const response = await fetch(`${normalizedBaseUrl(apiBaseUrl)}/api/v1/upload-sessions`, {
    method: "POST",
    headers: {
      ...identityHeaders(item),
      "Content-Type": "application/json",
      "Idempotency-Key": `${item.assetId}:session`,
    },
    body: JSON.stringify(buildDocumentSessionPayload(item)),
  });
  await ensureSuccess(response, response.status === 410);
  return response.json() as Promise<DocumentUploadSessionResponse>;
}

async function uploadContent(
  apiBaseUrl: string,
  uploadUrl: string,
  item: QueuedDocumentUpload,
): Promise<"Quarantined" | "Released" | "Rejected"> {
  const url = uploadUrl.startsWith("http") ? uploadUrl : `${normalizedBaseUrl(apiBaseUrl)}${uploadUrl}`;
  const response = await fetch(url, {
    method: "PUT",
    headers: {
      ...identityHeaders(item),
      "Content-Type": item.contentType,
      "Idempotency-Key": `${item.assetId}:content`,
    },
    body: item.blob,
  });
  await ensureSuccess(response, response.status === 410);
  const model = await response.json() as {
    readonly status: "Quarantined" | "Released" | "Rejected";
  };
  return model.status;
}

async function ensureSuccess(response: Response, retryableClientError: boolean): Promise<void> {
  if (response.ok) return;

  let problem: ApiProblem | null = null;
  try {
    problem = await response.json() as ApiProblem;
  } catch {
    // پاسخ غیرساختاریافته نیز به پیام فارسی کنترل‌شده تبدیل می‌شود.
  }

  const permanent = response.status >= 400 && response.status < 500 && !retryableClientError;
  throw new DocumentUploadError(apiProblemMessage(problem, response.status), permanent);
}

function validateFile(file: File): void {
  const type = file.type.toLowerCase();
  const extensions = allowedExtensions.get(type);
  const dotIndex = file.name.lastIndexOf(".");
  const extension = dotIndex >= 0 ? file.name.slice(dotIndex).toLowerCase() : "";
  if (!extensions?.includes(extension)) {
    throw new Error("نوع یا پسوند فایل در سیاست امن اسناد سامانه مجاز نیست.");
  }

  if (file.size <= 0 || file.size > maximumSizeBytes) {
    throw new Error("حجم فایل باید بیشتر از صفر و حداکثر ۲۵ مگابایت باشد.");
  }
}

function validateOwnerScope(ownerType: DocumentOwnerType, projectId: string | null): void {
  const projectOwned = ["ProjectGeneral", "ProjectChat", "ReportOutput", "TechnicalDocument"]
    .includes(ownerType);
  if (projectOwned !== Boolean(projectId)) {
    throw new Error("مالک فایل با محدوده پروژه یا سازمان هم‌خوان نیست.");
  }
}

async function calculateSha256(file: File): Promise<string> {
  const digest = await crypto.subtle.digest("SHA-256", await file.arrayBuffer());
  return Array.from(new Uint8Array(digest), (byte) => byte.toString(16).padStart(2, "0")).join("");
}

function readByScope(
  database: IDBDatabase,
  scopeKey: string,
  limit: number,
): Promise<QueuedDocumentUpload[]> {
  return new Promise((resolve, reject) => {
    const items: QueuedDocumentUpload[] = [];
    const transaction = database.transaction(documentUploadStoreName, "readonly");
    const request = transaction.objectStore(documentUploadStoreName)
      .index("by-scope").openCursor(IDBKeyRange.only(scopeKey));
    request.onsuccess = () => {
      const cursor = request.result;
      if (!cursor || items.length >= limit) {
        resolve(items);
        return;
      }

      items.push(cursor.value as QueuedDocumentUpload);
      cursor.continue();
    };
    request.onerror = () => reject(request.error ?? new Error("خواندن صف فایل‌ها ممکن نشد."));
  });
}

function writeItems(database: IDBDatabase, items: readonly QueuedDocumentUpload[]): Promise<void> {
  return new Promise((resolve, reject) => {
    const transaction = database.transaction(documentUploadStoreName, "readwrite");
    const store = transaction.objectStore(documentUploadStoreName);
    items.forEach((item) => store.put(item));
    transaction.oncomplete = () => resolve();
    transaction.onerror = () => reject(transaction.error ?? new Error("ثبت صف فایل‌ها ناموفق بود."));
    transaction.onabort = () => reject(transaction.error ?? new Error("ثبت صف فایل‌ها متوقف شد."));
  });
}

function identityHeaders(item: QueuedDocumentUpload): Record<string, string> {
  return { "X-Tenant-Id": item.tenantId, "X-User-Id": item.userId };
}

function toScopeKey(projectId: string | null): string {
  return projectId ?? "tenant";
}

function normalizedBaseUrl(apiBaseUrl: string): string {
  return apiBaseUrl.replace(/\/$/, "");
}

class DocumentUploadError extends Error {
  readonly permanent: boolean;

  constructor(message: string, permanent: boolean) {
    super(message);
    this.permanent = permanent;
  }
}

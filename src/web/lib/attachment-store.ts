import {
  attachmentStoreName,
  currentLocalIdentityScope,
  openFieldDatabase,
} from "./field-database.ts";
import { apiProblemMessage, toUserMessage, type ApiProblem } from "./localization.ts";
import { requireOfflineAuthorization } from "./sync-client.ts";

export type AttachmentStatus = "queued" | "uploading" | "uploaded" | "rejected";

export interface QueuedAttachment {
  readonly attachmentId: string;
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly dailyReportId: string;
  readonly dailyFactId: string | null;
  readonly originalFileName: string;
  readonly contentType: string;
  readonly sizeBytes: number;
  readonly sha256: string;
  readonly capturedAtDevice: string;
  readonly blob: Blob;
  readonly status: AttachmentStatus;
  readonly attemptCount: number;
  readonly lastAttemptAt?: string;
  readonly lastError?: string;
  readonly uploadedAt?: string;
}

export interface EnqueueAttachmentInput {
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly dailyReportId: string;
  readonly dailyFactId?: string | null;
  readonly file: File;
  readonly capturedAtDevice?: string;
}

export interface AttachmentSyncSummary {
  readonly sent: number;
  readonly uploaded: number;
  readonly rejected: number;
  readonly deferred: number;
}

export interface AttachmentIssue {
  readonly attachmentId: string;
  readonly originalFileName: string;
  readonly createdAtDevice: string;
  readonly message: string;
}

export interface EvidenceUploadSessionPayload {
  readonly clientGeneratedId: string;
  readonly dailyReportId: string;
  readonly dailyFactId: string | null;
  readonly originalFileName: string;
  readonly contentType: string;
  readonly sizeBytes: number;
  readonly sha256: string;
  readonly capturedAtDevice: string;
}

interface EvidenceUploadSessionResponse {
  readonly evidence: { readonly id: string; readonly status: "PendingUpload" | "Uploaded" };
  readonly uploadMethod: "PUT" | null;
  readonly uploadUrl: string | null;
  readonly expiresAt: string | null;
}

const attachmentStore = attachmentStoreName;
const allowedContentTypes = new Set([
  "image/jpeg",
  "image/png",
  "image/webp",
  "image/heic",
  "image/heif",
  "application/pdf",
]);
const maximumSizeBytes = 25 * 1024 * 1024;
let activeSync: Promise<AttachmentSyncSummary> | null = null;

export async function enqueueAttachment(input: EnqueueAttachmentInput): Promise<QueuedAttachment> {
  validateFile(input.file);
  await requireOfflineAuthorization(input.projectId, "CaptureDailyReportFact");
  const sha256 = await calculateSha256(input.file);
  const attachment: QueuedAttachment = {
    attachmentId: crypto.randomUUID(),
    tenantId: input.tenantId,
    userId: input.userId,
    projectId: input.projectId,
    dailyReportId: input.dailyReportId,
    dailyFactId: input.dailyFactId ?? null,
    originalFileName: input.file.name,
    contentType: input.file.type.toLowerCase(),
    sizeBytes: input.file.size,
    sha256,
    capturedAtDevice: input.capturedAtDevice ?? new Date().toISOString(),
    blob: input.file,
    status: "queued",
    attemptCount: 0,
  };
  const database = await openFieldDatabase();
  await writeAttachments(database, [attachment]);
  database.close();
  return attachment;
}

export function buildEvidenceSessionPayload(attachment: QueuedAttachment): EvidenceUploadSessionPayload {
  return {
    clientGeneratedId: attachment.attachmentId,
    dailyReportId: attachment.dailyReportId,
    dailyFactId: attachment.dailyFactId,
    originalFileName: attachment.originalFileName,
    contentType: attachment.contentType,
    sizeBytes: attachment.sizeBytes,
    sha256: attachment.sha256,
    capturedAtDevice: attachment.capturedAtDevice,
  };
}

export async function countPendingAttachments(): Promise<number> {
  const database = await openFieldDatabase();
  const [queued, uploading] = await Promise.all([
    countByStatus(database, "queued"),
    countByStatus(database, "uploading"),
  ]);
  database.close();
  return queued + uploading;
}

export async function recoverInterruptedAttachments(): Promise<number> {
  const database = await openFieldDatabase();
  const interrupted = await readByStatus(database, "uploading", 100);
  if (interrupted.length > 0) {
    await writeAttachments(database, interrupted.map((item) => ({
      ...item,
      status: "queued" as const,
      lastError: "آپلود قبلی قطع شد و با همان شناسه دوباره تلاش می‌شود.",
    })));
  }
  database.close();
  return interrupted.length;
}

export async function listAttachmentIssues(limit = 20): Promise<readonly AttachmentIssue[]> {
  const database = await openFieldDatabase();
  const rejected = await readByStatus(database, "rejected", limit);
  database.close();
  return rejected.map((item) => ({
    attachmentId: item.attachmentId,
    originalFileName: item.originalFileName,
    createdAtDevice: item.capturedAtDevice,
    message: item.lastError ?? "سرور علت مشخصی برنگرداند.",
  }));
}

export function syncPendingAttachments(apiBaseUrl: string): Promise<AttachmentSyncSummary> {
  if (activeSync) {
    return activeSync;
  }

  activeSync = performSync(apiBaseUrl).finally(() => {
    activeSync = null;
  });
  return activeSync;
}

async function performSync(apiBaseUrl: string): Promise<AttachmentSyncSummary> {
  const database = await openFieldDatabase();
  const queued = await readByStatus(database, "queued", 20);
  if (queued.length === 0) {
    database.close();
    return { sent: 0, uploaded: 0, rejected: 0, deferred: 0 };
  }

  const scope = currentLocalIdentityScope();
  if (queued.some((item) => item.tenantId !== scope.tenantId || item.userId !== scope.userId)) {
    database.close();
    throw new Error("صف مدارک با نشست فعلی هم‌خوان نیست و بارگذاری نشد.");
  }

  let uploaded = 0;
  let rejected = 0;
  let deferred = 0;
  for (const item of queued) {
    const uploading: QueuedAttachment = {
      ...item,
      status: "uploading",
      attemptCount: item.attemptCount + 1,
      lastAttemptAt: new Date().toISOString(),
      lastError: undefined,
    };
    await writeAttachments(database, [uploading]);

    try {
      const session = await createUploadSession(apiBaseUrl, uploading);
      if (session.evidence.status !== "Uploaded") {
        if (!session.uploadUrl || session.uploadMethod !== "PUT") {
          throw new Error("سرور آدرس معتبر آپلود برنگرداند.");
        }

        await uploadBinary(apiBaseUrl, session.uploadUrl, uploading);
      }

      await writeAttachments(database, [{
        ...uploading,
        status: "uploaded",
        blob: new Blob(),
        uploadedAt: new Date().toISOString(),
      }]);
      uploaded += 1;
    } catch (error) {
      const uploadError = error instanceof AttachmentUploadError
        ? error
        : new AttachmentUploadError(toUserMessage(error, "بارگذاری مدرک ناموفق بود؛ دوباره تلاش کنید."), false);
      await writeAttachments(database, [{
        ...uploading,
        status: uploadError.permanent ? "rejected" : "queued",
        lastError: uploadError.message,
      }]);
      if (uploadError.permanent) {
        rejected += 1;
      } else {
        deferred += 1;
      }
    }
  }

  database.close();
  return { sent: queued.length, uploaded, rejected, deferred };
}

async function createUploadSession(
  apiBaseUrl: string,
  attachment: QueuedAttachment,
): Promise<EvidenceUploadSessionResponse> {
  const response = await fetch(
    `${normalizedBaseUrl(apiBaseUrl)}/api/v1/projects/${attachment.projectId}/evidence/upload-sessions`,
    {
      method: "POST",
      headers: {
        ...identityHeaders(attachment),
        "Content-Type": "application/json",
        "Idempotency-Key": `${attachment.attachmentId}:session:${attachment.attemptCount}`,
      },
      body: JSON.stringify(buildEvidenceSessionPayload(attachment)),
    },
  );
  await ensureUploadSuccess(response, response.status === 404);
  return response.json() as Promise<EvidenceUploadSessionResponse>;
}

async function uploadBinary(
  apiBaseUrl: string,
  uploadUrl: string,
  attachment: QueuedAttachment,
): Promise<void> {
  const url = uploadUrl.startsWith("http") ? uploadUrl : `${normalizedBaseUrl(apiBaseUrl)}${uploadUrl}`;
  const response = await fetch(url, {
    method: "PUT",
    headers: {
      ...identityHeaders(attachment),
      "Content-Type": attachment.contentType,
      "Idempotency-Key": `${attachment.attachmentId}:content`,
    },
    body: attachment.blob,
  });
  await ensureUploadSuccess(response, response.status === 410);
}

async function ensureUploadSuccess(response: Response, retryableClientError: boolean): Promise<void> {
  if (response.ok) {
    return;
  }

  let problem: ApiProblem | null = null;
  try {
    problem = await response.json() as ApiProblem;
  } catch {
    // پاسخ غیرساختاریافته نیز به پیام فارسی تبدیل می‌شود.
  }

  const permanent = response.status >= 400 && response.status < 500 && !retryableClientError;
  throw new AttachmentUploadError(apiProblemMessage(problem, response.status), permanent);
}

function validateFile(file: File): void {
  if (!allowedContentTypes.has(file.type.toLowerCase())) {
    throw new Error("فقط تصویر با قالب جی‌پگ، پی‌ان‌جی، وب‌پی یا هِیک و سند پی‌دی‌اف قابل ثبت است.");
  }
  if (file.size <= 0 || file.size > maximumSizeBytes) {
    throw new Error("حجم هر مدرک باید بیشتر از صفر و حداکثر ۲۵ مگابایت باشد.");
  }
}

async function calculateSha256(file: File): Promise<string> {
  const digest = await crypto.subtle.digest("SHA-256", await file.arrayBuffer());
  return Array.from(new Uint8Array(digest), (byte) => byte.toString(16).padStart(2, "0")).join("");
}

function countByStatus(database: IDBDatabase, status: AttachmentStatus): Promise<number> {
  return new Promise((resolve, reject) => {
    const transaction = database.transaction(attachmentStore, "readonly");
    const request = transaction.objectStore(attachmentStore).index("by-status").count(status);
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error ?? new Error("شمارش صف محلی مدارک ممکن نشد."));
  });
}

function readByStatus(
  database: IDBDatabase,
  status: AttachmentStatus,
  limit: number,
): Promise<QueuedAttachment[]> {
  return new Promise((resolve, reject) => {
    const items: QueuedAttachment[] = [];
    const transaction = database.transaction(attachmentStore, "readonly");
    const request = transaction.objectStore(attachmentStore).index("by-status").openCursor(status);
    request.onsuccess = () => {
      const cursor = request.result;
      if (!cursor || items.length >= limit) {
        resolve(items);
        return;
      }
      items.push(cursor.value as QueuedAttachment);
      cursor.continue();
    };
    request.onerror = () => reject(request.error ?? new Error("خواندن صف محلی مدارک ممکن نشد."));
  });
}

function writeAttachments(database: IDBDatabase, items: readonly QueuedAttachment[]): Promise<void> {
  return new Promise((resolve, reject) => {
    const transaction = database.transaction(attachmentStore, "readwrite");
    const store = transaction.objectStore(attachmentStore);
    items.forEach((item) => store.put(item));
    transaction.oncomplete = () => resolve();
    transaction.onerror = () => reject(transaction.error ?? new Error("ثبت مدرک در پایگاه داده محلی ناموفق بود."));
    transaction.onabort = () => reject(transaction.error ?? new Error("ثبت مدرک در پایگاه داده محلی متوقف شد."));
  });
}

function identityHeaders(attachment: QueuedAttachment): Record<string, string> {
  return {
    "X-Tenant-Id": attachment.tenantId,
    "X-User-Id": attachment.userId,
  };
}

function normalizedBaseUrl(apiBaseUrl: string): string {
  return apiBaseUrl.replace(/\/$/, "");
}

class AttachmentUploadError extends Error {
  readonly permanent: boolean;

  constructor(message: string, permanent: boolean) {
    super(message);
    this.permanent = permanent;
  }
}

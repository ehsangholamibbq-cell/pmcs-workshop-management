import { createUlid } from "./offline-ulid.ts";
import {
  currentLocalIdentityScope,
  getOrCreateDeviceId,
  openFieldDatabase,
  operationStoreName,
  qualitySafetyIntakeStoreName,
  scopedStorageKey,
} from "./field-database.ts";
import { apiProblemMessage, toUserMessage } from "./localization.ts";
import {
  noteSuccessfulPush,
  prepareSyncSession,
  pullAuthorizedChanges,
  requireOfflineAuthorization,
  listSyncConflicts,
  resolveServerConflict,
} from "./sync-client.ts";

export type OperationStatus = "queued" | "syncing" | "synced" | "conflict" | "rejected" | "resolved";

export interface ClientOperation<TPayload = unknown> {
  readonly operationId: string;
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly entityType: string;
  readonly entityId: string;
  readonly commandType: string;
  readonly baseRevision: number | null;
  readonly payloadSchemaVersion: number;
  readonly payload: TPayload;
  readonly createdAtDevice: string;
  readonly offlineLeaseId: string;
  readonly authorizationVersion: number;
  readonly localSequence: number;
  readonly dependencies: readonly string[];
  readonly correlationId: string;
  readonly deviceTimezoneOffsetMinutes: number;
  readonly status: OperationStatus;
  readonly attemptCount: number;
  readonly lastAttemptAt?: string;
  readonly serverRevision?: number;
  readonly lastError?: string;
  readonly conflictId?: string;
  readonly resolutionReplacementOperationId?: string;
}

export interface EnqueueOperationInput<TPayload> {
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly entityType: string;
  readonly entityId: string;
  readonly commandType: string;
  readonly baseRevision?: number;
  readonly payload: TPayload;
  readonly offlineLeaseId?: string;
  readonly authorizationVersion?: number;
  readonly localSequence?: number;
}

interface SyncOperationResult {
  readonly operationId: string;
  readonly status: "Applied" | "Conflict" | "Rejected" | "Unsupported";
  readonly serverRevision?: number;
  readonly code?: string;
  readonly message?: string;
  readonly conflictId?: string;
}

interface SyncBatchResult {
  readonly processedAt: string;
  readonly operations: readonly SyncOperationResult[];
}

export interface SyncSummary {
  readonly sent: number;
  readonly applied: number;
  readonly conflicts: number;
  readonly rejected: number;
}

export interface OperationIssue {
  readonly operationId: string;
  readonly commandType: string;
  readonly entityId: string;
  readonly status: "conflict" | "rejected";
  readonly createdAtDevice: string;
  readonly serverRevision?: number;
  readonly conflictId?: string;
  readonly message: string;
}

const operationStore = operationStoreName;
const activeSyncs = new Map<string, Promise<SyncSummary>>();

export function buildClientOperation<TPayload>(input: EnqueueOperationInput<TPayload>): ClientOperation<TPayload> {
  return {
    operationId: createUlid(),
    tenantId: input.tenantId,
    userId: input.userId,
    projectId: input.projectId,
    entityType: input.entityType,
    entityId: input.entityId,
    commandType: input.commandType,
    baseRevision: input.baseRevision ?? null,
    payloadSchemaVersion: 1,
    payload: input.payload,
    createdAtDevice: new Date().toISOString(),
    offlineLeaseId: input.offlineLeaseId ?? "00000000-0000-0000-0000-000000000000",
    authorizationVersion: input.authorizationVersion ?? 0,
    localSequence: input.localSequence ?? 1,
    dependencies: [],
    correlationId: crypto.randomUUID(),
    deviceTimezoneOffsetMinutes: -new Date().getTimezoneOffset(),
    status: "queued",
    attemptCount: 0,
  };
}

export async function enqueueOperation<TPayload>(input: EnqueueOperationInput<TPayload>): Promise<ClientOperation<TPayload>> {
  const authorization = await requireOfflineAuthorization(input.projectId, input.commandType);
  const operation = buildClientOperation({
    ...input,
    ...authorization,
    localSequence: nextLocalSequence(),
  });
  const database = await openFieldDatabase();
  await writeOperations(database, [operation]);
  database.close();
  return operation;
}

export async function countPendingOperations(): Promise<number> {
  const database = await openFieldDatabase();
  const operationCount = await new Promise<number>((resolve, reject) => {
    const transaction = database.transaction(operationStore, "readonly");
    const request = transaction.objectStore(operationStore).index("by-status").count("queued");
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error ?? new Error("شمارش عملیات محلی ممکن نشد."));
  });
  const qualitySafetyCount = await new Promise<number>((resolve, reject) => {
    const transaction = database.transaction(qualitySafetyIntakeStoreName, "readonly");
    const request = transaction.objectStore(qualitySafetyIntakeStoreName).count();
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error ?? new Error("شمارش ثبت‌های محلی کیفیت و ایمنی ممکن نشد."));
  });
  database.close();
  return operationCount + qualitySafetyCount;
}

export async function listOperationIssues(limit = 20): Promise<readonly OperationIssue[]> {
  const database = await openFieldDatabase();
  const [conflicts, rejected] = await Promise.all([
    readOperationsByStatus(database, "conflict", limit),
    readOperationsByStatus(database, "rejected", limit),
  ]);
  database.close();

  return [...conflicts, ...rejected]
    .sort((left, right) => right.createdAtDevice.localeCompare(left.createdAtDevice))
    .slice(0, limit)
    .map((operation) => ({
      operationId: operation.operationId,
      commandType: operation.commandType,
      entityId: operation.entityId,
      status: operation.status as "conflict" | "rejected",
      createdAtDevice: operation.createdAtDevice,
      serverRevision: operation.serverRevision,
      conflictId: operation.conflictId,
      message: operation.lastError ?? "سرور علت مشخصی برنگرداند.",
    }));
}

export async function recoverInterruptedOperations(): Promise<number> {
  const database = await openFieldDatabase();
  const interrupted = await readOperationsByStatus(database, "syncing", 1000);
  if (interrupted.length > 0) {
    await writeOperations(
      database,
      interrupted.map((operation) => ({
        ...operation,
        status: "queued",
        lastError: "همگام‌سازی قبلی قطع شد و با همان شناسه دوباره انجام می‌شود.",
      })),
    );
  }
  database.close();
  return interrupted.length;
}

export function syncPendingOperations(apiBaseUrl: string, projectId: string): Promise<SyncSummary> {
  const scope = currentLocalIdentityScope();
  const syncKey = `${scope.tenantId}:${scope.userId}:${projectId}`;
  const activeSync = activeSyncs.get(syncKey);
  if (activeSync) return activeSync;

  const sync = performSync(apiBaseUrl, projectId).finally(() => {
    activeSyncs.delete(syncKey);
  });
  activeSyncs.set(syncKey, sync);
  return sync;
}

async function performSync(apiBaseUrl: string, projectId: string): Promise<SyncSummary> {
  const database = await openFieldDatabase();
  const allQueued = await readOperationsByStatus(database, "queued", 1000);
  const projectQueue = allQueued.filter((operation) => operation.projectId === projectId);
  const queued = projectQueue.slice(0, 100);

  const scope = currentLocalIdentityScope();
  if (queued.some((operation) => operation.tenantId !== scope.tenantId || operation.userId !== scope.userId)) {
    database.close();
    throw new Error("صف محلی با نشست فعلی هم‌خوان نیست و همگام‌سازی نشد.");
  }

  let manifest;
  try {
    manifest = await prepareSyncSession(apiBaseUrl, projectId, {
      pendingOperations: projectQueue.length,
      pendingAttachments: 0,
      pendingAttachmentBytes: 0,
      oldestOperationAt: projectQueue.map((item) => item.createdAtDevice).sort()[0],
    });
  } catch (error) {
    database.close();
    throw error;
  }
  if (queued.length === 0) {
    database.close();
    if (manifest.allowedOperations.includes("CaptureDailyReportFact")) {
      await pullAuthorizedChanges(apiBaseUrl, manifest);
    }
    return { sent: 0, applied: 0, conflicts: 0, rejected: 0 };
  }

  const attemptedAt = new Date().toISOString();
  const syncing = queued.map((operation) => ({
    ...operation,
    status: "syncing" as const,
    attemptCount: operation.attemptCount + 1,
    lastAttemptAt: attemptedAt,
    lastError: undefined,
  }));
  await writeOperations(database, syncing);
  let pushResultPersisted = false;

  try {
    const deviceId = getOrCreateDeviceId();
    const response = await fetch(`${apiBaseUrl.replace(/\/$/, "")}/api/v1/sync/operations`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-Pmcs-Sync-Session": manifest.sessionId,
      },
      body: JSON.stringify({
        deviceId,
        operations: queued.map((operation) => ({
          operationId: operation.operationId,
          projectId: operation.projectId,
          entityType: operation.entityType,
          entityId: operation.entityId,
          commandType: operation.commandType,
          baseRevision: operation.baseRevision,
          payloadSchemaVersion: operation.payloadSchemaVersion,
          payload: operation.payload,
          createdAtDevice: operation.createdAtDevice,
          offlineLeaseId: operation.offlineLeaseId,
          authorizationVersion: operation.authorizationVersion,
          localSequence: operation.localSequence,
          dependencies: operation.dependencies,
          correlationId: operation.correlationId,
          deviceTimezoneOffsetMinutes: operation.deviceTimezoneOffsetMinutes,
        })),
      }),
    });

    if (!response.ok) {
      throw new Error(apiProblemMessage(null, response.status));
    }

    const batch = (await response.json()) as SyncBatchResult;
    const resultById = new Map(batch.operations.map((result) => [result.operationId, result]));
    const completed = syncing.map((operation): ClientOperation => {
      const result = resultById.get(operation.operationId);
      if (!result) {
        return { ...operation, status: "queued", lastError: "سرور برای این عملیات پاسخی برنگرداند." };
      }

      const status: OperationStatus = result.status === "Applied"
        ? "synced"
        : result.status === "Conflict"
          ? "conflict"
          : "rejected";
      return {
        ...operation,
        status,
        serverRevision: result.serverRevision,
        conflictId: result.conflictId,
        lastError: status === "synced"
          ? undefined
          : apiProblemMessage(
              { code: result.code, message: result.message },
              status === "conflict" ? 409 : 422,
            ),
      };
    });
    await writeOperations(database, completed);
    pushResultPersisted = true;
    const pushedManifest = await noteSuccessfulPush(manifest, batch.processedAt);
    if (pushedManifest.allowedOperations.includes("CaptureDailyReportFact")) {
      await pullAuthorizedChanges(apiBaseUrl, pushedManifest);
    }
    database.close();

    return {
      sent: completed.length,
      applied: completed.filter((operation) => operation.status === "synced").length,
      conflicts: completed.filter((operation) => operation.status === "conflict").length,
      rejected: completed.filter((operation) => operation.status === "rejected").length,
    };
  } catch (error) {
    const message = toUserMessage(error, "ارتباط با سرور برقرار نشد؛ عملیات محلی محفوظ است.");
    if (!pushResultPersisted) {
      await writeOperations(
        database,
        syncing.map((operation) => ({ ...operation, status: "queued", lastError: message })),
      );
    }
    database.close();
    throw error;
  }
}

export async function resolveConflictOperation(
  apiBaseUrl: string,
  projectId: string,
  operationId: string,
  resolution: "keep-server" | "reapply",
): Promise<void> {
  const database = await openFieldDatabase();
  const operation = await readOperation(database, operationId);
  database.close();
  if (!operation || operation.status !== "conflict" || !operation.conflictId) {
    throw new Error("رکورد تعارض معتبر روی این دستگاه پیدا نشد.");
  }

  const manifest = await prepareSyncSession(apiBaseUrl, projectId, {
    pendingOperations: await countQueuedForProject(projectId),
    pendingAttachments: 0,
    pendingAttachmentBytes: 0,
    oldestOperationAt: operation.createdAtDevice,
  });
  const serverConflicts = await listSyncConflicts(apiBaseUrl, projectId);
  const serverConflict = serverConflicts.find((candidate) =>
    candidate.conflictId === operation.conflictId &&
    candidate.operationId === operation.operationId);
  if (!serverConflict) {
    throw new Error("پرونده تعارض روی سرور پیدا نشد؛ وضعیت دستگاه را تازه‌سازی کنید.");
  }

  if (resolution === "keep-server") {
    if (serverConflict.status === "Open") {
      await resolveServerConflict(
        apiBaseUrl,
        manifest,
        operation.conflictId,
        serverConflict.revision,
        "KeepServer",
      );
    } else if (serverConflict.resolutionType !== "KeepServer") {
      throw new Error("این تعارض قبلاً با تصمیم دیگری تعیین تکلیف شده است.");
    }
    await replaceOperations([{ ...operation, status: "resolved", lastError: "نسخه رسمی سرور پذیرفته شد." }]);
    return;
  }

  if (operation.serverRevision === undefined) {
    throw new Error("نسخه فعلی سرور مشخص نیست؛ این تعارض باید توسط مدیر پروژه بررسی شود.");
  }

  const replacementOperationId = operation.resolutionReplacementOperationId ?? createUlid();
  if (!operation.resolutionReplacementOperationId) {
    await replaceOperations([{ ...operation, resolutionReplacementOperationId: replacementOperationId }]);
  }
  const replacement: ClientOperation = {
    ...operation,
    operationId: replacementOperationId,
    baseRevision: operation.serverRevision,
    offlineLeaseId: manifest.leaseId,
    authorizationVersion: manifest.authorizationVersion,
    localSequence: nextLocalSequence(),
    correlationId: crypto.randomUUID(),
    createdAtDevice: new Date().toISOString(),
    status: "queued",
    attemptCount: 0,
    lastAttemptAt: undefined,
    serverRevision: undefined,
    conflictId: undefined,
    resolutionReplacementOperationId: undefined,
    lastError: undefined,
  };
  if (serverConflict.status === "Open") {
    await resolveServerConflict(
      apiBaseUrl,
      manifest,
      operation.conflictId,
      serverConflict.revision,
      "Reapply",
      replacementOperationId,
    );
  } else if (serverConflict.resolutionType !== "Reapply" ||
      serverConflict.replacementOperationId !== replacementOperationId) {
    throw new Error("این تعارض قبلاً با تصمیم یا شناسه جایگزین دیگری تعیین تکلیف شده است.");
  }
  await replaceOperations([
    { ...operation, status: "resolved", lastError: "قصد محلی با شناسه جدید دوباره در صف قرار گرفت." },
    replacement,
  ]);
}

export function getOrCreateDailyReportId(projectId: string, reportDate: string): string {
  if (typeof localStorage === "undefined") {
    throw new Error("فضای ذخیره‌سازی محلی در این مرورگر در دسترس نیست.");
  }

  const key = scopedStorageKey(`pmcs-daily-report:${projectId}:${reportDate}`);
  const existing = localStorage.getItem(key);
  if (existing) {
    return existing;
  }

  const generated = crypto.randomUUID();
  localStorage.setItem(key, generated);
  return generated;
}

export function findDailyReportId(projectId: string, reportDate: string): string | null {
  if (typeof localStorage === "undefined") {
    return null;
  }

  return localStorage.getItem(scopedStorageKey(`pmcs-daily-report:${projectId}:${reportDate}`));
}

async function countQueuedForProject(projectId: string): Promise<number> {
  const database = await openFieldDatabase();
  const operations = await readOperationsByStatus(database, "queued", 1000);
  database.close();
  return operations.filter((item) => item.projectId === projectId).length;
}

function readOperation(database: IDBDatabase, operationId: string): Promise<ClientOperation | null> {
  return new Promise((resolve, reject) => {
    const transaction = database.transaction(operationStore, "readonly");
    const request = transaction.objectStore(operationStore).get(operationId);
    request.onsuccess = () => resolve((request.result as ClientOperation | undefined) ?? null);
    request.onerror = () => reject(request.error ?? new Error("خواندن عملیات محلی ممکن نشد."));
  });
}

async function replaceOperations(operations: readonly ClientOperation[]): Promise<void> {
  const database = await openFieldDatabase();
  await writeOperations(database, operations);
  database.close();
}

function nextLocalSequence(): number {
  const key = scopedStorageKey("pmcs-operation-sequence");
  const current = Number.parseInt(localStorage.getItem(key) ?? "0", 10);
  const next = Number.isSafeInteger(current) && current >= 0 ? current + 1 : 1;
  localStorage.setItem(key, String(next));
  return next;
}

function readOperationsByStatus(
  database: IDBDatabase,
  status: OperationStatus,
  limit: number,
): Promise<ClientOperation[]> {
  return new Promise((resolve, reject) => {
    const operations: ClientOperation[] = [];
    const transaction = database.transaction(operationStore, "readonly");
    const request = transaction.objectStore(operationStore).index("by-status").openCursor(status);
    request.onsuccess = () => {
      const cursor = request.result;
      if (!cursor || operations.length >= limit) {
        resolve(operations);
        return;
      }

      operations.push(cursor.value as ClientOperation);
      cursor.continue();
    };
    request.onerror = () => reject(request.error ?? new Error("خواندن صف عملیات محلی ممکن نشد."));
  });
}

function writeOperations(database: IDBDatabase, operations: readonly ClientOperation[]): Promise<void> {
  return new Promise((resolve, reject) => {
    const transaction = database.transaction(operationStore, "readwrite");
    const store = transaction.objectStore(operationStore);
    operations.forEach((operation) => store.put(operation));
    transaction.oncomplete = () => resolve();
    transaction.onerror = () => reject(transaction.error ?? new Error("ثبت عملیات در پایگاه داده محلی ناموفق بود."));
    transaction.onabort = () => reject(transaction.error ?? new Error("ثبت عملیات در پایگاه داده محلی متوقف شد."));
  });
}

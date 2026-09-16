import {
  readAttachmentDiagnostics,
  recoverInterruptedAttachments,
  syncPendingAttachments,
  type AttachmentSyncSummary,
} from "./attachment-store.ts";
import {
  currentLocalIdentityScope,
  openFieldDatabase,
  syncRecoveryStoreName,
} from "./field-database.ts";
import { ApiRequestError, toUserMessage } from "./localization.ts";
import {
  readOperationDiagnostics,
  recoverInterruptedOperations,
  syncPendingOperations,
  type SyncSummary,
} from "./operation-store.ts";
import {
  countPendingQualitySafetyIntakes,
  syncOfflineQualitySafetyIntakes,
} from "./quality-safety-offline.ts";
import {
  readLocalSyncDiagnostics,
  readServerSyncDiagnostics,
} from "./sync-client.ts";

export type SyncRecoveryPhase =
  | "offline"
  | "recovering"
  | "pushing"
  | "uploading"
  | "verifying"
  | "succeeded"
  | "attention"
  | "retry-scheduled"
  | "blocked";

export type SyncConsistency =
  | "consistent"
  | "pending-upload"
  | "pending-download"
  | "attention"
  | "diverged"
  | "unavailable";

export type SyncRecoveryTrigger = "startup" | "reconnect" | "manual" | "retry" | "test";

export interface SyncConsistencyInput {
  readonly deviceStatus: "Active" | "Revoked";
  readonly leaseExpiresAt?: string;
  readonly localCheckpointSequence: number;
  readonly serverCheckpointSequence: number;
  readonly serverWatermark: number;
  readonly pendingOperations: number;
  readonly pendingAttachments: number;
  readonly pendingQualitySafetyIntakes: number;
  readonly localConflicts: number;
  readonly localRejected: number;
  readonly serverOpenConflicts: number;
  readonly serverRejected: number;
}

export interface LocalSyncRecoveryState {
  readonly key: string;
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly cycleId: string;
  readonly trigger: SyncRecoveryTrigger;
  readonly phase: SyncRecoveryPhase;
  readonly attempt: number;
  readonly startedAt: string;
  readonly updatedAt: string;
  readonly completedAt?: string;
  readonly nextRetryAt?: string;
  readonly lastErrorCode?: string;
  readonly lastErrorMessage?: string;
  readonly recoveredOperations: number;
  readonly recoveredAttachments: number;
  readonly appliedOperations: number;
  readonly uploadedAttachments: number;
  readonly syncedQualitySafetyIntakes: number;
  readonly pendingOperations: number;
  readonly pendingAttachments: number;
  readonly pendingQualitySafetyIntakes: number;
  readonly openConflicts: number;
  readonly rejectedItems: number;
  readonly localCheckpointSequence: number;
  readonly serverCheckpointSequence: number;
  readonly serverWatermark: number;
  readonly consistency: SyncConsistency;
}

export interface SyncRecoveryResult {
  readonly state: LocalSyncRecoveryState;
  readonly operations: SyncSummary;
  readonly attachments: AttachmentSyncSummary;
}

export interface RetryClassification {
  readonly retryable: boolean;
  readonly code: string;
  readonly retryAfterSeconds?: number;
}

const activeRecoveries = new Map<string, Promise<SyncRecoveryResult>>();
const retryScheduleMs = [5_000, 15_000, 45_000, 120_000, 300_000] as const;
const emptyOperationSummary: SyncSummary = {
  sent: 0,
  applied: 0,
  conflicts: 0,
  rejected: 0,
  deferred: 0,
  remaining: 0,
};
const emptyAttachmentSummary: AttachmentSyncSummary = {
  sent: 0,
  uploaded: 0,
  rejected: 0,
  deferred: 0,
  remaining: 0,
};

export function runSyncRecoveryCycle(
  apiBaseUrl: string,
  projectId: string,
  trigger: SyncRecoveryTrigger,
): Promise<SyncRecoveryResult> {
  const scope = currentLocalIdentityScope();
  const activeKey = `${scope.tenantId}:${scope.userId}:${projectId}`;
  const active = activeRecoveries.get(activeKey);
  if (active) return active;

  const recovery = performSyncRecoveryCycle(apiBaseUrl, projectId, trigger).finally(() => {
    activeRecoveries.delete(activeKey);
  });
  activeRecoveries.set(activeKey, recovery);
  return recovery;
}

export function evaluateSyncConsistency(input: SyncConsistencyInput, now = Date.now()): SyncConsistency {
  if (input.deviceStatus === "Revoked" ||
      (input.leaseExpiresAt && Date.parse(input.leaseExpiresAt) <= now)) {
    return "attention";
  }
  if (input.localConflicts > 0 || input.localRejected > 0 ||
      input.serverOpenConflicts > 0 || input.serverRejected > 0) {
    return "attention";
  }
  if (input.pendingOperations > 0 || input.pendingAttachments > 0 || input.pendingQualitySafetyIntakes > 0) {
    return "pending-upload";
  }
  if (input.localCheckpointSequence > input.serverWatermark ||
      input.localCheckpointSequence !== input.serverCheckpointSequence) {
    return "diverged";
  }
  if (input.localCheckpointSequence < input.serverWatermark) {
    return "pending-download";
  }
  return "consistent";
}

export function classifySyncFailure(error: unknown): RetryClassification {
  if (error instanceof ApiRequestError) {
    const retryable = error.status === 408 || error.status === 425 || error.status === 429 ||
      error.status >= 500 || error.code === "sync.session.invalid_or_expired" ||
      error.code === "sync.checkpoint.offer.invalid";
    return {
      retryable,
      code: error.code ?? `http.${error.status}`,
      retryAfterSeconds: error.retryAfterSeconds,
    };
  }
  if (error instanceof TypeError) {
    return { retryable: true, code: "network.unavailable" };
  }
  return { retryable: false, code: "sync.local.failure" };
}

export function retryDelayMilliseconds(attempt: number, retryAfterSeconds?: number): number {
  if (retryAfterSeconds !== undefined && Number.isFinite(retryAfterSeconds)) {
    return Math.min(900_000, Math.max(1_000, retryAfterSeconds * 1_000));
  }
  const normalizedAttempt = Number.isInteger(attempt) && attempt > 0 ? attempt : 1;
  return retryScheduleMs[Math.min(normalizedAttempt - 1, retryScheduleMs.length - 1)];
}

export function isRecoveryRetryDue(state: LocalSyncRecoveryState | null, now = Date.now()): boolean {
  return state?.phase === "retry-scheduled" &&
    state.nextRetryAt !== undefined &&
    Date.parse(state.nextRetryAt) <= now;
}

export async function readSyncRecoveryState(projectId: string): Promise<LocalSyncRecoveryState | null> {
  const database = await openFieldDatabase();
  try {
    return await new Promise((resolve, reject) => {
      const transaction = database.transaction(syncRecoveryStoreName, "readonly");
      const request = transaction.objectStore(syncRecoveryStoreName).get(recoveryKey(projectId));
      request.onsuccess = () => resolve((request.result as LocalSyncRecoveryState | undefined) ?? null);
      request.onerror = () => reject(request.error ?? new Error("خواندن وضعیت بازیابی همگام‌سازی ممکن نشد."));
    });
  } finally {
    database.close();
  }
}

async function performSyncRecoveryCycle(
  apiBaseUrl: string,
  projectId: string,
  trigger: SyncRecoveryTrigger,
): Promise<SyncRecoveryResult> {
  const scope = currentLocalIdentityScope();
  const previous = await readSyncRecoveryState(projectId);
  const startedAt = new Date().toISOString();
  const attempt = previous?.phase === "retry-scheduled" ? previous.attempt + 1 : 1;
  let operations = emptyOperationSummary;
  let attachments = emptyAttachmentSummary;
  let state = baseState(scope.tenantId, scope.userId, projectId, trigger, attempt, startedAt);

  if (typeof navigator !== "undefined" && !navigator.onLine) {
    state = { ...state, phase: "offline", updatedAt: new Date().toISOString() };
    await writeSyncRecoveryState(state);
    return { state, operations, attachments };
  }

  try {
    state = await updatePhase(state, "recovering");
    const [recoveredOperations, recoveredAttachments] = await Promise.all([
      recoverInterruptedOperations(projectId),
      recoverInterruptedAttachments(projectId),
    ]);
    state = { ...state, recoveredOperations, recoveredAttachments };

    state = await updatePhase(state, "pushing");
    operations = await syncPendingOperations(apiBaseUrl, projectId);

    state = await updatePhase(state, "uploading");
    const syncedQualitySafetyIntakes = await syncOfflineQualitySafetyIntakes(apiBaseUrl, projectId);
    attachments = await syncPendingAttachments(apiBaseUrl, projectId);

    state = await updatePhase({ ...state, syncedQualitySafetyIntakes }, "verifying");
    const [local, server, operationDiagnostics, attachmentDiagnostics, pendingQualitySafetyIntakes] = await Promise.all([
      readLocalSyncDiagnostics(projectId),
      readServerSyncDiagnostics(apiBaseUrl, projectId),
      readOperationDiagnostics(projectId),
      readAttachmentDiagnostics(projectId),
      countPendingQualitySafetyIntakes(projectId),
    ]);
    const pendingOperations = operationDiagnostics.queued + operationDiagnostics.syncing;
    const pendingAttachments = attachmentDiagnostics.queued + attachmentDiagnostics.uploading;
    const openConflicts = Math.max(operationDiagnostics.conflicts, server.openConflictCount);
    const rejectedItems = operationDiagnostics.rejected + attachmentDiagnostics.rejected;
    const consistency = evaluateSyncConsistency({
      deviceStatus: server.deviceStatus,
      leaseExpiresAt: server.leaseExpiresAt,
      localCheckpointSequence: local.checkpointSequence,
      serverCheckpointSequence: server.lastCheckpointSequence,
      serverWatermark: server.serverWatermark,
      pendingOperations,
      pendingAttachments,
      pendingQualitySafetyIntakes,
      localConflicts: operationDiagnostics.conflicts,
      localRejected: rejectedItems,
      serverOpenConflicts: server.openConflictCount,
      serverRejected: server.recentRejectedOperationCount,
    });
    const completedAt = new Date().toISOString();
    state = {
      ...state,
      phase: consistency === "consistent" ? "succeeded" : "attention",
      updatedAt: completedAt,
      completedAt,
      nextRetryAt: undefined,
      lastErrorCode: undefined,
      lastErrorMessage: undefined,
      appliedOperations: operations.applied,
      uploadedAttachments: attachments.uploaded,
      syncedQualitySafetyIntakes,
      pendingOperations,
      pendingAttachments,
      pendingQualitySafetyIntakes,
      openConflicts,
      rejectedItems,
      localCheckpointSequence: local.checkpointSequence,
      serverCheckpointSequence: server.lastCheckpointSequence,
      serverWatermark: server.serverWatermark,
      consistency,
    };
    await writeSyncRecoveryState(state);
    return { state, operations, attachments };
  } catch (error) {
    const classification = classifySyncFailure(error);
    const updatedAt = new Date().toISOString();
    const nextRetryAt = classification.retryable
      ? new Date(Date.now() + retryDelayMilliseconds(attempt, classification.retryAfterSeconds)).toISOString()
      : undefined;
    state = {
      ...state,
      phase: classification.retryable ? "retry-scheduled" : "blocked",
      updatedAt,
      nextRetryAt,
      lastErrorCode: classification.code,
      lastErrorMessage: toUserMessage(error, "همگام‌سازی کامل نشد؛ داده محلی محفوظ است."),
      appliedOperations: operations.applied,
      uploadedAttachments: attachments.uploaded,
      consistency: "unavailable",
    };
    await writeSyncRecoveryState(state);
    return { state, operations, attachments };
  }
}

function baseState(
  tenantId: string,
  userId: string,
  projectId: string,
  trigger: SyncRecoveryTrigger,
  attempt: number,
  startedAt: string,
): LocalSyncRecoveryState {
  return {
    key: recoveryKey(projectId),
    tenantId,
    userId,
    projectId,
    cycleId: crypto.randomUUID(),
    trigger,
    phase: "recovering",
    attempt,
    startedAt,
    updatedAt: startedAt,
    recoveredOperations: 0,
    recoveredAttachments: 0,
    appliedOperations: 0,
    uploadedAttachments: 0,
    syncedQualitySafetyIntakes: 0,
    pendingOperations: 0,
    pendingAttachments: 0,
    pendingQualitySafetyIntakes: 0,
    openConflicts: 0,
    rejectedItems: 0,
    localCheckpointSequence: 0,
    serverCheckpointSequence: 0,
    serverWatermark: 0,
    consistency: "unavailable",
  };
}

async function updatePhase(
  state: LocalSyncRecoveryState,
  phase: SyncRecoveryPhase,
): Promise<LocalSyncRecoveryState> {
  const updated = { ...state, phase, updatedAt: new Date().toISOString() };
  await writeSyncRecoveryState(updated);
  return updated;
}

async function writeSyncRecoveryState(state: LocalSyncRecoveryState): Promise<void> {
  const database = await openFieldDatabase();
  try {
    await new Promise<void>((resolve, reject) => {
      const transaction = database.transaction(syncRecoveryStoreName, "readwrite");
      transaction.objectStore(syncRecoveryStoreName).put(state);
      transaction.oncomplete = () => resolve();
      transaction.onerror = () => reject(transaction.error ?? new Error("ثبت وضعیت بازیابی همگام‌سازی ناموفق بود."));
      transaction.onabort = () => reject(transaction.error ?? new Error("ثبت وضعیت بازیابی همگام‌سازی متوقف شد."));
    });
  } finally {
    database.close();
  }
}

function recoveryKey(projectId: string): string {
  return `recovery:${projectId}`;
}

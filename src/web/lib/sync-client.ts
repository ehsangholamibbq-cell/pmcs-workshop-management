import {
  appliedChangeStoreName,
  currentLocalIdentityScope,
  getOrCreateDeviceId,
  openFieldDatabase,
  syncMetadataStoreName,
} from "./field-database.ts";
import { ensureApiSuccess } from "./localization.ts";

export const syncProtocolVersion = 2;
export const syncLocalSchemaVersion = 5;
export const syncClientVersion = "0.1.0";

export interface SyncQueueSummary {
  readonly pendingOperations: number;
  readonly pendingAttachments: number;
  readonly pendingAttachmentBytes: number;
  readonly oldestOperationAt?: string;
}

export interface LocalSyncManifest {
  readonly key: string;
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly deviceId: string;
  readonly sessionId: string;
  readonly sessionExpiresAt: string;
  readonly leaseId: string;
  readonly authorizationVersion: number;
  readonly leaseIssuedAt: string;
  readonly leaseExpiresAt: string;
  readonly checkpoint: string | null;
  readonly serverWatermark: number;
  readonly policyVersion: string;
  readonly protocolVersion: number;
  readonly localSchemaVersion: number;
  readonly bootstrapRequired: boolean;
  readonly clockSkewSeconds: number;
  readonly clockSkewWarning: boolean;
  readonly allowedOperations: readonly string[];
  readonly entityTypes: readonly string[];
  readonly scopes: readonly string[];
  readonly classification: string;
  readonly lastHandshakeAt: string;
  readonly lastPushAt?: string;
  readonly lastPullAt?: string;
}

export interface LocalOperationAuthorization {
  readonly offlineLeaseId: string;
  readonly authorizationVersion: number;
}

interface HandshakeResponse {
  readonly sessionId: string;
  readonly sessionExpiresAt: string;
  readonly serverAt: string;
  readonly lease: {
    readonly leaseId: string;
    readonly authorizationVersion: number;
    readonly issuedAt: string;
    readonly expiresAt: string;
  };
  readonly policyVersion: string;
  readonly protocolVersion: number;
  readonly localSchemaVersion: number;
  readonly bootstrapRequired: boolean;
  readonly serverWatermark: number;
  readonly currentCheckpoint: string | null;
  readonly clockSkewSeconds: number;
  readonly clockSkewWarning: boolean;
  readonly allowedOperations: readonly string[];
  readonly dataset: {
    readonly entityTypes: readonly string[];
    readonly scopes: readonly string[];
    readonly classification: string;
  };
}

interface PullResponse {
  readonly serverAt: string;
  readonly serverWatermark: number;
  readonly checkpointOffer: string;
  readonly hasMore: boolean;
  readonly changes: readonly AppliedServerChange[];
}

interface AdvanceCheckpointResponse {
  readonly checkpoint: string;
  readonly sequence: number;
  readonly advancedAt: string;
}

export interface AppliedServerChange {
  readonly sequence: number;
  readonly changeId: string;
  readonly entityType: string;
  readonly entityId: string;
  readonly changeType: string;
  readonly revision?: number;
  readonly projection: unknown;
  readonly effectiveAt: string;
  readonly serverAt: string;
  readonly correlationId: string;
  readonly projectId?: string;
}

export interface SyncDeviceModel {
  readonly registrationId: string;
  readonly deviceId: string;
  readonly displayName: string;
  readonly platform: string;
  readonly appVersion: string;
  readonly status: "Active" | "Revoked";
  readonly registeredAt: string;
  readonly lastSeenAt: string;
  readonly revokedAt?: string;
  readonly revision: number;
}

export interface LocalSyncDiagnostics {
  readonly leaseExpiresAt?: string;
  readonly lastHandshakeAt?: string;
  readonly lastPushAt?: string;
  readonly lastPullAt?: string;
  readonly checkpoint: string | null;
  readonly serverWatermark: number;
  readonly clockSkewWarning: boolean;
  readonly bootstrapRequired: boolean;
  readonly policyVersion?: string;
}

const activeHandshakes = new Map<string, Promise<LocalSyncManifest>>();

export function prepareSyncSession(
  apiBaseUrl: string,
  projectId: string,
  queue: SyncQueueSummary,
): Promise<LocalSyncManifest> {
  const active = activeHandshakes.get(projectId);
  if (active) return active;
  const handshake = performHandshake(apiBaseUrl, projectId, queue).finally(() => {
    activeHandshakes.delete(projectId);
  });
  activeHandshakes.set(projectId, handshake);
  return handshake;
}

async function performHandshake(
  apiBaseUrl: string,
  projectId: string,
  queue: SyncQueueSummary,
): Promise<LocalSyncManifest> {
  const scope = currentLocalIdentityScope();
  const deviceId = getOrCreateDeviceId();
  const current = await readSyncManifest(projectId);
  const response = await fetch(`${normalizedBaseUrl(apiBaseUrl)}/api/v1/sync/handshake`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      deviceId,
      projectId,
      deviceName: browserDeviceName(),
      platform: browserPlatform(),
      appVersion: syncClientVersion,
      protocolVersion: syncProtocolVersion,
      localSchemaVersion: syncLocalSchemaVersion,
      lastCheckpoint: current?.checkpoint ?? null,
      deviceTime: new Date().toISOString(),
      queue,
    }),
  });
  await ensureApiSuccess(response);
  const model = await response.json() as HandshakeResponse;
  const manifest: LocalSyncManifest = {
    key: manifestKey(projectId),
    tenantId: scope.tenantId,
    userId: scope.userId,
    projectId,
    deviceId,
    sessionId: model.sessionId,
    sessionExpiresAt: model.sessionExpiresAt,
    leaseId: model.lease.leaseId,
    authorizationVersion: model.lease.authorizationVersion,
    leaseIssuedAt: model.lease.issuedAt,
    leaseExpiresAt: model.lease.expiresAt,
    checkpoint: model.currentCheckpoint,
    serverWatermark: model.serverWatermark,
    policyVersion: model.policyVersion,
    protocolVersion: model.protocolVersion,
    localSchemaVersion: model.localSchemaVersion,
    bootstrapRequired: model.bootstrapRequired,
    clockSkewSeconds: model.clockSkewSeconds,
    clockSkewWarning: model.clockSkewWarning,
    allowedOperations: model.allowedOperations,
    entityTypes: model.dataset.entityTypes,
    scopes: model.dataset.scopes,
    classification: model.dataset.classification,
    lastHandshakeAt: model.serverAt,
    lastPushAt: current?.lastPushAt,
    lastPullAt: current?.lastPullAt,
  };
  await writeSyncManifest(manifest);
  return manifest;
}

export async function requireOfflineAuthorization(
  projectId: string,
  commandType: string,
): Promise<LocalOperationAuthorization> {
  const manifest = await readSyncManifest(projectId);
  const scope = currentLocalIdentityScope();
  if (!manifest || manifest.tenantId !== scope.tenantId || manifest.userId !== scope.userId ||
      manifest.deviceId !== getOrCreateDeviceId()) {
    throw new Error("برای ثبت آفلاین، ابتدا یک‌بار آنلاین وارد پروژه شوید.");
  }

  if (new Date(manifest.leaseExpiresAt).getTime() <= Date.now()) {
    throw new Error("مجوز آفلاین این پروژه منقضی شده است؛ برای تمدید به سرور متصل شوید.");
  }

  if (!manifest.allowedOperations.includes(commandType)) {
    throw new Error("این عملیات در محدوده مجاز آفلاین این پروژه نیست.");
  }

  return {
    offlineLeaseId: manifest.leaseId,
    authorizationVersion: manifest.authorizationVersion,
  };
}

export async function pullAuthorizedChanges(
  apiBaseUrl: string,
  manifest: LocalSyncManifest,
): Promise<LocalSyncManifest> {
  let current = manifest;
  let hasMore = true;
  while (hasMore) {
    const query = new URLSearchParams({ projectId: current.projectId, limit: "100" });
    if (current.checkpoint) query.set("checkpoint", current.checkpoint);
    const pull = await fetch(`${normalizedBaseUrl(apiBaseUrl)}/api/v1/sync/pull?${query}`, {
      headers: syncSessionHeaders(current),
      cache: "no-store",
    });
    await ensureApiSuccess(pull);
    const page = await pull.json() as PullResponse;

    await applyServerChanges(current.projectId, page.changes);
    const acknowledgment = await fetch(`${normalizedBaseUrl(apiBaseUrl)}/api/v1/sync/checkpoints`, {
      method: "POST",
      headers: {
        ...syncSessionHeaders(current),
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ projectId: current.projectId, checkpointOffer: page.checkpointOffer }),
    });
    await ensureApiSuccess(acknowledgment);
    const advanced = await acknowledgment.json() as AdvanceCheckpointResponse;
    current = {
      ...current,
      checkpoint: advanced.checkpoint,
      serverWatermark: page.serverWatermark,
      bootstrapRequired: false,
      lastPullAt: advanced.advancedAt,
    };
    await writeSyncManifest(current);
    hasMore = page.hasMore;
  }
  return current;
}

export async function noteSuccessfulPush(manifest: LocalSyncManifest, pushedAt: string): Promise<LocalSyncManifest> {
  const updated = { ...manifest, lastPushAt: pushedAt };
  await writeSyncManifest(updated);
  return updated;
}

export async function readSyncManifest(projectId: string): Promise<LocalSyncManifest | null> {
  const database = await openFieldDatabase();
  const value = await readRecord<LocalSyncManifest>(database, syncMetadataStoreName, manifestKey(projectId));
  database.close();
  return value;
}

export async function readLocalSyncDiagnostics(projectId: string): Promise<LocalSyncDiagnostics> {
  const manifest = await readSyncManifest(projectId);
  return {
    leaseExpiresAt: manifest?.leaseExpiresAt,
    lastHandshakeAt: manifest?.lastHandshakeAt,
    lastPushAt: manifest?.lastPushAt,
    lastPullAt: manifest?.lastPullAt,
    checkpoint: manifest?.checkpoint ?? null,
    serverWatermark: manifest?.serverWatermark ?? 0,
    clockSkewWarning: manifest?.clockSkewWarning ?? false,
    bootstrapRequired: manifest?.bootstrapRequired ?? false,
    policyVersion: manifest?.policyVersion,
  };
}

export async function listSyncDevices(apiBaseUrl: string): Promise<readonly SyncDeviceModel[]> {
  const response = await fetch(`${normalizedBaseUrl(apiBaseUrl)}/api/v1/sync/devices`, { cache: "no-store" });
  await ensureApiSuccess(response);
  return response.json() as Promise<readonly SyncDeviceModel[]>;
}

export async function revokeSyncDevice(apiBaseUrl: string, device: SyncDeviceModel, reason: string): Promise<void> {
  const response = await fetch(
    `${normalizedBaseUrl(apiBaseUrl)}/api/v1/sync/devices/${device.registrationId}/revoke`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ baseRevision: device.revision, reason }),
    },
  );
  await ensureApiSuccess(response);
}

export async function resolveServerConflict(
  apiBaseUrl: string,
  manifest: LocalSyncManifest,
  conflictId: string,
  baseRevision: number,
  resolution: "KeepServer" | "Reapply",
  replacementOperationId?: string,
): Promise<void> {
  const response = await fetch(
    `${normalizedBaseUrl(apiBaseUrl)}/api/v1/sync/conflicts/${conflictId}/resolve`,
    {
      method: "POST",
      headers: {
        ...syncSessionHeaders(manifest),
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        baseRevision,
        resolution,
        replacementOperationId: replacementOperationId ?? null,
        comment: resolution === "KeepServer"
          ? "نسخه رسمی سرور پس از بازبینی کاربر پذیرفته شد."
          : "قصد محلی با شناسه جدید و روی نسخه فعلی سرور دوباره در صف قرار گرفت.",
      }),
    },
  );
  await ensureApiSuccess(response);
}

function syncSessionHeaders(manifest: LocalSyncManifest): HeadersInit {
  return { "X-Pmcs-Sync-Session": manifest.sessionId };
}

async function applyServerChanges(projectId: string, changes: readonly AppliedServerChange[]): Promise<void> {
  if (changes.length === 0) return;
  const database = await openFieldDatabase();
  await new Promise<void>((resolve, reject) => {
    const transaction = database.transaction(appliedChangeStoreName, "readwrite");
    const store = transaction.objectStore(appliedChangeStoreName);
    changes.forEach((change) => store.put({ ...change, projectId }));
    transaction.oncomplete = () => resolve();
    transaction.onerror = () => reject(transaction.error ?? new Error("اعمال تغییرهای سرور روی دستگاه ناموفق بود."));
    transaction.onabort = () => reject(transaction.error ?? new Error("اعمال تغییرهای سرور متوقف شد."));
  });
  database.close();
}

async function writeSyncManifest(manifest: LocalSyncManifest): Promise<void> {
  const database = await openFieldDatabase();
  await new Promise<void>((resolve, reject) => {
    const transaction = database.transaction(syncMetadataStoreName, "readwrite");
    transaction.objectStore(syncMetadataStoreName).put(manifest);
    transaction.oncomplete = () => resolve();
    transaction.onerror = () => reject(transaction.error ?? new Error("ثبت وضعیت همگام‌سازی ناموفق بود."));
    transaction.onabort = () => reject(transaction.error ?? new Error("ثبت وضعیت همگام‌سازی متوقف شد."));
  });
  database.close();
}

function readRecord<T>(database: IDBDatabase, storeName: string, key: IDBValidKey): Promise<T | null> {
  return new Promise((resolve, reject) => {
    const transaction = database.transaction(storeName, "readonly");
    const request = transaction.objectStore(storeName).get(key);
    request.onsuccess = () => resolve((request.result as T | undefined) ?? null);
    request.onerror = () => reject(request.error ?? new Error("خواندن وضعیت محلی ممکن نشد."));
  });
}

function manifestKey(projectId: string): string {
  return `manifest:${projectId}`;
}

function browserDeviceName(): string {
  if (typeof navigator === "undefined") return "مرورگر کارگاه";
  return navigator.userAgent.includes("Mobile") ? "مرورگر دستگاه همراه" : "مرورگر رایانه";
}

function browserPlatform(): string {
  if (typeof navigator === "undefined") return "unknown";
  return navigator.userAgent.slice(0, 120).replace(/[\u0000-\u001f\u007f]/gu, " ").trim() || "unknown";
}

function normalizedBaseUrl(value: string): string {
  return value.replace(/\/$/u, "");
}

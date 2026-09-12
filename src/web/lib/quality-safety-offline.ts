import { currentLocalIdentityScope, openFieldDatabase, qualitySafetyIntakeStoreName } from "./field-database.ts";
import { ensureApiSuccess } from "./localization.ts";
import type { DataClassification, InitialSeverity, IntakeKind } from "./quality-safety.ts";
import { prepareSyncSession, requireOfflineAuthorization } from "./sync-client.ts";

export interface OfflineQualitySafetyIntakePayload {
  readonly kind: IntakeKind; readonly observedAt: string; readonly location: string; readonly facts: string;
  readonly initialSeverity: InitialSeverity; readonly immediateAction?: string;
  readonly evidenceReferences: readonly string[]; readonly classification: DataClassification;
}
interface OfflineQualitySafetyIntake extends OfflineQualitySafetyIntakePayload {
  readonly draftId: string; readonly idempotencyKey: string; readonly tenantId: string; readonly userId: string;
  readonly projectId: string; readonly createdAtDevice: string;
  readonly offlineLeaseId: string; readonly authorizationVersion: number;
}

export async function enqueueOfflineQualitySafetyIntake(projectId: string, payload: OfflineQualitySafetyIntakePayload) {
  const scope = currentLocalIdentityScope();
  const authorization = await requireOfflineAuthorization(projectId, "CaptureQualitySafetyIntake");
  const draft: OfflineQualitySafetyIntake = { ...payload, draftId: crypto.randomUUID(), idempotencyKey: crypto.randomUUID(),
    tenantId: scope.tenantId, userId: scope.userId, projectId, createdAtDevice: new Date().toISOString(), ...authorization };
  const database = await openFieldDatabase();
  await transaction(database, "readwrite", (store) => store.put(draft)); database.close(); return draft.draftId;
}

export async function syncOfflineQualitySafetyIntakes(baseUrl: string, projectId: string) {
  const scope = currentLocalIdentityScope(); const database = await openFieldDatabase();
  try {
    const drafts = await readProject(database, projectId);
    if (drafts.length === 0) return 0;
    const manifest = await prepareSyncSession(baseUrl, projectId, {
      pendingOperations: drafts.length, pendingAttachments: 0, pendingAttachmentBytes: 0,
      oldestOperationAt: drafts.map((item) => item.createdAtDevice).sort()[0],
    });
    let applied = 0;
    for (const draft of drafts) {
      if (draft.tenantId !== scope.tenantId || draft.userId !== scope.userId) continue;
      const response = await fetch(`${baseUrl.replace(/\/$/, "")}/api/v1/projects/${projectId}/quality-safety/intakes`, {
        method: "POST", headers: { "Content-Type": "application/json", "Idempotency-Key": draft.idempotencyKey,
          "X-Tenant-Id": scope.tenantId, "X-User-Id": scope.userId,
          "X-Pmcs-Sync-Session": manifest.sessionId },
        body: JSON.stringify({ clientGeneratedId: draft.draftId, kind: draft.kind, observedAt: draft.observedAt,
          location: draft.location, facts: draft.facts, initialSeverity: draft.initialSeverity,
          immediateAction: draft.immediateAction?.trim() || null, evidenceReferences: draft.evidenceReferences,
          classification: draft.classification }),
      });
      await ensureApiSuccess(response);
      await transaction(database, "readwrite", (store) => store.delete(draft.draftId)); applied += 1;
    }
    return applied;
  } finally { database.close(); }
}

function readProject(database: IDBDatabase, projectId: string): Promise<OfflineQualitySafetyIntake[]> {
  return new Promise((resolve, reject) => { const result: OfflineQualitySafetyIntake[] = [];
    const request = database.transaction(qualitySafetyIntakeStoreName, "readonly").objectStore(qualitySafetyIntakeStoreName).index("by-project").openCursor(IDBKeyRange.only(projectId));
    request.onsuccess = () => { const cursor = request.result; if (!cursor) { resolve(result); return; } result.push(cursor.value as OfflineQualitySafetyIntake); cursor.continue(); };
    request.onerror = () => reject(request.error ?? new Error("خواندن ثبت‌های محلی کیفیت و ایمنی ممکن نشد.")); });
}
function transaction(database: IDBDatabase, mode: IDBTransactionMode, operation: (store: IDBObjectStore) => IDBRequest) {
  return new Promise<void>((resolve, reject) => { const tx = database.transaction(qualitySafetyIntakeStoreName, mode); operation(tx.objectStore(qualitySafetyIntakeStoreName));
    tx.oncomplete = () => resolve(); tx.onerror = () => reject(tx.error ?? new Error("ذخیره محلی کیفیت و ایمنی ناموفق بود.")); tx.onabort = () => reject(tx.error ?? new Error("ذخیره محلی کیفیت و ایمنی متوقف شد.")); });
}

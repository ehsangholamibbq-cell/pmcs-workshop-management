import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const syncClient = readFileSync(new URL("../lib/sync-client.ts", import.meta.url), "utf8");
const operationStore = readFileSync(new URL("../lib/operation-store.ts", import.meta.url), "utf8");
const fieldDatabase = readFileSync(new URL("../lib/field-database.ts", import.meta.url), "utf8");

test("sync handshake carries device, schema, checkpoint and bounded queue context", () => {
  assert.match(syncClient, /deviceId,[\s\S]*projectId,[\s\S]*protocolVersion:[\s\S]*localSchemaVersion:/u);
  assert.match(syncClient, /lastCheckpoint: current\?\.checkpoint \?\? null/u);
  assert.match(syncClient, /pendingAttachmentBytes/u);
  assert.ok(syncClient.includes("const handshakeKey = `${scope.tenantId}:${scope.userId}:${projectId}`"));
});

test("pulled changes are persisted before the server checkpoint is acknowledged", () => {
  const applyIndex = syncClient.indexOf("await applyServerChanges(current.projectId, page.changes)");
  const acknowledgeIndex = syncClient.indexOf("/api/v1/sync/checkpoints", applyIndex);
  assert.ok(applyIndex > 0);
  assert.ok(acknowledgeIndex > applyIndex);
});

test("offline push sends the immutable lease, sequence and correlation envelope", () => {
  assert.match(operationStore, /"X-Pmcs-Sync-Session": manifest\.sessionId/u);
  assert.match(operationStore, /offlineLeaseId: operation\.offlineLeaseId/u);
  assert.match(operationStore, /authorizationVersion: operation\.authorizationVersion/u);
  assert.match(operationStore, /localSequence: operation\.localSequence/u);
  assert.match(operationStore, /correlationId: operation\.correlationId/u);
  assert.match(operationStore, /const activeSyncs = new Map<string, Promise<SyncSummary>>/u);
  assert.match(operationStore, /tenantId.*userId.*projectId/u);
});

test("local schema keeps sync metadata and idempotent applied changes in separate stores", () => {
  assert.match(fieldDatabase, /fieldDatabaseVersion = 6/u);
  assert.match(fieldDatabase, /sync-metadata/u);
  assert.match(fieldDatabase, /applied-changes/u);
  assert.match(fieldDatabase, /sync-recovery/u);
});

test("conflict resolution reads the current server revision instead of using a fixed value", () => {
  assert.match(operationStore, /await listSyncConflicts\(apiBaseUrl, projectId\)/u);
  assert.match(operationStore, /serverConflict\.revision/u);
  assert.doesNotMatch(operationStore, /operation\.conflictId,\s*0,\s*"(?:KeepServer|Reapply)"/u);
});

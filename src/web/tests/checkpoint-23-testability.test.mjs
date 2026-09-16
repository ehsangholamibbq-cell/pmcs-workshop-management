import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const dashboard = readFileSync(new URL("../components/foundation-dashboard.tsx", import.meta.url), "utf8");
const issuesPanel = readFileSync(new URL("../components/sync-issues-panel.tsx", import.meta.url), "utf8");
const recovery = readFileSync(new URL("../lib/sync-recovery.ts", import.meta.url), "utf8");
const attachmentStore = readFileSync(new URL("../lib/attachment-store.ts", import.meta.url), "utf8");
const operationStore = readFileSync(new URL("../lib/operation-store.ts", import.meta.url), "utf8");
const serviceWorker = readFileSync(new URL("../public/sw.js", import.meta.url), "utf8");
const gateway = readFileSync(
  new URL("../../backend/Pmcs.Modules.Sync/Endpoints/SyncGatewayEndpoints.cs", import.meta.url),
  "utf8",
);
const workflow = readFileSync(new URL("../../../.github/workflows/ci.yml", import.meta.url), "utf8");

test("disconnect, reconnect and scheduled retry have explicit testable transitions", () => {
  assert.match(serviceWorker, /fetch\(request\)\.catch\(\(\) => caches\.match\("\/offline\.html"\)\)/u);
  assert.match(dashboard, /addEventListener\("online", handleOnline\)/u);
  assert.match(dashboard, /synchronize\(true, "reconnect"\)/u);
  assert.match(dashboard, /synchronize\(true, "retry"\)/u);
  assert.match(dashboard, /result\.state\.phase === "offline"/u);
  assert.match(recovery, /"retry-scheduled"/u);
  assert.match(recovery, /recoverInterruptedOperations/u);
  assert.match(recovery, /recoverInterruptedAttachments/u);
});

test("recovery queues and active cycles stay isolated by project", () => {
  assert.match(recovery, /recoverInterruptedOperations\(projectId\)/u);
  assert.match(recovery, /recoverInterruptedAttachments\(projectId\)/u);
  assert.match(recovery, /syncPendingAttachments\(apiBaseUrl, projectId\)/u);
  assert.match(operationStore, /readOperationsForProjectByStatus/u);
  assert.match(attachmentStore, /const syncKey = `\$\{scope\.tenantId\}:\$\{scope\.userId\}:\$\{projectId\}`/u);
});

test("local/server verification and recovery diagnostics are durable and payload-free", () => {
  assert.match(recovery, /syncRecoveryStoreName/u);
  assert.match(recovery, /serverCheckpointSequence/u);
  assert.match(recovery, /serverWatermark/u);
  assert.match(gateway, /sync_control\.operation_receipts/u);
  assert.doesNotMatch(gateway.match(/insert into sync_control\.operation_receipts[\s\S]*?;/u)?.[0] ?? "", /payload/u);
});

test("conflict and recovery UI exposes stable selectors for later E2E automation", () => {
  assert.match(issuesPanel, /data-testid="sync-recovery-center"/u);
  assert.match(issuesPanel, /data-testid="sync-recovery-status"/u);
  assert.match(issuesPanel, /data-sync-phase/u);
});

test("CI verifies replay, two-user conflict, recovery, audit and diagnostics directly", () => {
  assert.match(workflow, /checkpoint23-db-verification\.sh/u);
  assert.match(workflow, /Verify Checkpoint 23 sync recovery and conflict evidence/u);
});

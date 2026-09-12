import assert from "node:assert/strict";
import test from "node:test";
import { buildClientOperation } from "../lib/operation-store.ts";

test("offline operation keeps the authenticated tenant and user boundary", () => {
  const operation = buildClientOperation({
    tenantId: "tenant-a",
    userId: "user-a",
    projectId: "project-a",
    entityType: "DailyReport",
    entityId: "report-a",
    commandType: "CaptureDailyReportFact",
    payload: { factId: "fact-a" },
    offlineLeaseId: "lease-a",
    authorizationVersion: 7,
    localSequence: 12,
  });

  assert.equal(operation.tenantId, "tenant-a");
  assert.equal(operation.userId, "user-a");
  assert.equal(operation.status, "queued");
  assert.equal(operation.authorizationVersion, 7);
  assert.equal(operation.localSequence, 12);
  assert.ok(operation.correlationId);
});

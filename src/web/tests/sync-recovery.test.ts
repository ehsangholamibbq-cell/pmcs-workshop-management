import assert from "node:assert/strict";
import test from "node:test";
import { ApiRequestError } from "../lib/localization.ts";
import {
  classifySyncFailure,
  evaluateSyncConsistency,
  isRecoveryRetryDue,
  retryDelayMilliseconds,
  type LocalSyncRecoveryState,
  type SyncConsistencyInput,
} from "../lib/sync-recovery.ts";

const healthy: SyncConsistencyInput = {
  deviceStatus: "Active",
  leaseExpiresAt: "2026-09-17T00:00:00.000Z",
  localCheckpointSequence: 12,
  serverCheckpointSequence: 12,
  serverWatermark: 12,
  pendingOperations: 0,
  pendingAttachments: 0,
  pendingQualitySafetyIntakes: 0,
  localConflicts: 0,
  localRejected: 0,
  serverOpenConflicts: 0,
  serverRejected: 0,
};

test("local and server verification distinguishes pending, conflict and divergence", () => {
  const now = Date.parse("2026-09-16T12:00:00.000Z");
  assert.equal(evaluateSyncConsistency(healthy, now), "consistent");
  assert.equal(evaluateSyncConsistency({ ...healthy, pendingOperations: 1 }, now), "pending-upload");
  assert.equal(evaluateSyncConsistency({ ...healthy, serverWatermark: 13 }, now), "pending-download");
  assert.equal(evaluateSyncConsistency({ ...healthy, localConflicts: 1 }, now), "attention");
  assert.equal(evaluateSyncConsistency({ ...healthy, localCheckpointSequence: 13 }, now), "diverged");
  assert.equal(evaluateSyncConsistency({ ...healthy, deviceStatus: "Revoked" }, now), "attention");
});

test("retry policy is bounded and honors Retry-After", () => {
  assert.deepEqual(
    [1, 2, 3, 4, 5, 9].map((attempt) => retryDelayMilliseconds(attempt)),
    [5_000, 15_000, 45_000, 120_000, 300_000, 300_000],
  );
  assert.equal(retryDelayMilliseconds(1, 42), 42_000);
  assert.equal(retryDelayMilliseconds(1, 10_000), 900_000);
});

test("only transient transport and server failures are retried automatically", () => {
  assert.equal(classifySyncFailure(new TypeError("fetch failed")).retryable, true);
  assert.equal(classifySyncFailure(new ApiRequestError("busy", 429, "rate_limit.exceeded", 30)).retryable, true);
  assert.equal(classifySyncFailure(new ApiRequestError("server", 503)).retryable, true);
  assert.equal(classifySyncFailure(new ApiRequestError("forbidden", 403, "permission.denied")).retryable, false);
  assert.equal(classifySyncFailure(new Error("indexed db failed")).retryable, false);
});

test("durable recovery state becomes due only after its scheduled instant", () => {
  const state = {
    phase: "retry-scheduled",
    nextRetryAt: "2026-09-16T12:00:05.000Z",
  } as LocalSyncRecoveryState;
  assert.equal(isRecoveryRetryDue(state, Date.parse("2026-09-16T12:00:04.999Z")), false);
  assert.equal(isRecoveryRetryDue(state, Date.parse("2026-09-16T12:00:05.000Z")), true);
});

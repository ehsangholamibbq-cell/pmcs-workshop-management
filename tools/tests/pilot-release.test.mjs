import assert from "node:assert/strict";
import test from "node:test";
import { validatePilotCandidate } from "../validate-pilot-release.mjs";

const now = "2026-09-12T12:00:00Z";
const commit = "6dea26efbc75eac5d195c630bb4c4b9b64ccb7b2";
const gates = [
  "repository-validation",
  "backend-tests",
  "web-check",
  "postgres-integration",
  "object-storage-roundtrip",
  "backup-restore-drill",
  "environment-smoke",
  "device-uat",
];

function validCandidate() {
  return {
    schemaVersion: 1,
    releaseId: "pmcs-2026.09.12-rc.1",
    version: "1.20.0-rc.1",
    commit,
    builtAt: "2026-09-12T08:00:00Z",
    createdAt: "2026-09-12T11:00:00Z",
    expiresAt: "2026-09-20T11:00:00Z",
    artifacts: [
      { name: "api-image", sha256: "a".repeat(64) },
      { name: "web-image", sha256: "b".repeat(64) },
      { name: "source-bundle", sha256: "c".repeat(64) },
    ],
    evidence: gates.map((gateId, index) => ({
      gateId,
      status: "passed",
      commit,
      executedAt: `2026-09-12T09:0${index}:00Z`,
      expiresAt: gateId === "backup-restore-drill"
        ? "2026-12-11T09:05:00Z"
        : gateId === "device-uat"
          ? "2026-09-26T09:07:00Z"
          : `2026-09-13T09:0${index}:00Z`,
      reportSha256: String(index + 1).repeat(64),
      reportUri: `evidence://pilot/${gateId}.json`,
    })),
    approvals: [
      { role: "product", decision: "approved", approvedBy: "product-owner", approvedAt: "2026-09-12T10:00:00Z", commit },
      { role: "security", decision: "approved", approvedBy: "security-owner", approvedAt: "2026-09-12T10:10:00Z", commit },
      { role: "operations", decision: "approved", approvedBy: "operations-owner", approvedAt: "2026-09-12T10:20:00Z", commit },
    ],
  };
}

test("accepts a complete candidate tied to one commit and three independent approvals", () => {
  const result = validatePilotCandidate(validCandidate(), { now });
  assert.equal(result.gateCount, 8);
  assert.equal(result.approvalCount, 3);
  assert.equal(result.commit, commit);
});

test("rejects a missing mandatory gate", () => {
  const candidate = validCandidate();
  candidate.evidence.pop();
  assert.throws(() => validatePilotCandidate(candidate, { now }), /set does not match policy/u);
});

test("rejects evidence produced for a different commit", () => {
  const candidate = validCandidate();
  candidate.evidence[2].commit = "d".repeat(40);
  assert.throws(() => validatePilotCandidate(candidate, { now }), /another commit/u);
});

test("rejects expired evidence", () => {
  const candidate = validCandidate();
  candidate.evidence[0].expiresAt = "2026-09-12T11:59:59Z";
  assert.throws(() => validatePilotCandidate(candidate, { now }), /expired/u);
});

test("rejects one person approving multiple independent roles", () => {
  const candidate = validCandidate();
  candidate.approvals[2].approvedBy = candidate.approvals[0].approvedBy;
  assert.throws(() => validatePilotCandidate(candidate, { now }), /different people/u);
});

test("rejects candidates with unknown fields or incomplete artifact identity", () => {
  const candidate = validCandidate();
  candidate.artifacts[0].mutableTag = "latest";
  assert.throws(() => validatePilotCandidate(candidate, { now }), /unknown fields/u);
});

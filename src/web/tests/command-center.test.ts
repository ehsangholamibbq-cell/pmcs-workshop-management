import assert from "node:assert/strict";
import test from "node:test";
import { getCommandCenter, recalculateProjectState } from "../lib/command-center.ts";

const identity = { tenantId: "tenant-id", userId: "user-id" };

test("command center read is scoped to tenant and user", async () => {
  const originalFetch = globalThis.fetch;
  let capturedUrl = "";
  let capturedHeaders: HeadersInit | undefined;
  globalThis.fetch = async (input, init) => {
    capturedUrl = String(input);
    capturedHeaders = init?.headers;
    return Response.json(commandCenterResponse());
  };

  try {
    const model = await getCommandCenter("https://pmcs.test/", identity, "project-id");
    const headers = new Headers(capturedHeaders);

    assert.equal(capturedUrl, "https://pmcs.test/api/v1/projects/project-id/command-center");
    assert.equal(headers.get("X-Tenant-Id"), "tenant-id");
    assert.equal(headers.get("X-User-Id"), "user-id");
    assert.equal(model.snapshot, null);
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("recalculation is explicit and carries an idempotency key", async () => {
  const originalFetch = globalThis.fetch;
  let capturedInit: RequestInit | undefined;
  globalThis.fetch = async (_input, init) => {
    capturedInit = init;
    return Response.json(snapshotResponse(), { status: 201 });
  };

  try {
    const snapshot = await recalculateProjectState(
      "https://pmcs.test",
      identity,
      "project-id",
      "2026-09-09",
    );
    const headers = new Headers(capturedInit?.headers);

    assert.equal(capturedInit?.method, "POST");
    assert.ok(headers.get("Idempotency-Key"));
    assert.deepEqual(JSON.parse(String(capturedInit?.body)), { asOfDate: "2026-09-09" });
    assert.equal(snapshot.operationalStatus, "NoData");
  } finally {
    globalThis.fetch = originalFetch;
  }
});

function commandCenterResponse() {
  return {
    projectId: "project-id",
    projectCode: "DEMO-01",
    projectName: "Demo project",
    timeZone: "Asia/Tehran",
    hasSnapshot: false,
    isOutdated: false,
    canRecalculate: true,
    canTriage: true,
    canReadFinance: true,
    canReadCommercial: true,
    snapshot: null,
    financialState: null,
    commercialState: null,
    capabilities: [],
    trend: [],
  };
}

function snapshotResponse() {
  return {
    snapshotId: "snapshot-id",
    calculationVersion: "project-state-v2",
    projectConfigurationRevision: 1,
    asOfDate: "2026-09-09",
    windowStart: "2026-09-03",
    windowEnd: "2026-09-09",
    calculatedAt: "2026-09-09T08:00:00Z",
    assessmentScope: "ApprovedDailyOperations",
    isPartial: true,
    operationalStatus: "NoData",
    coverageStatus: "NoData",
    freshnessStatus: "NoData",
    confidenceStatus: "NoData",
    coverageBasis: "FallbackSevenCalendarDays",
    coveragePercent: 0,
    expectedReportDays: 7,
    approvedReportDays: 0,
    lastApprovedReportDate: null,
    approvedFactCount: 0,
    progressFactCount: 0,
    laborFactCount: 0,
    equipmentFactCount: 0,
    materialFactCount: 0,
    issueCount: 0,
    stoppageCount: 0,
    highImpactCount: 0,
    criticalImpactCount: 0,
    oldestAttentionAgeDays: null,
    sourceMaxChangedAt: null,
    attentionItems: [],
  };
}

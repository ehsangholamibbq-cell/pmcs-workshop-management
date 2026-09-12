import assert from "node:assert/strict";
import test from "node:test";
import {
  createMeasurementItem,
  createMilestoneProgressUpdate,
  createPlanningBaseline,
  deactivateMeasurementItem,
  getProgressLedger,
  type MeasurementItemModel,
} from "../lib/planning.ts";

const identity = { tenantId: "tenant-id", userId: "user-id" };

test("progress read uses the project-scoped planning endpoint", async () => {
  const originalFetch = globalThis.fetch;
  let capturedUrl = "";
  let capturedHeaders: HeadersInit | undefined;
  globalThis.fetch = async (input, init) => {
    capturedUrl = String(input);
    capturedHeaders = init?.headers;
    return Response.json({
      planningMode: "None",
      measurementBasisState: "NotConfigured",
      officialProgressBasisState: "NotConfigured",
      scheduleBasisState: "NotConfigured",
      approvedBaselineId: null,
      approvedBaselineVersion: null,
      approvedBaselineKind: null,
      officialOverallPhysicalPercent: null,
      plannedOverallPhysicalPercent: null,
      scheduleVariancePercent: null,
      forecastCompletionDate: null,
      missingActualEntryCount: 0,
      approvedFactCount: 0,
      provisionalFactCount: 0,
      unlinkedApprovedFactCount: 0,
      unlinkedProvisionalFactCount: 0,
      items: [],
      milestones: [],
    });
  };

  try {
    const ledger = await getProgressLedger("https://pmcs.test/", identity, "project-id");
    const headers = new Headers(capturedHeaders);

    assert.equal(capturedUrl, "https://pmcs.test/api/v1/projects/project-id/planning/progress");
    assert.equal(headers.get("X-Tenant-Id"), "tenant-id");
    assert.equal(ledger.officialOverallPhysicalPercent, null);
    assert.equal(ledger.scheduleVariancePercent, null);
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("baseline and milestone commands preserve versioned references", async () => {
  const originalFetch = globalThis.fetch;
  const calls: { url: string; init?: RequestInit }[] = [];
  globalThis.fetch = async (input, init) => {
    calls.push({ url: String(input), init });
    return Response.json({ id: `result-${calls.length}`, revision: 1 }, { status: 201 });
  };

  try {
    const entryId = crypto.randomUUID();
    await createPlanningBaseline("https://pmcs.test", identity, "project-id", {
      versionCode: "B-01",
      title: "Baseline",
      kind: "MilestonePlan",
      sourceSystem: null,
      sourceReference: null,
      entries: [{
        clientGeneratedId: entryId,
        parentEntryId: null,
        code: "M-01",
        title: "Handover",
        kind: "Milestone",
        measurementMethod: "ManualPercent",
        measurementItemId: null,
        plannedStart: "2026-09-30",
        plannedFinish: "2026-09-30",
        weightPercent: 100,
        externalId: null,
        sortOrder: 1,
      }],
    });
    await createMilestoneProgressUpdate("https://pmcs.test", identity, "project-id", {
      baselineId: "baseline-id",
      baselineEntryId: entryId,
      statusDate: "2026-09-11",
      progressPercent: 60,
      evidenceReference: "evidence-id",
      note: "",
    });

    const baselineBody = JSON.parse(String(calls[0]?.init?.body));
    const milestoneBody = JSON.parse(String(calls[1]?.init?.body));
    assert.equal(calls[0]?.url, "https://pmcs.test/api/v1/projects/project-id/planning/baselines");
    assert.equal(baselineBody.entries[0].clientGeneratedId, entryId);
    assert.equal(calls[1]?.url, "https://pmcs.test/api/v1/projects/project-id/planning/milestone-updates");
    assert.equal(milestoneBody.baselineEntryId, entryId);
    assert.equal(milestoneBody.note, null);
    assert.ok(new Headers(calls[1]?.init?.headers).get("Idempotency-Key"));
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("measurement item commands carry idempotency and current revision", async () => {
  const originalFetch = globalThis.fetch;
  const calls: { url: string; init?: RequestInit }[] = [];
  globalThis.fetch = async (input, init) => {
    calls.push({ url: String(input), init });
    return Response.json(itemResponse(), { status: calls.length === 1 ? 201 : 200 });
  };

  try {
    await createMeasurementItem("https://pmcs.test", identity, "project-id", {
      code: "CON-01",
      title: "Concrete",
      unit: "m3",
      targetQuantity: null,
      notes: "",
    });
    await deactivateMeasurementItem("https://pmcs.test", identity, "project-id", itemResponse());

    const createHeaders = new Headers(calls[0]?.init?.headers);
    const deactivateHeaders = new Headers(calls[1]?.init?.headers);
    assert.ok(createHeaders.get("Idempotency-Key"));
    assert.ok(deactivateHeaders.get("Idempotency-Key"));
    assert.equal(calls[1]?.url, "https://pmcs.test/api/v1/projects/project-id/planning/measurement-items/item-id/deactivate");
    assert.deepEqual(JSON.parse(String(calls[1]?.init?.body)), { baseRevision: 3 });
  } finally {
    globalThis.fetch = originalFetch;
  }
});

function itemResponse(): MeasurementItemModel {
  return {
    id: "item-id",
    code: "CON-01",
    title: "Concrete",
    unit: "m3",
    targetQuantity: null,
    notes: null,
    status: "Active",
    revision: 3,
    lastModifiedAt: "2026-09-11T08:00:00Z",
  };
}

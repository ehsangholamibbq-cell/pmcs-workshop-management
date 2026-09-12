import assert from "node:assert/strict";
import test from "node:test";
import {
  getAdvisoryInsights,
  requestAdvisoryInsight,
  reviewAdvisoryInsight,
  type AdvisoryInsightModel,
} from "../lib/intelligence.ts";

const identity = { tenantId: "tenant-id", userId: "user-id" };

test("advisory insight read is scoped to tenant and user", async () => {
  const originalFetch = globalThis.fetch;
  let capturedUrl = "";
  let capturedHeaders: HeadersInit | undefined;
  globalThis.fetch = async (input, init) => {
    capturedUrl = String(input);
    capturedHeaders = init?.headers;
    return Response.json({
      providerConfigured: false,
      canGenerate: true,
      canReview: true,
      insights: [],
      activeRequests: [],
      recentRequests: [],
    });
  };

  try {
    const result = await getAdvisoryInsights("https://pmcs.test/", identity, "project-id");
    const headers = new Headers(capturedHeaders);

    assert.equal(capturedUrl, "https://pmcs.test/api/v1/projects/project-id/insights");
    assert.equal(headers.get("X-Tenant-Id"), "tenant-id");
    assert.equal(headers.get("X-User-Id"), "user-id");
    assert.equal(result.providerConfigured, false);
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("generation request is explicit and idempotent", async () => {
  const originalFetch = globalThis.fetch;
  let capturedInit: RequestInit | undefined;
  globalThis.fetch = async (_input, init) => {
    capturedInit = init;
    return Response.json(generationResponse(), { status: 202 });
  };

  try {
    const result = await requestAdvisoryInsight("https://pmcs.test", identity, "project-id");
    const headers = new Headers(capturedInit?.headers);

    assert.equal(capturedInit?.method, "POST");
    assert.ok(headers.get("Idempotency-Key"));
    assert.deepEqual(JSON.parse(String(capturedInit?.body)), {});
    assert.equal(result.status, "Pending");
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("human review carries the current advisory revision", async () => {
  const originalFetch = globalThis.fetch;
  let capturedUrl = "";
  let capturedInit: RequestInit | undefined;
  globalThis.fetch = async (input, init) => {
    capturedUrl = String(input);
    capturedInit = init;
    return Response.json({ ...insightResponse(), reviewStatus: "Accepted", revision: 2 });
  };

  try {
    const result = await reviewAdvisoryInsight(
      "https://pmcs.test",
      identity,
      "project-id",
      insightResponse(),
      "accept",
    );
    const body = JSON.parse(String(capturedInit?.body));

    assert.equal(capturedUrl, "https://pmcs.test/api/v1/projects/project-id/insights/insight-id/accept");
    assert.equal(body.baseRevision, 1);
    assert.equal(result.reviewStatus, "Accepted");
  } finally {
    globalThis.fetch = originalFetch;
  }
});

function generationResponse() {
  return {
    requestId: "request-id",
    projectId: "project-id",
    status: "Pending",
    snapshotId: null,
    insightId: null,
    requestedAt: "2026-09-10T08:00:00Z",
    startedAt: null,
    completedAt: null,
    attempts: 0,
    lastErrorCode: null,
    revision: 1,
    nextAttemptAt: null,
  };
}

function insightResponse(): AdvisoryInsightModel {
  return {
    insightId: "insight-id",
    projectId: "project-id",
    generationRequestId: "request-id",
    snapshotId: "snapshot-id",
    output: {
      insightType: "ExecutiveSummary",
      statement: "خلاصه مستند",
      evidenceReferences: ["project-state:snapshot-id"],
      factsUsed: ["واقعیت تأییدشده"],
      assumptions: [],
      dataGaps: [],
      confidenceBand: "Medium",
      potentialImpact: "اثر احتمالی",
      suggestedActions: [],
      suggestedOwnerRole: "مدیر پروژه",
    },
    includesFinancialData: false,
    includesCommercialData: false,
    includesActionData: true,
    generatedAt: "2026-09-10T08:00:00Z",
    expiresAt: "2026-09-11T08:00:00Z",
    isStale: false,
    reviewStatus: "NeedsReview",
    reviewedAt: null,
    reviewComment: null,
    revision: 1,
  };
}

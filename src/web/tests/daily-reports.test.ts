import assert from "node:assert/strict";
import test from "node:test";
import {
  listReviewInbox,
  removeDailyReportFact,
  reviewDailyReport,
  startDailyReportCorrection,
} from "../lib/daily-reports.ts";

const identity = { tenantId: "tenant-id", userId: "user-id" };

test("review inbox sends tenant and user scope headers", async () => {
  const originalFetch = globalThis.fetch;
  let capturedUrl = "";
  let capturedHeaders: HeadersInit | undefined;
  globalThis.fetch = async (input, init) => {
    capturedUrl = String(input);
    capturedHeaders = init?.headers;
    return Response.json([]);
  };

  try {
    const reports = await listReviewInbox("https://pmcs.test/", identity, "project-id");
    const headers = new Headers(capturedHeaders);

    assert.deepEqual(reports, []);
    assert.equal(capturedUrl, "https://pmcs.test/api/v1/projects/project-id/daily-reports/inbox");
    assert.equal(headers.get("X-Tenant-Id"), "tenant-id");
    assert.equal(headers.get("X-User-Id"), "user-id");
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("return workflow sends revision, comment and an idempotency key", async () => {
  const originalFetch = globalThis.fetch;
  let capturedUrl = "";
  let capturedInit: RequestInit | undefined;
  globalThis.fetch = async (input, init) => {
    capturedUrl = String(input);
    capturedInit = init;
    return Response.json({
      id: "report-id",
      projectId: "project-id",
      reportDate: "2026-09-09",
      locationName: null,
      narrative: null,
      status: "Returned",
      revision: 5,
      factCount: 2,
      reviewedBy: "user-id",
      reviewedAt: "2026-09-09T10:00:00Z",
      reviewComment: "اصلاح مقدار",
    });
  };

  try {
    const report = await reviewDailyReport(
      "https://pmcs.test",
      identity,
      "project-id",
      "report-id",
      "return",
      4,
      "  اصلاح مقدار  ",
    );
    const headers = new Headers(capturedInit?.headers);

    assert.equal(capturedUrl, "https://pmcs.test/api/v1/projects/project-id/daily-reports/report-id/return");
    assert.equal(capturedInit?.method, "POST");
    assert.ok(headers.get("Idempotency-Key"));
    assert.deepEqual(JSON.parse(String(capturedInit?.body)), {
      baseRevision: 4,
      comment: "اصلاح مقدار",
    });
    assert.equal(report.status, "Returned");
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("correction workflow starts a version and removes copied facts with revision", async () => {
  const originalFetch = globalThis.fetch;
  const calls: { url: string; body: Record<string, unknown>; key: string | null }[] = [];
  globalThis.fetch = async (input, init) => {
    calls.push({
      url: String(input),
      body: JSON.parse(String(init?.body)) as Record<string, unknown>,
      key: new Headers(init?.headers).get("Idempotency-Key"),
    });
    return Response.json({
      id: "correction-id", projectId: "project-id", reportDate: "2026-09-09",
      locationName: null, narrative: null, status: "Draft", revision: 1,
      createdBy: "author-id", factCount: 1, reviewedBy: null, reviewedAt: null,
      reviewComment: null, rootReportId: "report-id", versionNumber: 2,
      supersedesReportId: "report-id", supersededByReportId: null, supersededAt: null,
      correctionReason: "اصلاح مقدار", correctionInitiatedBy: "reviewer-id", facts: [],
    });
  };

  try {
    await startDailyReportCorrection(
      "https://pmcs.test", identity, "project-id", "report-id", 4, "  اصلاح مقدار  ",
    );
    await removeDailyReportFact(
      "https://pmcs.test", identity, "project-id", "correction-id", "fact-id", 1,
    );

    assert.equal(calls[0].url, "https://pmcs.test/api/v1/projects/project-id/daily-reports/report-id/corrections");
    assert.equal(calls[0].body.baseRevision, 4);
    assert.equal(calls[0].body.reason, "اصلاح مقدار");
    assert.ok(calls[0].body.clientGeneratedId);
    assert.equal(calls[1].url, "https://pmcs.test/api/v1/projects/project-id/daily-reports/correction-id/facts/fact-id/remove");
    assert.deepEqual(calls[1].body, { baseRevision: 1 });
    assert.ok(calls.every((call) => call.key));
  } finally {
    globalThis.fetch = originalFetch;
  }
});

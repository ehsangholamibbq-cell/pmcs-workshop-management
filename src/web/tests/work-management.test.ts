import assert from "node:assert/strict";
import test from "node:test";
import {
  changeNotificationReceipt,
  getMyWork,
  listNotifications,
} from "../lib/work-management.ts";

const identity = { tenantId: "tenant-id", userId: "user-id" };

test("my work and notifications are scoped to the active actor and project", async () => {
  const originalFetch = globalThis.fetch;
  const calls: { url: string; headers: Headers }[] = [];
  globalThis.fetch = async (input, init) => {
    calls.push({ url: String(input), headers: new Headers(init?.headers) });
    return String(input).endsWith("/my-work")
      ? Response.json({ calculatedAt: "2026-09-13T08:00:00Z", unreadNotificationCount: 0, items: [] })
      : Response.json([]);
  };

  try {
    await getMyWork("https://pmcs.test/", identity, "project-id");
    await listNotifications("https://pmcs.test/", identity, "project-id");

    assert.deepEqual(calls.map((call) => call.url), [
      "https://pmcs.test/api/v1/projects/project-id/my-work",
      "https://pmcs.test/api/v1/projects/project-id/notifications",
    ]);
    assert.ok(calls.every((call) => call.headers.get("X-Tenant-Id") === "tenant-id"));
    assert.ok(calls.every((call) => call.headers.get("X-User-Id") === "user-id"));
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("notification acknowledgement carries revision and idempotency", async () => {
  const originalFetch = globalThis.fetch;
  let capturedUrl = "";
  let capturedInit: RequestInit | undefined;
  globalThis.fetch = async (input, init) => {
    capturedUrl = String(input);
    capturedInit = init;
    return Response.json({
      id: "notification-id", projectId: "project-id", category: "DailyReportReview",
      title: "گزارش جدید", body: "نیازمند بازبینی", targetType: "DailyReport",
      targetId: "report-id", occurredAt: "2026-09-13T08:00:00Z", readAt: "2026-09-13T08:01:00Z",
      acknowledgedAt: "2026-09-13T08:01:00Z", revision: 2,
    });
  };

  try {
    const result = await changeNotificationReceipt(
      "https://pmcs.test", identity, "project-id", "notification-id", 1, "acknowledge",
    );
    const headers = new Headers(capturedInit?.headers);
    assert.equal(capturedUrl, "https://pmcs.test/api/v1/projects/project-id/notifications/notification-id/acknowledge");
    assert.equal(capturedInit?.method, "POST");
    assert.deepEqual(JSON.parse(String(capturedInit?.body)), { baseRevision: 1 });
    assert.ok(headers.get("Idempotency-Key"));
    assert.equal(result.revision, 2);
  } finally {
    globalThis.fetch = originalFetch;
  }
});

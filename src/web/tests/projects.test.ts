import assert from "node:assert/strict";
import test from "node:test";
import { configureProjectCalendar, configureProjectPlanningMode } from "../lib/projects.ts";

test("calendar can be explicitly left unconfigured", async () => {
  const originalFetch = globalThis.fetch;
  let capturedInit: RequestInit | undefined;
  globalThis.fetch = async (_input, init) => {
    capturedInit = init;
    return Response.json({
      id: "project-id",
      calendarMode: "NotConfigured",
      workingDays: [],
      revision: 3,
    });
  };

  try {
    await configureProjectCalendar(
      "https://pmcs.test",
      { tenantId: "tenant-id", userId: "user-id" },
      "project-id",
      2,
      "NotConfigured",
      [],
    );
    const body = JSON.parse(String(capturedInit?.body));
    const headers = new Headers(capturedInit?.headers);

    assert.equal(capturedInit?.method, "PUT");
    assert.deepEqual(body, { baseRevision: 2, mode: "NotConfigured", workingDays: null });
    assert.ok(headers.get("Idempotency-Key"));
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("planning mode change is revision-controlled and idempotent", async () => {
  const originalFetch = globalThis.fetch;
  let capturedUrl = "";
  let capturedInit: RequestInit | undefined;
  globalThis.fetch = async (input, init) => {
    capturedUrl = String(input);
    capturedInit = init;
    return Response.json({ id: "project-id", planningMode: "Milestones", revision: 4 });
  };

  try {
    await configureProjectPlanningMode(
      "https://pmcs.test/",
      { tenantId: "tenant-id", userId: "user-id" },
      "project-id",
      3,
      "Milestones",
    );
    const headers = new Headers(capturedInit?.headers);
    assert.equal(capturedUrl, "https://pmcs.test/api/v1/projects/project-id/planning-mode");
    assert.deepEqual(JSON.parse(String(capturedInit?.body)), { baseRevision: 3, mode: "Milestones" });
    assert.ok(headers.get("Idempotency-Key"));
  } finally {
    globalThis.fetch = originalFetch;
  }
});

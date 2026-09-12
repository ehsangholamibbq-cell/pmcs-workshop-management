import assert from "node:assert/strict";
import test from "node:test";
import { createActionFromAttention, dismissAttention, transitionAction } from "../lib/actions.ts";

const identity = { tenantId: "tenant-id", userId: "user-id" };

test("attention conversion sends explicit accountability fields and idempotency", async () => {
  const originalFetch = globalThis.fetch;
  let capturedUrl = "";
  let capturedInit: RequestInit | undefined;
  globalThis.fetch = async (input, init) => {
    capturedUrl = String(input);
    capturedInit = init;
    return Response.json(actionResponse(), { status: 201 });
  };

  try {
    await createActionFromAttention("https://pmcs.test/", identity, "project-id", "fact-id", {
      assigneeUserId: "assignee-id",
      dueDate: "2026-09-12",
      priority: "High",
    });
    const headers = new Headers(capturedInit?.headers);
    const body = JSON.parse(String(capturedInit?.body));

    assert.equal(capturedUrl, "https://pmcs.test/api/v1/projects/project-id/attention/fact-id/actions");
    assert.ok(headers.get("Idempotency-Key"));
    assert.equal(body.assigneeUserId, "assignee-id");
    assert.equal(body.priority, "High");
    assert.ok(body.clientGeneratedId);
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("dismissal and transition use command endpoints", async () => {
  const originalFetch = globalThis.fetch;
  const calls: string[] = [];
  globalThis.fetch = async (input) => {
    calls.push(String(input));
    return Response.json(actionResponse());
  };

  try {
    await dismissAttention("https://pmcs.test", identity, "project-id", "fact-id", "Duplicate observation");
    await transitionAction("https://pmcs.test", identity, "project-id", "action-id", 1, "Done");

    assert.deepEqual(calls, [
      "https://pmcs.test/api/v1/projects/project-id/attention/fact-id/dismiss",
      "https://pmcs.test/api/v1/projects/project-id/actions/action-id/transition",
    ]);
  } finally {
    globalThis.fetch = originalFetch;
  }
});

function actionResponse() {
  return {
    id: "action-id",
    projectId: "project-id",
    sourceFactId: "fact-id",
    title: "Resolve site issue",
    description: null,
    assigneeUserId: "assignee-id",
    assigneeDisplayName: "Project Manager",
    dueDate: "2026-09-12",
    priority: "High",
    status: "Open",
    createdAt: "2026-09-09T08:00:00Z",
    lastChangedAt: null,
    completedAt: null,
    revision: 1,
  };
}

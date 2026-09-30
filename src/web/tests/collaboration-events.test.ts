import assert from "node:assert/strict";
import test from "node:test";
import { CollaborationAccessError, watchCollaborationEvents } from "../lib/collaboration-events.ts";

test("poll fallback resumes from durable sequence and stops on permission revocation", async () => {
  const previous = globalThis.fetch;
  const requested: string[] = [];
  try {
    globalThis.fetch = async (url) => {
      requested.push(String(url));
      if (requested.length === 1) return Response.json({ events: [
        { messageId: "message", projectId: "project", sequence: 8, createdAt: "2026-09-28T00:00:00Z" },
      ], nextSequence: 8 });
      return new Response(null, { status: 403 });
    };
    const observed: number[] = [];
    const states: string[] = [];
    await assert.rejects(watchCollaborationEvents("/api/pmcs", "project", 7,
      (event) => { observed.push(event.sequence); }, new AbortController().signal,
      (state) => { states.push(state); }),
    (error: unknown) => error instanceof CollaborationAccessError && error.status === 403);
    assert.deepEqual(observed, [8]);
    assert.deepEqual(states, ["connecting", "current"]);
    assert.match(requested[0], /after=7&waitSeconds=20$/u);
    assert.match(requested[1], /after=8&waitSeconds=20$/u);
  } finally {
    globalThis.fetch = previous;
  }
});

test("an invalid event page cannot announce a current connection or emit a partial event", async () => {
  const previous = globalThis.fetch;
  const controller = new AbortController();
  const states: string[] = [];
  const observed: number[] = [];
  try {
    globalThis.fetch = async () => Response.json({ events: [
      { projectId: "project", sequence: 8 }, { projectId: "other-project", sequence: 9 },
    ], nextSequence: 9 });
    await watchCollaborationEvents("/api/pmcs", "project", 7,
      (event) => { observed.push(event.sequence); }, controller.signal,
      (state) => { states.push(state); if (state === "retrying") controller.abort(); });
    assert.deepEqual(observed, []);
    assert.deepEqual(states, ["connecting", "retrying"]);
  } finally { globalThis.fetch = previous; }
});

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
    await assert.rejects(watchCollaborationEvents("/api/pmcs", "project", 7,
      (event) => { observed.push(event.sequence); }, new AbortController().signal),
    (error: unknown) => error instanceof CollaborationAccessError && error.status === 403);
    assert.deepEqual(observed, [8]);
    assert.match(requested[0], /after=7&waitSeconds=20$/u);
    assert.match(requested[1], /after=8&waitSeconds=20$/u);
  } finally {
    globalThis.fetch = previous;
  }
});

import assert from "node:assert/strict";
import test from "node:test";
import { loadProjectConversation } from "../lib/collaboration-room.ts";

const projectId = "10000000-0000-4000-8000-000000000001";

test("conversation reads the latest bounded project window and denies caching", async () => {
  const original = globalThis.fetch;
  const requests: { url: string; cache?: RequestCache }[] = [];
  globalThis.fetch = async (input, init) => {
    requests.push({ url: String(input), cache: init?.cache });
    return Response.json(requests.length === 1
      ? { projectId, lastSequence: 205, canModerate: true, canUpload: true,
        canEditOwn: true, canConvert: true }
      : { messages: [{ id: "m1", projectId, sequence: 205, authorUserId: "u1",
        body: "واقعیت پروژه", createdAt: "2026-09-28T00:00:00Z" }], nextSequence: 205 });
  };
  try {
    const result = await loadProjectConversation("/api/pmcs", projectId);
    assert.equal(result.kind, "ready");
    if (result.kind === "ready") {
      assert.equal(result.canModerate, true);
      assert.equal(result.canUpload, true);
      assert.equal(result.canEditOwn, true);
      assert.equal(result.canConvert, true);
    }
    assert.deepEqual(requests, [
      { url: `/api/pmcs/api/v1/projects/${projectId}/collaboration`, cache: "no-store" },
      { url: `/api/pmcs/api/v1/projects/${projectId}/collaboration/messages?after=105`, cache: "no-store" },
    ]);
  } finally { globalThis.fetch = original; }
});

test("disabled or revoked conversation fails closed before any message read", async () => {
  const original = globalThis.fetch;
  for (const [status, kind] of [[404, "unavailable"], [403, "forbidden"]] as const) {
    let requests = 0;
    globalThis.fetch = async () => { requests += 1; return new Response(null, { status }); };
    assert.equal((await loadProjectConversation("/api/pmcs", projectId)).kind, kind);
    assert.equal(requests, 1);
  }
  globalThis.fetch = original;
});

test("cross-project and out-of-order messages are never displayed", async () => {
  const original = globalThis.fetch;
  let requests = 0;
  globalThis.fetch = async () => Response.json(++requests === 1
    ? { projectId, lastSequence: 5 }
    : { messages: [{ projectId: "20000000-0000-4000-8000-000000000002", sequence: 3 }], nextSequence: 3 });
  try {
    await assert.rejects(loadProjectConversation("/api/pmcs", projectId), /معتبر نیست/u);
  } finally { globalThis.fetch = original; }
});

test("room capability is false without a validated moderator grant", async () => {
  const original = globalThis.fetch;
  let calls = 0;
  try {
    globalThis.fetch = async () => Response.json(++calls === 1
      ? { projectId, lastSequence: 0 }
      : { messages: [], nextSequence: 0 });
    const view = await loadProjectConversation("/api/pmcs", projectId);
    assert.equal(view.kind, "ready");
    if (view.kind === "ready") {
      assert.equal(view.canModerate, false);
      assert.equal(view.canUpload, false);
      assert.equal(view.canEditOwn, false);
      assert.equal(view.canConvert, false);
    }
    globalThis.fetch = async () => Response.json({ projectId, lastSequence: 0, canModerate: "true" });
    await assert.rejects(loadProjectConversation("/api/pmcs", projectId), /معتبر نیست/u);
    globalThis.fetch = async () => Response.json({ projectId, lastSequence: 0, canUpload: "true" });
    await assert.rejects(loadProjectConversation("/api/pmcs", projectId), /معتبر نیست/u);
    globalThis.fetch = async () => Response.json({ projectId, lastSequence: 0, canEditOwn: "true" });
    await assert.rejects(loadProjectConversation("/api/pmcs", projectId), /معتبر نیست/u);
    globalThis.fetch = async () => Response.json({ projectId, lastSequence: 0, canConvert: "true" });
    await assert.rejects(loadProjectConversation("/api/pmcs", projectId), /معتبر نیست/u);
  } finally { globalThis.fetch = original; }
});

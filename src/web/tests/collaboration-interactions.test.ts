import assert from "node:assert/strict";
import test from "node:test";
import { CollaborationAccessError } from "../lib/collaboration-events.ts";
import {
  loadProjectConversationUnread, markProjectConversationRead, searchProjectConversation,
} from "../lib/collaboration-interactions.ts";

const projectId = "10000000-0000-4000-8000-000000000001";

test("project search encodes a bounded query and rejects another project's messages", async () => {
  const previous = globalThis.fetch;
  const requests: string[] = [];
  try {
    globalThis.fetch = async (input) => {
      requests.push(String(input));
      return Response.json([{ id: "message", projectId, sequence: 1, body: "مورد پروژه" }]);
    };
    assert.equal((await searchProjectConversation("/api/pmcs", projectId, "  مورد پروژه  ")).length, 1);
    assert.match(requests[0], /messages\/search\?q=%D9%85%D9%88%D8%B1%D8%AF%20%D9%BE%D8%B1%D9%88%DA%98%D9%87$/u);
    globalThis.fetch = async () => Response.json([{ id: "message", projectId: "other", sequence: 1, body: "secret" }]);
    await assert.rejects(searchProjectConversation("/api/pmcs", projectId, "secret"), /محدودهٔ پروژه/u);
  } finally { globalThis.fetch = previous; }
});

test("unread and explicit read cursor stay within the current project and fail closed", async () => {
  const previous = globalThis.fetch;
  const calls: Array<{ url: string; method?: string; body?: string }> = [];
  try {
    globalThis.fetch = async (input, init) => {
      calls.push({ url: String(input), method: init?.method, body: String(init?.body ?? "") });
      return Response.json(calls.length === 1
        ? { lastReadSequence: 2, unreadCount: 3 } : { lastReadSequence: 5 });
    };
    assert.deepEqual(await loadProjectConversationUnread("/api/pmcs", projectId),
      { lastReadSequence: 2, unreadCount: 3 });
    assert.equal(await markProjectConversationRead("/api/pmcs", projectId, 5), 5);
    assert.match(calls[0].url, new RegExp(`${projectId}/collaboration/unread$`, "u"));
    assert.equal(calls[1].method, "PUT");
    assert.deepEqual(JSON.parse(calls[1].body ?? ""), { lastReadSequence: 5 });
    globalThis.fetch = async () => new Response(null, { status: 403 });
    await assert.rejects(loadProjectConversationUnread("/api/pmcs", projectId),
      (error: unknown) => error instanceof CollaborationAccessError && error.status === 403);
  } finally { globalThis.fetch = previous; }
});

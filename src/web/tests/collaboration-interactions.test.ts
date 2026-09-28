import assert from "node:assert/strict";
import test from "node:test";
import { CollaborationAccessError } from "../lib/collaboration-events.ts";
import {
  CollaborationRevisionConflict, editOwnProjectMessage,
  loadProjectConversationUnread, loadProjectMessageReactions, markProjectConversationRead,
  searchProjectConversation, setProjectMessagePin, setProjectMessageReaction,
} from "../lib/collaboration-interactions.ts";
import type { ProjectConversationMessage } from "../lib/collaboration-room.ts";

const projectId = "10000000-0000-4000-8000-000000000001";
const messageId = "10000000-0000-4000-8000-000000000011";

test("own-message edit uses stable revision/key and keeps a conflict explicit", async () => {
  const previous = globalThis.fetch;
  const message = { id: messageId, projectId, authorUserId: "10000000-0000-4000-8000-000000000099",
    body: "نسخه پیشین", revision: 2 } as ProjectConversationMessage;
  const key = "10000000-0000-4000-8000-000000000123";
  const calls: Array<{ url: string; method?: string; key?: string; body?: string }> = [];
  try {
    globalThis.fetch = async (input, init) => {
      calls.push({ url: String(input), method: init?.method,
        key: new Headers(init?.headers).get("idempotency-key") ?? undefined,
        body: String(init?.body) });
      return Response.json({ ...message, body: "نسخه تازه", revision: 3,
        editedAt: "2026-09-28T01:00:00Z" });
    };
    assert.equal((await editOwnProjectMessage("/api/pmcs", projectId, message,
      "  نسخه تازه  ", key)).revision, 3);
    assert.equal(calls[0].method, "PATCH");
    assert.equal(calls[0].key, key);
    assert.deepEqual(JSON.parse(calls[0].body ?? ""), { baseRevision: 2, body: "نسخه تازه" });
    globalThis.fetch = async () => Response.json({ code: "collaboration.message.revision.conflict",
      currentRevision: 4 }, { status: 409 });
    await assert.rejects(editOwnProjectMessage("/api/pmcs", projectId, message, "نسخه تازه", key),
      (error: unknown) => error instanceof CollaborationRevisionConflict && error.currentRevision === 4);
    globalThis.fetch = async () => new Response(null, { status: 403 });
    await assert.rejects(editOwnProjectMessage("/api/pmcs", projectId, message, "نسخه تازه", key),
      (error: unknown) => error instanceof CollaborationAccessError && error.status === 403);
    await assert.rejects(editOwnProjectMessage("/api/pmcs", projectId, message, message.body, key),
      /پیش‌نویس ویرایش/u);
  } finally { globalThis.fetch = previous; }
});

test("pin and unpin require a scoped server confirmation and fail closed on revocation", async () => {
  const previous = globalThis.fetch;
  const calls: Array<{ url: string; method?: string }> = [];
  try {
    globalThis.fetch = async (input, init) => {
      calls.push({ url: String(input), method: init?.method });
      return Response.json({ id: messageId, projectId,
        pinnedAt: init?.method === "PUT" ? "2026-09-28T00:00:00Z" : null });
    };
    assert.equal(await setProjectMessagePin("/api/pmcs", projectId, messageId, true),
      "2026-09-28T00:00:00Z");
    assert.equal(await setProjectMessagePin("/api/pmcs", projectId, messageId, false), null);
    assert.deepEqual(calls.map(({ method }) => method), ["PUT", "DELETE"]);
    assert.equal(calls[0].url, `/api/pmcs/api/v1/projects/${projectId}/collaboration/messages/${messageId}/pin`);
    globalThis.fetch = async () => Response.json({ id: messageId,
      projectId: "20000000-0000-4000-8000-000000000002", pinnedAt: null });
    await assert.rejects(setProjectMessagePin("/api/pmcs", projectId, messageId, false), /تأیید سنجاق/u);
    globalThis.fetch = async () => new Response(null, { status: 403 });
    await assert.rejects(setProjectMessagePin("/api/pmcs", projectId, messageId, true),
      (error: unknown) => error instanceof CollaborationAccessError && error.status === 403);
  } finally { globalThis.fetch = previous; }
});

test("reaction summary verifies message scope and restores the four supported choices", async () => {
  const previous = globalThis.fetch;
  try {
    globalThis.fetch = async () => Response.json({ messageId, canReact: true,
      reactions: [{ emoji: "👍", count: 2, reactedByMe: true }] });
    const view = await loadProjectMessageReactions("/api/pmcs", projectId, messageId);
    assert.equal(view.canReact, true);
    assert.deepEqual(view.reactions.map(({ emoji, count, reactedByMe }) =>
      [emoji, count, reactedByMe]), [
      ["👍", 2, true], ["✅", 0, false], ["⚠️", 0, false], ["❤️", 0, false],
    ]);
    globalThis.fetch = async () => Response.json({ messageId: projectId, canReact: true, reactions: [] });
    await assert.rejects(loadProjectMessageReactions("/api/pmcs", projectId, messageId), /واکنش‌های پیام معتبر/u);
    globalThis.fetch = async () => Response.json({ messageId, canReact: true,
      reactions: [{ emoji: "👍", count: 0, reactedByMe: true }] });
    await assert.rejects(loadProjectMessageReactions("/api/pmcs", projectId, messageId), /واکنش‌های پیام معتبر/u);
  } finally { globalThis.fetch = previous; }
});

test("reaction mutations use the scoped endpoint and respect permission loss", async () => {
  const previous = globalThis.fetch;
  const calls: Array<{ url: string; method?: string }> = [];
  try {
    globalThis.fetch = async (input, init) => {
      calls.push({ url: String(input), method: init?.method });
      return init?.method === "DELETE" ? new Response(null, { status: 204 }) :
        Response.json({ messageId, emoji: "⚠️", reacted: true });
    };
    await setProjectMessageReaction("/api/pmcs", projectId, messageId, "⚠️", true);
    await setProjectMessageReaction("/api/pmcs", projectId, messageId, "⚠️", false);
    assert.equal(calls[0].method, "PUT");
    assert.equal(calls[1].method, "DELETE");
    assert.equal(calls[0].url, calls[1].url);
    assert.match(calls[0].url, new RegExp(`${projectId}/collaboration/messages/${messageId}/reactions/%E2%9A%A0%EF%B8%8F$`, "u"));
    globalThis.fetch = async () => new Response(null, { status: 403 });
    await assert.rejects(loadProjectMessageReactions("/api/pmcs", projectId, messageId),
      (error: unknown) => error instanceof CollaborationAccessError && error.status === 403);
  } finally { globalThis.fetch = previous; }
});

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

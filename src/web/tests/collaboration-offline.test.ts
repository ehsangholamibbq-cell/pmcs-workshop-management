import assert from "node:assert/strict";
import test from "node:test";
import { createQueuedCollaborationMessage } from "../lib/collaboration-offline.ts";

test("project message queue captures immutable stable identity and normalized text", () => {
  const item = createQueuedCollaborationMessage({
    tenantId: "tenant", userId: "actor", projectId: "project",
    body: "  پیام پروژه\r\nدوم  ", mentionedUserIds: ["member", "member"],
  });
  assert.match(item.clientMessageId, /^[0-9a-f-]{36}$/u);
  assert.equal(item.body, "پیام پروژه\nدوم");
  assert.deepEqual(item.mentionedUserIds, ["member"]);
  assert.equal(item.attemptCount, 0);
  assert.equal(item.projectId, "project");
});

test("project message queue rejects malformed body and self mention", () => {
  assert.throws(() => createQueuedCollaborationMessage({
    tenantId: "tenant", userId: "actor", projectId: "project", body: "\u0000bad",
  }));
  assert.throws(() => createQueuedCollaborationMessage({
    tenantId: "tenant", userId: "actor", projectId: "project", body: "متن",
    mentionedUserIds: ["actor"],
  }));
});

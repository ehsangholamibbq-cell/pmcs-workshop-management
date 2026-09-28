import assert from "node:assert/strict";
import test from "node:test";
import { CollaborationAccessError } from "../lib/collaboration-events.ts";
import { CollaborationActionValidationError, CollaborationIssueAlreadyExists,
  CollaborationIssueValidationError, convertProjectMessageToAction,
  convertProjectMessageToIssue, loadProjectMessageConversions } from "../lib/collaboration-conversions.ts";
import { CollaborationRevisionConflict } from "../lib/collaboration-interactions.ts";
import type { ProjectConversationMessage } from "../lib/collaboration-room.ts";

const projectId = "10000000-0000-4000-8000-000000000001";
const messageId = "10000000-0000-4000-8000-000000000011";
const item = {
  id: "10000000-0000-4000-8000-000000000021", messageId,
  messageRevision: 2, destinationType: "Action", destinationId: "10000000-0000-4000-8000-000000000031",
  destinationReference: "ACT-001", confirmedBy: "10000000-0000-4000-8000-000000000041",
  confirmedAt: "2026-09-28T00:00:00Z",
  documents: [{ id: "10000000-0000-4000-8000-000000000051", sha256: "a".repeat(64),
    versionNumber: 1, fileName: "مدرک.pdf", contentType: "application/pdf", sizeBytes: 100 }],
};

test("formal conversion lineage is a bounded scoped read with verified document hashes", async () => {
  const previous = globalThis.fetch;
  try {
    let url = "";
    globalThis.fetch = async (input, init) => {
      url = String(input);
      assert.equal(init?.cache, "no-store");
      return Response.json([item]);
    };
    assert.equal((await loadProjectMessageConversions("/api/pmcs", projectId, messageId))[0].destinationReference,
      "ACT-001");
    assert.match(url, new RegExp(`${projectId}/collaboration/messages/${messageId}/conversions$`, "u"));
    globalThis.fetch = async () => Response.json([{ ...item, messageId: projectId }]);
    await assert.rejects(loadProjectMessageConversions("/api/pmcs", projectId, messageId),
      /تبار تبدیل/u);
    globalThis.fetch = async () => Response.json([{ ...item,
      documents: [{ ...item.documents[0], sha256: "bad" }] }]);
    await assert.rejects(loadProjectMessageConversions("/api/pmcs", projectId, messageId),
      /تبار تبدیل/u);
    globalThis.fetch = async () => new Response(null, { status: 403 });
    await assert.rejects(loadProjectMessageConversions("/api/pmcs", projectId, messageId),
      (error: unknown) => error instanceof CollaborationAccessError && error.status === 403);
  } finally { globalThis.fetch = previous; }
});

test("confirmed Action conversion keeps destination and retry identity, scope and revision", async () => {
  const previous = globalThis.fetch;
  const actor = "10000000-0000-4000-8000-000000000041";
  const destinationId = item.destinationId;
  const key = "10000000-0000-4000-8000-000000000061";
  const message = { id: messageId, projectId, revision: 2, deletedAt: null, redactedAt: null,
    body: "پیام کارگاه" } as ProjectConversationMessage;
  const details = { assigneeUserId: actor, dueDate: "2099-01-01", priority: "High" as const,
    title: " اقدام رسمی ", description: " پیگیری " };
  try {
    let payload: unknown;
    globalThis.fetch = async (input, init) => {
      assert.match(String(input), new RegExp(`${messageId}/conversions$`, "u"));
      assert.equal(init?.method, "POST");
      assert.equal(new Headers(init?.headers).get("Idempotency-Key"), key);
      payload = JSON.parse(String(init?.body)) as unknown;
      return Response.json({ ...item, documents: [], confirmedBy: actor }, { status: 201 });
    };
    const result = await convertProjectMessageToAction("/api/pmcs", projectId, message,
      actor, details, destinationId, key);
    assert.equal(result.destinationType, "Action");
    assert.deepEqual(payload, { destinationId, destinationType: "Action", baseRevision: 2,
      confirmed: true, details: { assigneeUserId: actor, dueDate: "2099-01-01",
        priority: "High", title: "اقدام رسمی", description: "پیگیری" }, documentIds: [] });
    globalThis.fetch = async () => Response.json({ code: "collaboration.message.revision.conflict",
      currentRevision: 3 }, { status: 409 });
    await assert.rejects(convertProjectMessageToAction("/api/pmcs", projectId, message,
      actor, details, destinationId, key), (error: unknown) =>
        error instanceof CollaborationRevisionConflict && error.currentRevision === 3);
    globalThis.fetch = async () => new Response(null, { status: 422 });
    await assert.rejects(convertProjectMessageToAction("/api/pmcs", projectId, message,
      actor, details, destinationId, key), CollaborationActionValidationError);
    globalThis.fetch = async () => Response.json({ ...item, documents: [],
      destinationId: "20000000-0000-4000-8000-000000000002" }, { status: 201 });
    await assert.rejects(convertProjectMessageToAction("/api/pmcs", projectId, message,
      actor, details, destinationId, key), /تأیید تبدیل/u);
    globalThis.fetch = async () => new Response(null, { status: 403 });
    await assert.rejects(convertProjectMessageToAction("/api/pmcs", projectId, message,
      actor, details, destinationId, key), (error: unknown) =>
        error instanceof CollaborationAccessError && error.status === 403);
    await assert.rejects(convertProjectMessageToAction("/api/pmcs", projectId,
      { ...message, deletedAt: "2026-09-28T00:02:00Z" }, actor, details, destinationId, key),
    /مشخصات تبدیل/u);
  } finally { globalThis.fetch = previous; }
});

test("confirmed Issue conversion keeps general-project classification, actor and stable retry identity", async () => {
  const previous = globalThis.fetch;
  const actor = "10000000-0000-4000-8000-000000000041";
  const destinationId = "10000000-0000-4000-8000-000000000032";
  const key = "10000000-0000-4000-8000-000000000062";
  const message = { id: messageId, projectId, revision: 2, deletedAt: null, redactedAt: null,
    body: "مسئلهٔ کارگاه" } as ProjectConversationMessage;
  const details = { ownerUserId: actor, targetResolutionDate: "2099-01-01", title: " مسئلهٔ رسمی ",
    observedFact: " تأخیر تأیید شد ", category: " هماهنگی ", severity: "High" as const,
    urgency: "Immediate" as const };
  const response = { ...item, destinationType: "Issue", destinationId, documents: [],
    destinationReference: "ISS-001", confirmedBy: actor };
  try {
    let payload: unknown;
    globalThis.fetch = async (input, init) => {
      assert.match(String(input), new RegExp(`${messageId}/conversions$`, "u"));
      assert.equal(init?.method, "POST");
      assert.equal(init?.cache, "no-store");
      assert.equal(new Headers(init?.headers).get("Idempotency-Key"), key);
      payload = JSON.parse(String(init?.body)) as unknown;
      return Response.json(response, { status: 201 });
    };
    assert.equal((await convertProjectMessageToIssue("/api/pmcs", projectId, message,
      actor, details, destinationId, key)).destinationReference, "ISS-001");
    assert.deepEqual(payload, { destinationId, destinationType: "Issue", baseRevision: 2,
      confirmed: true, details: { ownerUserId: actor, targetResolutionDate: "2099-01-01",
        title: "مسئلهٔ رسمی", observedFact: "تأخیر تأیید شد", category: "هماهنگی",
        severity: "High", urgency: "Immediate", confidentiality: "GeneralProject" }, documentIds: [] });
    globalThis.fetch = async () => Response.json({ code: "collaboration.message.revision.conflict",
      currentRevision: 3 }, { status: 409 });
    await assert.rejects(convertProjectMessageToIssue("/api/pmcs", projectId, message,
      actor, details, destinationId, key), (error: unknown) =>
        error instanceof CollaborationRevisionConflict && error.currentRevision === 3);
    globalThis.fetch = async () => Response.json({ code: "collaboration.conversion.issue.already_created" },
      { status: 409 });
    await assert.rejects(convertProjectMessageToIssue("/api/pmcs", projectId, message,
      actor, details, destinationId, key), CollaborationIssueAlreadyExists);
    globalThis.fetch = async () => new Response(null, { status: 422 });
    await assert.rejects(convertProjectMessageToIssue("/api/pmcs", projectId, message,
      actor, details, destinationId, key), CollaborationIssueValidationError);
    globalThis.fetch = async () => Response.json({ ...response, messageRevision: 3 }, { status: 201 });
    await assert.rejects(convertProjectMessageToIssue("/api/pmcs", projectId, message,
      actor, details, destinationId, key), /تأیید تبدیل/u);
    globalThis.fetch = async () => new Response(null, { status: 403 });
    await assert.rejects(convertProjectMessageToIssue("/api/pmcs", projectId, message,
      actor, details, destinationId, key), (error: unknown) =>
        error instanceof CollaborationAccessError && error.status === 403);
    await assert.rejects(convertProjectMessageToIssue("/api/pmcs", projectId, message,
      actor, { ...details, ownerUserId: destinationId }, destinationId, key), /مشخصات تبدیل/u);
    await assert.rejects(convertProjectMessageToIssue("/api/pmcs", projectId,
      { ...message, redactedAt: "2026-09-28T00:02:00Z" }, actor, details, destinationId, key),
    /مشخصات تبدیل/u);
  } finally { globalThis.fetch = previous; }
});

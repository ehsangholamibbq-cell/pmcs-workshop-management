import assert from "node:assert/strict";
import test from "node:test";
import { CollaborationAccessError } from "../lib/collaboration-events.ts";
import { CollaborationActionValidationError, CollaborationIssueAlreadyExists,
  CollaborationIssueValidationError, CollaborationRfiAlreadyExists, CollaborationRfiValidationError,
  CollaborationDailyFactAlreadyExists, CollaborationDailyFactTargetConflict,
  CollaborationDailyFactValidationError, convertProjectMessageToDailyFact,
  CollaborationEvidenceSourceAlreadyExists, CollaborationEvidenceValidationError,
  convertProjectMessageToEvidence,
  convertProjectMessageToAction, convertProjectMessageToIssue, convertProjectMessageToRfi,
  loadProjectMessageConversions } from "../lib/collaboration-conversions.ts";
import { CollaborationRevisionConflict } from "../lib/collaboration-interactions.ts";
import type { ProjectConversationMessage } from "../lib/collaboration-room.ts";
import type { ProjectMessageAttachment } from "../lib/collaboration-attachments.ts";

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

test("confirmed Daily Fact appends to one Draft report revision with stable identity and scope", async () => {
  const previous = globalThis.fetch;
  const actor = "10000000-0000-4000-8000-000000000041";
  const destinationId = "10000000-0000-4000-8000-000000000034";
  const key = "10000000-0000-4000-8000-000000000064";
  const reportId = "10000000-0000-4000-8000-000000000071";
  const locationId = "10000000-0000-4000-8000-000000000072";
  const message = { id: messageId, projectId, revision: 2, deletedAt: null, redactedAt: null,
    body: "مشاهدهٔ روزانهٔ کارگاه" } as ProjectConversationMessage;
  const details = { reportId, baseReportRevision: 5, locationId,
    kind: "Material" as const, description: "  ورود مصالح  ", category: " بتن ",
    quantity: 12, unit: " مترمکعب ", resourceCount: null, hours: null, impactLevel: null };
  const response = { ...item, destinationType: "DailyFact", destinationId, documents: [],
    destinationReference: `${reportId}/${destinationId}`, confirmedBy: actor };
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
    assert.equal((await convertProjectMessageToDailyFact("/api/pmcs", projectId, message,
      actor, details, destinationId, key)).destinationType, "DailyFact");
    assert.deepEqual(payload, { destinationId, destinationType: "DailyFact", baseRevision: 2,
      confirmed: true, details: { ...details, description: "ورود مصالح", category: "بتن", unit: "مترمکعب" },
      documentIds: [] });
    globalThis.fetch = async () => Response.json({ code: "collaboration.message.revision.conflict",
      currentRevision: 3 }, { status: 409 });
    await assert.rejects(convertProjectMessageToDailyFact("/api/pmcs", projectId, message,
      actor, details, destinationId, key), (error: unknown) =>
        error instanceof CollaborationRevisionConflict && error.currentRevision === 3);
    globalThis.fetch = async () => Response.json({ code: "collaboration.conversion.fact.report.conflict" },
      { status: 409 });
    await assert.rejects(convertProjectMessageToDailyFact("/api/pmcs", projectId, message,
      actor, details, destinationId, key), CollaborationDailyFactTargetConflict);
    globalThis.fetch = async () => Response.json({ code: "collaboration.conversion.fact.already_created" },
      { status: 409 });
    await assert.rejects(convertProjectMessageToDailyFact("/api/pmcs", projectId, message,
      actor, details, destinationId, key), CollaborationDailyFactAlreadyExists);
    globalThis.fetch = async () => new Response(null, { status: 422 });
    await assert.rejects(convertProjectMessageToDailyFact("/api/pmcs", projectId, message,
      actor, details, destinationId, key), CollaborationDailyFactValidationError);
    globalThis.fetch = async () => Response.json({ ...response, destinationId: reportId }, { status: 201 });
    await assert.rejects(convertProjectMessageToDailyFact("/api/pmcs", projectId, message,
      actor, details, destinationId, key), /تأیید تبدیل/u);
    globalThis.fetch = async () => new Response(null, { status: 403 });
    await assert.rejects(convertProjectMessageToDailyFact("/api/pmcs", projectId, message,
      actor, details, destinationId, key), (error: unknown) =>
        error instanceof CollaborationAccessError && error.status === 403);
    await assert.rejects(convertProjectMessageToDailyFact("/api/pmcs", projectId, message,
      actor, { ...details, baseReportRevision: 0 }, destinationId, key), /مشخصات تبدیل/u);
    await assert.rejects(convertProjectMessageToDailyFact("/api/pmcs", projectId, message,
      actor, { ...details, kind: "Labor", resourceCount: 0 }, destinationId, key), /مشخصات تبدیل/u);
    await assert.rejects(convertProjectMessageToDailyFact("/api/pmcs", projectId,
      { ...message, projectId: reportId }, actor, details, destinationId, key), /مشخصات تبدیل/u);
  } finally { globalThis.fetch = previous; }
});

test("confirmed Evidence preserves one Released chat document hash and checks the owner response", async () => {
  const previous = globalThis.fetch;
  const actor = "10000000-0000-4000-8000-000000000041";
  const destinationId = "10000000-0000-4000-8000-000000000035";
  const key = "10000000-0000-4000-8000-000000000065";
  const reportId = "10000000-0000-4000-8000-000000000071";
  const factId = "10000000-0000-4000-8000-000000000072";
  const message = { id: messageId, projectId, revision: 2, deletedAt: null, redactedAt: null,
    body: "مدرک کارگاه" } as ProjectConversationMessage;
  const attachment = { messageId, documentId: item.documents[0].id,
    originalFileName: item.documents[0].fileName, contentType: "application/pdf",
    sizeBytes: 100, sha256: "a".repeat(64), versionNumber: 1,
    releasedAt: "2026-09-28T00:01:00Z" } as ProjectMessageAttachment;
  const response = { ...item, destinationType: "Evidence", destinationId,
    destinationReference: "EVD-001", confirmedBy: actor };
  try {
    let payload: unknown;
    globalThis.fetch = async (input, init) => {
      assert.match(String(input), new RegExp(`${messageId}/conversions$`, "u"));
      assert.equal(init?.method, "POST");
      assert.equal(new Headers(init?.headers).get("Idempotency-Key"), key);
      payload = JSON.parse(String(init?.body)) as unknown;
      return Response.json(response, { status: 201 });
    };
    assert.equal((await convertProjectMessageToEvidence("/api/pmcs", projectId, message,
      actor, attachment, reportId, factId, destinationId, key)).destinationType, "Evidence");
    assert.deepEqual(payload, { destinationId, destinationType: "Evidence", baseRevision: 2,
      confirmed: true, details: { dailyReportId: reportId, dailyFactId: factId },
      documentIds: [attachment.documentId] });
    globalThis.fetch = async () => Response.json({ code: "collaboration.message.revision.conflict",
      currentRevision: 3 }, { status: 409 });
    await assert.rejects(convertProjectMessageToEvidence("/api/pmcs", projectId, message,
      actor, attachment, reportId, null, destinationId, key), (error: unknown) =>
        error instanceof CollaborationRevisionConflict && error.currentRevision === 3);
    globalThis.fetch = async () => Response.json({ code: "collaboration.conversion.evidence.source.already_created" },
      { status: 409 });
    await assert.rejects(convertProjectMessageToEvidence("/api/pmcs", projectId, message,
      actor, attachment, reportId, factId, destinationId, key), CollaborationEvidenceSourceAlreadyExists);
    globalThis.fetch = async () => new Response(null, { status: 422 });
    await assert.rejects(convertProjectMessageToEvidence("/api/pmcs", projectId, message,
      actor, attachment, reportId, factId, destinationId, key), CollaborationEvidenceValidationError);
    globalThis.fetch = async () => Response.json({ ...response,
      documents: [{ ...item.documents[0], sha256: "b".repeat(64) }] }, { status: 201 });
    await assert.rejects(convertProjectMessageToEvidence("/api/pmcs", projectId, message,
      actor, attachment, reportId, factId, destinationId, key), /تبار فایل/u);
    globalThis.fetch = async () => new Response(null, { status: 403 });
    await assert.rejects(convertProjectMessageToEvidence("/api/pmcs", projectId, message,
      actor, attachment, reportId, factId, destinationId, key), (error: unknown) =>
        error instanceof CollaborationAccessError && error.status === 403);
    await assert.rejects(convertProjectMessageToEvidence("/api/pmcs", projectId, message,
      actor, { ...attachment, contentType: "text/plain" }, reportId, factId, destinationId, key),
    /مشخصات تبدیل/u);
    await assert.rejects(convertProjectMessageToEvidence("/api/pmcs", projectId, message,
      actor, { ...attachment, messageId: reportId }, reportId, factId, destinationId, key),
    /مشخصات تبدیل/u);
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

test("confirmed RFI creates only a Draft owner command with explicit impact flags and scoped identity", async () => {
  const previous = globalThis.fetch;
  const actor = "10000000-0000-4000-8000-000000000041";
  const destinationId = "10000000-0000-4000-8000-000000000033";
  const key = "10000000-0000-4000-8000-000000000063";
  const message = { id: messageId, projectId, revision: 2, deletedAt: null, redactedAt: null,
    body: "پرسش فنی کارگاه" } as ProjectConversationMessage;
  const details = { title: " پرسش بتن ", question: " مغایرت مشخصات ", requestedFrom: " مشاور ",
    discipline: " سازه ", requiredByDate: "2099-01-01", potentialImpacts: ["Time", "Quality"] as const,
    isBlocking: true, proposedSolution: " بررسی نقشه " };
  const response = { ...item, destinationType: "RFI", destinationId, documents: [],
    destinationReference: "RFI-001", confirmedBy: actor };
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
    assert.equal((await convertProjectMessageToRfi("/api/pmcs", projectId, message,
      actor, details, destinationId, key)).destinationReference, "RFI-001");
    assert.deepEqual(payload, { destinationId, destinationType: "RFI", baseRevision: 2,
      confirmed: true, details: { title: "پرسش بتن", question: "مغایرت مشخصات",
        requestedFrom: "مشاور", discipline: "سازه", requiredByDate: "2099-01-01",
        potentialImpact: "Time, Quality", isBlocking: true, proposedSolution: "بررسی نقشه" },
      documentIds: [] });
    globalThis.fetch = async () => Response.json({ code: "collaboration.message.revision.conflict",
      currentRevision: 3 }, { status: 409 });
    await assert.rejects(convertProjectMessageToRfi("/api/pmcs", projectId, message,
      actor, details, destinationId, key), (error: unknown) =>
        error instanceof CollaborationRevisionConflict && error.currentRevision === 3);
    globalThis.fetch = async () => Response.json({ code: "collaboration.conversion.rfi.already_created" },
      { status: 409 });
    await assert.rejects(convertProjectMessageToRfi("/api/pmcs", projectId, message,
      actor, details, destinationId, key), CollaborationRfiAlreadyExists);
    globalThis.fetch = async () => new Response(null, { status: 422 });
    await assert.rejects(convertProjectMessageToRfi("/api/pmcs", projectId, message,
      actor, details, destinationId, key), CollaborationRfiValidationError);
    globalThis.fetch = async () => Response.json({ ...response, destinationType: "Action" }, { status: 201 });
    await assert.rejects(convertProjectMessageToRfi("/api/pmcs", projectId, message,
      actor, details, destinationId, key), /تأیید تبدیل/u);
    globalThis.fetch = async () => new Response(null, { status: 403 });
    await assert.rejects(convertProjectMessageToRfi("/api/pmcs", projectId, message,
      actor, details, destinationId, key), (error: unknown) =>
        error instanceof CollaborationAccessError && error.status === 403);
    await assert.rejects(convertProjectMessageToRfi("/api/pmcs", projectId, message,
      actor, { ...details, potentialImpacts: ["Time", "Time"] }, destinationId, key),
    /مشخصات تبدیل/u);
    await assert.rejects(convertProjectMessageToRfi("/api/pmcs", projectId,
      { ...message, projectId: destinationId }, actor, details, destinationId, key),
    /مشخصات تبدیل/u);
  } finally { globalThis.fetch = previous; }
});

import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import test from "node:test";
import { CollaborationAccessError } from "../lib/collaboration-events.ts";
import {
  attachProjectChatDocument, downloadProjectMessageAttachment, loadProjectMessageAttachments,
  loadProjectChatUploadState,
  type ProjectMessageAttachment,
} from "../lib/collaboration-attachments.ts";

const projectId = "10000000-0000-4000-8000-000000000001";
const messageId = "10000000-0000-4000-8000-000000000011";
const documentId = "10000000-0000-4000-8000-000000000101";
const content = "PMCS collaboration document";
const attachment: ProjectMessageAttachment = {
  messageId, documentId, originalFileName: "گزارش.pdf", contentType: "application/pdf",
  sizeBytes: Buffer.byteLength(content), sha256: createHash("sha256").update(content).digest("hex"),
  classification: "Internal", retentionPolicy: "Standard", legalHold: false,
  releasedAt: "2026-09-28T00:00:00Z", versionNumber: 1,
  contentUrl: `/api/v1/projects/${projectId}/collaboration/messages/${messageId}/attachments/${documentId}/content`,
};

test("Released attachment list stays within one live message and rejects foreign metadata", async () => {
  const previous = globalThis.fetch;
  const calls: string[] = [];
  try {
    globalThis.fetch = async (input) => { calls.push(String(input)); return Response.json([attachment]); };
    assert.deepEqual(await loadProjectMessageAttachments("/api/pmcs", projectId, messageId), [attachment]);
    assert.equal(calls[0], `/api/pmcs/api/v1/projects/${projectId}/collaboration/messages/${messageId}/attachments`);
    globalThis.fetch = async () => Response.json([{ ...attachment, messageId: projectId }]);
    await assert.rejects(loadProjectMessageAttachments("/api/pmcs", projectId, messageId), /فهرست پیوست/u);
    globalThis.fetch = async () => Response.json([{ ...attachment,
      contentUrl: `/api/v1/projects/${projectId}/collaboration/messages/${messageId}/attachments/${projectId}/content` }]);
    await assert.rejects(loadProjectMessageAttachments("/api/pmcs", projectId, messageId), /فهرست پیوست/u);
    globalThis.fetch = async () => Response.json([attachment, attachment]);
    await assert.rejects(loadProjectMessageAttachments("/api/pmcs", projectId, messageId), /فهرست پیوست/u);
  } finally { globalThis.fetch = previous; }
});

test("attachment download verifies scoped content, size, MIME and SHA-256", async () => {
  const previous = globalThis.fetch;
  const calls: string[] = [];
  try {
    globalThis.fetch = async (input) => {
      calls.push(String(input));
      return new Response(content, { status: 200, headers: {
        "content-type": attachment.contentType, "content-length": String(attachment.sizeBytes),
      } });
    };
    const blob = await downloadProjectMessageAttachment("/api/pmcs", projectId, messageId, attachment);
    assert.equal(await blob.text(), content);
    assert.equal(calls[0], `/api/pmcs${attachment.contentUrl}`);
    globalThis.fetch = async () => new Response("different content", { status: 200,
      headers: { "content-type": attachment.contentType } });
    await assert.rejects(downloadProjectMessageAttachment("/api/pmcs", projectId, messageId, attachment),
      /اندازهٔ پیوست/u);
    globalThis.fetch = async () => new Response(content, { status: 200,
      headers: { "content-type": "text/plain" } });
    await assert.rejects(downloadProjectMessageAttachment("/api/pmcs", projectId, messageId, attachment),
      /محتوای پیوست/u);
    globalThis.fetch = async () => new Response(content, { status: 200,
      headers: { "content-type": attachment.contentType } });
    await assert.rejects(downloadProjectMessageAttachment("/api/pmcs", projectId, messageId,
      { ...attachment, sha256: "0".repeat(64) }), /صحت پیوست/u);
  } finally { globalThis.fetch = previous; }
});

test("revoked permission and invalid identity fail before any unrestricted download", async () => {
  const previous = globalThis.fetch;
  let requested = 0;
  try {
    globalThis.fetch = async () => { requested += 1; return new Response(null, { status: 403 }); };
    await assert.rejects(loadProjectMessageAttachments("/api/pmcs", projectId, messageId),
      (error: unknown) => error instanceof CollaborationAccessError && error.status === 403);
    await assert.rejects(downloadProjectMessageAttachment("/api/pmcs", projectId, messageId, attachment),
      (error: unknown) => error instanceof CollaborationAccessError && error.status === 403);
    assert.equal(requested, 2);
    await assert.rejects(downloadProjectMessageAttachment("/api/pmcs", projectId, projectId, attachment),
      /مشخصات پیوست/u);
    assert.equal(requested, 2);
  } finally { globalThis.fetch = previous; }
});

test("author-only upload status validates owner identity and quarantine transition", async () => {
  const previous = globalThis.fetch;
  const expected = { assetId: documentId, originalFileName: attachment.originalFileName,
    contentType: attachment.contentType, sizeBytes: attachment.sizeBytes, sha256: attachment.sha256 };
  const calls: string[] = [];
  try {
    globalThis.fetch = async (input) => {
      calls.push(String(input));
      return Response.json({ id: documentId, messageId, originalFileName: expected.originalFileName,
        contentType: expected.contentType, sizeBytes: expected.sizeBytes, sha256: expected.sha256,
        status: "Quarantined", versionNumber: 1, releasedAt: null });
    };
    assert.equal((await loadProjectChatUploadState("/api/pmcs", projectId, messageId, expected)).status,
      "Quarantined");
    assert.equal(calls[0], `/api/pmcs/api/v1/projects/${projectId}/collaboration/messages/${messageId}/uploads/${documentId}`);
    globalThis.fetch = async () => Response.json({ id: documentId, messageId: projectId,
      ...expected, status: "Released", versionNumber: 1, releasedAt: null });
    await assert.rejects(loadProjectChatUploadState("/api/pmcs", projectId, messageId, expected),
      /وضعیت آپلود/u);
    globalThis.fetch = async () => new Response(null, { status: 403 });
    await assert.rejects(loadProjectChatUploadState("/api/pmcs", projectId, messageId, expected),
      (error: unknown) => error instanceof CollaborationAccessError && error.status === 403);
  } finally { globalThis.fetch = previous; }
});

test("Released association requires a matching server response and rejects access loss", async () => {
  const previous = globalThis.fetch;
  const calls: Array<{ url: string; method?: string }> = [];
  const expected = { assetId: documentId, originalFileName: attachment.originalFileName,
    contentType: attachment.contentType, sizeBytes: attachment.sizeBytes, sha256: attachment.sha256 };
  try {
    globalThis.fetch = async (input, init) => {
      calls.push({ url: String(input), method: init?.method });
      return Response.json(attachment);
    };
    assert.deepEqual(await attachProjectChatDocument("/api/pmcs", projectId, messageId, expected), attachment);
    assert.deepEqual(calls, [{ url: `/api/pmcs/api/v1/projects/${projectId}/collaboration/messages/${messageId}/attachments/${documentId}`,
      method: "PUT" }]);
    globalThis.fetch = async () => Response.json({ ...attachment, documentId: projectId });
    await assert.rejects(attachProjectChatDocument("/api/pmcs", projectId, messageId, expected),
      /تأیید اتصال/u);
    globalThis.fetch = async () => new Response(null, { status: 403 });
    await assert.rejects(attachProjectChatDocument("/api/pmcs", projectId, messageId, expected),
      (error: unknown) => error instanceof CollaborationAccessError && error.status === 403);
  } finally { globalThis.fetch = previous; }
});

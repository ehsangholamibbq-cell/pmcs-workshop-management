import assert from "node:assert/strict";
import test from "node:test";
import { CollaborationAccessError } from "../lib/collaboration-events.ts";
import { loadProjectMessageConversions } from "../lib/collaboration-conversions.ts";

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

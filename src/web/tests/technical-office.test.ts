import assert from "node:assert/strict";
import test from "node:test";
import {
  createDocumentRevision,
  createRfi,
  createTechnicalDocument,
  getTechnicalOfficeState,
  transitionDocumentRevision,
  transitionRfi,
  type DocumentRevisionModel,
  type RfiModel,
} from "../lib/technical-office.ts";

const identity = { tenantId: "tenant-id", userId: "user-id" };

test("technical office state stays project and actor scoped", async () => {
  const originalFetch = globalThis.fetch;
  let url = "";
  let headers: HeadersInit | undefined;
  globalThis.fetch = async (input, init) => {
    url = String(input);
    headers = init?.headers;
    return Response.json({
      documents: [], documentRevisions: [], transmittals: [], rfis: [], submittals: [],
      blockingRfiCount: 0, overdueRfiCount: 0, overdueSubmittalCount: 0, supersededRevisionCount: 0,
    });
  };
  try {
    await getTechnicalOfficeState("https://pmcs.test/", identity, "project-id");
    const sent = new Headers(headers);
    assert.equal(url, "https://pmcs.test/api/v1/projects/project-id/technical-office/state");
    assert.equal(sent.get("X-Tenant-Id"), "tenant-id");
    assert.equal(sent.get("X-User-Id"), "user-id");
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("document number is never accepted from the browser and revision carries hash lineage", async () => {
  const originalFetch = globalThis.fetch;
  const calls: { url: string; init?: RequestInit }[] = [];
  globalThis.fetch = async (input, init) => {
    calls.push({ url: String(input), init });
    return Response.json({ id: `result-${calls.length}`, revision: 1 }, { status: 201 });
  };
  try {
    await createTechnicalDocument("https://pmcs.test", identity, "project-id", {
      title: "Drawing", type: "Drawing", discipline: "Electrical",
    });
    await createDocumentRevision("https://pmcs.test", identity, "project-id", "document-id", {
      revisionCode: "A", revisionDate: "2026-09-11", purpose: "ForApproval",
      fileName: "drawing.pdf", fileReference: "evidence:1",
      sha256: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
    });

    const documentBody = JSON.parse(String(calls[0]?.init?.body));
    const revisionBody = JSON.parse(String(calls[1]?.init?.body));
    assert.equal(documentBody.number, undefined);
    assert.equal(documentBody.wbsReference, null);
    assert.equal(revisionBody.documentNumber, undefined);
    assert.equal(revisionBody.sha256.length, 64);
    assert.ok(new Headers(calls[1]?.init?.headers).get("Idempotency-Key"));
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("formal revision and RFI transitions carry current server revision", async () => {
  const originalFetch = globalThis.fetch;
  const calls: { url: string; init?: RequestInit }[] = [];
  globalThis.fetch = async (input, init) => {
    calls.push({ url: String(input), init });
    return Response.json({ id: "result", revision: 4 });
  };
  try {
    await transitionDocumentRevision(
      "https://pmcs.test", identity, "project-id", revisionModel(), "approve", "reviewed");
    await transitionRfi(
      "https://pmcs.test", identity, "project-id", rfiModel(), "accept", "sufficient");

    assert.equal(calls[0]?.url, "https://pmcs.test/api/v1/projects/project-id/technical-office/document-revisions/revision-id/approve");
    assert.deepEqual(JSON.parse(String(calls[0]?.init?.body)), { baseRevision: 3, comment: "reviewed" });
    assert.equal(calls[1]?.url, "https://pmcs.test/api/v1/projects/project-id/technical-office/rfis/rfi-id/accept");
    assert.deepEqual(JSON.parse(String(calls[1]?.init?.body)), { baseRevision: 5, comment: "sufficient" });
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("RFI draft preserves evidence and independent impact flags without client numbering", async () => {
  const originalFetch = globalThis.fetch;
  let body: Record<string, unknown> = {};
  globalThis.fetch = async (_input, init) => {
    body = JSON.parse(String(init?.body)) as Record<string, unknown>;
    return Response.json({ id: "rfi-id", revision: 1 }, { status: 201 });
  };
  try {
    await createRfi("https://pmcs.test", identity, "project-id", {
      title: "Question", question: "Which route?", requestedFrom: "Consultant",
      discipline: "Electrical", raisedDate: "2026-09-11", potentialImpact: 11,
      isBlocking: true, evidenceReferences: ["evidence:photo-1"],
    });
    assert.equal(body.number, undefined);
    assert.equal(body.potentialImpact, 11);
    assert.deepEqual(body.evidenceReferences, ["evidence:photo-1"]);
    assert.equal(body.wbsReference, null);
  } finally {
    globalThis.fetch = originalFetch;
  }
});

function revisionModel(): DocumentRevisionModel {
  return {
    id: "revision-id", documentId: "document-id", revisionCode: "A", revisionDate: "2026-09-11",
    purpose: "ForApproval", fileName: "drawing.pdf", fileReference: "evidence:1",
    sha256: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
    supersedesRevisionId: null, status: "Submitted", reviewComment: null,
    issuedThroughTransmittalId: null, issuedAt: null, supersededAt: null, revision: 3,
  };
}

function rfiModel(): RfiModel {
  return {
    id: "rfi-id", number: "RFI-1", title: "Question", question: "Which route?",
    requestedFrom: "Consultant", discipline: "Electrical", contractId: null,
    locationReference: null, workItemReference: null, wbsReference: null, sourceIssueId: null,
    raisedDate: "2026-09-11", requiredByDate: null, potentialImpact: 1, isBlocking: false,
    proposedSolution: null, evidenceReferences: ["evidence:1"], relatedRevisionIds: [],
    responses: [], status: "Answered", submittedAt: "2026-09-11T10:00:00Z", closedAt: null, revision: 5,
  };
}

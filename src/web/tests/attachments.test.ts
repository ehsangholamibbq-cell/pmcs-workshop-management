import assert from "node:assert/strict";
import test from "node:test";
import { buildEvidenceSessionPayload, type QueuedAttachment } from "../lib/attachment-store.ts";

test("attachment upload intent keeps stable id, lineage and sha256", () => {
  const attachment: QueuedAttachment = {
    attachmentId: "attachment-id",
    tenantId: "tenant-id",
    userId: "user-id",
    projectId: "project-id",
    dailyReportId: "report-id",
    dailyFactId: "fact-id",
    originalFileName: "photo.jpg",
    contentType: "image/jpeg",
    sizeBytes: 123,
    sha256: "a".repeat(64),
    capturedAtDevice: "2026-09-09T08:00:00Z",
    blob: new Blob(["evidence"], { type: "image/jpeg" }),
    status: "queued",
    attemptCount: 0,
  };

  assert.deepEqual(buildEvidenceSessionPayload(attachment), {
    clientGeneratedId: "attachment-id",
    dailyReportId: "report-id",
    dailyFactId: "fact-id",
    originalFileName: "photo.jpg",
    contentType: "image/jpeg",
    sizeBytes: 123,
    sha256: "a".repeat(64),
    capturedAtDevice: "2026-09-09T08:00:00Z",
  });
});

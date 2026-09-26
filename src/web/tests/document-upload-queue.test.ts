import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import {
  buildDocumentSessionPayload,
  type QueuedDocumentUpload,
} from "../lib/document-upload-queue.ts";

const source = readFileSync(new URL("../lib/document-upload-queue.ts", import.meta.url), "utf8");

test("shared document retry preserves owner, governance, hash and stable client identity", () => {
  const item: QueuedDocumentUpload = {
    assetId: "asset-id",
    tenantId: "tenant-id",
    userId: "user-id",
    projectId: "project-id",
    scopeKey: "project-id",
    ownerType: "ProjectChat",
    ownerId: "thread-id",
    originalFileName: "صورتجلسه.pdf",
    contentType: "application/pdf",
    sizeBytes: 123,
    sha256: "a".repeat(64),
    classification: "Confidential",
    retentionPolicy: "LongTerm",
    retainUntil: "2036-09-18T08:00:00Z",
    legalHold: false,
    createdAtDevice: "2026-09-18T08:00:00Z",
    blob: new Blob(["%PDF-1.7"]),
    status: "queued",
    attemptCount: 2,
  };

  assert.deepEqual(buildDocumentSessionPayload(item), {
    clientGeneratedId: "asset-id",
    projectId: "project-id",
    ownerType: "ProjectChat",
    ownerId: "thread-id",
    originalFileName: "صورتجلسه.pdf",
    contentType: "application/pdf",
    sizeBytes: 123,
    sha256: "a".repeat(64),
    classification: "Confidential",
    retentionPolicy: "LongTerm",
    retainUntil: "2036-09-18T08:00:00Z",
    legalHold: false,
  });
});

test("offline retry reuses one idempotency identity and recovers interrupted state", () => {
  assert.ok(source.includes('"Idempotency-Key": `${item.assetId}:session`'));
  assert.ok(source.includes('"Idempotency-Key": `${item.assetId}:content`'));
  assert.ok(!source.includes("${item.attemptCount}:session"));
  assert.match(source, /status: "queued" as const/u);
});

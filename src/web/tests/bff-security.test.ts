import assert from "node:assert/strict";
import test from "node:test";
import {
  browserResponseHeaders,
  pmcsUpstreamPath,
  upstreamRequestHeaders,
} from "../lib/bff-security.ts";

test("BFF only forwards PMCS API version one paths", () => {
  assert.equal(
    pmcsUpstreamPath(["api", "v1", "projects", "project id"]),
    "/api/v1/projects/project%20id",
  );
  assert.throws(() => pmcsUpstreamPath(["health"]));
  assert.throws(() => pmcsUpstreamPath(["api", "v1", "..", "health"]));
});

test("BFF replaces browser identity headers with its server-side access token", () => {
  const source = new Headers({
    Authorization: "Bearer browser-token",
    Cookie: "secret=cookie",
    "X-Tenant-Id": "spoofed-tenant",
    "X-User-Id": "spoofed-user",
    "Content-Type": "application/json",
    "Idempotency-Key": "operation-1",
    "X-Pmcs-Sync-Session": "sync-session-1",
  });

  const result = upstreamRequestHeaders(source, "server-token");

  assert.equal(result.get("Authorization"), "Bearer server-token");
  assert.equal(result.get("Content-Type"), "application/json");
  assert.equal(result.get("Idempotency-Key"), "operation-1");
  assert.equal(result.get("X-Pmcs-Sync-Session"), "sync-session-1");
  assert.equal(result.get("Cookie"), null);
  assert.equal(result.get("X-Tenant-Id"), null);
  assert.equal(result.get("X-User-Id"), null);
});

test("BFF response never forwards cookies and is not cacheable", () => {
  const result = browserResponseHeaders(new Headers({
    "Content-Type": "application/json",
    "Set-Cookie": "upstream=value",
    ETag: '"revision-2"',
  }));

  assert.equal(result.get("Set-Cookie"), null);
  assert.equal(result.get("Cache-Control"), "no-store");
  assert.equal(result.get("ETag"), '"revision-2"');
});

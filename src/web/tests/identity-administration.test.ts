import assert from "node:assert/strict";
import test from "node:test";
import {
  changeUserStatus,
  inviteUser,
  revokeMembership,
  upsertMembership,
} from "../lib/identity-administration.ts";

test("invitation is sent as an idempotent server command", async () => {
  const originalFetch = globalThis.fetch;
  let capturedUrl = "";
  let capturedInit: RequestInit | undefined;
  globalThis.fetch = async (input, init) => {
    capturedUrl = String(input);
    capturedInit = init;
    return Response.json({ id: "invitation-id" }, { status: 202 });
  };

  try {
    await inviteUser("https://pmcs.test/", {
      displayName: "کاربر نمونه",
      email: "user@example.com",
      tenantRole: "Member",
      projects: [{ projectId: "project-id", roleCode: "Observer" }],
    });
    assert.equal(capturedUrl, "https://pmcs.test/api/v1/identity/invitations");
    assert.equal(capturedInit?.method, "POST");
    assert.ok(new Headers(capturedInit?.headers).get("Idempotency-Key"));
    assert.deepEqual(JSON.parse(String(capturedInit?.body)), {
      displayName: "کاربر نمونه",
      email: "user@example.com",
      tenantRole: "Member",
      projects: [{ projectId: "project-id", roleCode: "Observer" }],
    });
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("account suspension and membership changes use explicit commands", async () => {
  const originalFetch = globalThis.fetch;
  const calls: Array<{ url: string; method?: string; body?: BodyInit | null }> = [];
  globalThis.fetch = async (input, init) => {
    calls.push({ url: String(input), method: init?.method, body: init?.body });
    return Response.json({}, { status: 200 });
  };

  try {
    await changeUserStatus("/api/pmcs", "user-id", "Suspended");
    await upsertMembership("/api/pmcs", "user-id", "project-id", "SiteSupervisor");
    await revokeMembership("/api/pmcs", "user-id", "project-id");

    assert.deepEqual(calls.map((call) => [call.method, call.url]), [
      ["PUT", "/api/pmcs/api/v1/identity/users/user-id/status"],
      ["PUT", "/api/pmcs/api/v1/identity/users/user-id/memberships/project-id"],
      ["DELETE", "/api/pmcs/api/v1/identity/users/user-id/memberships/project-id"],
    ]);
    assert.deepEqual(JSON.parse(String(calls[0]?.body)), { status: "Suspended" });
  } finally {
    globalThis.fetch = originalFetch;
  }
});

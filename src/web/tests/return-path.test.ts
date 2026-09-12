import assert from "node:assert/strict";
import test from "node:test";
import { safeApplicationReturnPath } from "../lib/return-path.ts";

test("return path only accepts application pages", () => {
  assert.equal(safeApplicationReturnPath("/portfolio?sort=attention"), "/portfolio?sort=attention");
  assert.equal(
    safeApplicationReturnPath("/projects/11111111-1111-4111-8111-111111111111"),
    "/projects/11111111-1111-4111-8111-111111111111",
  );
  assert.equal(safeApplicationReturnPath("/api/auth/sign-out"), "/portfolio");
  assert.equal(safeApplicationReturnPath("/admin/users"), "/admin/users");
  assert.equal(safeApplicationReturnPath("/admin/unknown"), "/portfolio");
  assert.equal(safeApplicationReturnPath("//example.com"), "/portfolio");
  assert.equal(safeApplicationReturnPath("https://example.com"), "/portfolio");
});

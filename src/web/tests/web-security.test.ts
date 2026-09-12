import assert from "node:assert/strict";
import test from "node:test";
import { webSecurityHeaders } from "../lib/web-security.ts";

test("web responses deny framing and restrict browser capabilities", () => {
  const headers = new Map(webSecurityHeaders.map((header) => [header.key, header.value]));

  assert.equal(headers.get("X-Frame-Options"), "DENY");
  assert.match(headers.get("Content-Security-Policy") ?? "", /frame-ancestors 'none'/u);
  assert.match(headers.get("Content-Security-Policy") ?? "", /connect-src 'self'/u);
  assert.match(headers.get("Permissions-Policy") ?? "", /microphone=\(\)/u);
});

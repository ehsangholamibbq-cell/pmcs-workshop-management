import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import { profileAssetUrl } from "../lib/member-profile.ts";

test("profile assets are only routed through the authenticated BFF", () => {
  assert.equal(
    profileAssetUrl("/api/v1/member-profiles/user/avatar?v=asset-2"),
    "/api/pmcs/api/v1/member-profiles/user/avatar?v=asset-2",
  );
  assert.equal(profileAssetUrl("https://untrusted.example/avatar.png"), null);
});

test("profile editor uses shared document quarantine before association", () => {
  const source = readFileSync(new URL("../components/member-profile.tsx", import.meta.url), "utf8");
  assert.match(source, /ownerType: "MemberProfile"/u);
  assert.match(source, /classification: "Confidential"/u);
  assert.match(source, /document\.status === "Quarantined"/u);
  assert.match(source, /releaseOwnProfileImage/u);
  assert.match(source, /document\.status !== "Released"/u);
});

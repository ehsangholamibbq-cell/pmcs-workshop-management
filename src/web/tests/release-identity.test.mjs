import assert from "node:assert/strict";
import test from "node:test";
import { createReleaseIdentity } from "../tools/release-identity.mjs";

const valid = {
  PMCS_RELEASE_REQUIRED: "true",
  PMCS_RELEASE_COMMIT: "6dea26efbc75eac5d195c630bb4c4b9b64ccb7b2",
  PMCS_RELEASE_VERSION: "1.20.0-rc.1",
  PMCS_RELEASE_BUILT_AT: "2026-09-12T12:00:00.000Z",
};

test("web release identity binds the built artifact to commit, version and time", () => {
  assert.deepEqual(createReleaseIdentity(valid), {
    schemaVersion: 1,
    artifact: "web",
    commit: valid.PMCS_RELEASE_COMMIT,
    version: valid.PMCS_RELEASE_VERSION,
    builtAt: valid.PMCS_RELEASE_BUILT_AT,
  });
});

test("strict web release identity rejects missing or malformed provenance", () => {
  assert.throws(() => createReleaseIdentity({ ...valid, PMCS_RELEASE_COMMIT: "6dea26e" }), /40-character/u);
  assert.throws(() => createReleaseIdentity({ ...valid, PMCS_RELEASE_VERSION: "release" }), /semantic/u);
  assert.throws(() => createReleaseIdentity({ ...valid, PMCS_RELEASE_BUILT_AT: "2026-09-12" }), /UTC/u);
});

test("developer builds remain explicit and never impersonate a release", () => {
  assert.deepEqual(createReleaseIdentity({}), {
    schemaVersion: 1,
    artifact: "web",
    commit: "development",
    version: "0.0.0-dev",
    builtAt: "unknown",
  });
});

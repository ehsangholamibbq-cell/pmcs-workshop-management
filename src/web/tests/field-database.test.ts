import assert from "node:assert/strict";
import test from "node:test";
import {
  clearLocalIdentityScope,
  scopedStorageKey,
  setLocalIdentityScope,
} from "../lib/field-database.ts";

test("local storage keys are isolated by tenant and user", async () => {
  setLocalIdentityScope("tenant-a", "user-a");
  assert.equal(
    scopedStorageKey("pmcs-command-center:project-a"),
    "pmcs-command-center:project-a:tenant-a:user-a",
  );

  await clearLocalIdentityScope();
  assert.throws(() => scopedStorageKey("pmcs-command-center:project-a"));
});

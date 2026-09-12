import assert from "node:assert/strict";
import test from "node:test";
import { createUlid } from "../lib/offline-ulid.ts";

test("creates a canonical 26 character ULID", () => {
  const value = createUlid(1_725_840_000_000);
  assert.match(value, /^[0-9A-HJKMNP-TV-Z]{26}$/);
});

test("time prefix sorts older identifiers before newer identifiers", () => {
  const older = createUlid(1_000).slice(0, 10);
  const newer = createUlid(2_000).slice(0, 10);
  assert.ok(older < newer);
});

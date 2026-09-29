import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import vm from "node:vm";

test("dense review fixture has stable complete rows, nontrivial text and exact integer amounts", () => {
  const context = { window: {} };
  vm.runInNewContext(readFileSync(new URL("../../docs/ux/prototypes/ms62/fixture.js", import.meta.url), "utf8"), context);
  const rows = context.window.pmcsDenseFixture;
  assert.equal(rows.length, 48);
  assert.deepEqual(Array.from(rows, row => row.number), Array.from({ length: 48 }, (_, i) => i + 1));
  assert.equal(rows.reduce((sum, row) => sum + row.amountRial, 0), 1482000000);
  assert.equal(new Set(rows.map(row => row.status)).size, 6);
  assert.equal(new Set(rows.map(row => row.owner)).size, 4);
  assert.ok(rows.every(row => row.description.length >= 100 && Number.isSafeInteger(row.amountRial)));
  assert.ok(Object.isFrozen(rows) && rows.every(Object.isFrozen));
});

import { test } from "node:test";
import assert from "node:assert/strict";
import { mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { pngSize, validateInventory, verifyCaptures } from "../qa/visual-baseline.mjs";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const inventory = JSON.parse(await readFile(path.join(root, "src/web/e2e/visual-baseline.json"), "utf8"));
const pixel = Buffer.from(
  "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL/nwAAAABJRU5ErkJggg==",
  "base64",
);

test("every active Next page and critical state has a declared, executable capture", async () => {
  assert.deepEqual(await validateInventory(inventory, root), { routes: 11, captures: 38 });
  for (const id of ["04-project-command", "21-mobile-project", "28-tablet-project"]) {
    assert.equal(inventory.captures.find((item) => item.id === id)?.scroll, "top");
  }
  await assert.rejects(validateInventory({ ...inventory, captures: inventory.captures.filter(
    (item) => item.route !== "/admin/users",
  ) }, root), /Missing route baseline/u);
  await assert.rejects(validateInventory({ ...inventory, captures: [
    ...inventory.captures, { ...inventory.captures[0], id: "39-undeclared" },
  ] }, root), /absent from/u);
});

test("archive verification rejects incomplete, malformed and wrong-viewport screenshots", async () => {
  const directory = await mkdtemp(path.join(os.tmpdir(), "pmcs-visual-baseline-"));
  const sample = { captures: [{ id: "01-sample", route: "/", state: "default", viewport: "tiny" }],
    viewports: { tiny: { width: 1, height: 1 } } };
  try {
    await assert.rejects(verifyCaptures(sample, directory), /Missing or undeclared/u);
    await writeFile(path.join(directory, "01-sample.png"), pixel);
    const [verified] = await verifyCaptures(sample, directory);
    assert.equal(verified.sha256.length, 64);
    await assert.rejects(verifyCaptures({ ...sample, viewports: { tiny: { width: 2, height: 1 } } },
      directory), /Wrong viewport/u);
    assert.deepEqual(pngSize(pixel), { width: 1, height: 1 });
    assert.throws(() => pngSize(pixel.subarray(0, -8)), /Missing PNG end/u);
  } finally {
    await rm(directory, { recursive: true, force: true });
  }
});

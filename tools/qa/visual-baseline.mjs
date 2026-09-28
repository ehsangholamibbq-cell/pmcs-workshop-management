#!/usr/bin/env node

import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { execFileSync } from "node:child_process";
import { readFile, readdir, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const web = path.join(root, "src/web");
const manifestPath = path.join(web, "e2e/visual-baseline.json");
const outputDirectory = path.join(web, "artifacts/visual-tour");

export function pngSize(bytes) {
  const signature = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
  assert.ok(bytes.length >= 45 && bytes.subarray(0, 8).equals(signature), "Invalid PNG signature");
  assert.equal(bytes.readUInt32BE(8), 13, "Invalid PNG IHDR length");
  assert.equal(bytes.toString("ascii", 12, 16), "IHDR", "Missing PNG IHDR");
  assert.equal(bytes.toString("ascii", bytes.length - 8, bytes.length - 4), "IEND", "Missing PNG end");
  const width = bytes.readUInt32BE(16);
  const height = bytes.readUInt32BE(20);
  assert.ok(width > 0 && height > 0, "Empty PNG dimensions");
  return { width, height };
}

export async function validateInventory(manifest, projectRoot = root) {
  assert.equal(manifest.schemaVersion, 1);
  assert.equal(manifest.browser, "chromium");
  assert.equal(manifest.locale, "fa-IR");
  assert.equal(manifest.timezone, "Asia/Tehran");
  const pageRoot = path.join(projectRoot, "src/web/app");
  const pages = await discoverPages(pageRoot);
  const discovered = new Map(pages.map((file) => [file === "page.tsx" ? "/" :
    `/${file.replace(/\/page\.tsx$/u, "")}`, file]));
  const ids = new Set();
  const states = new Set();
  for (const capture of manifest.captures) {
    assert.match(capture.id, /^\d{2}-[a-z0-9-]+$/u, "Unsafe or invalid baseline ID");
    assert.ok(!ids.has(capture.id), `Duplicate baseline: ${capture.id}`);
    ids.add(capture.id);
    states.add(capture.state);
    assert.ok(discovered.has(capture.route) ||
      (capture.route === "/missing-visual-baseline-route" && capture.state === "not-found"),
    `Unregistered route: ${capture.route}`);
    assert.ok(manifest.viewports[capture.viewport], `Unknown viewport: ${capture.viewport}`);
    const source = path.join(projectRoot, "src/web/e2e", capture.source);
    const text = await readFile(source, "utf8");
    assert.ok(text.includes(`"${capture.id}"`), `Capture ${capture.id} is absent from ${capture.source}`);
  }
  for (const route of discovered.keys()) {
    assert.ok(manifest.captures.some((capture) => capture.route === route), `Missing route baseline: ${route}`);
  }
  for (const state of ["default-off", "permission-denied", "authorized-catalog", "authorized-group",
    "empty-filter", "loading", "upstream-failure", "calendar-dialog", "form-validation-offline",
    "offline-restart", "not-found"]) {
    assert.ok(states.has(state), `Missing critical state: ${state}`);
  }
  return { routes: discovered.size, captures: ids.size };
}

export async function verifyCaptures(manifest, directory) {
  const expected = new Set(manifest.captures.map((capture) => `${capture.id}.png`));
  const actual = (await readdir(directory)).filter((file) => file.endsWith(".png"));
  assert.deepEqual(actual.sort(), [...expected].sort(), "Missing or undeclared screenshot files");
  const captures = [];
  for (const entry of manifest.captures) {
    const bytes = await readFile(path.join(directory, `${entry.id}.png`));
    const dimensions = pngSize(bytes);
    assert.deepEqual(dimensions, manifest.viewports[entry.viewport], `Wrong viewport: ${entry.id}`);
    captures.push({
      ...entry,
      file: `${entry.id}.png`,
      ...dimensions,
      bytes: bytes.length,
      sha256: createHash("sha256").update(bytes).digest("hex"),
    });
  }
  return captures;
}

async function discoverPages(directory, prefix = "") {
  const files = [];
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    if (entry.isDirectory()) files.push(...await discoverPages(path.join(directory, entry.name), `${prefix}${entry.name}/`));
    else if (entry.name === "page.tsx") files.push(`${prefix}${entry.name}`);
  }
  return files;
}

async function main() {
  const bytes = await readFile(manifestPath);
  const manifest = JSON.parse(bytes.toString("utf8"));
  const inventory = await validateInventory(manifest);
  if (process.argv[2] === "check-source") {
    console.log(JSON.stringify({ valid: true, ...inventory }));
    return;
  }
  assert.equal(process.argv[2], "verify", "Expected check-source or verify");
  const captures = await verifyCaptures(manifest, outputDirectory);
  const checkoutCommit = execFileSync("git", ["rev-parse", "HEAD"], { cwd: root, encoding: "utf8" }).trim();
  const index = {
    schemaVersion: 1,
    sourceCommit: process.env.PMCS_SOURCE_HEAD_SHA ?? checkoutCommit,
    checkoutCommit,
    runId: process.env.GITHUB_RUN_ID ?? null,
    runAttempt: process.env.GITHUB_RUN_ATTEMPT ?? null,
    manifestSha256: createHash("sha256").update(bytes).digest("hex"),
    inventory,
    browser: manifest.browser,
    locale: manifest.locale,
    timezone: manifest.timezone,
    fixture: manifest.fixture,
    captures,
  };
  await writeFile(path.join(outputDirectory, "index.json"), `${JSON.stringify(index, null, 2)}\n`);
  console.log(JSON.stringify({ valid: true, ...inventory, index: "index.json" }));
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().catch((error) => { console.error(error); process.exitCode = 1; });
}

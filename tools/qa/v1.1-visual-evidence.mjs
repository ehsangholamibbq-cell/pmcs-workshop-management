#!/usr/bin/env node

import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, readdirSync, renameSync, statSync, writeFileSync } from "node:fs";
import { dirname, join, relative, resolve, sep } from "node:path";
import { pathToFileURL } from "node:url";
import { repositoryRoot } from "./regression-runner.mjs";

function sha256(value) { return createHash("sha256").update(value).digest("hex"); }
function indexes(root) {
  return readdirSync(root, { withFileTypes: true }).flatMap(entry => {
    const path = join(root, entry.name);
    return entry.isDirectory() ? indexes(path) : entry.name === "index.json" ? [path] : [];
  });
}

export function inspectVisualEvidence({ g5Root, tourIndexPath, expectedSource, expectedCommit }) {
  const tour = JSON.parse(readFileSync(tourIndexPath, "utf8"));
  assert.equal(tour.sourceCommit, expectedSource);
  assert.equal(tour.checkoutCommit, expectedCommit);
  assert.ok(tour.inventory.routes >= 11 && tour.captures.length >= 45);
  const paths = indexes(g5Root), summary = { indexes: paths.length, png: 0, axe: 0, pdf: 0 };
  const browserNames = new Set();
  const indexDigests = [];
  for (const path of paths) {
    const bytes = readFileSync(path), index = JSON.parse(bytes.toString("utf8"));
    assert.equal(index.contractVersion, 1);
    assert.equal(index.source, expectedSource);
    assert.ok(["chromium", "firefox", "webkit"].includes(index.browser));
    assert.ok(Array.isArray(index.files) && index.files.length > 0);
    browserNames.add(index.browser);
    indexDigests.push({ path: relative(g5Root, path), sha256: sha256(bytes) });
    for (const item of index.files) {
      assert.ok(typeof item.name === "string" && item.name && !item.name.includes("/") && !item.name.includes("\\") && item.name !== "..", "Unsafe evidence path.");
      const file = resolve(dirname(path), item.name);
      assert.ok(file.startsWith(`${resolve(dirname(path))}${sep}`));
      assert.ok(statSync(file).isFile());
      const content = readFileSync(file);
      assert.equal(content.length, item.bytes);
      assert.equal(sha256(content), item.sha256, `Visual artifact digest mismatch: ${item.name}`);
      if (item.name.endsWith(".png")) {
        assert.equal(content.subarray(0, 8).toString("hex"), "89504e470d0a1a0a");
        assert.equal(content.readUInt32BE(16), item.width);
        assert.equal(content.readUInt32BE(20), item.height);
        summary.png++;
      } else if (item.name.endsWith(".axe.json")) {
        assert.equal(item.violations, 0);
        assert.equal(JSON.parse(content.toString("utf8")).violations.length, 0);
        summary.axe++;
      } else if (item.name.endsWith(".pdf")) {
        assert.equal(content.subarray(0, 5).toString("ascii"), "%PDF-");
        assert.ok(content.subarray(-1024).toString("ascii").includes("%%EOF"));
        summary.pdf++;
      }
    }
  }
  assert.deepEqual([...browserNames].sort(), ["chromium", "firefox", "webkit"]);
  assert.ok(summary.indexes >= 25 && summary.png >= 296 && summary.axe >= 195 && summary.pdf >= 20,
    `VX-G5 inventory shrank: ${JSON.stringify(summary)}`);
  return { summary, indexDigests, tourIndexSha256: sha256(readFileSync(tourIndexPath)) };
}

function save(path, value) {
  const temporary = `${path}.${process.pid}.tmp`;
  writeFileSync(temporary, `${JSON.stringify(value, null, 2)}\n`, { mode: 0o600 });
  renameSync(temporary, path);
}

function main() {
  const git = (...args) => execFileSync("git", args, { cwd: repositoryRoot, encoding: "utf8" }).trim();
  const commit = git("rev-parse", "HEAD"), tree = git("rev-parse", "HEAD^{tree}");
  const sourceHead = (process.env.PMCS_SOURCE_HEAD_SHA || commit).trim();
  const visual = inspectVisualEvidence({
    g5Root: join(repositoryRoot, "src/web/artifacts/vx-g5"),
    tourIndexPath: join(repositoryRoot, "src/web/artifacts/visual-tour/index.json"),
    expectedSource: sourceHead, expectedCommit: commit,
  });
  const output = resolve(process.argv[2] ?? join(repositoryRoot, "artifacts/qa/v1.1-evidence"));
  mkdirSync(output, { recursive: true });
  const artifactPath = join(output, "browser-visual-print-a11y.artifact.json");
  save(artifactPath, {
    contractVersion: 1, reportType: "pmcs-v1.1-visual-candidate-index",
    commit, tree, sourceHead, ...visual,
  });
  save(join(output, "browser-visual-print-a11y.json"), {
    contractVersion: 1, reportType: "pmcs-v1.1-qa-evidence", id: "browser-visual-print-a11y",
    commit, tree, status: "passed",
    checks: [
      { name: "Three-browser G5 screenshot and axe digests and print PDFs", status: "passed" },
      { name: "Complete active-route visual baseline inventory on Candidate", status: "passed" },
    ],
    artifacts: [{ path: "browser-visual-print-a11y.artifact.json", sha256: sha256(readFileSync(artifactPath)) }],
  });
  console.log(JSON.stringify({ status: "passed", id: "browser-visual-print-a11y", ...visual.summary }));
}

if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
  try { main(); } catch (error) { console.error(error.message); process.exitCode = 1; }
}

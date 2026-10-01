#!/usr/bin/env node

import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, renameSync, writeFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { repositoryRoot } from "./regression-runner.mjs";

export function validateLoadSoak(service, reporting) {
  assert.equal(service.stage, "pmcs-v1.1-permission-diagnostics-load-soak");
  assert.equal(service.status, "passed");
  assert.ok(service.durationSeconds >= 30 && service.workers === 2 && service.requests >= 80);
  assert.equal(service.failedRequests, 0);
  assert.ok(Number.isFinite(service.p95Milliseconds) && service.p95Milliseconds < 1500);
  assert.equal(reporting.stage, "reporting-worker-capacity-regression");
  assert.equal(reporting.status, "passed");
  assert.ok(reporting.assertions >= 11 && reporting.healthyRuns >= 20 &&
    reporting.poisonAttempts === 3 && reporting.p95BudgetSeconds <= 30);
}

function save(path, value) {
  const temporary = `${path}.${process.pid}.tmp`;
  writeFileSync(temporary, `${JSON.stringify(value, null, 2)}\n`, { mode: 0o600 });
  renameSync(temporary, path);
}

function main() {
  const output = resolve(process.argv[2] ?? join(repositoryRoot, "artifacts/qa/v1.1-evidence"));
  const files = ["load-soak-service.artifact.json", "load-soak-reporting.artifact.txt"];
  const service = JSON.parse(readFileSync(join(output, files[0]), "utf8"));
  const reportingText = readFileSync(join(output, files[1]), "utf8");
  const line = reportingText.trim().split("\n").findLast(value => value.includes('"stage":"reporting-worker-capacity-regression"'));
  const reporting = JSON.parse(line ?? "null");
  validateLoadSoak(service, reporting);
  const git = (...args) => execFileSync("git", args, { cwd: repositoryRoot, encoding: "utf8" }).trim();
  const commit = git("rev-parse", "HEAD"), tree = git("rev-parse", "HEAD^{tree}");
  mkdirSync(output, { recursive: true });
  save(join(output, "load-and-soak.json"), {
    contractVersion: 1, reportType: "pmcs-v1.1-qa-evidence", id: "load-and-soak", commit, tree, status: "passed",
    checks: [
      { name: "Thirty-second two-worker authenticated permission and diagnostics soak with measured P95", status: "passed" },
      { name: "Twenty reporting runs, poison isolation and bounded P95", status: "passed" },
    ],
    artifacts: files.map(path => ({ path, sha256: createHash("sha256").update(readFileSync(join(output, path))).digest("hex") })),
  });
  console.log(JSON.stringify({ status: "passed", id: "load-and-soak", commit }));
}

if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
  try { main(); } catch (error) { console.error(error.message); process.exitCode = 1; }
}

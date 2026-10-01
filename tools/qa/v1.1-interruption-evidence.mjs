#!/usr/bin/env node

import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, renameSync, writeFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { repositoryRoot } from "./regression-runner.mjs";

export function validateInterruptionObservations(value) {
  assert.ok(Number.isInteger(Number(value.before)) && Number(value.before) > 0 && Number(value.before) < 70);
  assert.equal(value.afterFailure, value.before, "Interrupted transaction changed V1 ledger.");
  assert.equal(value.recovered, "70", "Migration did not recover to Candidate ledger.");
  assert.match(value.v1DataCounts ?? "", /^[1-9][0-9]*\|[1-9][0-9]*$/u);
  assert.equal(value.recoveredDataCounts, value.v1DataCounts, "V1 data changed during recovery.");
}

function save(path, value) {
  const temporary = `${path}.${process.pid}.tmp`;
  writeFileSync(temporary, `${JSON.stringify(value, null, 2)}\n`, { mode: 0o600 });
  renameSync(temporary, path);
}

function main() {
  const value = {
    before: process.env.PMCS_INTERRUPT_BEFORE,
    afterFailure: process.env.PMCS_INTERRUPT_AFTER_FAILURE,
    recovered: process.env.PMCS_INTERRUPT_RECOVERED,
    recoveredDataCounts: process.env.PMCS_INTERRUPT_DATA_COUNTS,
    v1DataCounts: process.env.PMCS_INTERRUPT_V1_DATA_COUNTS,
  };
  validateInterruptionObservations(value);
  const git = (...args) => execFileSync("git", args, { cwd: repositoryRoot, encoding: "utf8" }).trim();
  const commit = git("rev-parse", "HEAD"), tree = git("rev-parse", "HEAD^{tree}");
  const output = resolve(process.argv[2] ?? join(repositoryRoot, "artifacts/qa/v1.1-evidence"));
  mkdirSync(output, { recursive: true });
  const name = "migration-interruption-recovery.artifact.json", path = join(output, name);
  save(path, {
    contractVersion: 1, reportType: "pmcs-v1.1-migration-interruption", commit, tree,
    fault: "terminate PostgreSQL connection during transaction ledger insert after migration SQL",
    baselineCommit: "26bf222d44634562ca7f3fc0931f3f8b79ca04a1",
    observations: value,
  });
  save(join(output, "migration-interruption-recovery.json"), {
    contractVersion: 1, reportType: "pmcs-v1.1-qa-evidence", id: "migration-interruption-recovery", commit, tree,
    status: "passed",
    checks: [{ name: "Injected mid-transaction connection loss rolls back and then recovers once", status: "passed" }],
    artifacts: [{ path: name, sha256: createHash("sha256").update(readFileSync(path)).digest("hex") }],
  });
  console.log(JSON.stringify({ status: "passed", id: "migration-interruption-recovery", commit }));
}

if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
  try { main(); } catch (error) { console.error(error.message); process.exitCode = 1; }
}

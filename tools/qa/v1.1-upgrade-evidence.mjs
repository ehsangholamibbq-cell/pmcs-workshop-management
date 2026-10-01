#!/usr/bin/env node

import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, renameSync, writeFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { repositoryRoot } from "./regression-runner.mjs";

export function validateUpgradeObservations(value) {
  assert.ok(Number.isInteger(Number(value.v1Count)) && Number(value.v1Count) > 0 && Number(value.v1Count) < 70);
  assert.equal(value.v1RestoredCount, value.v1Count);
  assert.equal(value.upgradeCount, "70");
  assert.equal(value.rollbackCount, "70");
  assert.match(value.v1DataCounts ?? "", /^[1-9][0-9]*\|[1-9][0-9]*$/u);
  assert.equal(value.upgradeDataCounts, value.v1DataCounts);
  assert.equal(value.rollbackDataCounts, value.v1DataCounts);
  assert.match(value.backupSha256 ?? "", /^[0-9a-f]{64}$/u);
}

function save(path, value) {
  const temp = `${path}.${process.pid}.tmp`;
  writeFileSync(temp, `${JSON.stringify(value, null, 2)}\n`, { mode: 0o600 });
  renameSync(temp, path);
}

function main() {
  const value = {
    v1Count: process.env.PMCS_V1_LEDGER_COUNT,
    v1RestoredCount: process.env.PMCS_V1_RESTORED_COUNT,
    upgradeCount: process.env.PMCS_V11_UPGRADE_COUNT,
    rollbackCount: process.env.PMCS_V11_ROLLBACK_COUNT,
    v1DataCounts: process.env.PMCS_V1_DATA_COUNTS,
    upgradeDataCounts: process.env.PMCS_V11_DATA_COUNTS,
    rollbackDataCounts: process.env.PMCS_V11_ROLLBACK_DATA_COUNTS,
    backupSha256: process.env.PMCS_V1_BACKUP_SHA256,
  };
  validateUpgradeObservations(value);
  const git = (...args) => execFileSync("git", args, { cwd: repositoryRoot, encoding: "utf8" }).trim();
  const commit = git("rev-parse", "HEAD"), tree = git("rev-parse", "HEAD^{tree}");
  const output = resolve(process.argv[2] ?? join(repositoryRoot, "artifacts/qa/v1.1-evidence"));
  mkdirSync(output, { recursive: true });
  for (const name of ["migration-upgrade-probe.artifact.json", "migration-rollback-probe.artifact.json"]) {
    const probe = JSON.parse(readFileSync(join(output, name), "utf8"));
    assert.equal(probe.overallHealth, "Healthy", `${name} did not prove readiness.`);
  }
  const matrix = JSON.parse(readFileSync(join(output, "migration-upgrade-permissions.artifact.json"), "utf8"));
  assert.equal(matrix.passed, true, "Upgrade permission/workflow matrix failed.");
  assert.equal(matrix.failedCount, 0);
  assert.ok(matrix.assertionCount > 0 && matrix.assertionCount === matrix.passedCount);
  const scenarios = {
    "migration-v1-upgrade": "Locked V1 database and checked backup restored before Candidate upgrade; V1 data preserved",
    "migration-upgrade-smoke": "Candidate health, QA diagnostics and V1 permission matrix passed on upgraded database",
    "migration-runtime-rollback": "V1 API and QA diagnostics started on expanded V1.1 schema; V1 data and ledger preserved",
  };
  for (const [id, name] of Object.entries(scenarios)) {
    const artifactName = `${id}.artifact.json`, path = join(output, artifactName);
    save(path, { contractVersion: 1, reportType: "pmcs-v1.1-upgrade-scenario", commit, tree, baselineCommit: "26bf222d44634562ca7f3fc0931f3f8b79ca04a1", id, name, observations: value });
    const sourcePaths = id === "migration-upgrade-smoke"
      ? ["migration-upgrade-probe.artifact.json", "migration-upgrade-permissions.artifact.json"]
      : id === "migration-runtime-rollback"
        ? ["migration-rollback-probe.artifact.json"] : [];
    const sourceArtifacts = sourcePaths.map(source => ({
      path: source,
      sha256: createHash("sha256").update(readFileSync(join(output, source))).digest("hex"),
    }));
    save(join(output, `${id}.json`), {
      contractVersion: 1, reportType: "pmcs-v1.1-qa-evidence", id, commit, tree, status: "passed",
      checks: [{ name, status: "passed" }],
      artifacts: [{ path: artifactName, sha256: createHash("sha256").update(readFileSync(path)).digest("hex") }, ...sourceArtifacts],
    });
  }
  console.log(JSON.stringify({ status: "passed", upgradeScenarios: Object.keys(scenarios).length, commit }));
}

if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
  try { main(); } catch (error) { console.error(error.message); process.exitCode = 1; }
}

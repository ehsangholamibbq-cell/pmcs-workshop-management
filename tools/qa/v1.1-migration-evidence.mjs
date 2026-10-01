#!/usr/bin/env node

import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, renameSync, writeFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { repositoryRoot } from "./regression-runner.mjs";

export function validateMigrationObservations({ firstLedger, repeatedLedger, restoredLedger, restoredInt1, backupSha256 }) {
  assert.match(firstLedger, /^70\|[0-9a-f]{32}$/u, "Fresh Candidate must apply all 70 migrations once.");
  assert.equal(repeatedLedger, firstLedger, "Restart/repeated runner changed the migration ledger.");
  assert.equal(restoredLedger, firstLedger, "Restored Candidate ledger differs from source.");
  assert.equal(restoredInt1, "1|5|true", "Restored INT1 lineage/schema differs.");
  assert.match(backupSha256, /^[0-9a-f]{64}$/u, "Backup digest missing.");
}

function save(path, value) {
  const temp = `${path}.${process.pid}.tmp`;
  writeFileSync(temp, `${JSON.stringify(value, null, 2)}\n`, { mode: 0o600 });
  renameSync(temp, path);
}

function main() {
  const observations = {
    firstLedger: process.env.PMCS_MIGRATION_FIRST_LEDGER,
    repeatedLedger: process.env.PMCS_MIGRATION_REPEATED_LEDGER,
    restoredLedger: process.env.PMCS_MIGRATION_RESTORED_LEDGER,
    restoredInt1: process.env.PMCS_MIGRATION_RESTORED_INT1,
    backupSha256: process.env.PMCS_MIGRATION_BACKUP_SHA256,
  };
  validateMigrationObservations(observations);
  const git = (...args) => execFileSync("git", args, { cwd: repositoryRoot, encoding: "utf8" }).trim();
  const commit = git("rev-parse", "HEAD"), tree = git("rev-parse", "HEAD^{tree}");
  const output = resolve(process.argv[2] ?? join(repositoryRoot, "artifacts/qa/v1.1-evidence"));
  mkdirSync(output, { recursive: true });
  const artifacts = {
    "migration-empty": { scenario: "isolated empty QA database to Candidate migrations", ledger: observations.firstLedger },
    "migration-repeat": { scenario: "Candidate API restarts repeat migration runner without ledger drift", before: observations.firstLedger, after: observations.repeatedLedger },
    "migration-backup-restore": { scenario: "Candidate backup/restore to a new independent database", ledger: observations.restoredLedger, int1Lineage: observations.restoredInt1, backupSha256: observations.backupSha256 },
  };
  for (const [id, result] of Object.entries(artifacts)) {
    const name = `${id}.artifact.json`, path = join(output, name);
    save(path, { contractVersion: 1, reportType: "pmcs-v1.1-migration-scenario", commit, tree, id, result });
    save(join(output, `${id}.json`), {
      contractVersion: 1, reportType: "pmcs-v1.1-qa-evidence", id, commit, tree,
      status: "passed", checks: [{ name: result.scenario, status: "passed" }],
      artifacts: [{ path: name, sha256: createHash("sha256").update(readFileSync(path)).digest("hex") }],
    });
  }
  console.log(JSON.stringify({ status: "passed", migrationScenarios: Object.keys(artifacts).length, commit }));
}

if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
  try { main(); } catch (error) { console.error(error.message); process.exitCode = 1; }
}

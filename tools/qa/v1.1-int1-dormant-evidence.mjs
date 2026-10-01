#!/usr/bin/env node

import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, renameSync, writeFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { repositoryRoot } from "./regression-runner.mjs";

const expectedProviders = new Set(["OpenAI", "GoogleGemini", "AnthropicClaude"]);

export function validateDormantProbe(probe, dbState) {
  assert.equal(dbState, "0|0|0|0|0", "A fresh QA registry must have no grants, selections, runs, active providers or active models.");
  assert.equal(probe.stage, "V1.1-INT1");
  assert.equal(probe.allAvailable, false);
  assert.equal(probe.configuredFailure, false);
  assert.equal(probe.liveProviderEvidence?.length, 3);
  assert.deepEqual(new Set(probe.liveProviderEvidence.map(item => item.provider)), expectedProviders);
  for (const item of probe.liveProviderEvidence) {
    assert.equal(item.configuration, "Unavailable");
    assert.equal(item.structured, "Unavailable");
    assert.equal(item.toolCalling, "Unavailable");
  }
}

function save(path, value) {
  const temp = `${path}.${process.pid}.tmp`;
  writeFileSync(temp, `${JSON.stringify(value, null, 2)}\n`, { mode: 0o600 });
  renameSync(temp, path);
}

function main() {
  const output = resolve(process.argv[2] ?? join(repositoryRoot, "artifacts/qa/v1.1-evidence"));
  const commit = execFileSync("git", ["rev-parse", "HEAD"], { cwd: repositoryRoot, encoding: "utf8" }).trim();
  const tree = execFileSync("git", ["rev-parse", "HEAD^{tree}"], { cwd: repositoryRoot, encoding: "utf8" }).trim();
  const probe = JSON.parse(process.env.PMCS_INT1_PROBE_JSON ?? "null");
  validateDormantProbe(probe, process.env.PMCS_INT1_DB_STATE);
  const artifact = {
    contractVersion: 1, reportType: "pmcs-v1.1-int1-dormant-state",
    commit, tree, database: "isolated QA fresh migration before INT1 fixture",
    activeGrantCount: 0, activeSelectionCount: 0, referenceRunCount: 0,
    activeProviderCount: 0, activeModelCount: 0,
    probes: probe.liveProviderEvidence.map(item => ({ provider: item.provider, configuration: item.configuration, structured: item.structured, toolCalling: item.toolCalling })),
    liveConnectionTest: "deferred by ADR 0033",
  };
  mkdirSync(output, { recursive: true });
  const path = join(output, "int1-dormant.artifact.json");
  save(path, artifact);
  save(join(output, "int1-dormant.json"), {
    contractVersion: 1, reportType: "pmcs-v1.1-qa-evidence", id: "int1-dormant",
    commit, tree, status: "passed",
    checks: [
      { name: "Fresh registry has zero grants, selections, runs and active providers/models", status: "passed" },
      { name: "All three unconfigured providers probe Unavailable without credentials", status: "passed" },
    ],
    artifacts: [{ path: "int1-dormant.artifact.json", sha256: createHash("sha256").update(readFileSync(path)).digest("hex") }],
  });
  console.log(JSON.stringify({ status: "passed", id: "int1-dormant", commit }));
}

if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
  try { main(); } catch (error) { console.error(error.message); process.exitCode = 1; }
}

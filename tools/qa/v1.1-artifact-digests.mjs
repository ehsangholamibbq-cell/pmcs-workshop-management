#!/usr/bin/env node

import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, readdirSync, renameSync, statSync, writeFileSync } from "node:fs";
import { join, resolve, sep } from "node:path";
import { pathToFileURL } from "node:url";
import { repositoryRoot } from "./regression-runner.mjs";

function digest(bytes) { return createHash("sha256").update(bytes).digest("hex"); }
function git(...args) { return execFileSync("git", args, { cwd: repositoryRoot, encoding: "utf8" }).trim(); }
function readJson(path) { return JSON.parse(readFileSync(path, "utf8")); }
function save(path, data) {
  const temp = `${path}.${process.pid}.tmp`;
  writeFileSync(temp, `${JSON.stringify(data, null, 2)}\n`, { mode: 0o600 });
  renameSync(temp, path);
}

export function inspectArtifactInputs({ evidenceDirectory, requirements, commit, tree, budgetPath }) {
  const candidate = readJson(join(evidenceDirectory, "candidate-manifest.artifact.json"));
  assert.equal(candidate.commit, commit);
  assert.equal(candidate.tree, tree);
  assert.equal(candidate.product, "PMCS V1.1");
  assert.equal(candidate.releaseState, "qa1-preflight");
  assert.equal(candidate.targetVersion, "1.1.0");
  assert.equal(candidate.apiContractVersion, "v1");
  assert.equal(candidate.webPackageVersion, "0.1.0");
  assert.ok(candidate.migrationInventory.count >= 70);
  const release = readJson(join(evidenceDirectory, "release-build.artifact.json"));
  assert.equal(release.commit, commit);
  assert.equal(release.tree, tree);
  assert.equal(release.status, "passed");
  assert.equal(release.sourceHead, candidate.sourceHead);
  assert.equal(release.identity.commit, candidate.sourceHead);
  assert.equal(release.identity.version, "1.1.0");
  assert.equal(release.identity.builtAt, release.api.builtAt);
  assert.equal(release.identity.builtAt, release.web.builtAt);
  for (const name of ["api", "web"]) {
    assert.equal(release[name].artifact, name);
    assert.equal(release[name].commit, candidate.sourceHead);
    assert.equal(release[name].version, "1.1.0");
    assert.match(release.images[name].id, /^sha256:[0-9a-f]{64}$/u);
    assert.equal(release.images[name].revision, candidate.sourceHead);
    assert.equal(release.images[name].version, "1.1.0");
  }
  const budget = readJson(budgetPath);
  assert.equal(budget.contractVersion, 1);
  assert.equal(budget.source, candidate.sourceHead);
  for (const [key, maximum] of Object.entries(budget.budgets.build ?? budget.budgets)) {
    assert.ok(Number.isFinite(maximum) && maximum > 0);
    assert.ok(Number.isFinite(budget.measurements[key]) && budget.measurements[key] > 0 && budget.measurements[key] <= maximum, `${key} budget exceeded`);
  }
  const required = requirements.requiredEvidence.filter(id => id !== "artifact-digests");
  assert.equal(required.length, requirements.requiredEvidence.length - 1);
  const entries = [];
  const seen = new Set();
  for (const id of required) {
    const path = `${id}.json`;
    const envelope = readJson(join(evidenceDirectory, path));
    assert.equal(envelope.id, id);
    assert.equal(envelope.contractVersion, 1);
    assert.equal(envelope.reportType, "pmcs-v1.1-qa-evidence");
    assert.equal(envelope.commit, commit);
    assert.equal(envelope.tree, tree);
    assert.equal(envelope.status, "passed");
    assert.ok(envelope.checks?.length && envelope.checks.every(check => check.status === "passed"));
    assert.ok(envelope.artifacts?.length);
    for (const artifact of envelope.artifacts) {
      assert.ok(typeof artifact.path === "string" && !artifact.path.startsWith("/") && !artifact.path.split(/[\\/]/u).includes(".."));
      const full = resolve(evidenceDirectory, artifact.path);
      assert.ok(full.startsWith(`${resolve(evidenceDirectory)}${sep}`));
      assert.equal(digest(readFileSync(full)), artifact.sha256, `${id}/${artifact.path} digest mismatch`);
      seen.add(artifact.path);
    }
    seen.add(path);
  }
  const budgetBytes = readFileSync(budgetPath);
  const releaseBytes = readFileSync(join(evidenceDirectory, "release-build.artifact.json"));
  const inputFiles = [...seen].sort().map(path => ({ path, sha256: digest(readFileSync(join(evidenceDirectory, path))), bytes: statSync(join(evidenceDirectory, path)).size }));
  assert.deepEqual(readdirSync(evidenceDirectory).filter(path => path.endsWith(".json") && !path.endsWith(".artifact.json") && !path.endsWith(".source.json")).sort(), required.sort().map(id => `${id}.json`).sort(), "Unexpected or duplicate evidence envelopes");
  return { candidate, release, inputFiles, budgetSha256: digest(budgetBytes), budgetBytes: budgetBytes.length, releaseSha256: digest(releaseBytes) };
}

function main() {
  const directory = resolve(process.argv[2] ?? join(repositoryRoot, "artifacts/qa/v1.1-evidence"));
  const budgetPath = resolve(process.argv[3] ?? join(repositoryRoot, "artifacts/qa/v1.1-build-budget/vx-g5-build-budget.json"));
  const requirements = readJson(join(repositoryRoot, "tools/qa/v1.1-qualification-requirements.json"));
  const commit = git("rev-parse", "HEAD"), tree = git("rev-parse", "HEAD^{tree}");
  const inspected = inspectArtifactInputs({ evidenceDirectory: directory, budgetPath, requirements, commit, tree });
  const artifactName = "artifact-digests.artifact.json";
  mkdirSync(directory, { recursive: true });
  save(join(directory, artifactName), {
    contractVersion: 1, reportType: "pmcs-v1.1-artifact-digest-index", commit, tree,
    sourceHead: inspected.candidate.sourceHead, version: "1.1.0",
    releaseBuildSha256: inspected.releaseSha256, imageIds: inspected.release.images,
    webBuildBudget: { sha256: inspected.budgetSha256, bytes: inspected.budgetBytes },
    files: inspected.inputFiles,
  });
  save(join(directory, "artifact-digests.json"), {
    contractVersion: 1, reportType: "pmcs-v1.1-qa-evidence", id: "artifact-digests", commit, tree, status: "passed",
    checks: [
      { name: "Seventeen complete same-tree V1.1 evidence envelopes and their SHA-256 digests", status: "passed" },
      { name: "Built API/Web release identity, source commit and image IDs match", status: "passed" },
      { name: "Web build measurements satisfy budget", status: "passed" },
    ],
    artifacts: [{ path: artifactName, sha256: digest(readFileSync(join(directory, artifactName))) }],
  });
  console.log(JSON.stringify({ status: "passed", commit, evidence: 17, files: inspected.inputFiles.length }));
}

if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
  try { main(); } catch (error) { console.error(error.message); process.exitCode = 1; }
}

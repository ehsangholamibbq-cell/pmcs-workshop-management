#!/usr/bin/env node

import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, renameSync, writeFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { repositoryRoot } from "./regression-runner.mjs";

const commitPattern = /^[0-9a-f]{40}$/u;

export function validateCandidateDefaults(appsettings, compose) {
  const settings = JSON.parse(appsettings);
  assert.equal(settings.Intelligence?.INT1ReferenceEnabled, false, "INT1 reference must be explicitly disabled in API defaults.");
  assert.equal(settings.Intelligence?.INT1FixtureEnabled, false, "INT1 fixture must be explicitly disabled in API defaults.");
  const apiEnvironment = compose.match(/^  api:\s*\n[\s\S]*?(?=^  [a-z][a-z0-9-]*:|(?![\s\S]))/mu)?.[0] ?? "";
  assert.match(apiEnvironment, /^      Intelligence__INT1ReferenceEnabled: "false"$/mu);
  assert.match(apiEnvironment, /^      Intelligence__INT1FixtureEnabled: "false"$/mu);
}

function git(...args) { return execFileSync("git", args, { cwd: repositoryRoot, encoding: "utf8" }).trim(); }
function sha256(value) { return createHash("sha256").update(value).digest("hex"); }
function save(path, value) {
  const temporary = `${path}.${process.pid}.tmp`;
  writeFileSync(temporary, `${JSON.stringify(value, null, 2)}\n`, { mode: 0o600 });
  renameSync(temporary, path);
}

export function buildCandidateManifest({ commit, tree, sourceHead, migrationFiles, generatedAt = new Date() }) {
  assert.match(commit, commitPattern);
  assert.match(tree, commitPattern);
  assert.match(sourceHead, commitPattern);
  assert.ok(migrationFiles.length > 0);
  return {
    contractVersion: 1, reportType: "pmcs-v1.1-candidate-manifest",
    product: "PMCS V1.1", releaseState: "candidate",
    commit, tree, sourceHead,
    parentBaseline: "26bf222d44634562ca7f3fc0931f3f8b79ca04a1",
    generatedAt: generatedAt.toISOString(),
    migrationInventory: { count: migrationFiles.length, filesSha256: sha256(`${migrationFiles.join("\n")}\n`) },
    configuration: { apiInt1ReferenceEnabled: false, apiInt1FixtureEnabled: false, composeInt1ReferenceEnabled: false, composeInt1FixtureEnabled: false },
    deferred: ["AGENT-S1-LIVE provider connections in V1.2, after V1.1 publication"],
  };
}

function main() {
  const output = resolve(process.argv[2] ?? join(repositoryRoot, "artifacts/qa/v1.1-evidence"));
  if (process.argv.length > 3) throw new Error("Usage: node tools/qa/v1.1-candidate-preflight.mjs [evidence-directory]");
  assert.equal(git("status", "--porcelain"), "", "Candidate preflight requires a clean checkout.");
  const commit = git("rev-parse", "HEAD");
  const tree = git("rev-parse", "HEAD^{tree}");
  const sourceHead = (process.env.PMCS_SOURCE_HEAD_SHA || commit).trim().toLowerCase();
  assert.equal((process.env.GITHUB_SHA || commit).trim().toLowerCase(), commit, "Checkout does not match workflow commit.");
  validateCandidateDefaults(
    readFileSync(join(repositoryRoot, "src/backend/Pmcs.Api/appsettings.json"), "utf8"),
    readFileSync(join(repositoryRoot, "docker-compose.yml"), "utf8"),
  );
  const migrationFiles = git("ls-files", "src/backend").split("\n").filter(path => /\/Migrations\/.*\.cs$/u.test(path));
  const manifest = buildCandidateManifest({ commit, tree, sourceHead, migrationFiles });
  mkdirSync(output, { recursive: true });
  const artifactPath = join(output, "candidate-manifest.artifact.json");
  save(artifactPath, manifest);
  const evidence = {
    contractVersion: 1, reportType: "pmcs-v1.1-qa-evidence", id: "candidate-manifest",
    commit, tree, status: "passed",
    checks: [
      { name: "Clean checkout and exact workflow commit/tree", status: "passed" },
      { name: "Explicit INT1 default-off API and Compose settings", status: "passed" },
      { name: "Migration source inventory recorded", status: "passed" },
    ],
    artifacts: [{ path: "candidate-manifest.artifact.json", sha256: sha256(readFileSync(artifactPath)) }],
  };
  save(join(output, "candidate-manifest.json"), evidence);
  console.log(JSON.stringify({ status: "passed", id: evidence.id, commit, migrations: migrationFiles.length }));
}

if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
  try { main(); } catch (error) { console.error(error.message); process.exitCode = 1; }
}

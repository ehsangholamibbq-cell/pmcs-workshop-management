#!/usr/bin/env node

import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { copyFileSync, mkdirSync, readFileSync, renameSync, writeFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { repositoryRoot } from "./regression-runner.mjs";

// Each domain points to an actual connected command. These mappings do not
// substitute for the separate migration and load/soak rehearsals.
export const domainSources = {
  "scope-and-contracts": { architecture: ["repository-validation", "system-contract-audit"], "pilot-contract": ["node-contract-tests"], backend: ["dotnet-test"] },
  "permission-and-security": { backend: ["dotnet-test"], integration: ["connected-integration-regression"], "identity-container": ["identity-container-verification"] },
  "documents-and-identity": { backend: ["dotnet-test"], integration: ["connected-integration-regression"], "identity-container": ["identity-container-verification"], "ui-e2e": ["playwright-browser-qualification"] },
  "project-and-collaboration": { backend: ["dotnet-test"], integration: ["connected-integration-regression"], "ui-e2e": ["playwright-browser-qualification"] },
  "offline-and-reconnect": { backend: ["dotnet-test"], integration: ["connected-integration-regression"], "ui-e2e": ["playwright-browser-qualification"] },
  "reporting-and-exports": { backend: ["dotnet-test"], integration: ["connected-integration-regression"], web: ["web-check"], "ui-e2e": ["playwright-browser-qualification"] },
};

export function verifyDomainSources(reports, commit, sources) {
  const checks = [];
  for (const [suite, commandIds] of Object.entries(sources)) {
    const report = reports[suite];
    assert.equal(report?.contractVersion, 1, `${suite} contract missing`);
    assert.equal(report.reportType, "pmcs-regression-suite");
    assert.equal(report.suite, suite);
    assert.equal(report.commit, commit, `${suite} mixed SHA`);
    assert.equal(report.status, "passed", `${suite} failed`);
    assert.equal(report.executedCommandCount, report.plannedCommandCount, `${suite} incomplete`);
    for (const id of commandIds) {
      const commands = report.commands?.filter(command => command.id === id) ?? [];
      assert.equal(commands.length, 1, `${suite}/${id} missing or duplicate`);
      assert.equal(commands[0].status, "passed", `${suite}/${id} failed`);
      assert.equal(commands[0].exitCode, 0, `${suite}/${id} exited nonzero`);
      checks.push({ name: `${suite}/${id}`, status: "passed" });
    }
  }
  return checks;
}

function save(path, value) {
  const temp = `${path}.${process.pid}.tmp`;
  writeFileSync(temp, `${JSON.stringify(value, null, 2)}\n`, { mode: 0o600 });
  renameSync(temp, path);
}

function main() {
  const input = resolve(process.argv[2] ?? join(repositoryRoot, "artifacts/qa/regression"));
  const output = resolve(process.argv[3] ?? join(repositoryRoot, "artifacts/qa/v1.1-evidence"));
  if (process.argv.length > 4) throw new Error("Usage: node tools/qa/v1.1-domain-evidence.mjs [regression-dir] [evidence-dir]");
  const git = (...args) => execFileSync("git", args, { cwd: repositoryRoot, encoding: "utf8" }).trim();
  const commit = git("rev-parse", "HEAD"), tree = git("rev-parse", "HEAD^{tree}");
  const suites = [...new Set(Object.values(domainSources).flatMap(sources => Object.keys(sources)))];
  const reports = Object.fromEntries(suites.map(suite => [suite, JSON.parse(readFileSync(join(input, `${suite}.json`), "utf8"))]));
  const domainChecks = Object.fromEntries(Object.entries(domainSources).map(([id, sources]) => [id, verifyDomainSources(reports, commit, sources)]));
  mkdirSync(output, { recursive: true });
  for (const [id, checks] of Object.entries(domainChecks)) {
    const sources = Object.keys(domainSources[id]);
    const artifact = { contractVersion: 1, reportType: "pmcs-v1.1-domain-source-index", id, commit, tree, sourceSuites: sources, checks };
    const artifactName = `${id}.artifact.json`, artifactPath = join(output, artifactName);
    save(artifactPath, artifact);
    const sourceArtifacts = sources.map(suite => {
      const name = `${suite}.source.json`, path = join(output, name);
      copyFileSync(join(input, `${suite}.json`), path);
      return { path: name, sha256: createHash("sha256").update(readFileSync(path)).digest("hex") };
    });
    save(join(output, `${id}.json`), {
      contractVersion: 1, reportType: "pmcs-v1.1-qa-evidence", id, commit, tree, status: "passed", checks,
      artifacts: [{ path: artifactName, sha256: createHash("sha256").update(readFileSync(artifactPath)).digest("hex") }, ...sourceArtifacts],
    });
  }
  console.log(JSON.stringify({ status: "passed", domains: Object.keys(domainChecks).length, commit }));
}

if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
  try { main(); } catch (error) { console.error(error.message); process.exitCode = 1; }
}

#!/usr/bin/env node

import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, readdirSync, realpathSync, renameSync, statSync, writeFileSync } from "node:fs";
import { basename, dirname, join, resolve, sep } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { repositoryRoot } from "./regression-runner.mjs";

const directory = dirname(fileURLToPath(import.meta.url));
const shaPattern = /^[0-9a-f]{40}$/u;
const digestPattern = /^[0-9a-f]{64}$/u;
const idPattern = /^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$/u;

export function buildV11QualificationReport({
  requirements, regressionReport, evidenceReports, expectedCommit, expectedTree,
  evidenceDirectory, workflowRunId = null, workflowRunUrl = null,
  generatedAt = new Date(), inputErrors = [],
}) {
  assert.match(expectedCommit, shaPattern);
  assert.match(expectedTree, shaPattern);
  assert.equal(requirements?.contractVersion, 1);
  assert.equal(requirements?.product, "PMCS V1.1");
  assert.ok(Array.isArray(requirements.requiredEvidence) && requirements.requiredEvidence.length > 0);
  assert.ok(requirements.requiredEvidence.every(id => idPattern.test(id)));
  assert.equal(new Set(requirements.requiredEvidence).size, requirements.requiredEvidence.length);

  const failures = [...inputErrors];
  const expectedSuiteIds = new Set(["architecture", "backend", "integration", "pilot-contract", "web", "ui-e2e", "identity-container"]);
  if (regressionReport?.reportType !== "pmcs-v1-qualification" ||
      regressionReport?.product !== "PMCS V1" ||
      regressionReport?.baselineCommit !== expectedCommit ||
      regressionReport?.status !== "qualified" ||
      regressionReport?.baselineLockEligible !== true ||
      !Array.isArray(regressionReport?.suites) ||
      regressionReport.suites.length !== 7 ||
      new Set(regressionReport.suites.map(suite => suite.suite)).size !== 7 ||
      regressionReport.suites.some(suite => suite.status !== "passed" || !expectedSuiteIds.has(suite.suite))) {
    failures.push("Full PMCS V1 regression is missing, failed, or belongs to another commit.");
  }

  const required = new Set(requirements.requiredEvidence);
  const seen = new Set();
  const evidence = [];
  for (const item of evidenceReports) {
    const id = item?.id;
    if (!required.has(id)) {
      failures.push(`Unexpected V1.1 evidence: ${id ?? "<missing>"}.`);
      continue;
    }
    if (seen.has(id)) {
      failures.push(`Duplicate V1.1 evidence: ${id}.`);
      continue;
    }
    seen.add(id);
    let valid = item.contractVersion === 1 &&
      item.reportType === "pmcs-v1.1-qa-evidence" &&
      item.commit === expectedCommit && item.tree === expectedTree &&
      item.status === "passed" &&
      Array.isArray(item.checks) && item.checks.length > 0 &&
      item.checks.every(check => typeof check?.name === "string" && check.name.trim() && check.status === "passed") &&
      Array.isArray(item.artifacts) && item.artifacts.length > 0;
    if (valid) {
      for (const artifact of item.artifacts) {
        if (!verifyArtifact(evidenceDirectory, artifact)) {
          valid = false;
          failures.push(`Missing or digest-mismatched artifact for ${id}: ${artifact?.path ?? "<missing>"}.`);
        }
      }
    }
    if (!valid) failures.push(`Invalid, failed, or mixed-commit V1.1 evidence: ${id}.`);
    evidence.push({ id, status: valid ? "passed" : "failed", checkCount: Array.isArray(item.checks) ? item.checks.length : 0 });
  }
  for (const id of requirements.requiredEvidence) {
    if (!seen.has(id)) failures.push(`Missing V1.1 evidence: ${id}.`);
  }
  evidence.sort((a, b) => requirements.requiredEvidence.indexOf(a.id) - requirements.requiredEvidence.indexOf(b.id));
  const uniqueFailures = [...new Set(failures)];
  const qualified = uniqueFailures.length === 0 && evidence.length === required.size;
  return {
    contractVersion: 1,
    reportType: "pmcs-v1.1-qualification",
    product: "PMCS V1.1",
    candidateCommit: expectedCommit,
    candidateTree: expectedTree,
    status: qualified ? "qualified" : "failed",
    baselineLockEligible: qualified,
    generatedAt: generatedAt.toISOString(),
    workflowRun: { id: workflowRunId, url: workflowRunUrl },
    fullRegression: { status: regressionReport?.status ?? "missing", suiteCount: regressionReport?.suites?.length ?? 0 },
    summary: { expectedEvidenceCount: required.size, passedEvidenceCount: evidence.filter(item => item.status === "passed").length, failureCount: uniqueFailures.length },
    evidence,
    failures: uniqueFailures,
    deferred: ["Live GPT/Gemini/Claude provider connection and AGENT-S1-LIVE after V1.1 publication"],
  };
}

function verifyArtifact(root, artifact) {
  if (typeof artifact?.path !== "string" || !artifact.path ||
      artifact.path.startsWith("/") || artifact.path.split(/[\\/]/u).includes("..") ||
      !digestPattern.test(artifact.sha256 ?? "")) return false;
  const path = resolve(root, artifact.path);
  if (!path.startsWith(`${resolve(root)}${sep}`)) return false;
  try {
    if (!realpathSync(path).startsWith(`${realpathSync(root)}${sep}`) || !statSync(path).isFile()) return false;
    return createHash("sha256").update(readFileSync(path)).digest("hex") === artifact.sha256;
  } catch { return false; }
}

export function renderV11QualificationMarkdown(report) {
  const rows = report.evidence.map(item => `| ${item.id} | ${item.status} | ${item.checkCount} |`).join("\n");
  return `# PMCS V1.1 Qualification Report

- Status: \`${report.status}\`
- Candidate commit: \`${report.candidateCommit}\`
- Candidate tree: \`${report.candidateTree}\`
- Full V1 regression: \`${report.fullRegression.status}\` (${report.fullRegression.suiteCount} suites)
- Baseline lock eligible: \`${report.baselineLockEligible}\`
- Workflow: ${report.workflowRun.url ?? report.workflowRun.id ?? "not supplied"}

| Evidence | Status | Checks |
| --- | --- | ---: |
${rows}

## Failures

${report.failures.length ? report.failures.map(failure => `- ${failure}`).join("\n") : "- None"}

## Deferred by ADR 0033

- Live GPT/Gemini/Claude connection remains in AGENT-S1-LIVE after V1.1 publication. This report does not qualify Stage 1.
`;
}

function parseArguments(args) {
  const options = {
    regression: join(repositoryRoot, "artifacts/qa/qualification/pmcs-v1-qualification.json"),
    evidence: join(repositoryRoot, "artifacts/qa/v1.1-evidence"),
    output: join(repositoryRoot, "artifacts/qa/v1.1-qualification"),
  };
  for (let index = 0; index < args.length; index += 2) {
    const name = args[index];
    if (!args[index + 1]) throw new Error(`Missing value for ${name}.`);
    if (name === "--regression") options.regression = resolve(args[index + 1]);
    else if (name === "--evidence") options.evidence = resolve(args[index + 1]);
    else if (name === "--output") options.output = resolve(args[index + 1]);
    else throw new Error(`Unknown option: ${name}.`);
  }
  return options;
}

function readJson(path) { return JSON.parse(readFileSync(path, "utf8")); }
function writeAtomically(path, value) {
  const temp = `${path}.${process.pid}.tmp`;
  writeFileSync(temp, value, { mode: 0o600 });
  renameSync(temp, path);
}

async function main() {
  const options = parseArguments(process.argv.slice(2));
  const errors = [];
  let regressionReport = null;
  try { regressionReport = readJson(options.regression); }
  catch (error) { errors.push(`Cannot read full regression: ${error.message}`); }
  const evidenceReports = [];
  try {
    for (const name of readdirSync(options.evidence).filter(name =>
      name.endsWith(".json") && !name.endsWith(".artifact.json") && !name.endsWith(".source.json"))) {
      try { evidenceReports.push(readJson(join(options.evidence, name))); }
      catch (error) { errors.push(`Cannot read ${basename(name)}: ${error.message}`); }
    }
  } catch (error) { errors.push(`Cannot list V1.1 evidence: ${error.message}`); }
  const report = buildV11QualificationReport({
    requirements: readJson(join(directory, "v1.1-qualification-requirements.json")),
    regressionReport, evidenceReports, evidenceDirectory: options.evidence,
    expectedCommit: (process.env.PMCS_EXPECTED_COMMIT || process.env.GITHUB_SHA || "").trim().toLowerCase(),
    expectedTree: (process.env.PMCS_EXPECTED_TREE || "").trim().toLowerCase(),
    workflowRunId: process.env.GITHUB_RUN_ID?.trim() || null,
    workflowRunUrl: process.env.PMCS_WORKFLOW_RUN_URL?.trim() || null,
    inputErrors: errors,
  });
  mkdirSync(options.output, { recursive: true });
  writeAtomically(join(options.output, "pmcs-v1.1-qualification.json"), `${JSON.stringify(report, null, 2)}\n`);
  writeAtomically(join(options.output, "pmcs-v1.1-qualification.md"), renderV11QualificationMarkdown(report));
  console.log(JSON.stringify({ status: report.status, passed: report.summary.passedEvidenceCount, required: report.summary.expectedEvidenceCount }));
  process.exitCode = report.status === "qualified" ? 0 : 1;
}

if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
  main().catch(error => { console.error(error.message); process.exitCode = 1; });
}

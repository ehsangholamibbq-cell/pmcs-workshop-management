#!/usr/bin/env node

import assert from "node:assert/strict";
import { mkdirSync, readFileSync, readdirSync, renameSync, statSync, writeFileSync } from "node:fs";
import { basename, dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { defaultManifestPath, loadRegressionManifest, repositoryRoot } from "./regression-runner.mjs";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const defaultInputDirectory = join(repositoryRoot, "artifacts/qa/regression");
const defaultOutputDirectory = join(repositoryRoot, "artifacts/qa/qualification");
const commitPattern = /^[0-9a-f]{40}$/u;

export function buildQualificationReport({
  manifest,
  suiteReports,
  expectedCommit,
  jobResults,
  workflowRunId = null,
  workflowRunUrl = null,
  generatedAt = new Date(),
  inputErrors = [],
}) {
  assert.match(expectedCommit, commitPattern, "Qualification requires one full lowercase commit SHA.");
  const expectedSuites = manifest.suites.map(suite => suite.id);
  const expectedSet = new Set(expectedSuites);
  const seen = new Set();
  const failures = [...inputErrors];
  const normalizedSuites = [];

  for (const report of suiteReports) {
    if (!report || typeof report !== "object") {
      failures.push("A suite report is not a JSON object.");
      continue;
    }
    const suite = report.suite;
    if (!expectedSet.has(suite)) {
      failures.push(`Unexpected suite report: ${suite ?? "<missing>"}.`);
      continue;
    }
    if (seen.has(suite)) {
      failures.push(`Duplicate suite report: ${suite}.`);
      continue;
    }
    seen.add(suite);

    const contractValid = report.contractVersion === 1 &&
      report.reportType === "pmcs-regression-suite" &&
      report.runnerVersion === manifest.runnerVersion &&
      report.commit === expectedCommit &&
      ["passed", "failed"].includes(report.status) &&
      Number.isInteger(report.plannedCommandCount) && report.plannedCommandCount > 0 &&
      Number.isInteger(report.executedCommandCount) && report.executedCommandCount >= 0 &&
      Array.isArray(report.commands);
    if (!contractValid) failures.push(`Invalid or mismatched suite report contract: ${suite}.`);
    if (report.status !== "passed") failures.push(`Regression suite failed: ${suite}.`);
    if (report.executedCommandCount !== report.plannedCommandCount) failures.push(`Regression suite did not execute every command: ${suite}.`);
    const jobResult = jobResults[suite];
    if (jobResult !== "success") failures.push(`Connected job did not succeed: ${suite} (${jobResult ?? "missing"}).`);

    normalizedSuites.push({
      suite,
      displayName: report.displayName ?? suite,
      layer: report.layer ?? "unknown",
      status: contractValid && report.status === "passed" && jobResult === "success" ? "passed" : "failed",
      plannedCommandCount: Number.isInteger(report.plannedCommandCount) ? report.plannedCommandCount : 0,
      executedCommandCount: Number.isInteger(report.executedCommandCount) ? report.executedCommandCount : 0,
      durationMs: Number.isInteger(report.durationMs) ? report.durationMs : 0,
    });
  }

  for (const suite of expectedSuites) {
    if (!seen.has(suite)) failures.push(`Missing suite report: ${suite}.`);
    if (!(suite in jobResults)) failures.push(`Missing connected job result: ${suite}.`);
  }

  normalizedSuites.sort((left, right) => expectedSuites.indexOf(left.suite) - expectedSuites.indexOf(right.suite));
  const uniqueFailures = [...new Set(failures)];
  const qualified = uniqueFailures.length === 0 && normalizedSuites.length === expectedSuites.length;
  return {
    contractVersion: 1,
    reportType: "pmcs-v1-qualification",
    qualificationVersion: "pmcs-v1-qualification-1",
    product: "PMCS V1",
    baselineCommit: expectedCommit,
    status: qualified ? "qualified" : "failed",
    baselineLockEligible: qualified,
    generatedAt: generatedAt.toISOString(),
    workflowRun: {
      id: workflowRunId,
      url: workflowRunUrl,
    },
    summary: {
      expectedSuiteCount: expectedSuites.length,
      receivedSuiteCount: normalizedSuites.length,
      passedSuiteCount: normalizedSuites.filter(suite => suite.status === "passed").length,
      plannedCommandCount: normalizedSuites.reduce((total, suite) => total + suite.plannedCommandCount, 0),
      executedCommandCount: normalizedSuites.reduce((total, suite) => total + suite.executedCommandCount, 0),
      failureCount: uniqueFailures.length,
    },
    suites: normalizedSuites,
    failures: uniqueFailures,
  };
}

export function renderQualificationMarkdown(report) {
  const rows = report.suites.map(suite =>
    `| ${suite.suite} | ${suite.layer} | ${suite.status} | ${suite.executedCommandCount}/${suite.plannedCommandCount} | ${suite.durationMs} |`).join("\n");
  const failures = report.failures.length === 0
    ? "- None"
    : report.failures.map(failure => `- ${failure}`).join("\n");
  return `# PMCS V1 Qualification Report

- Status: \`${report.status}\`
- Baseline commit: \`${report.baselineCommit}\`
- Baseline lock eligible: \`${report.baselineLockEligible}\`
- Generated at: \`${report.generatedAt}\`
- Workflow run: ${report.workflowRun.url ?? report.workflowRun.id ?? "not supplied"}

| Suite | Layer | Status | Commands | Duration (ms) |
| --- | --- | --- | ---: | ---: |
${rows}

## Summary

- Suites: ${report.summary.passedSuiteCount}/${report.summary.expectedSuiteCount} passed
- Commands: ${report.summary.executedCommandCount}/${report.summary.plannedCommandCount} executed
- Failures: ${report.summary.failureCount}

## Failures

${failures}
`;
}

function readSuiteReports(inputDirectory) {
  const reports = [];
  const errors = [];
  if (!existsDirectory(inputDirectory)) return { reports, errors: [`Regression report directory is missing: ${inputDirectory}.`] };
  for (const path of walkJson(inputDirectory)) {
    try {
      const value = JSON.parse(readFileSync(path, "utf8"));
      if (value.reportType === "pmcs-regression-suite") reports.push(value);
    } catch (error) {
      errors.push(`Cannot read ${basename(path)}: ${error instanceof Error ? error.message : String(error)}`);
    }
  }
  return { reports, errors };
}

function readJobResults(manifest, environment) {
  return Object.fromEntries(manifest.suites.map(suite => {
    const name = `PMCS_JOB_RESULT_${suite.id.toUpperCase().replaceAll("-", "_")}`;
    return [suite.id, environment[name]?.trim()];
  }).filter(([, value]) => value));
}

function writeReports(outputDirectory, report) {
  mkdirSync(outputDirectory, { recursive: true });
  writeAtomically(join(outputDirectory, "pmcs-v1-qualification.json"), `${JSON.stringify(report, null, 2)}\n`);
  writeAtomically(join(outputDirectory, "pmcs-v1-qualification.md"), renderQualificationMarkdown(report));
}

function writeAtomically(path, contents) {
  const temporaryPath = `${path}.${process.pid}.tmp`;
  writeFileSync(temporaryPath, contents, { encoding: "utf8", mode: 0o600 });
  renameSync(temporaryPath, path);
}

function existsDirectory(path) {
  try {
    return statSync(path).isDirectory();
  } catch {
    return false;
  }
}

function walkJson(directory) {
  return readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) return walkJson(path);
    return entry.isFile() && entry.name.endsWith(".json") ? [path] : [];
  });
}

function parseArguments(args) {
  const options = { inputDirectory: defaultInputDirectory, outputDirectory: defaultOutputDirectory, manifestPath: defaultManifestPath };
  for (let index = 0; index < args.length; index += 2) {
    const flag = args[index];
    const value = args[index + 1];
    if (!value) throw new Error(`Missing value for ${flag}.`);
    if (flag === "--input-dir") options.inputDirectory = resolve(value);
    else if (flag === "--output-dir") options.outputDirectory = resolve(value);
    else if (flag === "--manifest") options.manifestPath = resolve(value);
    else throw new Error(`Unknown option: ${flag}`);
  }
  return options;
}

async function main() {
  const options = parseArguments(process.argv.slice(2));
  const manifest = loadRegressionManifest(options.manifestPath);
  const expectedCommit = (process.env.PMCS_EXPECTED_COMMIT || process.env.GITHUB_SHA || "").trim().toLowerCase();
  const { reports, errors } = readSuiteReports(options.inputDirectory);
  const report = buildQualificationReport({
    manifest,
    suiteReports: reports,
    expectedCommit,
    jobResults: readJobResults(manifest, process.env),
    workflowRunId: process.env.GITHUB_RUN_ID?.trim() || null,
    workflowRunUrl: process.env.PMCS_WORKFLOW_RUN_URL?.trim() || null,
    inputErrors: errors,
  });
  writeReports(options.outputDirectory, report);
  console.log(JSON.stringify({ status: report.status, suites: report.summary.passedSuiteCount, expectedSuites: report.summary.expectedSuiteCount, failures: report.summary.failureCount }));
  process.exitCode = report.status === "qualified" ? 0 : 1;
}

if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
  main().catch(error => {
    console.error(error instanceof Error ? error.message : String(error));
    process.exitCode = 1;
  });
}

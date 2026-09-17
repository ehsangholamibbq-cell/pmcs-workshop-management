import assert from "node:assert/strict";
import test from "node:test";
import { buildQualificationReport, renderQualificationMarkdown } from "../qa/test-report-generator.mjs";
import { runRegressionSuite, validateRegressionManifest } from "../qa/regression-runner.mjs";

const commit = "a".repeat(40);
const generatedAt = new Date("2026-09-17T18:00:00.000Z");
const manifest = {
  contractVersion: 1,
  runnerVersion: "pmcs-v1-regression-1",
  suites: [
    { id: "architecture", displayName: "Architecture", layer: "architecture", commands: [{ id: "validate", file: "node", args: ["validate.mjs"] }] },
    { id: "backend", displayName: "Backend", layer: "automated", commands: [{ id: "test", file: "dotnet", args: ["test"] }] },
  ],
};

test("regression runner records a fail-closed suite without exposing environment values", () => {
  const reports = [];
  let tick = 0;
  const report = runRegressionSuite({
    manifest,
    suiteId: "architecture",
    root: process.cwd(),
    outputDirectory: "/tmp/pmcs-regression-runner-test",
    commit,
    execute: () => ({ status: 0, signal: null, error: undefined }),
    clock: () => new Date(generatedAt.getTime() + tick++ * 10),
  });
  reports.push(report);
  assert.equal(report.status, "passed");
  assert.equal(report.executedCommandCount, 1);
  assert.equal("environment" in report, false);
  assert.equal(JSON.stringify(reports).includes("PMCS_QA_AUTH_KEY"), false);
});

test("regression manifest rejects duplicate suite identities", () => {
  assert.throws(
    () => validateRegressionManifest({ ...manifest, suites: [manifest.suites[0], manifest.suites[0]] }),
    /Duplicate regression suite/u,
  );
});

test("qualification report becomes lock-eligible only when every suite and connected job passes", () => {
  const report = buildQualificationReport({
    manifest,
    suiteReports: manifest.suites.map(validSuiteReport),
    expectedCommit: commit,
    jobResults: { architecture: "success", backend: "success" },
    workflowRunId: "70",
    workflowRunUrl: "https://github.example/actions/runs/70",
    generatedAt,
  });
  assert.equal(report.status, "qualified");
  assert.equal(report.baselineLockEligible, true);
  assert.equal(report.summary.passedSuiteCount, 2);
  assert.match(renderQualificationMarkdown(report), /Suites: 2\/2 passed/u);
});

test("qualification report fails closed when a suite artifact is missing", () => {
  const report = buildQualificationReport({
    manifest,
    suiteReports: [validSuiteReport(manifest.suites[0])],
    expectedCommit: commit,
    jobResults: { architecture: "success", backend: "success" },
    generatedAt,
  });
  assert.equal(report.status, "failed");
  assert.equal(report.baselineLockEligible, false);
  assert.ok(report.failures.includes("Missing suite report: backend."));
});

test("qualification report rejects evidence from another commit or a failed connected job", () => {
  const reports = manifest.suites.map(validSuiteReport);
  reports[1] = { ...reports[1], commit: "b".repeat(40) };
  const report = buildQualificationReport({
    manifest,
    suiteReports: reports,
    expectedCommit: commit,
    jobResults: { architecture: "success", backend: "failure" },
    generatedAt,
  });
  assert.equal(report.status, "failed");
  assert.ok(report.failures.some(failure => failure.includes("mismatched suite report contract: backend")));
  assert.ok(report.failures.some(failure => failure.includes("Connected job did not succeed: backend")));
});

function validSuiteReport(suite) {
  return {
    contractVersion: 1,
    reportType: "pmcs-regression-suite",
    runnerVersion: manifest.runnerVersion,
    suite: suite.id,
    displayName: suite.displayName,
    layer: suite.layer,
    commit,
    status: "passed",
    startedAt: generatedAt.toISOString(),
    completedAt: generatedAt.toISOString(),
    durationMs: 10,
    plannedCommandCount: 1,
    executedCommandCount: 1,
    commands: [{ id: suite.commands[0].id, status: "passed", exitCode: 0, signal: null, durationMs: 10, failureCode: null }],
  };
}

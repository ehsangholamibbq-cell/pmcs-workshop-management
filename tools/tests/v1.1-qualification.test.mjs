import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";
import { buildV11QualificationReport, renderV11QualificationMarkdown } from "../qa/v1.1-test-report-generator.mjs";
import { validateCandidateDefaults } from "../qa/v1.1-candidate-preflight.mjs";
import { validateDormantProbe } from "../qa/v1.1-int1-dormant-evidence.mjs";
import { verifyDomainSources } from "../qa/v1.1-domain-evidence.mjs";
import { validateMigrationObservations } from "../qa/v1.1-migration-evidence.mjs";
import { validateUpgradeObservations } from "../qa/v1.1-upgrade-evidence.mjs";
import { validateInterruptionObservations } from "../qa/v1.1-interruption-evidence.mjs";
import { validateLoadSoak } from "../qa/v1.1-load-soak-evidence.mjs";
import { validateReleaseBuild } from "../qa/v1.1-release-build-evidence.mjs";

const commit = "a".repeat(40);
const tree = "b".repeat(40);
const requirements = { contractVersion: 1, product: "PMCS V1.1", requiredEvidence: ["int1-dormant", "migration-v1-upgrade"] };
const regressionReport = {
  reportType: "pmcs-v1-qualification", product: "PMCS V1", baselineCommit: commit,
  status: "qualified", baselineLockEligible: true,
  suites: ["architecture", "backend", "integration", "pilot-contract", "web", "ui-e2e", "identity-container"].map(suite => ({ suite, status: "passed" })),
};

test("V1.1 release build requires matching API, Web and image identities", () => {
  const expected = { commit, version: "1.1.0", builtAt: "2026-10-01T00:00:00.000Z" };
  const images = Object.fromEntries(["api", "web"].map(name => [name, {
    id: `sha256:${"d".repeat(64)}`, revision: commit, version: expected.version,
  }]));
  const api = { schemaVersion: 1, artifact: "api", ...expected };
  const web = { schemaVersion: 1, artifact: "web", ...expected };
  validateReleaseBuild({ api, web, expected, images });
  assert.throws(() => validateReleaseBuild({ api, web: { ...web, commit: tree }, expected, images }));
  assert.throws(() => validateReleaseBuild({ api, web, expected, images: { ...images, api: { ...images.api, revision: tree } } }));
});

test("V1.1 qualification requires all evidence on the same commit and verifies artifact bytes", () => {
  const evidenceDirectory = mkdtempSync(join(tmpdir(), "pmcs-v11-qualification-"));
  try {
    const artifact = "verification.txt";
    const bytes = "actual verification output\n";
    writeFileSync(join(evidenceDirectory, artifact), bytes);
    const sha256 = createHash("sha256").update(bytes).digest("hex");
    const reports = requirements.requiredEvidence.map(id => ({
      contractVersion: 1, reportType: "pmcs-v1.1-qa-evidence", id,
      commit, tree, status: "passed", checks: [{ name: "connected check", status: "passed" }],
      artifacts: [{ path: artifact, sha256 }],
    }));
    const build = (evidenceReports, regression = regressionReport) => buildV11QualificationReport({
      requirements, regressionReport: regression, evidenceReports, evidenceDirectory,
      expectedCommit: commit, expectedTree: tree, generatedAt: new Date("2026-10-01T00:00:00Z"),
    });

    const complete = build(reports);
    assert.equal(complete.status, "qualified");
    assert.equal(complete.baselineLockEligible, true);
    assert.match(renderV11QualificationMarkdown(complete), /Deferred by ADR 0033/u);
    assert.equal(build([reports[0]]).status, "failed");
    assert.ok(build([reports[0]]).failures.includes("Missing V1.1 evidence: migration-v1-upgrade."));
    assert.equal(build([...reports, reports[0]]).status, "failed");
    assert.equal(build([{ ...reports[0], commit: "c".repeat(40) }, reports[1]]).status, "failed");
    assert.equal(build([{ ...reports[0], artifacts: [{ path: artifact, sha256: "0".repeat(64) }] }, reports[1]]).status, "failed");
    assert.equal(build([{ ...reports[0], artifacts: [{ path: "../verification.txt", sha256 }] }, reports[1]]).status, "failed");
    assert.equal(build(reports, { ...regressionReport, baselineCommit: "c".repeat(40) }).status, "failed");
  } finally { rmSync(evidenceDirectory, { recursive: true, force: true }); }
});

test("candidate defaults reject a reference flag enabled in API or Compose", () => {
  const config = JSON.stringify({ Intelligence: { INT1ReferenceEnabled: false, INT1FixtureEnabled: false } });
  const compose = "  api:\n    environment:\n      Intelligence__INT1ReferenceEnabled: \"false\"\n      Intelligence__INT1FixtureEnabled: \"false\"\n  web:\n";
  validateCandidateDefaults(config, compose);
  assert.throws(() => validateCandidateDefaults(config.replace('"INT1ReferenceEnabled":false', '"INT1ReferenceEnabled":true'), compose));
  assert.throws(() => validateCandidateDefaults(config, compose.replace('Intelligence__INT1ReferenceEnabled: "false"', 'Intelligence__INT1ReferenceEnabled: "true"')));
});

test("INT1 dormant evidence rejects active registry entries and misclassified probes", () => {
  const probe = {
    stage: "V1.1-INT1", configuredFailure: false, allAvailable: false,
    liveProviderEvidence: ["OpenAI", "GoogleGemini", "AnthropicClaude"].map(provider => ({
      provider, configuration: "Unavailable", structured: "Unavailable", toolCalling: "Unavailable",
    })),
  };
  validateDormantProbe(probe, "0|0|0|0|0");
  assert.throws(() => validateDormantProbe(probe, "0|0|0|1|0"));
  assert.throws(() => validateDormantProbe({ ...probe, liveProviderEvidence: [{ ...probe.liveProviderEvidence[0], structured: "Available" }, ...probe.liveProviderEvidence.slice(1)] }, "0|0|0|0|0"));
});

test("domain evidence is tied to a real connected command on one SHA", () => {
  const reports = {
    integration: {
      contractVersion: 1, reportType: "pmcs-regression-suite", suite: "integration", commit,
      status: "passed", plannedCommandCount: 1, executedCommandCount: 1,
      commands: [{ id: "connected-integration-regression", status: "passed", exitCode: 0 }],
    },
  };
  const sources = { integration: ["connected-integration-regression"] };
  assert.equal(verifyDomainSources(reports, commit, sources).length, 1);
  assert.throws(() => verifyDomainSources({ integration: { ...reports.integration, commit: tree } }, commit, sources));
  assert.throws(() => verifyDomainSources({ integration: { ...reports.integration, commands: [] } }, commit, sources));
  assert.throws(() => verifyDomainSources({ integration: { ...reports.integration, status: "failed" } }, commit, sources));
});

test("migration scenarios reject changed ledger, incomplete restore or missing digest", () => {
  const observations = {
    firstLedger: `70|${"a".repeat(32)}`,
    repeatedLedger: `70|${"a".repeat(32)}`,
    restoredLedger: `70|${"a".repeat(32)}`,
    restoredInt1: "1|5|true", backupSha256: "b".repeat(64),
  };
  validateMigrationObservations(observations);
  assert.throws(() => validateMigrationObservations({ ...observations, repeatedLedger: `71|${"a".repeat(32)}` }));
  assert.throws(() => validateMigrationObservations({ ...observations, restoredLedger: `70|${"c".repeat(32)}` }));
  assert.throws(() => validateMigrationObservations({ ...observations, restoredInt1: "1|4|true" }));
});

test("V1 upgrade and runtime rollback require preserved V1 data", () => {
  const value = {
    v1Count: "63", v1RestoredCount: "63", upgradeCount: "70", rollbackCount: "70",
    v1DataCounts: "2|5", upgradeDataCounts: "2|5", rollbackDataCounts: "2|5",
    backupSha256: "a".repeat(64),
  };
  validateUpgradeObservations(value);
  assert.throws(() => validateUpgradeObservations({ ...value, v1RestoredCount: "62" }));
  assert.throws(() => validateUpgradeObservations({ ...value, upgradeDataCounts: "1|5" }));
  assert.throws(() => validateUpgradeObservations({ ...value, rollbackCount: "69" }));
});

test("interrupted migration must roll back the ledger and preserve V1 data on recovery", () => {
  const value = { before: "63", afterFailure: "63", recovered: "70", v1DataCounts: "2|5", recoveredDataCounts: "2|5" };
  validateInterruptionObservations(value);
  assert.throws(() => validateInterruptionObservations({ ...value, afterFailure: "64" }));
  assert.throws(() => validateInterruptionObservations({ ...value, recoveredDataCounts: "1|5" }));
});

test("load/soak evidence rejects excessive latency and reporting capacity gaps", () => {
  const service = { stage: "pmcs-v1.1-permission-diagnostics-load-soak", status: "passed", durationSeconds: 30.5, workers: 2, requests: 100, failedRequests: 0, p95Milliseconds: 400 };
  const reporting = { stage: "reporting-worker-capacity-regression", status: "passed", assertions: 11, healthyRuns: 20, poisonAttempts: 3, p95BudgetSeconds: 30 };
  validateLoadSoak(service, reporting);
  assert.throws(() => validateLoadSoak({ ...service, p95Milliseconds: 1500 }, reporting));
  assert.throws(() => validateLoadSoak(service, { ...reporting, healthyRuns: 19 }));
});

import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";
import { buildV11QualificationReport, renderV11QualificationMarkdown } from "../qa/v1.1-test-report-generator.mjs";
import { validateCandidateDefaults } from "../qa/v1.1-candidate-preflight.mjs";
import { validateDormantProbe } from "../qa/v1.1-int1-dormant-evidence.mjs";

const commit = "a".repeat(40);
const tree = "b".repeat(40);
const requirements = { contractVersion: 1, product: "PMCS V1.1", requiredEvidence: ["int1-dormant", "migration-v1-upgrade"] };
const regressionReport = {
  reportType: "pmcs-v1-qualification", product: "PMCS V1", baselineCommit: commit,
  status: "qualified", baselineLockEligible: true,
  suites: ["architecture", "backend", "integration", "pilot-contract", "web", "ui-e2e", "identity-container"].map(suite => ({ suite, status: "passed" })),
};

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

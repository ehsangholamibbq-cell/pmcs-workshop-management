#!/usr/bin/env node
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const defaultCatalog = JSON.parse(readFileSync(resolve(root, "release/pilot-gates.json"), "utf8"));
const commitPattern = /^[0-9a-f]{40}$/u;
const shaPattern = /^[0-9a-f]{64}$/u;
const releaseIdPattern = /^pmcs-[0-9]{4}\.[0-9]{2}\.[0-9]{2}-rc\.[0-9]+$/u;
const versionPattern = /^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$/u;
const evidenceUriPattern = /^evidence:\/\/[A-Za-z0-9._/-]+$/u;

export function validatePilotCandidate(candidate, options = {}) {
  const catalog = options.catalog ?? defaultCatalog;
  const now = asDate(options.now ?? new Date().toISOString(), "validation time");
  object(candidate, "candidate");
  exactKeys(candidate, ["schemaVersion", "releaseId", "version", "commit", "builtAt", "createdAt", "expiresAt", "artifacts", "evidence", "approvals"], "candidate");

  equal(candidate.schemaVersion, 1, "schemaVersion must be 1");
  matches(candidate.releaseId, releaseIdPattern, "releaseId");
  matches(candidate.version, versionPattern, "version");
  matches(candidate.commit, commitPattern, "commit");
  const builtAt = asDate(candidate.builtAt, "builtAt");
  const createdAt = asDate(candidate.createdAt, "createdAt");
  const expiresAt = asDate(candidate.expiresAt, "expiresAt");
  assert(builtAt <= createdAt, "builtAt cannot be after createdAt");
  assert(createdAt <= now, "createdAt cannot be in the future");
  assert(expiresAt > now, "candidate has expired");
  assert(expiresAt.getTime() - createdAt.getTime() <= hours(catalog.candidateMaximumLifetimeHours), "candidate lifetime exceeds policy");

  array(candidate.artifacts, "artifacts");
  validateExactSet(candidate.artifacts, ["api-image", "web-image", "source-bundle"], item => item.name, "artifact");
  for (const artifact of candidate.artifacts) {
    object(artifact, "artifact");
    exactKeys(artifact, ["name", "sha256"], `artifact ${artifact.name}`);
    matches(artifact.sha256, shaPattern, `artifact ${artifact.name} sha256`);
  }

  array(candidate.evidence, "evidence");
  const gates = new Map(catalog.gates.map(gate => [gate.id, gate]));
  validateExactSet(candidate.evidence, [...gates.keys()], item => item.gateId, "gate evidence");
  let latestEvidenceAt = builtAt;
  for (const item of candidate.evidence) {
    object(item, "gate evidence");
    exactKeys(item, ["gateId", "status", "commit", "executedAt", "expiresAt", "reportSha256", "reportUri"], `gate ${item.gateId}`);
    equal(item.status, "passed", `gate ${item.gateId} is not passed`);
    equal(item.commit, candidate.commit, `gate ${item.gateId} targets another commit`);
    matches(item.reportSha256, shaPattern, `gate ${item.gateId} reportSha256`);
    matches(item.reportUri, evidenceUriPattern, `gate ${item.gateId} reportUri`);
    const executedAt = asDate(item.executedAt, `gate ${item.gateId} executedAt`);
    const evidenceExpiresAt = asDate(item.expiresAt, `gate ${item.gateId} expiresAt`);
    assert(executedAt >= builtAt, `gate ${item.gateId} predates the artifact build`);
    assert(executedAt <= now, `gate ${item.gateId} was executed in the future`);
    assert(evidenceExpiresAt > now, `gate ${item.gateId} evidence has expired`);
    assert(evidenceExpiresAt.getTime() - executedAt.getTime() <= hours(gates.get(item.gateId).maximumAgeHours), `gate ${item.gateId} evidence exceeds maximum age`);
    if (executedAt > latestEvidenceAt) latestEvidenceAt = executedAt;
  }

  array(candidate.approvals, "approvals");
  validateExactSet(candidate.approvals, catalog.approvalRoles, item => item.role, "approval");
  const approvers = new Set();
  for (const approval of candidate.approvals) {
    object(approval, "approval");
    exactKeys(approval, ["role", "decision", "approvedBy", "approvedAt", "commit"], `approval ${approval.role}`);
    equal(approval.decision, "approved", `approval ${approval.role} is not approved`);
    equal(approval.commit, candidate.commit, `approval ${approval.role} targets another commit`);
    assert(typeof approval.approvedBy === "string" && approval.approvedBy.trim().length >= 3 && approval.approvedBy.length <= 200, `approval ${approval.role} has an invalid approver`);
    const approverKey = approval.approvedBy.trim().toLocaleLowerCase("en-US");
    assert(!approvers.has(approverKey), "product, security and operations approvals must be made by different people");
    approvers.add(approverKey);
    const approvedAt = asDate(approval.approvedAt, `approval ${approval.role} approvedAt`);
    assert(approvedAt >= latestEvidenceAt, `approval ${approval.role} predates gate evidence`);
    assert(approvedAt <= createdAt, `approval ${approval.role} is after candidate creation`);
  }

  return Object.freeze({
    releaseId: candidate.releaseId,
    version: candidate.version,
    commit: candidate.commit,
    gateCount: candidate.evidence.length,
    approvalCount: candidate.approvals.length,
    expiresAt: candidate.expiresAt,
  });
}

function validateExactSet(items, expected, key, label) {
  const actual = items.map(key);
  assert(new Set(actual).size === actual.length, `${label} entries must be unique`);
  const actualSorted = [...actual].sort();
  const expectedSorted = [...expected].sort();
  assert(JSON.stringify(actualSorted) === JSON.stringify(expectedSorted), `${label} set does not match policy`);
}

function exactKeys(value, expected, label) {
  const actual = Object.keys(value).sort();
  const wanted = [...expected].sort();
  assert(JSON.stringify(actual) === JSON.stringify(wanted), `${label} has missing or unknown fields`);
}

function matches(value, pattern, label) {
  assert(typeof value === "string" && pattern.test(value), `${label} has an invalid format`);
}

function asDate(value, label) {
  assert(typeof value === "string" && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{3})?Z$/u.test(value), `${label} must be an ISO-8601 UTC timestamp`);
  const parsed = new Date(value);
  assert(!Number.isNaN(parsed.getTime()), `${label} is not a valid timestamp`);
  return parsed;
}

function array(value, label) { assert(Array.isArray(value), `${label} must be an array`); }
function object(value, label) { assert(value !== null && typeof value === "object" && !Array.isArray(value), `${label} must be an object`); }
function equal(actual, expected, message) { assert(actual === expected, message); }
function assert(condition, message) { if (!condition) throw new Error(message); }
function hours(value) { return Number(value) * 60 * 60 * 1000; }

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const candidateFlag = process.argv.indexOf("--candidate");
  if (candidateFlag < 0 || !process.argv[candidateFlag + 1]) {
    console.error("Usage: node tools/validate-pilot-release.mjs --candidate <candidate.json> [--now <UTC timestamp>]");
    process.exit(2);
  }
  const nowFlag = process.argv.indexOf("--now");
  try {
    const candidate = JSON.parse(readFileSync(resolve(process.cwd(), process.argv[candidateFlag + 1]), "utf8"));
    const result = validatePilotCandidate(candidate, { now: nowFlag >= 0 ? process.argv[nowFlag + 1] : undefined });
    console.log(JSON.stringify({ status: "passed", ...result }));
  } catch (error) {
    console.error(`Pilot release validation failed: ${error instanceof Error ? error.message : String(error)}`);
    process.exit(1);
  }
}

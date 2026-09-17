import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import test from "node:test";

const parentRuntime = "26bf222d44634562ca7f3fc0931f3f8b79ca04a1";
const repositoryStart = "0389b52cbd3385bdcc9f0e2a94411800389ae2fc";
const requiredFiles = [
  "docs/adr/0027-post-v1-extensibility-collaboration-reporting.md",
  "docs/adr/0028-configurable-login-member-profile-project-bootstrap.md",
  "docs/architecture/pmcs-v1.1-contract-migration-event-strategy.md",
  "docs/governance/pmcs-v1.1-development-baseline.md",
  "docs/governance/pmcs-v1.1-risk-register.md",
  "docs/governance/pmcs-v1.1-scope-and-change-control.md",
  "docs/governance/pmcs-version-and-baseline-policy.md",
  "docs/qa/pmcs-v1.1-test-and-qualification-contract.md",
  "docs/roadmaps/pmcs-managerial-agent-seven-stage-roadmap.md",
  "docs/roadmaps/pmcs-post-v1-product-evolution.md",
  "docs/roadmaps/pmcs-visual-excellence-program.md",
  "docs/security/pmcs-v1.1-permission-catalog.md",
  "release/pmcs-v1-baseline.json",
  "release/pmcs-v1.1-development-baseline.json",
];

test("V1.1 governance baseline pins exact and distinct runtime and repository commits", () => {
  const manifest = readJson("release/pmcs-v1.1-development-baseline.json");
  assert.equal(manifest.contractVersion, 1);
  assert.equal(manifest.product, "PMCS V1.1");
  assert.equal(manifest.targetVersion, "1.1.0");
  assert.equal(manifest.parentProductBaselineCommit, parentRuntime);
  assert.equal(manifest.repositoryStartCommit, repositoryStart);
  assert.notEqual(manifest.parentProductBaselineCommit, manifest.repositoryStartCommit);
  assert.equal(manifest.developmentBranch, "v1.1-development");
  assert.equal(manifest.runtimeChangesAfterParentBaseline, false);
  assert.ok(["governance-candidate", "architecture-approved"].includes(manifest.status));
});

test("every required V1.1 governance contract is present", () => {
  for (const path of requiredFiles) assert.equal(existsSync(path), true, `Missing ${path}`);
});

test("roadmap carries approved visual, profile and bootstrap decisions without shrinking Agent stages", () => {
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  for (const decision of ["D-PV1-12", "D-PV1-13", "D-PV1-14", "D-PV1-15"]) assert.match(roadmap, new RegExp(decision, "u"));
  for (const checkpoint of ["V1.1-IAM1", "V1.1-PRJ1", "V1.1-INT1", "V1.1-QA1"]) assert.match(roadmap, new RegExp(checkpoint.replaceAll(".", "\\."), "u"));
  const agent = read("docs/roadmaps/pmcs-managerial-agent-seven-stage-roadmap.md");
  for (let stage = 1; stage <= 7; stage += 1) assert.match(agent, new RegExp(`Stage ${stage}`, "u"));
});

test("login customization and project duplication retain fail-closed boundaries", () => {
  const adr = read("docs/adr/0028-configurable-login-member-profile-project-bootstrap.md");
  for (const boundary of [
    "بارگذاری HTML/CSS/JS آزاد",
    "Clone مستقیم Database پروژه",
    "کپی User account همراه پروژه",
    "انتقال خودکار تمام Roleها بدون Preview",
  ]) assert.match(adr, new RegExp(boundary, "u"));
  const architecture = read("docs/architecture/pmcs-v1.1-contract-migration-event-strategy.md");
  assert.match(architecture, /Agent → Permission-aware Tool → PMCS Application Service → Business Rules → Database/u);
  assert.match(architecture, /Project Bootstrap در Offline قابل اجرا نیست/u);
});

test("permission catalog separates sensitive duties", () => {
  const catalog = read("docs/security/pmcs-v1.1-permission-catalog.md");
  for (const permission of [
    "branding.login.publish",
    "branding.login.rollback",
    "identity.profile.photo_update_self",
    "projects.bootstrap.members_copy",
    "projects.bootstrap.activate",
    "documents.release_quarantine",
    "collaboration.record.convert",
    "intelligence.tool.invoke_read",
  ]) assert.match(catalog, new RegExp(permission.replaceAll(".", "\\."), "u"));
  assert.match(catalog, /Stage 1 Agent هیچ Permission نوشتن Domain ندارد/u);
});

function read(path) {
  return readFileSync(path, "utf8");
}

function readJson(path) {
  return JSON.parse(read(path));
}


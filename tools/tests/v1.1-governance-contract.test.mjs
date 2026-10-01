import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import test from "node:test";

const parentRuntime = "26bf222d44634562ca7f3fc0931f3f8b79ca04a1";
const repositoryStart = "0389b52cbd3385bdcc9f0e2a94411800389ae2fc";
const requiredFiles = [
  "docs/api/identity-experience-v1.md",
  "docs/checkpoints/v1.1-iam1-candidate.md",
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
  "release/pmcs-v1.1-iam1-candidate.json",
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
  if (manifest.status === "architecture-approved") {
    assert.match(manifest.governanceSourceCommit, /^[0-9a-f]{40}$/u);
    assert.match(manifest.governanceValidationRunId, /^\d+$/u);
    assert.equal(manifest.governanceValidationRunConclusion, "success");
    assert.match(manifest.governanceValidationRunUrl, /^https:\/\/github\.com\//u);
  }
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

test("V1.1 INT1 release keeps live qualification as the first post-baseline Agent gate", () => {
  const adr = read("docs/adr/0033-int1-v11-dormant-foundation-and-live-qualification.md");
  const scope = read("docs/governance/pmcs-v1.1-scope-and-change-control.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const agent = read("docs/roadmaps/pmcs-managerial-agent-seven-stage-roadmap.md");
  const qa = read("docs/qa/pmcs-v1.1-test-and-qualification-contract.md");
  const risk = read("docs/governance/pmcs-v1.1-risk-register.md");
  const endpoint = read("src/backend/Pmcs.Modules.Intelligence/Endpoints/IntelligenceReferenceRunEndpoints.cs");
  const recovery = read("src/backend/Pmcs.Modules.Intelligence/Services/ReferenceRunRecoveryWorker.cs");
  const grants = read("src/backend/Pmcs.Modules.Intelligence/Migrations/IntelligenceAdministrationGrantMigration.cs");
  const providerMigration = read("src/backend/Pmcs.Modules.Intelligence/Migrations/IntelligenceProviderRegistrationMigration.cs");
  const modelMigration = read("src/backend/Pmcs.Modules.Intelligence/Migrations/IntelligenceModelProfileMigration.cs");
  const defaults = read("src/backend/Pmcs.Api/appsettings.json");
  for (const contract of [adr, scope, roadmap, agent, qa, risk]) {
    assert.match(contract, /AGENT-S1-LIVE/u);
  }
  assert.match(adr, /پس از قفل Baseline نسخهٔ 1\.1/u);
  assert.match(adr, /AGENT-S2/u);
  assert.match(roadmap, /V1\.2 ابتدا `AGENT-S1-LIVE`/u);
  assert.match(qa, /INT1ReferenceEnabled/u);
  assert.match(risk, /R-AI-03[\s\S]*ریسک اتصال زنده باز تا آزمون سه Provider/u);
  assert.match(endpoint, /bool\.TryParse\(configuration\["Intelligence:INT1ReferenceEnabled"\], out var enabled\) && enabled/u);
  assert.match(recovery, /!bool\.TryParse\(configuration\["Intelligence:INT1ReferenceEnabled"\], out var enabled\) \|\|\s*!enabled\) return/u);
  assert.doesNotMatch(grants, /insert\s+into\s+intelligence\.administration_grants/iu);
  assert.doesNotMatch(providerMigration + modelMigration, /insert\s+into\s+intelligence\.(?:providers|models|model_catalog)/iu);
  assert.doesNotMatch(defaults, /"INT1ReferenceEnabled"\s*:\s*true/u);
  assert.doesNotMatch(defaults, /"(?:ApiKey|API_KEY|Secret)"\s*:/u);
});

test("V1.1 owner acceptance is a required post-lock, pre-publish gate", () => {
  const adr = read("docs/adr/0034-v1.1-owner-acceptance-and-release-gate.md");
  const policy = read("docs/governance/pmcs-version-and-baseline-policy.md");
  const release = read("docs/release/pmcs-v1.1-owner-acceptance.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const canonical = read("docs/PMCS-CANONICAL-PROJECT-REFERENCE.md");
  for (const text of [adr, policy, release, roadmap, canonical]) {
    assert.match(text, /OWNER-ACCEPTANCE/u);
    assert.match(text, /انتشار/u);
  }
  assert.match(adr, /QA1 کامل[\s\S]*Baseline Locked[\s\S]*آزمون کامل دستی مالک محصول[\s\S]*انتشار/u);
  assert.match(adr, /فقط پس از تأیید انجام انتشار[\s\S]*V1\.2/u);
  assert.match(release, /وضعیت: `Planned — اجرا نشده`/u);
  assert.match(roadmap, /V1\.2 فقط پس از QA1، قفل Baseline V1\.1، پذیرش کامل ثبت‌شدهٔ مالک و انتشار رسمی V1\.1/u);
  assert.match(canonical, /QA1 آغاز نشده/u);
  assert.match(canonical, /OWNER-ACCEPTANCE → PUBLISHED → V1\.2/u);
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
    "login-experience.manage",
    "member-profile.read-self",
    "member-profile.update-self",
    "member-profile.avatar.publish-self",
    "member-profile.read-directory",
    "member-profile.manage-directory",
    "projects.bootstrap.members_copy",
    "projects.bootstrap.activate",
    "documents.quarantine.release",
    "collaboration.record.convert",
    "intelligence.tool.invoke_read",
  ]) assert.match(catalog, new RegExp(permission.replaceAll(".", "\\."), "u"));
  assert.match(catalog, /Stage 1 Agent هیچ Permission نوشتن Domain ندارد/u);
});

test("IAM1 API, permission, event and migration contracts stay aligned with implementation", () => {
  const api = read("docs/api/identity-experience-v1.md");
  const module = read("src/backend/Pmcs.Modules.IdentityAccess/IdentityAccessModule.cs");
  const endpoints = read("src/backend/Pmcs.Modules.IdentityAccess/Endpoints/IdentityExperienceEndpoints.cs");
  const migration = read("src/backend/Pmcs.Modules.IdentityAccess/Migrations/IdentityExperienceMigration.cs");
  for (const permission of [
    "login-experience.manage",
    "member-profile.read-self",
    "member-profile.update-self",
    "member-profile.avatar.publish-self",
    "member-profile.read-directory",
    "member-profile.manage-directory",
  ]) {
    assert.match(api, new RegExp(permission.replaceAll(".", "\\."), "u"));
    assert.match(module, new RegExp(permission.replaceAll(".", "\\."), "u"));
  }
  for (const route of [
    "/api/v1/public/login-experience",
    "/api/v1/member-profile",
    "/api/v1/identity/login-experiences",
  ]) assert.match(endpoints, new RegExp(route.replaceAll("/", "\\/"), "u"));
  assert.match(api, /identity\.member-profile\.updated\.v1/u);
  assert.match(api, /identity\.login-experience\.published\.v1/u);
  assert.match(endpoints, /identity\.member-profile\.updated\.v1/u);
  assert.match(endpoints, /identity\.login-experience\.published\.v1/u);
  assert.match(migration, /20260918-002/u);
});

function read(path) {
  return readFileSync(path, "utf8");
}

function readJson(path) {
  return JSON.parse(read(path));
}

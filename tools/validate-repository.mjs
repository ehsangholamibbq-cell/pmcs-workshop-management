import assert from "node:assert/strict";
import { readFileSync, readdirSync, statSync } from "node:fs";
import { join, relative, resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const requiredFiles = [
  "PMCS.slnx",
  "Directory.Build.props",
  "Directory.Packages.props",
  "src/backend/Pmcs.Api/Program.cs",
  "src/backend/Pmcs.Api/Infrastructure/ProductionConfigurationValidator.cs",
  "src/backend/Pmcs.Api/Infrastructure/OperationalMetricsConfiguration.cs",
  "src/backend/Pmcs.Api/Infrastructure/ReleaseIdentity.cs",
  "src/backend/Pmcs.Api/Infrastructure/ActorAccessMiddleware.cs",
  "src/backend/Pmcs.Api/Infrastructure/ProjectLifecycleMiddleware.cs",
  "src/backend/Pmcs.BuildingBlocks/Domain/PersianDateCode.cs",
  "src/backend/Pmcs.Modules.IdentityAccess/Domain/UserInvitation.cs",
  "src/backend/Pmcs.Modules.IdentityAccess/Domain/MemberProfile.cs",
  "src/backend/Pmcs.Modules.IdentityAccess/Domain/LoginExperience.cs",
  "src/backend/Pmcs.Modules.IdentityAccess/Migrations/IdentityExperienceMigration.cs",
  "src/backend/Pmcs.Modules.IdentityAccess/Services/IdentityAdministrationWorker.cs",
  "src/backend/Pmcs.Modules.IdentityAccess/Services/KeycloakIdentityProviderAdministration.cs",
  "src/backend/Pmcs.Modules.FieldOperations/Endpoints/SyncEndpoints.cs",
  "src/backend/Pmcs.Modules.FieldOperations/Contracts/IProgressEvidenceReportingSource.cs",
  "src/backend/Pmcs.Modules.FieldOperations/Properties/AssemblyInfo.cs",
  "src/backend/Pmcs.Modules.FieldOperations/Migrations/FieldOperationsLocationLinkMigration.cs",
  "src/backend/Pmcs.Modules.Platform/Migrations/PlatformIdempotencyRetentionMigration.cs",
  "src/backend/Pmcs.Modules.Platform/Migrations/PlatformMigrationLedgerNormalizationMigration.cs",
  "src/backend/Pmcs.Modules.Platform/Services/IdempotencyRetentionWorker.cs",
  "src/backend/Pmcs.Modules.Projects/Domain/ProjectLocation.cs",
  "src/backend/Pmcs.Modules.Projects/Domain/ProjectBootstrapPlan.cs",
  "src/backend/Pmcs.Modules.Projects/Endpoints/ProjectBootstrapEndpoints.cs",
  "src/backend/Pmcs.Modules.Projects/Migrations/ProjectBootstrapMigration.cs",
  "src/backend/Pmcs.Modules.Projects/Services/ProjectBootstrapContributorCatalog.cs",
  "src/backend/Pmcs.Modules.Projects/Migrations/ProjectActivationMetadataMigration.cs",
  "src/backend/Pmcs.Modules.Projects/Migrations/ProjectLocationMigration.cs",
  "src/backend/Pmcs.Modules.Sync/Endpoints/SyncGatewayEndpoints.cs",
  "src/backend/Pmcs.Modules.Sync/Migrations/SyncRecoveryDiagnosticsMigration.cs",
  "src/backend/Pmcs.Modules.Planning/Domain/MeasurementItem.cs",
  "src/backend/Pmcs.Modules.Planning/Domain/ProgressLedgerCalculation.cs",
  "src/backend/Pmcs.Modules.Planning/Contracts/IProjectProgressReportingSource.cs",
  "src/backend/Pmcs.Modules.Planning/Services/ProjectProgressReportingSelector.cs",
  "src/backend/Pmcs.Modules.Planning/Services/ProjectProgressReportingCalculator.cs",
  "src/backend/Pmcs.Modules.Planning/Endpoints/PlanningEndpoints.cs",
  "src/backend/Pmcs.Modules.ProjectIntelligence/Domain/ProjectStateCalculation.cs",
  "src/backend/Pmcs.Modules.ProjectIntelligence/Domain/PortfolioOverviewCalculation.cs",
  "src/backend/Pmcs.Modules.ProjectIntelligence/Endpoints/PortfolioCommandCenterEndpoints.cs",
  "src/backend/Pmcs.Modules.ProjectIntelligence/Migrations/ProjectStateLocationLinkMigration.cs",
  "src/backend/Pmcs.Modules.Intelligence/Domain/AdvisoryInsight.cs",
  "src/backend/Pmcs.Modules.Intelligence/Services/PermissionAwareContextAssembler.cs",
  "src/backend/Pmcs.Modules.Evidence/Domain/EvidenceFile.cs",
  "src/backend/Pmcs.Modules.Evidence/Domain/EvidenceContentPolicy.cs",
  "src/backend/Pmcs.Modules.Documents/Domain/DocumentAsset.cs",
  "src/backend/Pmcs.Modules.Documents/Domain/DocumentContentPolicy.cs",
  "src/backend/Pmcs.Modules.Documents/Contracts/IGeneratedDocumentOrphanRemediator.cs",
  "src/backend/Pmcs.Modules.Documents/Services/GeneratedDocumentOrphanRemediator.cs",
  "src/backend/Pmcs.Modules.Documents/Endpoints/DocumentEndpoints.cs",
  "src/backend/Pmcs.Modules.Documents/Migrations/DocumentsInitialMigration.cs",
  "src/backend/Pmcs.Modules.Reporting/ReportingModule.cs",
  "src/backend/Pmcs.Modules.Reporting/Domain/ReportRun.cs",
  "src/backend/Pmcs.Modules.Reporting/Domain/ReportSnapshot.cs",
  "src/backend/Pmcs.Modules.Reporting/Domain/ProjectProgressReportRuntimeContract.cs",
  "src/backend/Pmcs.Modules.Reporting/Services/ProjectProgressReportSnapshotBuilder.cs",
  "src/backend/Pmcs.Modules.Reporting/Domain/ReportDefinitionRuntimePolicy.cs",
  "src/backend/Pmcs.Modules.Reporting/Contracts/IReportingReadService.cs",
  "src/backend/Pmcs.Modules.Reporting/Endpoints/ReportingEndpoints.cs",
  "src/backend/Pmcs.Modules.Reporting/Migrations/ReportingInitialMigration.cs",
  "src/backend/Pmcs.Modules.Reporting/Migrations/ProjectPeriodicReportCatalogMigration.cs",
  "src/backend/Pmcs.Modules.Reporting/Migrations/ExecutiveProjectStateReportCatalogMigration.cs",
  "src/backend/Pmcs.Modules.Reporting/ReportingWorkerQualificationOptions.cs",
  "src/backend/Pmcs.Modules.Reporting/ReportingOrphanRemediationOptions.cs",
  "src/backend/Pmcs.Modules.Reporting/Services/ReportGenerationWorker.cs",
  "src/backend/Pmcs.Modules.Reporting/Services/ReportOutputOrphanRemediationWorker.cs",
  "src/backend/Pmcs.Modules.Reporting/Services/ReportRunAdvisoryLock.cs",
  "src/backend/Pmcs.Modules.ActionControl/Domain/ManagementAction.cs",
  "src/backend/Pmcs.Modules.Finance/Domain/FinancialRecord.cs",
  "src/backend/Pmcs.Modules.Commercial/Domain/ProjectContract.cs",
  "src/backend/Pmcs.Modules.QualitySafety/Domain/QualitySafetyConfiguration.cs",
  "src/backend/Pmcs.Modules.QualitySafety/Domain/CorrectiveAction.cs",
  "src/backend/Pmcs.Modules.QualityAssurance/QualityAssuranceRuntimeOptions.cs",
  "src/backend/Pmcs.Modules.QualityAssurance/Endpoints/QualityAssuranceEndpoints.cs",
  "src/backend/Pmcs.TestHarness/Program.cs",
  "src/backend/Pmcs.TestHarness/FileVerification.cs",
  "src/backend/Pmcs.TestHarness/SyncVerification.cs",
  "src/backend/Pmcs.TestHarness/ExploratoryVerification.cs",
  "src/backend/Pmcs.TestHarness/ReportingGoldenVerification.cs",
  "src/backend/Pmcs.TestHarness/ReportingPeriodicVerification.cs",
  "src/backend/Pmcs.TestHarness/ReportingExecutiveProjectStateVerification.cs",
  "src/backend/Pmcs.TestHarness/ReportingRecoveryVerification.cs",
  "src/backend/Pmcs.TestHarness/ReportingObjectSecurityVerification.cs",
  "src/backend/Pmcs.TestHarness/ReportingOrphanRemediationVerification.cs",
  "src/backend/Pmcs.TestHarness/ReportingWorkerRevocationVerification.cs",
  "tools/qa/verify-reporting-capacity.sh",
  "tools/qa/verify-reporting-fairness.sh",
  "tools/qa/verify-reporting-observability.sh",
  "tools/qa/verify-reporting-orphan-remediation.sh",
  "tools/qa/verify-reporting-observability.mjs",
  "tools/qa/reporting-alert-receiver.mjs",
  "deploy/observability/otel-collector.yaml",
  "deploy/observability/prometheus.yaml",
  "deploy/observability/reporting-alerts.yaml",
  "deploy/observability/alertmanager.yaml",
  "deploy/observability/README.md",
  "src/web/app/page.tsx",
  "src/web/components/project-landing.tsx",
  "src/web/components/project-location-settings.tsx",
  "src/web/components/project-bootstrap-wizard.tsx",
  "src/web/app/project-bootstraps/page.tsx",
  "src/web/app/portfolio/page.tsx",
  "src/web/app/admin/users/page.tsx",
  "src/web/app/projects/[projectId]/page.tsx",
  "src/web/app/not-found.tsx",
  "src/web/lib/localization.ts",
  "src/web/lib/sync-recovery.ts",
  "src/web/lib/document-upload-queue.ts",
  "src/web/lib/login-experience.ts",
  "src/web/lib/member-profile.ts",
  "src/web/components/member-profile.tsx",
  "src/web/components/login-experience-administration.tsx",
  "src/web/app/profile/page.tsx",
  "src/web/app/admin/login-experience/page.tsx",
  "docs/api/identity-experience-v1.md",
  "docs/checkpoints/v1.1-iam1-candidate.md",
  "release/pmcs-v1.1-iam1-candidate.json",
  "src/web/lib/persian-date.ts",
  "src/web/components/persian-date-input.tsx",
  "src/web/lib/portfolio.ts",
  "src/web/lib/portfolio-view.ts",
  "src/web/public/sw.js",
  "src/web/playwright.config.ts",
  "src/web/e2e/authentication.setup.ts",
  "src/web/e2e/authenticated-shell.spec.ts",
  "src/web/e2e/offline-recovery.spec.ts",
  "src/web/tools/audit-persian-ui.mjs",
  "src/web/tools/audit-persian-calendar.mjs",
  "src/web/tools/release-identity.mjs",
  "docs/adr/0022-release-provenance-and-pilot-gate.md",
  "docs/adr/0023-persian-calendar-user-boundary.md",
  "docs/adr/0024-project-lifecycle-location-and-integrity.md",
  "docs/adr/0029-certified-reporting-core.md",
  "docs/architecture/pmcs-v1.1-reporting-center-phase1.md",
  "docs/api/reporting-v1.md",
  "docs/api/project-setup-and-locations-v1.md",
  "docs/api/project-bootstrap-v1.md",
  "docs/api/shared-documents-v1.md",
  "docs/audits/system-integrity-traceability-2026-09-13.md",
  "docs/audits/pmcs-continuity-audit-2026-09-18.md",
  "docs/checkpoints/foundation-19.md",
  "docs/checkpoints/foundation-19.1.md",
  "docs/checkpoints/foundation-20.md",
  "docs/checkpoints/foundation-22.md",
  "docs/adr/0026-daily-report-lineage-and-personal-work-inbox.md",
  "docs/adr/0027-offline-recovery-and-consistency.md",
  "docs/api/my-work-notifications-v1.md",
  "docs/roadmaps/pmcs-v1-development-and-qualification.md",
  "docs/checkpoints/foundation-23.md",
  "docs/qa/qa-foundation.md",
  "docs/checkpoints/qa-foundation-01.md",
  "docs/checkpoints/qa-foundation-02.md",
  "docs/checkpoints/qa-foundation-03.md",
  "docs/checkpoints/qa-foundation-04.md",
  "docs/checkpoints/qa-foundation-05.md",
  "docs/checkpoints/qa-foundation-06.md",
  "docs/checkpoints/qa-foundation-07.md",
  "docs/checkpoints/pmcs-v1-qualification.md",
  "docs/checkpoints/v1.1-doc1-candidate.md",
  "docs/checkpoints/v1.1-prj1-candidate.md",
  "docs/checkpoints/v1.1-rpt1-readiness.md",
  "docs/checkpoints/v1.1-rpt1-slice-01-candidate.md",
  "docs/checkpoints/v1.1-rpt1-slice-02-candidate.md",
  "docs/checkpoints/v1.1-rpt1-slice-03-candidate.md",
  "docs/checkpoints/v1.1-rpt1-slice-04-candidate.md",
  "docs/checkpoints/v1.1-rpt1-slice-05-candidate.md",
  "docs/checkpoints/v1.1-rpt1-slice-06-ms02-candidate.md",
  "docs/checkpoints/v1.1-rpt1-slice-06-ms03-candidate.md",
  "docs/checkpoints/v1.1-rpt1-slice-06-ms03-c2-candidate.md",
  "docs/checkpoints/v1.1-rpt1-slice-06-ms04-candidate.md",
  "docs/checkpoints/v1.1-rpt1-slice-06-ms05-candidate.md",
  "docs/checkpoints/v1.1-rpt1-slice-07-ms05-candidate.md",
  "docs/checkpoints/v1.1-rpt1-slice-07-ms06-candidate.md",
  "docs/checkpoints/v1.1-rpt1-slice-07-ms07-candidate.md",
  "docs/checkpoints/v1.1-rpt1-slice-07-ms08-candidate.md",
  "docs/checkpoints/v1.1-rpt1-slice-07-ms09-candidate.md",
  "docs/checkpoints/v1.1-rpt1-slice-07-ms10-candidate.md",
  "docs/checkpoints/v1.1-rpt1-slice-07-ms11-candidate.md",
  "docs/checkpoints/v1.1-rpt1-slice-07-ms12-candidate.md",
  "docs/checkpoints/v1.1-rpt1-slice-07-ms13-candidate.md",
  "docs/checkpoints/v1.1-rpt1-slice-07-ms14-candidate.md",
  "docs/architecture/pmcs-v1.1-rpt1-f03-executive-project-state-semantic-contract.md",
  "docs/architecture/pmcs-v1.1-rpt1-f04-progress-curve-semantic-contract.md",
  "docs/architecture/pmcs-v1.1-rpt1-f05-financial-position-semantic-contract.md",
  "docs/security/pmcs-v1.1-reporting-security.md",
  "docs/qa/pmcs-v1.1-rpt1-test-matrix.md",
  "docs/runbooks/reporting-center.md",
  "docs/runbooks/pilot-release.md",
  "tools/checkpoint22-db-verification.sh",
  "tools/checkpoint23-db-verification.sh",
  "tools/qa/reset-database.sh",
  "tools/qa/seed-diagnostics.sh",
  "tools/qa/verify-database.sh",
  "tools/qa/verify-files-database.sh",
  "tools/qa/verify-reporting-recovery.sh",
  "tools/qa/verify-reporting-object-security.sh",
  "tools/qa/verify-reporting-worker-revocation.sh",
  "tools/qa/verify-sync-database.sh",
  "tools/qa/prepare-ui-e2e.mjs",
  "tools/qa/regression-suites.json",
  "tools/qa/regression-runner.mjs",
  "tools/qa/test-report-generator.mjs",
  "tools/qa/run-integration-regression.sh",
  "tools/qa/verify-identity-container.mjs",
  "tools/tests/regression-qualification.test.mjs",
  "tools/tests/v1.1-rpt1-readiness.test.mjs",
  "tools/tests/v1.1-rpt1-runtime-contract.test.mjs",
  "tests/Pmcs.Domain.Tests/Pmcs.Domain.Tests.csproj",
  "tests/Pmcs.Domain.Tests/InfrastructureBoundaryTests.cs",
  "tests/Pmcs.Domain.Tests/OfflineOperationIdentityTests.cs",
  "tests/Pmcs.Domain.Tests/DocumentAssetTests.cs",
  "tests/Pmcs.Domain.Tests/ReportingTests.cs",
  "tests/Pmcs.Domain.Tests/IdentityExperienceTests.cs",
  "ops/backup/postgres-backup.sh",
  "ops/backup/postgres-restore-drill.sh",
  "tools/integration-smoke.sh",
  "tools/audit-system-contracts.mjs",
  "tools/release-smoke.sh",
  "tools/validate-pilot-release.mjs",
  "release/pilot-gates.json",
  "release/pilot-candidate.schema.json",
  "release/pmcs-v1-qualification.json",
  "release/pmcs-v1-baseline.json",
  "release/pmcs-v1.1-doc1-candidate.json",
  "release/pmcs-v1.1-prj1-candidate.json",
];

for (const file of requiredFiles) {
  assert.ok(statSync(join(root, file)).isFile(), `Required file is missing: ${file}`);
}

const allowedProjectReferences = new Map([
  ["Pmcs.BuildingBlocks", []],
  ["Pmcs.Modules.Platform", ["Pmcs.BuildingBlocks"]],
  ["Pmcs.Modules.IdentityAccess", ["Pmcs.BuildingBlocks", "Pmcs.Modules.Documents"]],
  ["Pmcs.Modules.Projects", ["Pmcs.BuildingBlocks"]],
  ["Pmcs.Modules.FieldOperations", ["Pmcs.BuildingBlocks", "Pmcs.Modules.Projects"]],
  ["Pmcs.Modules.Sync", ["Pmcs.BuildingBlocks", "Pmcs.Modules.FieldOperations", "Pmcs.Modules.Projects"]],
  ["Pmcs.Modules.Planning", ["Pmcs.BuildingBlocks", "Pmcs.Modules.FieldOperations", "Pmcs.Modules.Projects"]],
  ["Pmcs.Modules.Evidence", ["Pmcs.BuildingBlocks", "Pmcs.Modules.FieldOperations", "Pmcs.Modules.Projects"]],
  ["Pmcs.Modules.Documents", ["Pmcs.BuildingBlocks"]],
  ["Pmcs.Modules.ActionControl", ["Pmcs.BuildingBlocks", "Pmcs.Modules.FieldOperations", "Pmcs.Modules.IdentityAccess", "Pmcs.Modules.Projects"]],
  ["Pmcs.Modules.Commercial", ["Pmcs.BuildingBlocks", "Pmcs.Modules.Projects"]],
  ["Pmcs.Modules.Finance", ["Pmcs.BuildingBlocks", "Pmcs.Modules.Commercial", "Pmcs.Modules.Projects"]],
  ["Pmcs.Modules.QualitySafety", ["Pmcs.BuildingBlocks", "Pmcs.Modules.Projects"]],
  ["Pmcs.Modules.ProjectIntelligence", ["Pmcs.BuildingBlocks", "Pmcs.Modules.ActionControl", "Pmcs.Modules.Commercial", "Pmcs.Modules.FieldOperations", "Pmcs.Modules.Finance", "Pmcs.Modules.IdentityAccess", "Pmcs.Modules.Projects"]],
  ["Pmcs.Modules.Intelligence", ["Pmcs.BuildingBlocks", "Pmcs.Modules.ActionControl", "Pmcs.Modules.Commercial", "Pmcs.Modules.Finance", "Pmcs.Modules.ProjectIntelligence", "Pmcs.Modules.Projects"]],
  ["Pmcs.Modules.WorkManagement", ["Pmcs.BuildingBlocks", "Pmcs.Modules.ActionControl", "Pmcs.Modules.FieldOperations", "Pmcs.Modules.Projects"]],
  ["Pmcs.Modules.Reporting", ["Pmcs.BuildingBlocks", "Pmcs.Modules.Documents", "Pmcs.Modules.FieldOperations", "Pmcs.Modules.Planning", "Pmcs.Modules.ProjectIntelligence", "Pmcs.Modules.Projects"]],
  ["Pmcs.Modules.QualityAssurance", ["Pmcs.BuildingBlocks"]],
]);

for (const [project, allowed] of allowedProjectReferences) {
  const projectFile = join(root, "src/backend", project, `${project}.csproj`);
  const contents = readFileSync(projectFile, "utf8");
  const references = [...contents.matchAll(/ProjectReference Include="[^\"]*\/(Pmcs\.[^/\"]+)\.csproj"/g)]
    .map((match) => match[1])
    .sort();
  assert.deepEqual(references, [...allowed].sort(), `${project} has an invalid project dependency.`);
}

const moduleFiles = walk(join(root, "src/backend"))
  .filter((file) => file.endsWith(".cs") && file.includes("Pmcs.Modules."));
for (const file of moduleFiles) {
  const contents = readFileSync(file, "utf8");
  const currentModule = file.match(/src\/backend\/(Pmcs\.Modules\.[^/]+)/)?.[1];
  const persistenceImports = [...contents.matchAll(/using (Pmcs\.Modules\.[^.]+)\.Persistence;/g)]
    .map((match) => match[1]);
  assert.ok(
    persistenceImports.every((importedModule) => importedModule === currentModule),
    `Module reaches into another module persistence layer: ${relative(root, file)}`,
  );
}

const migrationFiles = moduleFiles.filter((file) => file.includes("/Migrations/"));
const migrationModulesByProject = new Map();
for (const file of migrationFiles) {
  const source = readFileSync(file, "utf8");
  const moduleName = source.match(/public string ModuleName => "([^"]+)";/)?.[1];
  assert.ok(moduleName, `Migration module identity is missing: ${relative(root, file)}`);
  assert.match(
    moduleName,
    /^[a-z]+(?:-[a-z]+)*$/,
    `Migration module identity is not canonical: ${relative(root, file)}`,
  );
  const project = file.match(/src\/backend\/(Pmcs\.Modules\.[^/]+)/)?.[1];
  const identities = migrationModulesByProject.get(project) ?? new Set();
  identities.add(moduleName);
  migrationModulesByProject.set(project, identities);
}
for (const [project, identities] of migrationModulesByProject) {
  assert.equal(
    identities.size,
    1,
    `${project} uses inconsistent migration module identities: ${[...identities].join(", ")}`,
  );
}

const migrationSchemas = [...new Set(migrationFiles.flatMap((file) =>
  [...readFileSync(file, "utf8").matchAll(/create schema if not exists ([a-z_]+);/g)]
    .map((match) => match[1])))]
  .sort();
const resetScript = readFileSync(join(root, "tools/qa/reset-database.sh"), "utf8");
const resetSchemaBlock = resetScript.match(/schemas=\(\n([\s\S]*?)\n\)/)?.[1];
assert.ok(resetSchemaBlock, "QA reset schema inventory is missing.");
const resetSchemas = resetSchemaBlock
  .split("\n")
  .map((line) => line.trim())
  .filter(Boolean)
  .sort();
assert.deepEqual(
  resetSchemas,
  migrationSchemas,
  "QA reset schema inventory must exactly match the application migration schemas.",
);

const webPackage = JSON.parse(readFileSync(join(root, "src/web/package.json"), "utf8"));
assert.equal(webPackage.engines.node, ">=24 <25");
assert.equal(webPackage.dependencies.next, "16.3.3");
assert.equal(webPackage.devDependencies["@playwright/test"], "1.63.0");
assert.equal(webPackage.scripts["test:e2e"], "playwright test");
assert.match(webPackage.scripts.check, /audit:fa/);
assert.match(webPackage.scripts.check, /audit:calendar/);

const browserE2e = readFileSync(join(root, "src/web/e2e/offline-recovery.spec.ts"), "utf8");
assert.match(browserE2e, /launchPersistentContext/);
assert.match(browserE2e, /offline: true/);
assert.match(browserE2e, /readStoredOperations/);
assert.match(browserE2e, /status === "synced"/);

const e2ePreparation = readFileSync(join(root, "tools/qa/prepare-ui-e2e.mjs"), "utf8");
assert.match(e2ePreparation, /grant_type: "client_credentials"/);
assert.match(e2ePreparation, /reset-password/);
assert.match(e2ePreparation, /requiredActions: \[\]/);

const ciWorkflow = readFileSync(join(root, ".github/workflows/ci.yml"), "utf8");
assert.match(ciWorkflow, /ui-e2e:/);
assert.match(ciWorkflow, /playwright install --with-deps chromium/);
assert.match(ciWorkflow, /node tools\/qa\/prepare-ui-e2e\.mjs/);
assert.match(ciWorkflow, /regression-runner\.mjs run ui-e2e/);
assert.match(ciWorkflow, /regression-runner\.mjs run integration/);
assert.equal(
  [...ciWorkflow.matchAll(/node tools\/qa\/regression-runner\.mjs run ([a-z0-9-]+)/g)]
    .map(match => match[1])
    .sort()
    .join(","),
  "architecture,backend,identity-container,integration,pilot-contract,ui-e2e,web",
  "CI must execute every regression suite exactly once.",
);
assert.match(ciWorkflow, /qualification-report:/);
assert.match(ciWorkflow, /node tools\/qa\/test-report-generator\.mjs/);
assert.match(ciWorkflow, /pmcs-v1-qualification-report/);

const regressionManifest = JSON.parse(
  readFileSync(join(root, "tools/qa/regression-suites.json"), "utf8"),
);
assert.equal(regressionManifest.contractVersion, 1);
assert.equal(regressionManifest.runnerVersion, "pmcs-v1-regression-1");
assert.deepEqual(
  regressionManifest.suites.map(suite => suite.id),
  ["architecture", "backend", "integration", "pilot-contract", "web", "ui-e2e", "identity-container"],
);
assert.ok(regressionManifest.suites.every(suite => suite.commands.length > 0));

const regressionRunner = readFileSync(join(root, "tools/qa/regression-runner.mjs"), "utf8");
const qualificationReporter = readFileSync(join(root, "tools/qa/test-report-generator.mjs"), "utf8");
assert.match(regressionRunner, /pmcs-regression-suite/);
assert.match(regressionRunner, /writeJsonAtomically/);
assert.doesNotMatch(regressionRunner, /JSON\.stringify\(process\.env/);
assert.match(qualificationReporter, /baselineLockEligible/);
assert.match(qualificationReporter, /Missing suite report/);
assert.match(qualificationReporter, /mismatched suite report contract/);

const testHarnessProgram = readFileSync(
  join(root, "src/backend/Pmcs.TestHarness/Program.cs"),
  "utf8",
);
const exploratoryVerification = readFileSync(
  join(root, "src/backend/Pmcs.TestHarness/ExploratoryVerification.cs"),
  "utf8",
);
const seedDiagnostics = readFileSync(join(root, "tools/qa/seed-diagnostics.sh"), "utf8");
const readme = readFileSync(join(root, "README.md"), "utf8");
const qaFoundationStatus = readFileSync(join(root, "docs/qa/qa-foundation.md"), "utf8");
assert.match(testHarnessProgram, /"verify-exploratory" => await VerifyExploratoryAsync\(\)/);
assert.match(exploratoryVerification, /deterministic-persona-explorer/);
assert.match(exploratoryVerification, /role-header-cannot-escalate/);
assert.match(exploratoryVerification, /destructive-route-absent/);
assert.match(exploratoryVerification, /preview-simulation-does-not-persist/);
assert.match(seedDiagnostics, /-- verify-exploratory/);
assert.match(readme, /QA Foundation Slice 6:[^\n]*173\/173/);
assert.match(readme, /PMCS V1 — Qualified \| Final \| Baseline Locked/);
assert.match(readme, /QA Foundation Slice 7:[^\n]*7\/7[^\n]*12\/12/);
assert.doesNotMatch(readme, /QA Foundation Slice 5:[^\n]*در انتظار تأیید/);
assert.match(qaFoundationStatus, /Slice 6[^\n]*173\/173[^\n]*Run 67/);
assert.match(qaFoundationStatus, /Slice 7[^\n]*Run 69[^\n]*7\/7[^\n]*12\/12/);
assert.doesNotMatch(qaFoundationStatus, /هنوز `Qualified`|Candidate آماده Full Regression/);
assert.doesNotMatch(qaFoundationStatus, /Slice 5[^\n]*در حال انجام است/);

const qualificationEvidence = JSON.parse(
  readFileSync(join(root, "release/pmcs-v1-qualification.json"), "utf8"),
);
const lockedBaseline = JSON.parse(
  readFileSync(join(root, "release/pmcs-v1-baseline.json"), "utf8"),
);
assert.equal(qualificationEvidence.status, "qualified");
assert.equal(qualificationEvidence.baselineLockEligible, true);
assert.equal(qualificationEvidence.baselineCommit, "26bf222d44634562ca7f3fc0931f3f8b79ca04a1");
assert.equal(qualificationEvidence.workflowRun.id, "35255343431");
assert.equal(qualificationEvidence.summary.passedSuiteCount, 7);
assert.equal(qualificationEvidence.summary.executedCommandCount, 12);
assert.equal(qualificationEvidence.summary.failureCount, 0);
assert.deepEqual(qualificationEvidence.failures, []);
assert.equal(lockedBaseline.status, "locked");
assert.equal(lockedBaseline.sourceBaselineCommit, qualificationEvidence.baselineCommit);
assert.equal(lockedBaseline.qualificationRunId, qualificationEvidence.workflowRun.id);
assert.equal(lockedBaseline.runtimeChangesAfterSourceBaseline, false);
assert.match(lockedBaseline.qualificationReportArtifactDigest, /^sha256:[0-9a-f]{64}$/u);

for (const file of [
  "src/backend/Pmcs.Modules.ActionControl/Domain/GovernanceRules.cs",
  "src/backend/Pmcs.Modules.Commercial/Domain/SupplyRules.cs",
  "src/backend/Pmcs.Modules.QualitySafety/Domain/QualitySafetyRules.cs",
  "src/backend/Pmcs.Modules.TechnicalOffice/Domain/TechnicalOfficeRules.cs",
]) {
  const officialNumbering = readFileSync(join(root, file), "utf8");
  assert.match(officialNumbering, /PersianDateCode\.FromInstant/,
    `${file} must use the Persian Tehran date in user-visible official numbers.`);
  assert.doesNotMatch(officialNumbering, /:yyyyMMdd/,
    `${file} leaks a Gregorian date through a user-visible official number.`);
}

const projectState = readFileSync(join(root, "src/web/lib/project-state.ts"), "utf8");
assert.match(projectState, /planning[\s\S]*status: "not-configured"/);
assert.match(projectState, /budget[\s\S]*status: "setup-required"/);
assert.match(projectState, /hse[\s\S]*status: "not-enabled"/);

const serviceWorker = readFileSync(join(root, "src/web/public/sw.js"), "utf8");
assert.match(serviceWorker, /url\.pathname\.startsWith\("\/api\/"\)/);

const apiProgram = readFileSync(join(root, "src/backend/Pmcs.Api/Program.cs"), "utf8");
assert.match(apiProgram, /AddJwtBearer/);
assert.match(apiProgram, /Authentication:Authority/);
assert.match(apiProgram, /MapHealthChecks\("\/health\/live"/);
assert.match(apiProgram, /MapHealthChecks\("\/health\/ready"/);
assert.match(apiProgram, /ApiRateLimitPolicies\.InsightGeneration/);
assert.match(apiProgram, /ApiRateLimitPolicies\.IdentityAdministration/);
assert.match(apiProgram, /UseMiddleware<ActorAccessMiddleware>/);
assert.match(apiProgram, /UseMiddleware<ProjectLifecycleMiddleware>/);
assert.match(apiProgram, /MapGet\("\/api\/v1\/release"/);
assert.match(apiProgram, /QualityAssuranceAuthenticationHandler/);
assert.match(apiProgram, /QualityAssuranceRuntimeOptions\.Create/);
assert.doesNotMatch(apiProgram, /UseMiddleware<DevelopmentIdentityMiddleware>/);

const qaEndpoints = readFileSync(
  join(root, "src/backend/Pmcs.Modules.QualityAssurance/Endpoints/QualityAssuranceEndpoints.cs"),
  "utf8",
);
assert.match(qaEndpoints, /MapGroup\("\/api\/qa\/v1"\)/);
assert.match(qaEndpoints, /IProjectPermissionService/);
assert.match(qaEndpoints, /IAuditTrail/);
assert.doesNotMatch(qaEndpoints, /DbContext|\.Persistence|Map(?:Post|Put|Patch|Delete)\(/);

const qaRuntime = readFileSync(
  join(root, "src/backend/Pmcs.Modules.QualityAssurance/QualityAssuranceRuntimeOptions.cs"),
  "utf8",
);
assert.match(qaRuntime, /DatabasePrefix = "pmcs_qa_"/);
assert.match(qaRuntime, /Development or QA/);

const evidenceEndpoints = readFileSync(
  join(root, "src/backend/Pmcs.Modules.Evidence/Endpoints/EvidenceEndpoints.cs"),
  "utf8",
);
assert.match(evidenceEndpoints, /EvidenceContentPolicy\.MatchesSignature/);
assert.match(evidenceEndpoints, /EvidenceDownloaded/);
assert.match(evidenceEndpoints, /evidence\.storage_integrity\.failed/);

const releaseIdentity = readFileSync(
  join(root, "src/backend/Pmcs.Api/Infrastructure/ReleaseIdentity.cs"),
  "utf8",
);
assert.match(releaseIdentity, /PMCS\.ReleaseCommit/);
assert.match(releaseIdentity, /full lowercase 40-character/);

const productionConfiguration = readFileSync(
  join(root, "src/backend/Pmcs.Api/Infrastructure/ProductionConfigurationValidator.cs"),
  "utf8",
);
assert.match(productionConfiguration, /PMCS_DEV_IDENTITY_ENABLED/);
assert.match(productionConfiguration, /PMCS_QA_GATEWAY_ENABLED/);
assert.match(productionConfiguration, /ObjectStorage:CreateBucketIfMissing/);
assert.match(productionConfiguration, /IdentityProvisioning:ClientSecret/);
assert.match(productionConfiguration, /ValidateForProduction/);
assert.match(productionConfiguration, /SslMode\.VerifyFull/);
assert.match(productionConfiguration, /RequireHttps\(configuration, "ObjectStorage:ServiceUrl"\)/);
assert.match(productionConfiguration, /AllowedHosts must contain explicit host names without wildcards/);

const webBuild = readFileSync(join(root, "src/web/tools/build.mjs"), "utf8");
assert.match(webBuild, /writeReleaseIdentity/);

const identityEndpoints = readFileSync(
  join(root, "src/backend/Pmcs.Modules.IdentityAccess/Endpoints/IdentityEndpoints.cs"),
  "utf8",
);
assert.match(identityEndpoints, /tenant\.last_administrator\.required/);
assert.match(identityEndpoints, /user\.self_lockout\.denied/);

const identityWorker = readFileSync(
  join(root, "src/backend/Pmcs.Modules.IdentityAccess/Services/IdentityAdministrationWorker.cs"),
  "utf8",
);
assert.match(identityWorker, /IdentityProviderOperationType\.DisableAndLogout/);
assert.match(identityWorker, /IdentityProviderOperationType\.Delete/);
assert.match(identityWorker, /BindProviderUser/);
assert.match(identityWorker, /UserAccount\.Create/);
assert.match(identityWorker, /userStatus == UserAccountStatus\.Active/);

const userAccount = readFileSync(
  join(root, "src/backend/Pmcs.Modules.IdentityAccess/Domain/UserAccount.cs"),
  "utf8",
);
assert.match(userAccount, /AccessValidAfter/);

const actorAccess = readFileSync(
  join(root, "src/backend/Pmcs.Modules.IdentityAccess/Services/ActorAccessValidator.cs"),
  "utf8",
);
assert.match(actorAccess, /tokenIssuedAt\.ToUnixTimeSeconds\(\) > accessValidAfter\.ToUnixTimeSeconds\(\)/);

const planningCalculation = readFileSync(
  join(root, "src/backend/Pmcs.Modules.Planning/Domain/ProgressLedgerCalculation.cs"),
  "utf8",
);
assert.match(planningCalculation, /decimal\? OfficialOverallPhysicalPercent/);
assert.match(planningCalculation, /decimal\? ScheduleVariancePercent/);
const planningEndpoint = readFileSync(
  join(root, "src/backend/Pmcs.Modules.Planning/Endpoints/PlanningEndpoints.cs"),
  "utf8",
);
assert.doesNotMatch(planningEndpoint, /Pmcs\.Modules\.FieldOperations\.Persistence/);

const syncGateway = readFileSync(
  join(root, "src/backend/Pmcs.Modules.Sync/Endpoints/SyncGatewayEndpoints.cs"),
  "utf8",
);
assert.match(syncGateway, /IOfflineFieldOperationHandler/);
assert.doesNotMatch(syncGateway, /Pmcs\.Modules\.FieldOperations\.Persistence/);
assert.match(syncGateway, /OfflineAuthorizationLease/);
assert.match(syncGateway, /CheckpointOffer/);
assert.match(syncGateway, /WriteAuditAsync/);
assert.match(syncGateway, /RecordOperationReceiptAsync/);
assert.match(syncGateway, /recentRejectedCount/);
assert.match(syncGateway, /excluded\.code = 'sync\.operation\.reused'/);
assert.match(syncGateway, /operationCorrelationId/);
assert.doesNotMatch(
  readFileSync(join(root, "src/backend/Pmcs.Modules.Sync/Persistence/SyncPersistenceRecords.cs"), "utf8"),
  /class SyncOperationReceipt[\s\S]*?PayloadJson/,
);

const projectLifecycle = readFileSync(
  join(root, "src/backend/Pmcs.Api/Infrastructure/ProjectLifecycleMiddleware.cs"),
  "utf8",
);
assert.match(projectLifecycle, /profile\.Status != ProjectStatus\.Active/);
assert.match(projectLifecycle, /project\.not_operational/);

const directFacts = readFileSync(
  join(root, "src/backend/Pmcs.Modules.FieldOperations/Endpoints/DailyReportEndpoints.cs"),
  "utf8",
);
const offlineFacts = readFileSync(
  join(root, "src/backend/Pmcs.Modules.FieldOperations/Endpoints/SyncEndpoints.cs"),
  "utf8",
);
assert.ok(offlineFacts.includes('$"sync:{context.UserId:N}:'));
assert.match(offlineFacts, /RequestHash\.Create\(context\.DeviceId\)/);
for (const source of [directFacts, offlineFacts]) {
  assert.match(source, /project\.location\.required/);
  assert.match(source, /projectLocationDirectory\.FindActiveAsync/);
}

const fieldOperationsMapping = readFileSync(
  join(root, "src/backend/Pmcs.Modules.FieldOperations/Persistence/FieldOperationsDbContext.cs"),
  "utf8",
);
const dailyFactMapping = fieldOperationsMapping.slice(
  fieldOperationsMapping.indexOf("modelBuilder.Entity<DailyReportFact>"),
);
assert.match(dailyFactMapping, /Property\(x => x\.LocationId\)\.HasColumnName\("location_id"\)/);

const projectLocation = readFileSync(
  join(root, "src/backend/Pmcs.Modules.Projects/Domain/ProjectLocation.cs"),
  "utf8",
);
assert.match(projectLocation, /isRoot != \(parentLocationId is null\)/);
assert.match(projectLocation, /project\.location\.root\.retire\.denied/);

const projectStateCalculation = readFileSync(
  join(root, "src/backend/Pmcs.Modules.ProjectIntelligence/Domain/ProjectStateCalculation.cs"),
  "utf8",
);
assert.match(projectStateCalculation, /fact\.LocationId/);

const projectStateMapping = readFileSync(
  join(root, "src/backend/Pmcs.Modules.ProjectIntelligence/Persistence/ProjectIntelligenceDbContext.cs"),
  "utf8",
);
assert.match(projectStateMapping, /Property\(x => x\.LocationId\)\.HasColumnName\("location_id"\)/);

const idempotencyStore = readFileSync(
  join(root, "src/backend/Pmcs.Modules.Platform/Services/IdempotencyStore.cs"),
  "utf8",
);
assert.match(idempotencyStore, /record\.ExpiresAt <= clock\.UtcNow/);
assert.match(idempotencyStore, /on conflict \(tenant_id, key, operation\) do update/);

const operationStore = readFileSync(join(root, "src/web/lib/operation-store.ts"), "utf8");
assert.match(operationStore, /serverConflict\.revision/);
assert.doesNotMatch(operationStore, /baseRevision:\s*0[,}]/);
assert.match(operationStore, /activeSyncs = new Map<string, Promise<SyncSummary>>/);

const syncClient = readFileSync(join(root, "src/web/lib/sync-client.ts"), "utf8");
assert.match(syncClient, /activeHandshakes = new Map<string, Promise<LocalSyncManifest>>/);
const syncRecovery = readFileSync(join(root, "src/web/lib/sync-recovery.ts"), "utf8");
assert.match(syncRecovery, /retryScheduleMs = \[5_000, 15_000, 45_000, 120_000, 300_000\]/);
assert.match(syncRecovery, /readServerSyncDiagnostics/);
assert.match(syncRecovery, /evaluateSyncConsistency/);

const portfolioEndpoint = readFileSync(
  join(root, "src/backend/Pmcs.Modules.ProjectIntelligence/Endpoints/PortfolioCommandCenterEndpoints.cs"),
  "utf8",
);
assert.match(portfolioEndpoint, /"portfolio\.read"/);
assert.doesNotMatch(portfolioEndpoint, /OverallHealth|CompositeHealth|HealthScore/);

const portfolioCalculation = readFileSync(
  join(root, "src/backend/Pmcs.Modules.ProjectIntelligence/Domain/PortfolioOverviewCalculation.cs"),
  "utf8",
);
assert.match(portfolioCalculation, /GroupBy\(item => item\.CurrencyCode/);

console.log(`Repository validation passed (${moduleFiles.length} C# module files checked).`);

function walk(directory) {
  return readdirSync(directory).flatMap((entry) => {
    const path = join(directory, entry);
    return statSync(path).isDirectory() ? walk(path) : [path];
  });
}

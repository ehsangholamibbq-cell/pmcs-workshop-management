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
  "src/backend/Pmcs.Api/Infrastructure/ReleaseIdentity.cs",
  "src/backend/Pmcs.Api/Infrastructure/ActorAccessMiddleware.cs",
  "src/backend/Pmcs.Api/Infrastructure/ProjectLifecycleMiddleware.cs",
  "src/backend/Pmcs.BuildingBlocks/Domain/PersianDateCode.cs",
  "src/backend/Pmcs.Modules.IdentityAccess/Domain/UserInvitation.cs",
  "src/backend/Pmcs.Modules.IdentityAccess/Services/IdentityAdministrationWorker.cs",
  "src/backend/Pmcs.Modules.IdentityAccess/Services/KeycloakIdentityProviderAdministration.cs",
  "src/backend/Pmcs.Modules.FieldOperations/Endpoints/SyncEndpoints.cs",
  "src/backend/Pmcs.Modules.FieldOperations/Migrations/FieldOperationsLocationLinkMigration.cs",
  "src/backend/Pmcs.Modules.Platform/Migrations/PlatformIdempotencyRetentionMigration.cs",
  "src/backend/Pmcs.Modules.Platform/Services/IdempotencyRetentionWorker.cs",
  "src/backend/Pmcs.Modules.Projects/Domain/ProjectLocation.cs",
  "src/backend/Pmcs.Modules.Projects/Migrations/ProjectActivationMetadataMigration.cs",
  "src/backend/Pmcs.Modules.Projects/Migrations/ProjectLocationMigration.cs",
  "src/backend/Pmcs.Modules.Sync/Endpoints/SyncGatewayEndpoints.cs",
  "src/backend/Pmcs.Modules.Sync/Migrations/SyncRecoveryDiagnosticsMigration.cs",
  "src/backend/Pmcs.Modules.Planning/Domain/MeasurementItem.cs",
  "src/backend/Pmcs.Modules.Planning/Domain/ProgressLedgerCalculation.cs",
  "src/backend/Pmcs.Modules.Planning/Endpoints/PlanningEndpoints.cs",
  "src/backend/Pmcs.Modules.ProjectIntelligence/Domain/ProjectStateCalculation.cs",
  "src/backend/Pmcs.Modules.ProjectIntelligence/Domain/PortfolioOverviewCalculation.cs",
  "src/backend/Pmcs.Modules.ProjectIntelligence/Endpoints/PortfolioCommandCenterEndpoints.cs",
  "src/backend/Pmcs.Modules.ProjectIntelligence/Migrations/ProjectStateLocationLinkMigration.cs",
  "src/backend/Pmcs.Modules.Intelligence/Domain/AdvisoryInsight.cs",
  "src/backend/Pmcs.Modules.Intelligence/Services/PermissionAwareContextAssembler.cs",
  "src/backend/Pmcs.Modules.Evidence/Domain/EvidenceFile.cs",
  "src/backend/Pmcs.Modules.ActionControl/Domain/ManagementAction.cs",
  "src/backend/Pmcs.Modules.Finance/Domain/FinancialRecord.cs",
  "src/backend/Pmcs.Modules.Commercial/Domain/ProjectContract.cs",
  "src/backend/Pmcs.Modules.QualitySafety/Domain/QualitySafetyConfiguration.cs",
  "src/backend/Pmcs.Modules.QualitySafety/Domain/CorrectiveAction.cs",
  "src/backend/Pmcs.Modules.QualityAssurance/QualityAssuranceRuntimeOptions.cs",
  "src/backend/Pmcs.Modules.QualityAssurance/Endpoints/QualityAssuranceEndpoints.cs",
  "src/backend/Pmcs.TestHarness/Program.cs",
  "src/web/app/page.tsx",
  "src/web/components/project-landing.tsx",
  "src/web/components/project-location-settings.tsx",
  "src/web/app/portfolio/page.tsx",
  "src/web/app/admin/users/page.tsx",
  "src/web/app/projects/[projectId]/page.tsx",
  "src/web/app/not-found.tsx",
  "src/web/lib/localization.ts",
  "src/web/lib/sync-recovery.ts",
  "src/web/lib/persian-date.ts",
  "src/web/components/persian-date-input.tsx",
  "src/web/lib/portfolio.ts",
  "src/web/lib/portfolio-view.ts",
  "src/web/public/sw.js",
  "src/web/tools/audit-persian-ui.mjs",
  "src/web/tools/audit-persian-calendar.mjs",
  "src/web/tools/release-identity.mjs",
  "docs/adr/0022-release-provenance-and-pilot-gate.md",
  "docs/adr/0023-persian-calendar-user-boundary.md",
  "docs/adr/0024-project-lifecycle-location-and-integrity.md",
  "docs/api/project-setup-and-locations-v1.md",
  "docs/audits/system-integrity-traceability-2026-09-13.md",
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
  "docs/runbooks/pilot-release.md",
  "tools/checkpoint22-db-verification.sh",
  "tools/checkpoint23-db-verification.sh",
  "tools/qa/reset-database.sh",
  "tools/qa/seed-diagnostics.sh",
  "tests/Pmcs.Domain.Tests/Pmcs.Domain.Tests.csproj",
  "tests/Pmcs.Domain.Tests/InfrastructureBoundaryTests.cs",
  "ops/backup/postgres-backup.sh",
  "ops/backup/postgres-restore-drill.sh",
  "tools/integration-smoke.sh",
  "tools/audit-system-contracts.mjs",
  "tools/release-smoke.sh",
  "tools/validate-pilot-release.mjs",
  "release/pilot-gates.json",
  "release/pilot-candidate.schema.json",
];

for (const file of requiredFiles) {
  assert.ok(statSync(join(root, file)).isFile(), `Required file is missing: ${file}`);
}

const allowedProjectReferences = new Map([
  ["Pmcs.BuildingBlocks", []],
  ["Pmcs.Modules.Platform", ["Pmcs.BuildingBlocks"]],
  ["Pmcs.Modules.IdentityAccess", ["Pmcs.BuildingBlocks"]],
  ["Pmcs.Modules.Projects", ["Pmcs.BuildingBlocks"]],
  ["Pmcs.Modules.FieldOperations", ["Pmcs.BuildingBlocks", "Pmcs.Modules.Projects"]],
  ["Pmcs.Modules.Sync", ["Pmcs.BuildingBlocks", "Pmcs.Modules.FieldOperations", "Pmcs.Modules.Projects"]],
  ["Pmcs.Modules.Planning", ["Pmcs.BuildingBlocks", "Pmcs.Modules.FieldOperations", "Pmcs.Modules.Projects"]],
  ["Pmcs.Modules.Evidence", ["Pmcs.BuildingBlocks", "Pmcs.Modules.FieldOperations", "Pmcs.Modules.Projects"]],
  ["Pmcs.Modules.ActionControl", ["Pmcs.BuildingBlocks", "Pmcs.Modules.FieldOperations", "Pmcs.Modules.IdentityAccess", "Pmcs.Modules.Projects"]],
  ["Pmcs.Modules.Commercial", ["Pmcs.BuildingBlocks", "Pmcs.Modules.Projects"]],
  ["Pmcs.Modules.Finance", ["Pmcs.BuildingBlocks", "Pmcs.Modules.Commercial", "Pmcs.Modules.Projects"]],
  ["Pmcs.Modules.QualitySafety", ["Pmcs.BuildingBlocks", "Pmcs.Modules.Projects"]],
  ["Pmcs.Modules.ProjectIntelligence", ["Pmcs.BuildingBlocks", "Pmcs.Modules.ActionControl", "Pmcs.Modules.Commercial", "Pmcs.Modules.FieldOperations", "Pmcs.Modules.Finance", "Pmcs.Modules.IdentityAccess", "Pmcs.Modules.Projects"]],
  ["Pmcs.Modules.Intelligence", ["Pmcs.BuildingBlocks", "Pmcs.Modules.ActionControl", "Pmcs.Modules.Commercial", "Pmcs.Modules.Finance", "Pmcs.Modules.ProjectIntelligence", "Pmcs.Modules.Projects"]],
  ["Pmcs.Modules.WorkManagement", ["Pmcs.BuildingBlocks", "Pmcs.Modules.ActionControl", "Pmcs.Modules.FieldOperations", "Pmcs.Modules.Projects"]],
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

const webPackage = JSON.parse(readFileSync(join(root, "src/web/package.json"), "utf8"));
assert.equal(webPackage.engines.node, ">=24 <25");
assert.equal(webPackage.dependencies.next, "16.3.3");
assert.match(webPackage.scripts.check, /audit:fa/);
assert.match(webPackage.scripts.check, /audit:calendar/);

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

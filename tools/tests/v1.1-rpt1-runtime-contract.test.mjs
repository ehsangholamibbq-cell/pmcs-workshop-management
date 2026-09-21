import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { existsSync, readFileSync } from "node:fs";
import test from "node:test";

const moduleRoot = "src/backend/Pmcs.Modules.Reporting";

test("RPT1 runtime slice registers an independent certified reporting module", () => {
  for (const path of [
    `${moduleRoot}/ReportingModule.cs`,
    `${moduleRoot}/Domain/ReportRun.cs`,
    `${moduleRoot}/Domain/ReportSnapshot.cs`,
    `${moduleRoot}/Domain/ReportDefinitionRuntimePolicy.cs`,
    `${moduleRoot}/Contracts/IReportingReadService.cs`,
    `${moduleRoot}/Endpoints/ReportingEndpoints.cs`,
    `${moduleRoot}/Migrations/ReportingInitialMigration.cs`,
    `${moduleRoot}/Migrations/ProjectPeriodicReportCatalogMigration.cs`,
    `${moduleRoot}/Migrations/ExecutiveProjectStateReportCatalogMigration.cs`,
    `${moduleRoot}/Migrations/ProjectProgressReportCatalogMigration.cs`,
    `${moduleRoot}/Migrations/ProjectFinancialPositionReportCatalogMigration.cs`,
    `${moduleRoot}/Services/ReportGenerationWorker.cs`,
    `${moduleRoot}/Services/ReportingWorkerHealthCheck.cs`,
    `${moduleRoot}/Services/ReportingWorkerTelemetry.cs`,
    `${moduleRoot}/ReportingExecutionOptions.cs`,
    `${moduleRoot}/ReportingOrphanRemediationOptions.cs`,
    `${moduleRoot}/Services/ReportOutputOrphanRemediationWorker.cs`,
    `${moduleRoot}/Services/ReportRunAdvisoryLock.cs`,
    `${moduleRoot}/Rendering/CertifiedPdfRuntimeContract.cs`,
    "src/backend/Pmcs.TestHarness/ReportingCancellationVerification.cs",
    "src/backend/Pmcs.TestHarness/ReportingGoldenVerification.cs",
    "src/backend/Pmcs.TestHarness/ReportingPdfGoldenVerification.cs",
    "src/backend/Pmcs.TestHarness/ReportingPeriodicVerification.cs",
    "src/backend/Pmcs.TestHarness/ReportingExecutiveProjectStateVerification.cs",
    "src/backend/Pmcs.TestHarness/ReportingProjectProgressVerification.cs",
    "src/backend/Pmcs.TestHarness/ReportingProjectFinancialPositionVerification.cs",
    "src/backend/Pmcs.TestHarness/ReportingObjectSecurityVerification.cs",
    "src/backend/Pmcs.TestHarness/ReportingOrphanRemediationVerification.cs",
    "src/backend/Pmcs.TestHarness/ReportingRecoveryVerification.cs",
    "src/backend/Pmcs.TestHarness/ReportingWorkerRevocationVerification.cs",
    "tools/qa/verify-reporting-object-security.sh",
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
    "tools/qa/verify-reporting-recovery.sh",
    "tools/qa/verify-reporting-security.sh",
    "tools/qa/verify-reporting-worker-revocation.sh",
    "assets/reporting/fonts/DejaVuSans.ttf",
    "assets/reporting/fonts/DejaVuSans-Bold.ttf",
    "assets/reporting/fonts/LICENSE.txt",
    "assets/reporting/fonts/manifest.json",
  ]) assert.equal(existsSync(path), true, `Missing ${path}`);

  const module = read(`${moduleRoot}/ReportingModule.cs`);
  assert.match(module, /"reporting\.center"/u);
  for (const permission of [
    "reporting.catalog.read",
    "reporting.run.create",
    "reporting.output.download",
    "reporting.template.publish",
  ]) assert.match(module, new RegExp(permission.replaceAll(".", "\\."), "u"));
  assert.match(module, /"reporting\.phase1"/u);
  assert.match(module, /ToolAccessMode\.ReadOnly/u);
  assert.match(module, /IReportingReadService/u);
  assert.doesNotMatch(module, /Intelligence|OpenAI|LLM/u);
  const readService = read(`${moduleRoot}/Services/ReportingReadService.cs`);
  assert.match(readService, /runtime\.Phase1Enabled/u);
  assert.match(readService, /runtime\.OutputAccessEnabled/u);
});

test("migrations 42 through 47 own reporting schema and the connected F02/F03/F04/F05 catalogs", () => {
  const migration = read(`${moduleRoot}/Migrations/ReportingInitialMigration.cs`);
  const verificationMigration = read(`${moduleRoot}/Migrations/ReportingVerificationCodeIndexMigration.cs`);
  const periodicMigration = read(`${moduleRoot}/Migrations/ProjectPeriodicReportCatalogMigration.cs`);
  const executiveMigration = read(
    `${moduleRoot}/Migrations/ExecutiveProjectStateReportCatalogMigration.cs`,
  );
  const progressMigration = read(
    `${moduleRoot}/Migrations/ProjectProgressReportCatalogMigration.cs`,
  );
  const financialMigration = read(
    `${moduleRoot}/Migrations/ProjectFinancialPositionReportCatalogMigration.cs`,
  );
  const dbContext = read(`${moduleRoot}/Persistence/ReportingDbContext.cs`);
  assert.match(migration, /public long Order => 1200/u);
  assert.match(migration, /public string Version => "20260918-001"/u);
  assert.match(migration, /create schema if not exists reporting/u);
  for (const table of [
    "report_definitions",
    "report_template_versions",
    "report_runs",
    "report_snapshots",
    "report_outputs",
  ]) assert.match(migration, new RegExp(`reporting\\.${table}`, "u"));
  assert.match(migration, /daily-report-certified/u);
  assert.match(migration, /'1\.0\.0'/u);
  assert.match(verificationMigration, /public long Order => 1201/u);
  assert.match(verificationMigration, /public string Version => "20260918-002"/u);
  assert.match(verificationMigration, /drop constraint if exists report_outputs_verification_code_key/u);
  assert.match(periodicMigration, /public long Order => 1202/u);
  assert.match(periodicMigration, /public string Version => "20260920-003"/u);
  assert.match(periodicMigration, /project-periodic-certified/u);
  assert.match(periodicMigration, /pmcs\.reporting\.project-periodic\.parameters\/v1/u);
  assert.match(periodicMigration, /pinned_project_profile jsonb/u);
  assert.match(executiveMigration, /public long Order => 1203/u);
  assert.match(executiveMigration, /public string Version => "20260920-004"/u);
  assert.match(executiveMigration, /executive-project-state-certified/u);
  assert.match(executiveMigration, /pmcs\.reporting\.executive-project-state\.parameters\/v1/u);
  assert.match(executiveMigration, /pmcs\.reporting\.executive-project-state\.renderer\/v1/u);
  assert.match(executiveMigration, /project-state\.read/u);
  assert.match(progressMigration, /public long Order => 1204/u);
  assert.match(progressMigration, /public string Version => "20260920-005"/u);
  assert.match(progressMigration, /project-progress-certified/u);
  assert.match(progressMigration, /pmcs\.reporting\.project-progress\.parameters\/v1/u);
  assert.match(progressMigration, /pmcs\.reporting\.project-progress\.renderer\/v1/u);
  assert.match(progressMigration, /"planning\.progress\.read"/u);
  assert.match(progressMigration, /"planning\.baselines\.read"/u);
  assert.match(progressMigration, /"planning\.milestones\.read"/u);
  assert.match(progressMigration, /'Landscape'/u);
  assert.match(financialMigration, /public long Order => 1205/u);
  assert.match(financialMigration, /public string Version => "20260921-006"/u);
  assert.match(financialMigration, /project-financial-position-certified/u);
  assert.match(financialMigration, /pmcs\.reporting\.project-financial-position\.parameters\/v1/u);
  assert.match(financialMigration, /pmcs\.reporting\.project-financial-position\.renderer\/v1/u);
  assert.match(financialMigration, /'Confidential'/u);
  assert.match(financialMigration, /"financial-state\.read"/u);
  assert.match(financialMigration, /"finance\.records\.read"/u);
  assert.match(financialMigration, /"finance\.obligations\.read"/u);
  assert.match(financialMigration, /"budget\.baselines\.read"/u);
  assert.match(financialMigration, /'Landscape'/u);
  assert.match(dbContext, /HasIndex\(item => item\.VerificationCode\);/u);
  assert.doesNotMatch(dbContext, /HasIndex\(item => item\.VerificationCode\)\.IsUnique/u);
  assert.match(read("tools/qa/verify-database.sh"), /canonical migration ledger size[\s\S]*?"47"/u);
  assert.match(read("tools/qa/reset-database.sh"), /\n  reporting\n/u);
});

test("reporting reads daily report lineage only through its application contract", () => {
  const contract = read("src/backend/Pmcs.Modules.FieldOperations/Contracts/IDailyReportReportingSource.cs");
  const source = read("src/backend/Pmcs.Modules.FieldOperations/Services/DailyReportReportingSource.cs");
  const worker = read(`${moduleRoot}/Services/ReportGenerationWorker.cs`);
  assert.match(contract, /LoadChainAsync/u);
  assert.match(contract, /CopiedFromFactId/u);
  assert.match(contract, /IsOfficialAt/u);
  assert.match(source, /DailyReportStatus\.Approved/u);
  assert.match(source, /DailyReportStatus\.Superseded/u);
  assert.match(source, /MapForCutoff/u);
  assert.match(source, /hasFutureSupersession/u);
  assert.match(source, /checked\(report\.Revision - 1\)/u);
  assert.match(worker, /IDailyReportReportingSource/u);
  assert.doesNotMatch(worker, /FieldOperations\.Persistence|field_operations\./u);
  assert.match(worker, /for update(?: of candidate)? skip locked/u);
  assert.match(worker, /PreviewProjectPermissionsAsync/u);
});

test("generic Documents routes fail closed for ReportOutput", () => {
  const endpoints = read("src/backend/Pmcs.Modules.Documents/Endpoints/DocumentEndpoints.cs");
  assert.match(endpoints, /asset\.OwnerType != DocumentOwnerType\.ReportOutput/u);
  assert.match(endpoints, /asset\.OwnerType == DocumentOwnerType\.ReportOutput[\s\S]*?return false/u);
  assert.match(endpoints, /documents\.report_output\.generated_only/u);
});

test("generated outputs use the Documents owner contract and certified renderers", () => {
  const publisherContract = read("src/backend/Pmcs.Modules.Documents/Contracts/IGeneratedDocumentPublisher.cs");
  const publisher = read("src/backend/Pmcs.Modules.Documents/Services/GeneratedDocumentPublisher.cs");
  const worker = read(`${moduleRoot}/Services/ReportGenerationWorker.cs`);
  const pdf = read(`${moduleRoot}/Rendering/DailyReportPdfRenderer.cs`);
  const pdfRuntime = read(`${moduleRoot}/Rendering/CertifiedPdfRuntime.cs`);
  const xlsx = read(`${moduleRoot}/Rendering/DailyReportXlsxRenderer.cs`);
  const endpoints = read(`${moduleRoot}/Endpoints/ReportingEndpoints.cs`);
  const pdfContract = read(`${moduleRoot}/Rendering/CertifiedPdfRuntimeContract.cs`);
  const packages = read("Directory.Packages.props");
  const dockerfile = read("deploy/docker/api.Dockerfile");
  const settings = JSON.parse(read("src/backend/Pmcs.Api/appsettings.json"));
  const fontManifest = JSON.parse(read("assets/reporting/fonts/manifest.json"));
  assert.match(publisherContract, /PublishReportOutputAsync/u);
  assert.doesNotMatch(publisherContract, /ObjectKey|Presign|UploadSession/u);
  assert.match(publisher, /DocumentOwnerType\.ReportOutput/u);
  assert.match(publisher, /MatchesSignature/u);
  assert.match(publisher, /ReadAsync\(objectKey/u);
  assert.match(publisher, /SequenceEqual\(request\.Bytes\)/u);
  assert.match(worker, /ReportRendererRegistry/u);
  assert.match(worker, /rendered\.Add[\s\S]*?foreach \(var item in rendered\)[\s\S]*?PublishReportOutputAsync/u);
  assert.match(worker, /reporting\.report\.completed\.v1/u);
  assert.match(pdf, /ContentFromRightToLeft/u);
  assert.match(pdf, /GenerateImages[\s\S]*QualificationRasterDpi/u);
  assert.match(pdf, /CertifiedPdfRuntime\.EnsureConfigured/u);
  assert.match(pdfRuntime, /SHA256\.HashData[\s\S]*FontManager\.RegisterFont\(/u);
  assert.match(pdfRuntime, /reporting\.renderer\.configuration_unpinned/u);
  assert.match(pdfRuntime, /reporting\.renderer\.font_integrity_failed/u);
  assert.match(pdfRuntime, /reporting\.renderer\.license_unapproved/u);
  assert.match(packages, /PackageVersion Include="QuestPDF" Version="2026\.8\.0"/u);
  assert.match(packages, /PackageVersion Include="PdfPig" Version="0\.1\.16"/u);
  assert.match(pdfContract, /LicenseDecision = "Community"/u);
  assert.match(pdfContract, /QualificationColdRenderBudgetMilliseconds = 5_000/u);
  assert.match(pdfContract, /QualificationWarmRenderBudgetMilliseconds = 2_500/u);
  assert.match(
    dockerfile,
    /dotnet\/sdk:10\.0@sha256:2fa828c68761b1b8c23d7662dc134421b9d3b59fe1425fdbc80804e390cdb24d/u,
  );
  assert.match(
    dockerfile,
    /dotnet\/aspnet:10\.0@sha256:6a94333d37514e385650a3c81a55e5350b67253dbe136e9cf17e499c35606a8c/u,
  );
  assert.doesNotMatch(dockerfile, /apt-get|fonts-dejavu-core/u);
  assert.equal(settings.ReportingCenter.PdfLicense, "Unconfigured");
  assert.equal(settings.ReportingCenter.Phase1Enabled, false);
  assert.equal(settings.ReportingCenter.OutputAccessEnabled, false);
  assert.equal(settings.ReportingCenter.WorkerEnabled, false);
  assert.equal(
    settings.ReportingCenter.PdfRendererImageDigest,
    "sha256:6a94333d37514e385650a3c81a55e5350b67253dbe136e9cf17e499c35606a8c",
  );
  for (const font of fontManifest.files) {
    const digest = createHash("sha256")
      .update(readFileSync(`assets/reporting/fonts/${font.path}`))
      .digest("hex");
    assert.equal(digest, font.sha256, `${font.path} digest drifted`);
  }
  assert.match(xlsx, /rightToLeft/u);
  assert.match(xlsx, /CompressionLevel\.NoCompression/u);
  assert.doesNotMatch(xlsx, /<f>|WriteStartElement\("f"/u);
  assert.match(endpoints, /outputs\/\{outputId:guid\}\/content/u);
  assert.match(endpoints, /outputs\/\{outputId:guid\}\/verify/u);
  assert.match(endpoints, /DocumentOwnerType\.ReportOutput/u);
  assert.match(endpoints, /reporting\.output\.integrity_failed/u);
  assert.match(endpoints, /CertifiedReportOutputIntegrityFailed/u);
  assert.match(
    endpoints,
    /new CreateRunIdentity\([\s\S]*?request\.AsOfUtc\?\.ToUniversalTime\(\),[\s\S]*?formats,[\s\S]*?parameters\)/u,
  );
});

test("generated report orphan remediation is dry-run first retention-safe and audited", () => {
  const contract = read("src/backend/Pmcs.Modules.Documents/Contracts/IGeneratedDocumentOrphanRemediator.cs");
  const remediator = read("src/backend/Pmcs.Modules.Documents/Services/GeneratedDocumentOrphanRemediator.cs");
  const options = read(`${moduleRoot}/ReportingOrphanRemediationOptions.cs`);
  const worker = read(`${moduleRoot}/Services/ReportOutputOrphanRemediationWorker.cs`);
  const lock = read(`${moduleRoot}/Services/ReportRunAdvisoryLock.cs`);
  const endpoints = read(`${moduleRoot}/Endpoints/ReportingEndpoints.cs`);
  const connected = read("tools/qa/verify-reporting-orphan-remediation.sh");

  assert.doesNotMatch(contract, /ObjectKey|Presign|UploadSession/u);
  assert.match(contract, /ExpectedRevision/u);
  assert.match(contract, /RetentionActive/u);
  assert.match(contract, /LegalHold/u);
  assert.match(remediator, /for update/u);
  assert.match(remediator, /asset\.MarkDeleted/u);
  assert.match(remediator, /GeneratedReportOrphanRemediated/u);
  assert.doesNotMatch(remediator, /\["objectKey"\]/u);
  assert.match(options, /ReportingOrphanRemediationMode\.Disabled/u);
  assert.match(options, /InventoryOnly/u);
  assert.match(options, /ApplyEligible/u);
  assert.match(options, /OrphanRemediationMinimumAgeHours/u);
  assert.match(worker, /run\.Status != ReportRunStatus\.Failed/u);
  assert.match(worker, /OwnerExistsAsync/u);
  assert.match(worker, /RetentionProtected/u);
  assert.match(worker, /LegalHoldProtected/u);
  assert.match(worker, /ReportRunAdvisoryLock\.AcquireAsync/u);
  assert.match(lock, /pg_advisory_xact_lock/u);
  assert.match(endpoints, /ReportRunAdvisoryLock\.AcquireAsync/u);
  assert.match(connected, /start_api InventoryOnly/u);
  assert.match(connected, /start_api ApplyEligible/u);
  assert.match(connected, /only the expired unheld orphan is remediated/u);
  assert.match(connected, /orphan remediation is idempotent across another sweep/u);
  assert.match(connected, /object-key free/u);
});

test("connected RPT1 qualification covers API, worker, storage and database evidence", () => {
  const harness = read("src/backend/Pmcs.TestHarness/ReportingVerification.cs");
  const golden = read("src/backend/Pmcs.TestHarness/ReportingGoldenVerification.cs");
  const pdfGolden = read("src/backend/Pmcs.TestHarness/ReportingPdfGoldenVerification.cs");
  const seed = read("tools/qa/seed-diagnostics.sh");
  const database = read("tools/qa/verify-database.sh");
  assert.match(harness, /reporting\.xlsx\.create\.idempotent-replay/u);
  assert.match(harness, /reporting\.xlsx\.download\.integrity/u);
  assert.match(harness, /reporting\.xlsx\.verify\.valid/u);
  assert.match(harness, /reporting\.pdf\.license\.fail-closed/u);
  assert.match(harness, /reporting\.pdf\.retry\.bounded/u);
  assert.match(golden, /reporting\.golden\.snapshot\.cutoff-hashes/u);
  assert.match(golden, /reporting\.golden\.before\.semantic-workbook/u);
  assert.match(golden, /reporting\.golden\.after\.semantic-workbook/u);
  assert.match(golden, /reporting\.golden\.xlsx\.typed-safety-and-lineage/u);
  assert.match(golden, /GoldenDraftMarker/u);
  assert.match(pdfGolden, /rpt1-pdf-golden-verification/u);
  assert.match(pdfGolden, /reporting\.pdf\.golden\.stored-bytes-deterministic/u);
  assert.match(pdfGolden, /reporting\.pdf\.golden\.structure-and-text/u);
  assert.match(pdfGolden, /reporting\.pdf\.golden\.end-to-end-budget/u);
  assert.match(seed, /-- verify-reporting-golden/u);
  assert.match(seed, /start_api true Community[\s\S]*?-- verify-reporting-pdf-golden/u);
  assert.match(seed, /ReportingCenter__PdfRegularFontSha256=ae7b7855/u);
  assert.match(seed, /ReportingCenter__PdfBoldFontSha256=5c1247ac/u);
  assert.match(seed, /ReportingCenter__PdfRendererImageDigest=sha256:6a94333d/u);
  const cancellation = read("src/backend/Pmcs.TestHarness/ReportingCancellationVerification.cs");
  assert.match(cancellation, /reporting\.cancel\.accepted-before-rendering/u);
  assert.match(cancellation, /reporting\.cancel\.idempotent-replay/u);
  assert.match(cancellation, /reporting\.cancel\.final-state-rejected/u);
  assert.match(cancellation, /reporting\.cancel\.remains-final-without-output/u);
  const security = read("tools/qa/verify-reporting-security.sh");
  assert.match(security, /anonymous report verification is denied/u);
  assert.match(security, /cross-tenant report verification is denied/u);
  assert.match(security, /generic Documents download hides ReportOutput/u);
  assert.match(security, /suspended membership cannot download/u);
  assert.match(security, /tampered report metadata fails closed/u);
  const recoveryHarness = read("src/backend/Pmcs.TestHarness/ReportingRecoveryVerification.cs");
  const recovery = read("tools/qa/verify-reporting-recovery.sh");
  const revocationHarness = read("src/backend/Pmcs.TestHarness/ReportingWorkerRevocationVerification.cs");
  const revocation = read("tools/qa/verify-reporting-worker-revocation.sh");
  const objectSecurityHarness = read("src/backend/Pmcs.TestHarness/ReportingObjectSecurityVerification.cs");
  const objectSecurity = read("tools/qa/verify-reporting-object-security.sh");
  const capacity = read("tools/qa/verify-reporting-capacity.sh");
  const fairness = read("tools/qa/verify-reporting-fairness.sh");
  const orphanRemediation = read("tools/qa/verify-reporting-orphan-remediation.sh");
  const qualificationOptions = read(`${moduleRoot}/ReportingWorkerQualificationOptions.cs`);
  const worker = read(`${moduleRoot}/Services/ReportGenerationWorker.cs`);
  assert.match(recoveryHarness, /"concurrency-locked"/u);
  assert.match(recoveryHarness, /"crash-after-storage"/u);
  assert.match(recoveryHarness, /reporting\.recovery\.\{fixture\.Code\}\.completed-once/u);
  assert.match(recovery, /AfterSnapshotRowLock/u);
  assert.match(recovery, /second worker skips the locked head run/u);
  assert.match(recovery, /before-storage crash window/u);
  assert.match(recovery, /after-storage crash window/u);
  assert.match(recovery, /stale snapshot-building lease is reclaimed/u);
  assert.match(recovery, /kill -KILL/u);
  assert.match(recovery, /orphan inventory identifies the crash-after-storage document/u);
  assert.match(recovery, /orphan inventory is empty after stable recovery/u);
  assert.match(revocationHarness, /reporting\.worker-revocation\.failed-before-storage/u);
  assert.match(revocationHarness, /reporting\.permission\.revoked/u);
  assert.match(revocation, /BeforeStoragePermissionRecheck/u);
  assert.match(revocation, /revocation during rendering fails before storage/u);
  assert.match(revocation, /worker-time revocation records both denied permissions/u);
  assert.match(worker, /BeforeStoragePermissionRecheck[\s\S]*?RequireProcessingPermissionsAsync[\s\S]*?PublishReportOutputAsync/u);
  assert.match(worker, /RecordRenderingPermissionSnapshot/u);
  assert.match(objectSecurityHarness, /byte-tamper\.verify-fails-closed/u);
  assert.match(objectSecurityHarness, /missing\.verify-fails-closed/u);
  assert.match(objectSecurityHarness, /malformed\.verify-fails-closed/u);
  assert.match(objectSecurityHarness, /finally[\s\S]*?PutObjectAsync/u);
  assert.match(objectSecurity, /exactly four integrity-failure audits/u);
  assert.match(capacity, /prepare-reporting-capacity/u);
  assert.match(capacity, /verify-reporting-capacity/u);
  assert.match(capacity, /twenty healthy runs and one poison run/u);
  assert.match(capacity, /poison audit lineage has two requeues and one terminal failure/u);
  assert.match(capacity, /healthy capacity p95 is below thirty seconds/u);
  assert.match(capacity, /reporting-worker.*Healthy/u);
  assert.match(fairness, /three project-A runs and one project-B run/u);
  assert.match(fairness, /AfterSnapshotRowLock/u);
  assert.match(fairness, /second worker serves project B before project A's second and third runs/u);
  assert.match(fairness, /idle in transaction/u);
  assert.match(fairness, /kill -KILL/u);
  assert.match(fairness, /Reporting health must be Degraded while the aged fair queue is locked/u);
  assert.match(fairness, /oldestQueueAgeSeconds/u);
  assert.match(fairness, /health":"degraded-queue-age/u);
  assert.match(qualificationOptions, /qualification controls require the isolated QA gateway/u);
  assert.match(qualificationOptions, /PMCS_QA_GATEWAY_ENABLED/u);
  assert.match(qualificationOptions, /QualificationPauseSeconds must be between 1 and 60/u);
  assert.match(seed, /ReportingCenter__PdfLicense="\$\{pdf_license\}"/u);
  assert.match(seed, /start_api true Unconfigured/u);
  assert.match(seed, /-- verify-reporting/u);
  assert.match(seed, /start_api false[\s\S]*?-- verify-reporting-cancellation/u);
  assert.match(seed, /verify-reporting-security\.sh/u);
  assert.match(seed, /verify-reporting-object-security\.sh/u);
  assert.match(seed, /verify-reporting-recovery\.sh/u);
  assert.match(seed, /verify-reporting-worker-revocation\.sh/u);
  assert.match(seed, /verify-reporting-capacity\.sh/u);
  assert.match(seed, /verify-reporting-observability\.sh/u);
  assert.match(seed, /verify-reporting-orphan-remediation\.sh/u);
  assert.match(orphanRemediation, /InventoryOnly/u);
  assert.match(orphanRemediation, /ApplyEligible/u);
  assert.match(database, /certified output is a released governed document/u);
  assert.match(database, /certified reporting transactional outbox coverage/u);
  assert.match(database, /queued report cancellation is final and unclaimed/u);
  assert.match(database, /worker concurrency and recovery runs completed with bounded attempts/u);
  assert.match(database, /worker-time permission revocation fails before document publication/u);
  assert.match(database, /generated report orphan inventory is empty after recovery/u);
  assert.match(database, /expired generated report orphan remediation is singular audited and object-key free/u);
  assert.match(database, /object and metadata tamper attempts are audited/u);
  assert.match(database, /capacity runs isolate poison without delaying healthy work/u);
  assert.match(database, /capacity outputs retain singular document and event ownership/u);
});

test("RPT1 operational metrics export scrape and alert delivery remain deployment controlled", () => {
  const composition = read("src/backend/Pmcs.Api/Infrastructure/OperationalMetricsConfiguration.cs");
  const program = read("src/backend/Pmcs.Api/Program.cs");
  const apiProject = read("src/backend/Pmcs.Api/Pmcs.Api.csproj");
  const reportingProject = read(`${moduleRoot}/Pmcs.Modules.Reporting.csproj`);
  const collector = read("deploy/observability/otel-collector.yaml");
  const prometheus = read("deploy/observability/prometheus.yaml");
  const rules = read("deploy/observability/reporting-alerts.yaml");
  const alertmanager = read("deploy/observability/alertmanager.yaml");
  const connected = read("tools/qa/verify-reporting-observability.sh");
  const fairness = read("tools/qa/verify-reporting-fairness.sh");
  const verifier = read("tools/qa/verify-reporting-observability.mjs");
  const receiver = read("tools/qa/reporting-alert-receiver.mjs");
  const seed = read("tools/qa/seed-diagnostics.sh");
  const settings = JSON.parse(read("src/backend/Pmcs.Api/appsettings.json"));

  assert.equal(settings.Observability.OtlpEndpoint, "");
  assert.match(program, /OperationalMetricsConfiguration\.Configure/u);
  assert.match(composition, /Observability:OtlpEndpoint/u);
  assert.match(composition, /return null/u);
  assert.match(composition, /AddMeter\(RequestTelemetryMiddleware\.MeterName, ReportingMeterName\)/u);
  assert.match(composition, /AddOtlpExporter/u);
  assert.match(composition, /without credentials, query or fragment/u);
  assert.match(apiProject, /OpenTelemetry\.Extensions\.Hosting/u);
  assert.match(apiProject, /OpenTelemetry\.Exporter\.OpenTelemetryProtocol/u);
  assert.doesNotMatch(reportingProject, /OpenTelemetry/u);

  assert.match(collector, /otlp:[\s\S]*?grpc:[\s\S]*?127\.0\.0\.1:4317/u);
  assert.match(collector, /prometheus:[\s\S]*?127\.0\.0\.1:9464/u);
  assert.match(collector, /translation_strategy: UnderscoreEscapingWithSuffixes/u);
  assert.match(collector, /without_scope_info: true/u);
  assert.match(prometheus, /job_name: pmcs-reporting/u);
  assert.match(prometheus, /127\.0\.0\.1:9464/u);
  assert.match(prometheus, /reporting-alerts\.yaml/u);
  for (const alert of [
    "PmcsReportingQueueAgeBudgetExceeded",
    "PmcsReportingHeartbeatMissingOrStale",
    "PmcsReportingFailureOrRetryDetected",
  ]) assert.match(rules, new RegExp(`alert: ${alert}`, "u"));
  assert.equal((rules.match(/^\s+- alert:/gmu) ?? []).length, 3);
  assert.doesNotMatch(rules, /tenant_id|project_id|user_id|run_id/u);
  assert.match(alertmanager, /127\.0\.0\.1:19093\/alerts/u);
  assert.match(alertmanager, /send_resolved: true/u);

  assert.match(connected, /opentelemetry-collector-contrib:0\.160\.0/u);
  assert.match(connected, /prom\/prometheus:v3\.14\.0/u);
  assert.match(connected, /prom\/alertmanager:v0\.34\.1/u);
  assert.match(connected, /verify-reporting-fairness\.sh/u);
  assert.match(fairness, /Observability__OtlpEndpoint/u);
  assert.match(fairness, /verify-reporting-observability\.mjs/u);
  assert.match(verifier, /pmcs_reporting_worker_queue_oldest_age_seconds/u);
  assert.match(verifier, /PmcsReportingQueueAgeBudgetExceeded/u);
  assert.match(verifier, /Forbidden metric label/u);
  assert.match(verifier, /reporting-observability-delivery-regression/u);
  assert.match(receiver, /127\.0\.0\.1/u);
  assert.match(receiver, /maximumBodyBytes/u);
  assert.match(seed, /verify-reporting-observability\.sh/u);
});

test("RPT1 worker capacity core is bounded observable and project-fair", () => {
  const options = read(`${moduleRoot}/ReportingExecutionOptions.cs`);
  const qualification = read(`${moduleRoot}/ReportingWorkerQualificationOptions.cs`);
  const worker = read(`${moduleRoot}/Services/ReportGenerationWorker.cs`);
  const telemetry = read(`${moduleRoot}/Services/ReportingWorkerTelemetry.cs`);
  const health = read(`${moduleRoot}/Services/ReportingWorkerHealthCheck.cs`);
  const module = read(`${moduleRoot}/ReportingModule.cs`);
  const endpoints = read(`${moduleRoot}/Endpoints/ReportingEndpoints.cs`);
  const pdf = read(`${moduleRoot}/Rendering/DailyReportPdfRenderer.cs`);
  const xlsx = read(`${moduleRoot}/Rendering/DailyReportXlsxRenderer.cs`);
  const harness = read("src/backend/Pmcs.TestHarness/ReportingCapacityVerification.cs");

  for (const key of [
    "MaximumAttempts",
    "MaximumPdfFacts",
    "MaximumXlsxRows",
    "MaximumOutputBytes",
    "RetryBaseDelaySeconds",
    "ProcessingTimeoutSeconds",
    "QueueAgeWarningSeconds",
  ]) assert.match(options, new RegExp(key, "u"));
  assert.match(worker, /CancelAfter\(execution\.ProcessingTimeout\)/u);
  assert.match(worker, /reporting\.run\.timeout/u);
  assert.match(worker, /reporting\.retry\.exhausted/u);
  assert.match(worker, /CertifiedReportRunFailed/u);
  assert.match(worker, /peer\.project_id = candidate\.project_id/u);
  assert.match(worker, /max\(history\.claimed_at\)/u);
  assert.match(worker, /for update of candidate skip locked/u);
  assert.match(worker, /execution\.RetryBaseDelay/u);
  assert.match(worker, /BeforeStorageTransientFailure/u);
  assert.match(qualification, /PMCS_QA_GATEWAY_ENABLED/u);
  assert.match(qualification, /QualificationFailurePoint/u);
  assert.match(telemetry, /Pmcs\.Reporting/u);
  assert.match(telemetry, /pmcs\.reporting\.worker\.heartbeat\.age/u);
  assert.match(telemetry, /pmcs\.reporting\.worker\.queue\.oldest_age/u);
  assert.match(health, /HealthCheckResult\.Degraded/u);
  assert.match(health, /QueueAgeWarning/u);
  assert.match(module, /AddCheck<ReportingWorkerHealthCheck>/u);
  const healthWriter = read("src/backend/Pmcs.Api/Infrastructure/HealthResponseWriter.cs");
  assert.match(healthWriter, /PublicNumericDataKeys/u);
  assert.match(healthWriter, /SelectPublicData/u);
  assert.doesNotMatch(healthWriter, /tenantId|projectId|userId|runId/u);
  assert.match(endpoints, /execution\.MaximumAttempts/u);
  assert.match(pdf, /execution\.MaximumPdfFacts/u);
  assert.match(xlsx, /execution\.MaximumXlsxRows/u);
  assert.match(harness, /Enumerable\.Range\(101, 20\)/u);
  assert.match(harness, /healthy-p95-under-30-seconds/u);
  assert.match(harness, /reporting\.qa\.transient_injected/u);
});

test("RPT1 is disabled by default until renderers and qualification are complete", () => {
  const settings = JSON.parse(read("src/backend/Pmcs.Api/appsettings.json"));
  assert.equal(settings.ReportingCenter.Phase1Enabled, false);
  assert.equal(settings.ReportingCenter.OutputAccessEnabled, false);
  assert.equal(settings.ReportingCenter.WorkerEnabled, false);
  assert.equal(settings.ReportingCenter.OrphanRemediationMode, "Disabled");
  const options = read(`${moduleRoot}/ReportingRuntimeOptions.cs`);
  assert.match(options, /workerEnabled = phase1Enabled &&/u);
  assert.match(options, /outputAccessEnabled = phase1Enabled \|\|/u);
});

test("RPT1 slice checkpoint separates source implementation from qualification", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-01-candidate.md");
  assert.match(checkpoint, /43cac1b83ac7764fe6005fee108029597091a238/u);
  assert.match(checkpoint, /Local structural evidence only \| Unqualified/u);
  assert.match(checkpoint, /37\/37 passed/u);
  assert.match(checkpoint, /dotnet[\s\S]*پاس‌شده اعلام نمی‌شوند/u);
  assert.match(checkpoint, /Generated Document publish\/read contract/u);
  assert.match(checkpoint, /Feature flag[\s\S]*پیش‌فرض خاموش/u);
});

test("RPT1 generated-output checkpoint pins source evidence without closing qualification", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-02-candidate.md");
  assert.match(checkpoint, /ef5d68e5d35b7f2b58ebd3da87b3b35dadf19173/u);
  assert.match(checkpoint, /4deccade8899a2438485fb1304cd918115af2654/u);
  assert.match(checkpoint, /Run 99 \(`35381177208`\)/u);
  assert.match(checkpoint, /40\/40 passed/u);
  assert.match(checkpoint, /139\/139 passed/u);
  assert.match(checkpoint, /295\/295/u);
  assert.match(checkpoint, /13\/13/u);
  assert.match(checkpoint, /43` Migration/u);
  assert.match(checkpoint, /۳۳۴ فایل C# ماژولی/u);
  assert.match(checkpoint, /PdfLicense[\s\S]*Unconfigured/u);
  assert.match(checkpoint, /هفت Stage Agent/u);
  assert.match(checkpoint, /هنوز `Feature Complete`[\s\S]*نیست/u);
});

test("RPT1 recovery-security checkpoint records connected evidence without closing the stage", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-03-candidate.md");
  assert.match(checkpoint, /b4da1e951debf76e1ba3b398bde2ccf60fbde5de/u);
  assert.match(checkpoint, /aa4063214ad1dea8fac19685a81818623296c24c/u);
  assert.match(checkpoint, /Run 102 \(`35383686315`\)/u);
  assert.match(checkpoint, /Security regression جدید هر `6\/6`/u);
  assert.match(checkpoint, /Cancellation regression جدید هر `6\/6`/u);
  assert.match(checkpoint, /Run 101[\s\S]*Audit موفق دوم/u);
  assert.match(checkpoint, /revocation پس از Queue و حین processing Worker/u);
  assert.match(checkpoint, /دو Worker واقعی/u);
  assert.match(checkpoint, /هر هفت Stage Agent/u);
  assert.match(checkpoint, /هنوز[\s\S]*`Feature Complete`/u);
});

test("RPT1 worker recovery checkpoint pins two-worker evidence without closing the stage", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-04-candidate.md");
  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S04-C1/u);
  assert.match(checkpoint, /e1ac3263df53a245b1aefb338a015be4854d367b/u);
  assert.match(checkpoint, /f58881f7e0a77bf89f65b872d4f988bd154a809f/u);
  assert.match(checkpoint, /Run 104 \(`35390054888`\)/u);
  assert.match(checkpoint, /298\/298/u);
  assert.match(checkpoint, /15\/15/u);
  assert.match(checkpoint, /attemptهای نهایی `1,1,2,2,2`/u);
  assert.match(checkpoint, /PMCS_QA_GATEWAY_ENABLED=true/u);
  assert.match(checkpoint, /revocation پس از Queue و حین processing Worker/u);
  assert.match(checkpoint, /object bytes/u);
  assert.match(checkpoint, /هر هفت Stage Agent/u);
  assert.match(checkpoint, /هنوز[\s\S]*`Feature Complete`/u);
});

test("RPT1 revocation and object-integrity checkpoint pins connected evidence without closing the stage", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-05-candidate.md");
  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S05-C1/u);
  assert.match(checkpoint, /167133fc1985c5b57c3dac90535f7a962dfd03b7/u);
  assert.match(checkpoint, /34fb70aee9a62a434a8446444d7c6d5c6c9819bd/u);
  assert.match(checkpoint, /Run 108 \(`35393509764`\)/u);
  assert.match(checkpoint, /302\/302/u);
  assert.match(checkpoint, /worker revocation[\s\S]*`8\/8`/u);
  assert.match(checkpoint, /object security[\s\S]*`8\/8`/u);
  assert.match(checkpoint, /`17\/17` assertion/u);
  assert.match(checkpoint, /BeforeStoragePermissionRecheck/u);
  assert.match(checkpoint, /sweeper[\s\S]*Gate باز/u);
  assert.match(checkpoint, /هر هفت Stage Agent/u);
  assert.match(checkpoint, /هنوز[\s\S]*`Feature Complete`/u);
});

test("RPT1 capacity Core micro-step records a resumable safe checkpoint", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-06-ms01-candidate.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S06-MS01-C1/u);
  assert.match(checkpoint, /d085c44f9ed8b3c085af62de6009fa1dafc9ed8e/u);
  assert.match(checkpoint, /9f8afd35b54fe7eacd38f202128538cc571c651f/u);
  assert.match(checkpoint, /Run 110 \(`35436466233`\)/u);
  assert.match(checkpoint, /311\/311/u);
  assert.match(checkpoint, /connected capacity\/fairness qualification pending/iu);
  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S06-MS02/u);
  assert.match(checkpoint, /هنوز `Feature Complete`/u);
  assert.match(roadmap, /\| `1\.15\.0` \| ثبت Safe Checkpoint `S06-MS01`/u);
});

test("RPT1 connected capacity and fairness micro-step records a resumable safe checkpoint", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-06-ms02-candidate.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S06-MS02-C1/u);
  assert.match(checkpoint, /346fbb778aa5c4475fd48df3241b700341e96d83/u);
  assert.match(checkpoint, /98b25e2dcd109356bdea08de138995f271260cfc/u);
  assert.match(checkpoint, /Run 113 \(`35437832281`\)/u);
  assert.match(checkpoint, /P95 نهایی `5\.529s`/u);
  assert.match(checkpoint, /Capacity fixture preparation هر `21\/21`/u);
  assert.match(checkpoint, /Fairness regression هر `9\/9`/u);
  assert.match(checkpoint, /`311\/311` تست C#/u);
  assert.match(checkpoint, /`45\/45` تست قراردادی Node/u);
  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S06-MS03/u);
  assert.match(checkpoint, /هنوز[\s\S]*`Feature Complete`/u);
  assert.match(roadmap, /\| `1\.16\.0` \| ثبت Safe Checkpoint متصل `S06-MS02`/u);
  assert.match(registry, /V1\.1 RPT1 Slice 06 MS02[\s\S]*346fbb778aa5c4475fd48df3241b700341e96d83/u);
});

test("RPT1 operational signal contract records a resumable intermediate checkpoint", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-06-ms03-candidate.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S06-MS03-C1/u);
  assert.match(checkpoint, /83f13cf43679b23a6a169cc0912985b391b1c017/u);
  assert.match(checkpoint, /1705d184bd494e80e50d8a85b723f0bc63e20abc/u);
  assert.match(checkpoint, /Run 117 \(`35441980440`\)/u);
  assert.match(checkpoint, /`313\/313` تست C#/u);
  assert.match(checkpoint, /Fairness regression هر `10\/10`/u);
  assert.match(checkpoint, /health=degraded-queue-age/u);
  assert.match(checkpoint, /Tenant\/Project\/User\/Run ID/u);
  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S06-MS03-C2/u);
  assert.match(checkpoint, /هنوز[\s\S]*`Feature Complete`/u);
  assert.match(roadmap, /\| `1\.17\.0` \| ثبت Safe Checkpoint میانی `S06-MS03-C1`/u);
  assert.match(registry, /V1\.1 RPT1 Slice 06 MS03-C1[\s\S]*83f13cf43679b23a6a169cc0912985b391b1c017/u);
});

test("RPT1 operational observability delivery records the final MS03 safe checkpoint", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-06-ms03-c2-candidate.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  const runbook = read("docs/runbooks/reporting-center.md");
  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S06-MS03-C2/u);
  assert.match(checkpoint, /9bb7ede9b89da2078e165cccb2927e0449116909/u);
  assert.match(checkpoint, /a960cddb5264b3de8857812906b7595db0664ba5/u);
  assert.match(checkpoint, /Run 120 \(`35443563270`\)/u);
  assert.match(checkpoint, /`321\/321` تست C#/u);
  assert.match(checkpoint, /`48\/48` تست قراردادی Node/u);
  assert.match(checkpoint, /qualification observability هر `5\/5`/u);
  assert.match(checkpoint, /PmcsReportingQueueAgeBudgetExceeded/u);
  assert.match(checkpoint, /sha256:08c497ddb804a197950b0fb3a40f056e1768b35e8fadc71b05ed755ae15c1290/u);
  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S06-MS04/u);
  assert.match(checkpoint, /MS03 بسته/u);
  assert.match(checkpoint, /هنوز[\s\S]*`Feature Complete`/u);
  assert.match(roadmap, /\| `1\.18\.0` \| ثبت Safe Checkpoint نهایی `S06-MS03-C2`/u);
  assert.match(registry, /V1\.1 RPT1 Slice 06 MS03-C2[\s\S]*9bb7ede9b89da2078e165cccb2927e0449116909/u);
  assert.match(runbook, /## Operational Observability/u);
  assert.match(runbook, /PmcsReportingQueueAgeBudgetExceeded/u);
});

test("RPT1 safe orphan remediation records the MS04 checkpoint without closing the stage", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-06-ms04-candidate.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  const runbook = read("docs/runbooks/reporting-center.md");
  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S06-MS04-C1/u);
  assert.match(checkpoint, /4ff44c96104ee1df87d267ca9a530d19b9248ba3/u);
  assert.match(checkpoint, /d4c320e7917121f64a70dea1251169bef4b516ce/u);
  assert.match(checkpoint, /Run 123 \(`35445497353`\)/u);
  assert.match(checkpoint, /`328\/328` تست C#/u);
  assert.match(checkpoint, /`50\/50` تست قراردادی Node/u);
  assert.match(checkpoint, /remediation regression هر `7\/7`/u);
  assert.match(checkpoint, /sha256:6314e5f38f452879610ca9d723fc22736e7d577d5f1dc3123573844ff1b7fb3d/u);
  assert.match(checkpoint, /OrphanRemediationMode=Disabled/u);
  assert.match(checkpoint, /RPT1 بسته نیست/u);
  assert.match(checkpoint, /هنوز[\s\S]*`Feature Complete`/u);
  assert.match(roadmap, /\| `1\.19\.0` \| ثبت Safe Checkpoint `S06-MS04`/u);
  assert.match(registry, /V1\.1 RPT1 Slice 06 MS04[\s\S]*4ff44c96104ee1df87d267ca9a530d19b9248ba3/u);
  assert.match(runbook, /## Generated Document orphan remediation/u);
  assert.match(runbook, /Run 123 \(`35445497353`\)/u);
});

test("RPT1 semantic and XLSX Golden records the MS05 checkpoint without closing the stage", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-06-ms05-candidate.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  const runbook = read("docs/runbooks/reporting-center.md");
  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S06-MS05-C1/u);
  assert.match(checkpoint, /38a03f33f4747d0b6a76696705877633acd17678/u);
  assert.match(checkpoint, /eb9369c9e32eb3f523c4faa22487d6428c2d7e34/u);
  assert.match(checkpoint, /Run 130 \(`35449387794`\)/u);
  assert.match(checkpoint, /`329\/329` تست C#/u);
  assert.match(checkpoint, /`51\/51` تست قراردادی Node/u);
  assert.match(checkpoint, /Golden متصل هر `13\/13` assertion/u);
  assert.match(checkpoint, /sha256:c7bbe07dc3e052772665fdffaedce99e85e7c340058eb6bbd3b8590dc0128666/u);
  assert.match(checkpoint, /revision `11`/u);
  assert.match(checkpoint, /revision `12`/u);
  assert.match(checkpoint, /RPT1 بسته نیست/u);
  assert.match(checkpoint, /هنوز[\s\S]*`Feature Complete`/u);
  assert.match(roadmap, /\| `1\.20\.0` \| ثبت Safe Checkpoint `S06-MS05`/u);
  assert.match(registry, /V1\.1 RPT1 Slice 06 MS05[\s\S]*38a03f33f4747d0b6a76696705877633acd17678/u);
  assert.match(runbook, /## Semantic\/XLSX Golden verification/u);
  assert.match(runbook, /Run 130 \(`35449387794`\)/u);
});

test("RPT1 certified PDF qualification records the MS06 checkpoint without closing the stage", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-06-ms06-candidate.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  const canonical = read("docs/PMCS-CANONICAL-PROJECT-REFERENCE.md");
  const decision = read("docs/adr/0030-questpdf-community-and-certified-runtime.md");
  const runbook = read("docs/runbooks/reporting-center.md");
  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S06-MS06-C1/u);
  assert.match(checkpoint, /b8f21492a4f44c7c412e5b7eda0b164e7f256758/u);
  assert.match(checkpoint, /e94b6ba3753e67b42ea0ec99e998761fdad0bcc3/u);
  assert.match(checkpoint, /Run 133 \(`35463350892`\)/u);
  assert.match(checkpoint, /`330\/330` تست C#/u);
  assert.match(checkpoint, /`52\/52` تست قراردادی Node/u);
  assert.match(checkpoint, /PDF Golden متصل هر `8\/8` assertion/u);
  assert.match(checkpoint, /cc188c842ddcced9a24acd5e18f92c4a6511252c40104055c8c38627cdfac863/u);
  assert.match(checkpoint, /95d6e71de15d9d130041d5c295c94239b9fe9095e6572e581aa9a655ee85c9b2/u);
  assert.match(checkpoint, /RPT1 فعال است/u);
  assert.match(checkpoint, /۹ خانوادهٔ دیگر کاتالوگ Done نیستند/u);
  assert.match(roadmap, /\| `1\.21\.0` \| ثبت Safe Checkpoint `S06-MS06`/u);
  assert.match(registry, /V1\.1 RPT1 Slice 06 MS06[\s\S]*b8f21492a4f44c7c412e5b7eda0b164e7f256758/u);
  assert.match(checkpoint, /Safe Resume Point اکنون `PMCS-V1\.1-RPT1-S06-MS06-C1`/u);
  assert.match(checkpoint, /Micro-Step بعدی[\s\S]*Decision\/Change Record رسمی/u);
  assert.match(canonical, /ADR 0031[\s\S]*F02/u);
  assert.match(decision, /QuestPDF Community/u);
  assert.match(decision, /حداقل در بازبینی سالانه/u);
  assert.match(runbook, /Run 133 \(`35463350892`\)/u);
  assert.match(runbook, /ReportingCenter:PdfLicense.*Unconfigured/u);
});

test("RPT1 preserves the approved ten-family catalog through an explicit owner decision", () => {
  const decision = read("docs/adr/0031-rpt1-ten-family-catalog-completion.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  const canonical = read("docs/PMCS-CANONICAL-PROJECT-REFERENCE.md");

  assert.match(decision, /PMCS-RPT1-CATALOG-DECISION-001/u);
  assert.match(decision, /انتخاب صریح «حفظ هر ۱۰ خانواده»/u);
  assert.equal((decision.match(/\| `RPT1-F\d{2}` \|/gu) ?? []).length, 10);
  assert.match(decision, /RPT1-F01[\s\S]*Qualified/u);
  for (const family of ["02", "03", "04", "05", "06", "07", "08", "09", "10"]) {
    assert.match(decision, new RegExp(`RPT1-F${family}[^\\n]*Required؛ Not Implemented`, "u"));
  }
  assert.match(decision, /Micro-Slice بعدی `RPT1-F02`/u);
  assert.match(decision, /هر ده خانواده[\s\S]*Gate خروج `V1\.1-RPT1`/u);
  assert.match(decision, /هیچ API، Migration، Renderer، feature flag یا Production setting/u);
  assert.match(roadmap, /`D-PV1-16`[\s\S]*ده خانوادهٔ استاندارد/u);
  assert.match(roadmap, /\| `1\.22\.0` \| ثبت ADR 0031 و تصمیم صریح حفظ Scope ده‌گانه/u);
  assert.match(registry, /V1\.1 RPT1 Slice 07 MS01/u);
  assert.match(canonical, /ADR 0031[\s\S]*RPT1-F02/u);
});

test("RPT1 ten-family decision records the S07-MS01 safe checkpoint without claiming implementation", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-07-ms01-candidate.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  const canonical = read("docs/PMCS-CANONICAL-PROJECT-REFERENCE.md");
  const baseline = read("docs/governance/pmcs-v1.1-development-baseline.md");
  const matrix = read("docs/qa/pmcs-v1.1-rpt1-test-matrix.md");

  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S07-MS01-C1/u);
  assert.match(checkpoint, /d81ecc00762145210e1c688f8f5843f46d62fc04/u);
  assert.match(checkpoint, /5f40383ad506d94520c741eb69fcd00086283734/u);
  assert.match(checkpoint, /Run 135 \(`35466775368`\)/u);
  assert.match(checkpoint, /`330\/330` تست C#/u);
  assert.match(checkpoint, /`54\/54` تست قراردادی Node/u);
  assert.match(checkpoint, /`139\/139` تست Web/u);
  assert.match(checkpoint, /`RPT1-F02` تا `RPT1-F10`[^\n]*`Required \/ Not Implemented`/u);
  assert.match(checkpoint, /هیچ API، Migration،[\s\S]*Domain model، Renderer/u);
  assert.match(checkpoint, /Micro-Step بعدی `RPT1-F02`/u);
  assert.match(roadmap, /\| `1\.23\.0` \| ثبت Safe Checkpoint `S07-MS01`/u);
  assert.match(registry, /V1\.1 RPT1 Slice 07 MS01[\s\S]*d81ecc00762145210e1c688f8f5843f46d62fc04/u);
  assert.match(checkpoint, /Safe Resume Point اکنون `PMCS-V1\.1-RPT1-S07-MS01-C1`/u);
  assert.match(baseline, /Slice 07 Micro-Step 01[\s\S]*Run 135/u);
  assert.match(matrix, /## ۲۳\.[\s\S]*Run 135/u);
});

test("RPT1-F02 keeps the weekly/monthly semantic contract aligned with its connected safe checkpoint", () => {
  const contract = read("docs/architecture/pmcs-v1.1-rpt1-f02-weekly-monthly-semantic-contract.md");
  const architecture = read("docs/architecture/pmcs-v1.1-reporting-center-phase1.md");
  const api = read("docs/api/reporting-v1.md");
  const security = read("docs/security/pmcs-v1.1-reporting-security.md");
  const matrix = read("docs/qa/pmcs-v1.1-rpt1-test-matrix.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  const canonical = read("docs/PMCS-CANONICAL-PROJECT-REFERENCE.md");

  assert.match(contract, /PMCS-RPT1-F02-SEMANTIC-001/u);
  assert.match(contract, /نسخه: `1\.3\.1`/u);
  assert.match(contract, /Connected Safe Checkpoint \| UI\/Production Disabled/u);
  assert.match(contract, /`periodKind`[\s\S]*`Weekly` یا `Monthly`/u);
  assert.match(contract, /`periodStartLocalDate`[\s\S]*برای Weekly باید شنبه[\s\S]*برای Monthly باید روز اول ماه شمسی/u);
  assert.match(contract, /بازه نیمه‌باز `[\s\S]*periodEndLocalDateExclusive/u);
  assert.match(contract, /period-read در FieldOperations[\s\S]*FieldOperationsDbContext[\s\S]*ممنوع/u);
  assert.match(contract, /`approvedAt <= asOfUtc`/u);
  assert.match(contract, /correction که بعد از cutoff تأیید می‌شود خروجی تاریخی را تغییر نمی‌دهد/u);
  assert.match(contract, /`NotConfigured`[\s\S]*`NoData`[\s\S]*`InsufficientData`[\s\S]*`Available`/u);
  assert.match(contract, /`ReportingCadenceMissing`[\s\S]*`OfficialReportEmpty`/u);
  assert.match(contract, /`reporting\.run\.create`/u);
  assert.match(contract, /`field\.daily-reports\.read`/u);
  assert.match(contract, /Caller نمی‌تواند آن را پایین بیاورد/u);
  assert.match(contract, /هیچ درصد کل[\s\S]*S-Curve/u);
  assert.equal((contract.match(/\| `F02-[A-Z]\d{2}` \|/gu) ?? []).length, 14);
  assert.match(contract, /Migration forward شمارهٔ 44[\s\S]*strict parse[\s\S]*registry اختصاصی/u);
  assert.match(architecture, /PMCS-RPT1-F02-SEMANTIC-001 v1\.3\.1/u);
  assert.match(api, /خانواده F02 روی API موجود/u);
  assert.match(security, /سیاست ثابت F02/u);
  assert.match(matrix, /## ۲۷\.[\s\S]*Catalog\/API\/Worker wiring متصل خانواده F02/u);
  assert.match(registry, /Slice 07 MS05[\s\S]*Run 144[\s\S]*F03–F10 open/u);
  assert.match(canonical, /`RPT1-F01` تا `RPT1-F05` checkpoint متصل دارند/u);
});

test("RPT1-F03 connects its checkpointed semantic runtime and renderers without production enablement", () => {
  const contract = read(
    "docs/architecture/pmcs-v1.1-rpt1-f03-executive-project-state-semantic-contract.md",
  );
  const architecture = read("docs/architecture/pmcs-v1.1-reporting-center-phase1.md");
  const api = read("docs/api/reporting-v1.md");
  const security = read("docs/security/pmcs-v1.1-reporting-security.md");
  const matrix = read("docs/qa/pmcs-v1.1-rpt1-test-matrix.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  const canonical = read("docs/PMCS-CANONICAL-PROJECT-REFERENCE.md");
  const rootReadme = read("README.md");
  const module = read(`${moduleRoot}/ReportingModule.cs`);
  const worker = read(`${moduleRoot}/Services/ReportGenerationWorker.cs`);
  const endpoints = read(`${moduleRoot}/Endpoints/ReportingEndpoints.cs`);
  const readService = read(`${moduleRoot}/Services/ReportingReadService.cs`);
  const migration = read(
    `${moduleRoot}/Migrations/ExecutiveProjectStateReportCatalogMigration.cs`,
  );
  const policy = read(`${moduleRoot}/Domain/ReportDefinitionRuntimePolicy.cs`);
  const sourceContract = read(
    "src/backend/Pmcs.Modules.ProjectIntelligence/Contracts/IProjectStateReportingSource.cs",
  );
  const selector = read(
    "src/backend/Pmcs.Modules.ProjectIntelligence/Services/ProjectStateReportingSelector.cs",
  );
  const source = read(
    "src/backend/Pmcs.Modules.ProjectIntelligence/Services/ProjectStateReportingSource.cs",
  );
  const projectIntelligenceModule = read(
    "src/backend/Pmcs.Modules.ProjectIntelligence/ProjectIntelligenceModule.cs",
  );
  const identity = read(
    `${moduleRoot}/Domain/ExecutiveProjectStateReportRuntimeContract.cs`,
  );
  const builder = read(
    `${moduleRoot}/Services/ExecutiveProjectStateReportSnapshotBuilder.cs`,
  );
  const tests = read("tests/Pmcs.Domain.Tests/ExecutiveProjectStateReportingTests.cs");
  const renderingContract = read(
    `${moduleRoot}/Rendering/ExecutiveProjectStateReportRenderingContracts.cs`,
  );
  const pdfRenderer = read(
    `${moduleRoot}/Rendering/ExecutiveProjectStateReportPdfRenderer.cs`,
  );
  const xlsxRenderer = read(
    `${moduleRoot}/Rendering/ExecutiveProjectStateReportXlsxRenderer.cs`,
  );
  const rendererRegistry = read(`${moduleRoot}/Rendering/ReportRendererRegistry.cs`);
  const renderingTests = read(
    "tests/Pmcs.Domain.Tests/ExecutiveProjectStateReportRenderingTests.cs",
  );
  const harness = read(
    "src/backend/Pmcs.TestHarness/ReportingExecutiveProjectStateVerification.cs",
  );
  const harnessProgram = read("src/backend/Pmcs.TestHarness/Program.cs");
  const diagnostics = read("tools/qa/seed-diagnostics.sh");
  const settings = JSON.parse(read("src/backend/Pmcs.Api/appsettings.json"));

  assert.match(contract, /PMCS-RPT1-F03-SEMANTIC-001/u);
  assert.match(contract, /نسخه: `1\.3\.1`/u);
  assert.match(contract, /Connected Safe Checkpoint \| UI\/Production Disabled/u);
  assert.match(contract, /Parent checkpoint: `PMCS-V1\.1-RPT1-S07-MS08-C1`/u);
  assert.match(contract, /پارامتر معنایی Client دقیقاً یک object خالی `\{\}`/u);
  assert.match(contract, /Client نمی‌تواند Snapshot مطلوب خود را[\s\S]*انتخاب کند/u);
  assert.match(contract, /`calculatedAt <= sourceCutoffUtc`/u);
  assert.match(contract, /`asOfDate <= cutoffLocalDate`/u);
  assert.match(contract, /بزرگ‌ترین `asOfDate`[\s\S]*جدیدترین `calculatedAt`[\s\S]*بزرگ‌ترین `snapshotId`/u);
  assert.match(contract, /حداکثر ۱۴ تاریخ متمایز/u);
  assert.match(contract, /ProjectIntelligenceDbContext[\s\S]*Recalculate کردن Project State[\s\S]*ممنوع/u);
  assert.match(contract, /خروجی `GET \/command-center` منبع مستقیم F03 نیست/u);
  assert.match(contract, /`project-state-v1` و `project-state-v2`[\s\S]*allowlisted/u);
  assert.match(contract, /`NotConfigured`[\s\S]*`NoData`[\s\S]*`InsufficientData`[\s\S]*`Available`/u);
  assert.match(contract, /`ProjectStateReportingNotConfigured`[\s\S]*`ApprovedSourceChangedAfterSnapshot`/u);
  assert.match(contract, /`isPartial=true`[\s\S]*به‌تنهایی[\s\S]*`InsufficientData` نمی‌کند/u);
  assert.match(contract, /`Stable` فقط وضعیت عملیاتی[\s\S]*سلامت\s+مالی/u);
  assert.match(contract, /هیچ رنگ\/امتیاز کل از feature stateها ساخته نمی‌شود/u);
  assert.match(contract, /`project-state\.read`/u);
  assert.match(contract, /`project-state\.recalculate` برای F03 لازم نیست/u);
  assert.match(contract, /حداقل `Internal`/u);
  assert.equal((contract.match(/\| `F03-[A-Z]\d{2}` \|/gu) ?? []).length, 17);
  assert.match(contract, /Migration forward شمارهٔ 45[\s\S]*strict empty-object parser[\s\S]*definition-aware/u);

  assert.match(architecture, /PMCS-RPT1-F03-SEMANTIC-001 v1\.3\.1/u);
  assert.match(api, /خانواده F03 روی API متصل/u);
  assert.match(security, /سیاست ثابت F03/u);
  assert.match(matrix, /## ۲۸\.[\s\S]*Golden matrix هفده‌سناریویی/u);
  assert.match(matrix, /## ۳۰\.[\s\S]*Renderer\/Golden خانواده F03/u);
  assert.match(matrix, /## ۳۱\.[\s\S]*Catalog\/API\/Worker wiring متصل خانواده F03/u);
  assert.match(roadmap, /نسخه سند: `1\.51\.0`/u);
  assert.match(registry, /Slice 07 MS06[\s\S]*Run 146[\s\S]*Runtime not implemented/u);
  assert.match(canonical, /PMCS-RPT1-F03-SEMANTIC-001 v1\.3\.1/u);
  assert.match(rootReadme, /F03 متصل و checkpointed/u);

  assert.match(sourceContract, /pmcs\.project-intelligence\.project-state-reporting\/v1/u);
  assert.match(sourceContract, /MaximumTrendDates = 14/u);
  assert.match(sourceContract, /interface IProjectStateReportingSource/u);
  assert.match(selector, /AsOfDate <= cutoffLocalDate/u);
  assert.match(selector, /CalculatedAt <= cutoff/u);
  assert.match(selector, /ThenByDescending\(snapshot => snapshot\.SnapshotId\.ToString\("D"\)/u);
  assert.match(source, /IApprovedDailyFactSource/u);
  assert.match(source, /GetLatestApprovedChangeAtAsync/u);
  assert.match(projectIntelligenceModule, /IProjectStateReportingSource, ProjectStateReportingSource/u);
  assert.match(identity, /executive-project-state-certified/u);
  assert.match(identity, /pmcs\.reporting\.executive-project-state\.snapshot\/v1/u);
  assert.match(identity, /TemplateVersion = "1\.0\.0"/u);
  assert.match(identity, /4bf4f1f5de92eda854ab16702fc87aaebae951eea17ef023569cc338a5ce7d7a/u);
  assert.match(identity, /pmcs\.reporting\.executive-project-state\.renderer\/v1/u);
  assert.match(identity, /pmcs\.reporting\.executive-project-state\.layout\/v1/u);
  assert.match(builder, /ProjectStateReportingContract\.Version/u);
  assert.match(builder, /ProjectConfigurationRevisionOutdated/u);
  assert.match(builder, /ApprovedSourceChangedAfterSnapshot/u);
  assert.match(builder, /CanonicalJson\.Sha256/u);
  assert.doesNotMatch(builder, /DbContext|ExecuteSql|CommandCenter|Finance|Commercial|Recalculate/u);
  assert.match(tests, /SelectorUsesCutoffAndCanonicalAsOfCalculatedAtAndOrdinalIdentityTieBreak/u);
  assert.match(tests, /SelectorKeepsFourteenDistinctDatesAndCollapsesSameDateRecalculations/u);
  assert.match(tests, /HistoricalCutoffIgnoresLaterCorrectionAndTwinSourceOrderIsDeterministic/u);
  assert.match(tests, /StablePartialSnapshotIsAvailableButRetainsItsBoundedAssessmentScope/u);
  assert.match(tests, /AttentionItemsUsePriorityAgeDateAndOrdinalLineageOrderingWithoutInventingImpact/u);
  assert.match(tests, /CrossTenantAndUnsupportedEligibleCalculationVersionFailClosed/u);
  assert.match(renderingContract, /ExecutiveProjectStateReportRenderModel/u);
  assert.match(renderingContract, /reporting\.executive_state\.snapshot\.payload_invalid/u);
  assert.match(pdfRenderer, /ContentFromRightToLeft/u);
  assert.match(pdfRenderer, /GenerateImages/u);
  assert.match(pdfRenderer, /MaximumPdfFacts/u);
  assert.match(xlsxRenderer, /CompressionLevel\.NoCompression/u);
  assert.match(xlsxRenderer, /rightToLeft/u);
  assert.match(xlsxRenderer, /MaximumXlsxRows/u);
  assert.match(rendererRegistry, /ExecutiveProjectStateReportRendererRegistry/u);
  assert.match(renderingTests, /e19809b6c3ffa5ff3443babe683c9f286c3b928986d176f1d515166f336cf5a3/u);
  assert.match(renderingTests, /d765dfc98873fbc07e28b7320524fd156cfa5acc80c6f4da2b2d42941c4e09d1/u);
  assert.match(renderingTests, /6d18d03ff3e9100ffe0e12c5da05a6f1976c3d7c1d2ba7f27b36656dcc5ff0ba/u);
  assert.match(renderingTests, /252a6dd6c8562242a37e4466dfb4d0a0155831abb39c30e2751309eb3acfa205/u);
  assert.match(policy, /ProjectStateSourcePermission = "project-state\.read"/u);
  assert.match(
    policy,
    /ExecutiveProjectStateReportRuntimeContract\.DefinitionCode => ProjectStateSourcePermission/u,
  );
  assert.match(migration, /public long Order => 1203/u);
  assert.match(migration, /pmcs\.reporting\.executive-project-state\.renderer\/v1/u);
  assert.match(module, /IExecutiveProjectStateReportRenderer, ExecutiveProjectStateReportPdfRenderer/u);
  assert.match(module, /IExecutiveProjectStateReportRenderer, ExecutiveProjectStateReportXlsxRenderer/u);
  assert.match(module, /ExecutiveProjectStateReportCatalogMigration/u);
  assert.match(worker, /IProjectStateReportingSource/u);
  assert.match(worker, /ExecutiveProjectStateReportSnapshotBuilder\.Build/u);
  assert.match(worker, /ExecutiveProjectStateReportRendererRegistry/u);
  assert.match(worker, /ExecutiveProjectStateReportRenderSnapshot\.Parse/u);
  assert.match(worker, /ExecutiveProjectStateReportRenderRequest/u);
  assert.match(endpoints, /ExecutiveProjectStateReportRuntimeContract\.DefinitionCode/u);
  assert.match(endpoints, /ParseExecutiveProjectStateParameters/u);
  assert.match(endpoints, /PermittedDefinitionCodesAsync/u);
  assert.match(readService, /ReportDefinitionRuntimePolicy\.SupportedDefinitionCodes/u);
  assert.match(readService, /PermittedDefinitionCodesAsync/u);
  assert.match(readService, /HasSourcePermissionAsync/u);
  assert.doesNotMatch(readService, /const string DailyReportSourcePermission/u);
  assert.match(harness, /VerifyReportingExecutiveProjectStateAsync/u);
  assert.match(harness, /permission-isolated/u);
  assert.match(harnessProgram, /verify-reporting-executive-state/u);
  assert.match(diagnostics, /verify-reporting-executive-state/u);
  assert.equal(settings.ReportingCenter.Phase1Enabled, false);
  assert.equal(settings.ReportingCenter.OutputAccessEnabled, false);
  assert.equal(settings.ReportingCenter.WorkerEnabled, false);
});

test("RPT1-F03 Catalog API and Worker record the S07-MS09 connected safe checkpoint", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-07-ms09-candidate.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  const canonical = read("docs/PMCS-CANONICAL-PROJECT-REFERENCE.md");
  const architecture = read("docs/architecture/pmcs-v1.1-reporting-center-phase1.md");
  const baseline = read("docs/governance/pmcs-v1.1-development-baseline.md");
  const matrix = read("docs/qa/pmcs-v1.1-rpt1-test-matrix.md");
  const api = read("docs/api/reporting-v1.md");
  const security = read("docs/security/pmcs-v1.1-reporting-security.md");

  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S07-MS09-C1/u);
  assert.match(checkpoint, /de76a91ec9047205fceba1321bae7187bf787fec/u);
  assert.match(checkpoint, /40afeb37d7bf90e97a988cae141901e28d336516/u);
  assert.match(checkpoint, /ae06285bf1a68fe2592dacc76c7d31cb291ab924/u);
  assert.match(checkpoint, /4944731391c2b649cd401fc9095619cb19d41f72/u);
  assert.match(checkpoint, /Run 156 \(`35515989200`\)/u);
  assert.match(checkpoint, /`387\/387` تست C#/u);
  assert.match(checkpoint, /`14\/14` assertion/u);
  assert.match(checkpoint, /`66\/66` تست قراردادی Node/u);
  assert.match(checkpoint, /`139\/139` تست Web/u);
  assert.match(checkpoint, /`367` فایل C#/u);
  assert.match(checkpoint, /`274` endpoint، `204` mutation و `5`/u);
  assert.match(checkpoint, /Restore Drill کامل `45` Migration/u);
  assert.match(checkpoint, /`7\/7` Suite و `12\/12` Command/u);
  assert.match(checkpoint, /Qualification artifact: `10606892723`/u);
  assert.match(checkpoint, /sha256:6d0eb7ee8a5bb9254946f8e04f1577920cdba9d51ca74f8dd8885aa8e632df6b/u);
  assert.match(checkpoint, /Integration artifact: `10606788092`/u);
  assert.match(checkpoint, /sha256:faf08b54b3dba1697c99cbc48a4246cb0a73b5922086a451b15c31fe184fc188/u);
  assert.match(checkpoint, /UI-E2E artifact: `10606882688`/u);
  assert.match(checkpoint, /sha256:ecbfd966d7fa016b572bed3a329a4f266c3fb0d54a5ac2fbea212cc85864ed0f/u);
  assert.match(checkpoint, /Phase1Enabled=false[\s\S]*PdfLicense=Unconfigured/u);
  assert.match(checkpoint, /Safe Resume Point اکنون `PMCS-V1\.1-RPT1-S07-MS09-C1`/u);
  assert.match(checkpoint, /Micro-Slice بعدی باید فقط از[\s\S]*`RPT1-F04`/u);
  assert.match(roadmap, /\| `1\.39\.0` \| ثبت Safe Checkpoint `S07-MS09`/u);
  assert.match(registry, /Slice 07 MS09[\s\S]*40afeb37d7bf90e97a988cae141901e28d336516[\s\S]*Run 156/u);
  assert.match(canonical, /Safe Resume Point قطعی فعلی آن `PMCS-V1\.1-RPT1-S07-MS17-C1`/u);
  assert.match(canonical, /PMCS-RPT1-F04-SEMANTIC-001 v1\.0\.0[\s\S]*Run 158/u);
  assert.match(architecture, /نسخه: `1\.31\.0`[\s\S]*Run 156/u);
  assert.match(baseline, /Slice 07 Micro-Step 09[\s\S]*Run 156/u);
  assert.match(matrix, /## ۳۱\.[\s\S]*Run 156/u);
  assert.match(matrix, /نسخه: `1\.34\.0`/u);
  assert.match(api, /خانواده F03 روی API متصل — Safe Checkpoint/u);
  assert.match(security, /نسخه: `1\.16\.0`[\s\S]*Run 156/u);
});

test("RPT1-F04 keeps bounded semantics while its connected pipeline remains default-off", () => {
  const contract = read(
    "docs/architecture/pmcs-v1.1-rpt1-f04-progress-curve-semantic-contract.md",
  );
  const architecture = read("docs/architecture/pmcs-v1.1-reporting-center-phase1.md");
  const api = read("docs/api/reporting-v1.md");
  const security = read("docs/security/pmcs-v1.1-reporting-security.md");
  const matrix = read("docs/qa/pmcs-v1.1-rpt1-test-matrix.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  const canonical = read("docs/PMCS-CANONICAL-PROJECT-REFERENCE.md");
  const rootReadme = read("README.md");
  const module = read(`${moduleRoot}/ReportingModule.cs`);
  const worker = read(`${moduleRoot}/Services/ReportGenerationWorker.cs`);
  const endpoints = read(`${moduleRoot}/Endpoints/ReportingEndpoints.cs`);
  const migration = read(
    `${moduleRoot}/Migrations/ProjectProgressReportCatalogMigration.cs`,
  );
  const policy = read(`${moduleRoot}/Domain/ReportDefinitionRuntimePolicy.cs`);
  const readService = read(`${moduleRoot}/Services/ReportingReadService.cs`);
  const harness = read(
    "src/backend/Pmcs.TestHarness/ReportingProjectProgressVerification.cs",
  );
  const harnessProgram = read("src/backend/Pmcs.TestHarness/Program.cs");
  const diagnostics = read("tools/qa/seed-diagnostics.sh");
  const evidenceContract = read(
    "src/backend/Pmcs.Modules.FieldOperations/Contracts/IProgressEvidenceReportingSource.cs",
  );
  const evidenceSelector = read(
    "src/backend/Pmcs.Modules.FieldOperations/Services/ProgressEvidenceReportingSelector.cs",
  );
  const evidenceSource = read(
    "src/backend/Pmcs.Modules.FieldOperations/Services/ProgressEvidenceReportingSource.cs",
  );
  const fieldModule = read("src/backend/Pmcs.Modules.FieldOperations/FieldOperationsModule.cs");
  const sourceContract = read(
    "src/backend/Pmcs.Modules.Planning/Contracts/IProjectProgressReportingSource.cs",
  );
  const selector = read(
    "src/backend/Pmcs.Modules.Planning/Services/ProjectProgressReportingSelector.cs",
  );
  const calculator = read(
    "src/backend/Pmcs.Modules.Planning/Services/ProjectProgressReportingCalculator.cs",
  );
  const source = read(
    "src/backend/Pmcs.Modules.Planning/Services/ProjectProgressReportingSource.cs",
  );
  const planningModule = read("src/backend/Pmcs.Modules.Planning/PlanningModule.cs");
  const identity = read(`${moduleRoot}/Domain/ProjectProgressReportRuntimeContract.cs`);
  const builder = read(`${moduleRoot}/Services/ProjectProgressReportSnapshotBuilder.cs`);
  const tests = read("tests/Pmcs.Domain.Tests/ProjectProgressReportingTests.cs");
  const renderingContracts = read(
    `${moduleRoot}/Rendering/ProjectProgressReportRenderingContracts.cs`,
  );
  const pdfRenderer = read(`${moduleRoot}/Rendering/ProjectProgressReportPdfRenderer.cs`);
  const xlsxRenderer = read(`${moduleRoot}/Rendering/ProjectProgressReportXlsxRenderer.cs`);
  const rendererRegistry = read(`${moduleRoot}/Rendering/ReportRendererRegistry.cs`);
  const renderingTests = read(
    "tests/Pmcs.Domain.Tests/ProjectProgressReportRenderingTests.cs",
  );
  const reportingProject = read(`${moduleRoot}/Pmcs.Modules.Reporting.csproj`);
  const settings = JSON.parse(read("src/backend/Pmcs.Api/appsettings.json"));

  assert.match(contract, /PMCS-RPT1-F04-SEMANTIC-001/u);
  assert.match(contract, /نسخه: `1\.3\.1`/u);
  assert.match(contract, /Connected Safe Checkpoint \| UI\/Production Disabled/u);
  assert.match(contract, /Parent checkpoint: `PMCS-V1\.1-RPT1-S07-MS12-C1`/u);
  assert.match(contract, /پارامتر معنایی Client دقیقاً یک object خالی `\{\}`/u);
  assert.match(contract, /Client نمی‌تواند Baseline مطلوب خود را[\s\S]*انتخاب کند/u);
  assert.match(contract, /`approvedAt <= sourceCutoffUtc`/u);
  assert.match(contract, /`sourceCutoffUtc < supersededAt`/u);
  assert.match(contract, /حداکثر یک Baseline[\s\S]*هم‌پوشان[\s\S]*fail-closed/u);
  assert.match(contract, /`PlanningDbContext`[\s\S]*`FieldOperationsDbContext`[\s\S]*`GET \/planning\/progress`[\s\S]*`IProgressFactSource`/u);
  assert.match(contract, /Draft، Submitted، Returned و مقدار Provisional وارد Actual رسمی نمی‌شوند/u);
  assert.match(contract, /`approvedQuantity \/ pinnedTargetQuantity × 100`/u);
  assert.match(contract, /درصد هر ردیف حداکثر `100`/u);
  assert.match(contract, /missing به صفر تبدیل نمی‌شود/u);
  assert.match(contract, /`MeasurementWeights` Schedule ندارد/u);
  assert.match(contract, /`variancePercent = actualPercent - plannedPercent`/u);
  assert.match(contract, /`floor\(i × spanDays \/ 364\)`/u);
  assert.match(contract, /حداکثر ۳۶۶ نقطه/u);
  assert.match(contract, /نقطه‌های آینده Actual و Variance برابر[\s\S]*`null`/u);
  assert.match(contract, /`NotConfigured`[\s\S]*`NoData`[\s\S]*`InsufficientData`[\s\S]*`Available`/u);
  assert.match(contract, /`ProgressReportingNotConfigured`[\s\S]*`ApprovedProgressOutsideBaseline`/u);
  assert.match(contract, /`planning\.progress\.read`[\s\S]*`planning\.baselines\.read`[\s\S]*`planning\.milestones\.read`/u);
  assert.match(contract, /حداقل `Internal`/u);
  assert.match(contract, /`IProgressFactSource\.LoadAsync` cutoff ندارد/u);
  assert.match(contract, /`PlanningBaseline\.ReviewedAt` هنگام Supersede بازنویسی می‌شود/u);
  assert.match(contract, /target\/unit\/title قلم اندازه‌گیری را هنگام Approval snapshot نمی‌کند/u);
  assert.equal((contract.match(/\| `F04-[A-Z]{1,2}\d{2}` \|/gu) ?? []).length, 22);
  assert.match(contract, /Catalog\/API\/Worker و qualification انتهابه‌انتهای این خانواده[\s\S]*بسته شده‌اند/u);

  assert.match(architecture, /PMCS-RPT1-F04-SEMANTIC-001 v1\.3\.1/u);
  assert.match(api, /خانواده F04 روی API متصل — Safe Checkpoint/u);
  assert.match(security, /سیاست ثابت F04/u);
  assert.match(matrix, /## ۳۲\.[\s\S]*Golden matrix بیست‌ودوسناریویی/u);
  assert.match(matrix, /## ۳۳\.[\s\S]*Runtime Core محدود خانواده F04/u);
  assert.match(matrix, /## ۳۴\.[\s\S]*Renderer\/Golden خانواده F04/u);
  assert.match(matrix, /## ۳۵\.[\s\S]*اتصال Catalog\/API\/Worker خانواده F04/u);
  assert.match(roadmap, /نسخه سند: `1\.51\.0`/u);
  assert.match(registry, /Slice 07 MS10[\s\S]*Run 158[\s\S]*Runtime not implemented/u);
  assert.match(registry, /Slice 07 MS13[\s\S]*4c48c03aad126a594e5328fc7995a72728ba2274[\s\S]*Run 169/u);
  assert.match(canonical, /F04 Connected Source MS13:[\s\S]*4c48c03aad126a594e5328fc7995a72728ba2274[\s\S]*Run 169/u);
  assert.match(rootReadme, /F04 متصل و checkpointed/u);

  assert.match(evidenceContract, /pmcs\.field-operations\.progress-evidence-reporting\/v1/u);
  assert.match(evidenceContract, /interface IProgressEvidenceReportingSource/u);
  assert.match(evidenceSelector, /sourceCutoffUtc < supersededAt\.Value/u);
  assert.match(evidenceSource, /DailyReportStatus\.Approved/u);
  assert.match(evidenceSource, /DailyReportStatus\.Superseded/u);
  assert.match(evidenceSource, /DailyFactKind\.WorkProgress/u);
  assert.doesNotMatch(evidenceContract, /Narrative|ReviewComment|Description|EvidenceReference/u);
  assert.match(fieldModule, /IProgressEvidenceReportingSource, ProgressEvidenceReportingSource/u);
  assert.match(sourceContract, /pmcs\.planning\.project-progress-reporting\/v1/u);
  assert.match(sourceContract, /MaximumBaselineEntries = 5_000/u);
  assert.match(sourceContract, /MaximumCurvePoints = 366/u);
  assert.match(sourceContract, /interface IProjectProgressReportingSource/u);
  assert.match(selector, /baseline\.overlap/u);
  assert.match(selector, /baseline\.target_snapshot\.missing/u);
  assert.match(selector, /ProjectProgressCanonicalJson\.Sha256/u);
  assert.match(calculator, /Math\.Min\(100m, item\.ActualPercent/u);
  assert.match(calculator, /actual\.Value - planned\.Value/u);
  assert.match(calculator, /UniformCurveGridPoints/u);
  assert.match(calculator, /pointDate <= selection\.CutoffLocalDate/u);
  assert.doesNotMatch(calculator, /Forecast|EarnedValue|Finance|Commercial/u);
  assert.match(source, /configuration_history\.unavailable/u);
  assert.match(source, /baseline_history\.unavailable/u);
  assert.match(source, /milestone_history\.unavailable/u);
  assert.match(source, /target_history\.unavailable/u);
  assert.match(source, /IProgressEvidenceReportingSource/u);
  assert.doesNotMatch(source, /IProgressFactSource/u);
  assert.match(planningModule, /IProjectProgressReportingSource, ProjectProgressReportingSource/u);
  assert.match(identity, /project-progress-certified/u);
  assert.match(identity, /pmcs\.reporting\.project-progress\.snapshot\/v1/u);
  assert.match(identity, /TemplateVersion = "1\.0\.0"/u);
  assert.match(identity, /3f19d880a7790854fcc0d79d4822c5653cb6bb888294eadf8eaeeee8b5857816/u);
  assert.match(identity, /pmcs\.reporting\.project-progress\.renderer\/v1/u);
  assert.match(identity, /pmcs\.reporting\.project-progress\.layout\/v1/u);
  assert.match(builder, /ProjectProgressReportingContract\.Version/u);
  assert.match(builder, /CanonicalJson\.Sha256/u);
  assert.doesNotMatch(builder, /DbContext|ExecuteSql|IProgressFactSource|Forecast|EarnedValue/u);
  assert.match(reportingProject, /Pmcs\.Modules\.Planning\/Pmcs\.Modules\.Planning\.csproj/u);
  assert.match(tests, /DailyReportCorrectionOfficialAfterCutoffDoesNotRewriteHistoricalEvidence/u);
  assert.match(tests, /OverlappingEffectiveBaselinesFailClosedWithoutLatestTieBreak/u);
  assert.match(tests, /QuantityOverrunIsPreservedOnRowButCappedOnlyInWeightedAggregate/u);
  assert.match(tests, /LongCurveUsesUniform365GridPlusCutoffAndNeverExceeds366Points/u);
  assert.match(tests, /TwinRunsWithDifferentQueryOrderRunIdAndBuildTimeAreDeterministic/u);
  assert.match(renderingContracts, /ProjectProgressReportRenderModel/u);
  assert.match(renderingContracts, /ProjectProgressReportRenderingContract\.ValidateRequest/u);
  assert.match(renderingContracts, /MaximumEntryTitleLength = 400/u);
  assert.match(pdfRenderer, /PageSizes\.A4\.Landscape\(\)/u);
  assert.match(pdfRenderer, /ContentFromRightToLeft/u);
  assert.match(pdfRenderer, /GenerateImages/u);
  assert.doesNotMatch(pdfRenderer, /Forecast|EarnedValue|CompositeHealth/u);
  assert.match(xlsxRenderer, /CompressionLevel\.NoCompression/u);
  assert.match(xlsxRenderer, /MetadataSheet\(model\)[\s\S]*LineageSheet\(model\)/u);
  assert.match(xlsxRenderer, /rightToLeft/u);
  assert.match(rendererRegistry, /ProjectProgressReportRendererRegistry/u);
  assert.match(renderingTests, /8a1866b7bdb3b9cb96d83a1897d80db4584c1590b3727e9ebb6a17856d672fb7/u);
  assert.match(renderingTests, /bdc9c3a99c1dc5a0da57f9431d7bc7f04830fbbfbeb578b24c7234df708785ef/u);
  assert.match(renderingTests, /NoDataWorkbookKeepsSemanticSheetsHeaderOnly/u);
  assert.match(renderingTests, /RendererRejectsOversizedTextAndFutureActual/u);

  assert.match(migration, /public long Order => 1204/u);
  assert.match(migration, /public string Version => "20260920-005"/u);
  assert.match(migration, /project-progress-certified/u);
  assert.match(migration, /pmcs\.reporting\.project-progress\.renderer\/v1/u);
  assert.match(migration, /3f19d880a7790854fcc0d79d4822c5653cb6bb888294eadf8eaeeee8b5857816/u);
  assert.match(migration, /planning\.progress\.read/u);
  assert.match(migration, /planning\.baselines\.read/u);
  assert.match(migration, /planning\.milestones\.read/u);
  assert.match(policy, /ProjectProgressReportRuntimeContract\.DefinitionCode/u);
  assert.match(policy, /TryGetSourcePermissions/u);
  assert.match(policy, /ProjectProgressSourcePermissions/u);
  assert.match(module, /IProjectProgressReportRenderer, ProjectProgressReportPdfRenderer/u);
  assert.match(module, /IProjectProgressReportRenderer, ProjectProgressReportXlsxRenderer/u);
  assert.match(module, /ProjectProgressReportCatalogMigration/u);
  assert.match(worker, /IProjectProgressReportingSource/u);
  assert.match(worker, /ProjectProgressReportSnapshotBuilder\.Build/u);
  assert.match(worker, /ProjectProgressReportRendererRegistry/u);
  assert.match(worker, /ProjectProgressReportRenderSnapshot\.Parse/u);
  assert.match(worker, /ProjectProgressReportRenderRequest/u);
  assert.match(endpoints, /ProjectProgressReportRuntimeContract\.DefinitionCode/u);
  assert.match(endpoints, /ParseProjectProgressParameters/u);
  assert.match(endpoints, /ProjectProgressPinnedProjectProfile\.Capture/u);
  assert.match(endpoints, /HasAllPermissionsAsync/u);
  assert.match(readService, /RequireSourcePermissions/u);
  assert.match(readService, /HasSourcePermissionAsync/u);
  assert.match(harness, /VerifyReportingProjectProgressAsync/u);
  assert.match(harness, /requires-all-source-permissions/u);
  assert.match(harness, /strict-empty-object/u);
  assert.match(harnessProgram, /verify-reporting-project-progress/u);
  assert.match(diagnostics, /verify-reporting-project-progress/u);
  assert.equal(settings.ReportingCenter.Phase1Enabled, false);
  assert.equal(settings.ReportingCenter.OutputAccessEnabled, false);
  assert.equal(settings.ReportingCenter.WorkerEnabled, false);
});

test("RPT1-F04 Runtime Core records the S07-MS11 safe checkpoint without opening renderer or wiring", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-07-ms11-candidate.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  const canonical = read("docs/PMCS-CANONICAL-PROJECT-REFERENCE.md");
  const architecture = read("docs/architecture/pmcs-v1.1-reporting-center-phase1.md");
  const contract = read(
    "docs/architecture/pmcs-v1.1-rpt1-f04-progress-curve-semantic-contract.md",
  );
  const baseline = read("docs/governance/pmcs-v1.1-development-baseline.md");
  const matrix = read("docs/qa/pmcs-v1.1-rpt1-test-matrix.md");
  const rootReadme = read("README.md");

  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S07-MS11-C1/u);
  assert.match(checkpoint, /517a7dba5c20061fab3ef6f9a3dd6407cc76499f/u);
  assert.match(checkpoint, /b96c3f23d73114555d25088335f73ca79f6e3668/u);
  assert.match(checkpoint, /deb1571ec66d820868e8f4b77b631471e3c8207c/u);
  assert.match(checkpoint, /9e41495a357480af03f1555ef640962ab863d332/u);
  assert.match(checkpoint, /f0d3a5550d9bd1c10d8ddd5a3c0ada24eb0fead5/u);
  assert.match(checkpoint, /Run 163 \(`35527577826`\)/u);
  assert.match(checkpoint, /`412\/412` تست C#/u);
  assert.match(checkpoint, /`25\/25` case متمرکز F04/u);
  assert.match(checkpoint, /`69\/69` تست قراردادی Node/u);
  assert.match(checkpoint, /`139\/139` تست Web/u);
  assert.match(checkpoint, /`378` فایل C#/u);
  assert.match(checkpoint, /`274` endpoint، `204` mutation و `5`/u);
  assert.match(checkpoint, /Restore Drill کامل `45` Migration/u);
  assert.match(checkpoint, /`7\/7` Suite و `12\/12` Command/u);
  assert.match(checkpoint, /Qualification artifact: `10610087527`/u);
  assert.match(checkpoint, /sha256:c5aedf39da9ff26acf0229022be0fc2c07ac4276feea2ea963bfbcafef085aef/u);
  assert.match(checkpoint, /Integration artifact: `10609649369`/u);
  assert.match(checkpoint, /sha256:3bd0caa9ce75948cee75835844e5313e4d3aed971d1d843c6b7fb27d45c44fab/u);
  assert.match(checkpoint, /UI-E2E artifact: `10609914179`/u);
  assert.match(checkpoint, /sha256:4ff0672c2ff02f142a4dfc901e563bcaed6249c14ca3d4b4bdbd8c0aac5726f2/u);
  assert.match(checkpoint, /هیچ Migration، endpoint، Catalog\/Template seed، Worker dispatch، Renderer/u);
  assert.match(checkpoint, /هیچ گزارش F04 هنوز از API قابل ایجاد، retry، مشاهده، verify یا دانلود نیست/u);
  assert.match(checkpoint, /Safe Resume Point اکنون `PMCS-V1\.1-RPT1-S07-MS11-C1`/u);
  assert.match(checkpoint, /Micro-Step بعدی فقط قرارداد[\s\S]*Template\/Renderer\/Layout/u);
  assert.match(roadmap, /نسخه سند: `1\.51\.0`/u);
  assert.match(roadmap, /\| `1\.43\.0` \| ثبت Safe Checkpoint `S07-MS11`/u);
  assert.match(registry, /Slice 07 MS11[\s\S]*deb1571ec66d820868e8f4b77b631471e3c8207c[\s\S]*Run 163/u);
  assert.match(canonical, /F04 Runtime Core Source:[\s\S]*deb1571ec66d820868e8f4b77b631471e3c8207c[\s\S]*Run 163/u);
  assert.match(canonical, /Safe Resume Point قطعی فعلی آن `PMCS-V1\.1-RPT1-S07-MS17-C1`/u);
  assert.match(architecture, /نسخه: `1\.31\.0`[\s\S]*Run 163/u);
  assert.match(contract, /نسخه: `1\.3\.1`[\s\S]*Checkpoint `S07-MS11`[\s\S]*Run 163/u);
  assert.match(baseline, /Slice 07 Micro-Step 11[\s\S]*Run 163/u);
  assert.match(matrix, /## ۳۳\.[\s\S]*Run 163/u);
  assert.match(matrix, /نسخه: `1\.34\.0`/u);
  assert.match(rootReadme, /F04 Runtime Core Safe Checkpoint:[^\n]*v1\.1-rpt1-slice-07-ms11/u);
});

test("RPT1-F04 Renderer and Golden record the S07-MS12 safe checkpoint without opening wiring", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-07-ms12-candidate.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  const canonical = read("docs/PMCS-CANONICAL-PROJECT-REFERENCE.md");
  const architecture = read("docs/architecture/pmcs-v1.1-reporting-center-phase1.md");
  const contract = read(
    "docs/architecture/pmcs-v1.1-rpt1-f04-progress-curve-semantic-contract.md",
  );
  const baseline = read("docs/governance/pmcs-v1.1-development-baseline.md");
  const matrix = read("docs/qa/pmcs-v1.1-rpt1-test-matrix.md");
  const rootReadme = read("README.md");

  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S07-MS12-C1/u);
  assert.match(checkpoint, /bbe34ee57b9c96c0b075444e3b7757aa147b5770/u);
  assert.match(checkpoint, /59b35b123fb45e97597f082e85069e040ae99e9c/u);
  assert.match(checkpoint, /6a717f10e4bff167ad7e2643313008f5afcc8264/u);
  assert.match(checkpoint, /995c7fae108bbb5265faa036f951036d36e7061e/u);
  assert.match(checkpoint, /782f42ff73425cf5cad69b0635bacf05790d2ff1/u);
  assert.match(checkpoint, /Run 167 \(`35532522587`\)/u);
  assert.match(checkpoint, /`418\/418` تست C#/u);
  assert.match(checkpoint, /شش case Renderer\/Golden تازه/u);
  assert.match(checkpoint, /`31\/31` case متمرکز F04/u);
  assert.match(checkpoint, /`70\/70` تست قراردادی Node/u);
  assert.match(checkpoint, /`139\/139` تست Web/u);
  assert.match(checkpoint, /`381` فایل C#/u);
  assert.match(checkpoint, /`274` endpoint، `204` mutation و `5`/u);
  assert.match(checkpoint, /Restore Drill کامل `45` Migration/u);
  assert.match(checkpoint, /`7\/7` Suite و `12\/12` Command/u);
  assert.match(checkpoint, /Qualification artifact: `10611741833`/u);
  assert.match(checkpoint, /sha256:d0f0c1e6b7e580823a9f07d8adcaf4bf6ada44e0712d7f843ea53ce662e9cc00/u);
  assert.match(checkpoint, /Integration artifact: `10611777630`/u);
  assert.match(checkpoint, /sha256:71a090f4c49a39a24803948a248aa0cec45c1a16316b1730ae62c9a6423b03f6/u);
  assert.match(checkpoint, /UI-E2E artifact: `10611587190`/u);
  assert.match(checkpoint, /sha256:720ca87b8e30b3904cd327e345f086262587f7824e0c16e550eedc99f837e423/u);
  assert.match(checkpoint, /8a1866b7bdb3b9cb96d83a1897d80db4584c1590b3727e9ebb6a17856d672fb7/u);
  assert.match(checkpoint, /bdc9c3a99c1dc5a0da57f9431d7bc7f04830fbbfbeb578b24c7234df708785ef/u);
  assert.match(checkpoint, /556d3b6a56d59e0c4ac8e8fcd526fca405fe9ba066ae5823b2c9c7482ea4b69b/u);
  assert.match(checkpoint, /8fbb9b69d9322522e8a55f041e16c8785716dc7554660bf2eacdf9030fe5e850/u);
  assert.match(checkpoint, /هیچ endpoint تازه، Migration، Catalog\/Template seed/u);
  assert.match(checkpoint, /هیچ گزارش F04 هنوز از API قابل ایجاد، retry، مشاهده، verify یا دانلود نیست/u);
  assert.match(checkpoint, /Safe Resume Point اکنون `PMCS-V1\.1-RPT1-S07-MS12-C1`/u);
  assert.match(checkpoint, /Micro-Step بعدی فقط Catalog\/Template[\s\S]*Worker wiring/u);
  assert.match(roadmap, /نسخه سند: `1\.51\.0`/u);
  assert.match(roadmap, /\| `1\.45\.0` \| ثبت Safe Checkpoint `S07-MS12`/u);
  assert.match(registry, /Slice 07 MS12[\s\S]*6a717f10e4bff167ad7e2643313008f5afcc8264[\s\S]*Run 167/u);
  assert.match(canonical, /F04 Renderer\/Golden Source:[\s\S]*6a717f10e4bff167ad7e2643313008f5afcc8264[\s\S]*Run 167/u);
  assert.match(canonical, /Safe Resume Point قطعی فعلی آن `PMCS-V1\.1-RPT1-S07-MS17-C1`/u);
  assert.match(architecture, /نسخه: `1\.31\.0`[\s\S]*Run 167/u);
  assert.match(contract, /نسخه: `1\.3\.1`[\s\S]*Checkpointed in Run 167/u);
  assert.match(baseline, /Slice 07 Micro-Step 12[\s\S]*Run 167/u);
  assert.match(matrix, /## ۳۴\.[\s\S]*Run 167/u);
  assert.match(matrix, /نسخه: `1\.34\.0`/u);
  assert.match(rootReadme, /F04 Renderer\/Golden Safe Checkpoint:[^\n]*v1\.1-rpt1-slice-07-ms12/u);
});

test("RPT1-F04 Catalog API and Worker record the S07-MS13 connected safe checkpoint", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-07-ms13-candidate.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  const canonical = read("docs/PMCS-CANONICAL-PROJECT-REFERENCE.md");
  const architecture = read("docs/architecture/pmcs-v1.1-reporting-center-phase1.md");
  const contract = read(
    "docs/architecture/pmcs-v1.1-rpt1-f04-progress-curve-semantic-contract.md",
  );
  const baseline = read("docs/governance/pmcs-v1.1-development-baseline.md");
  const matrix = read("docs/qa/pmcs-v1.1-rpt1-test-matrix.md");
  const api = read("docs/api/reporting-v1.md");
  const security = read("docs/security/pmcs-v1.1-reporting-security.md");
  const rootReadme = read("README.md");
  const validator = read("tools/validate-repository.mjs");

  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S07-MS13-C1/u);
  assert.match(checkpoint, /09fbcf391ba81af73b7cf1b1cb74726079e3032d/u);
  assert.match(checkpoint, /524d518da501c1190f77b3c3c9cf4da1f72be758/u);
  assert.match(checkpoint, /4c48c03aad126a594e5328fc7995a72728ba2274/u);
  assert.match(checkpoint, /49f957729fdccb0397dd153b93135ce2eaddd68a/u);
  assert.match(checkpoint, /05ca8ac7e3fa643e111b9c8511e3e08d62be60a5/u);
  assert.match(checkpoint, /Run 169 \(`35535904655`\)/u);
  assert.match(checkpoint, /`419\/419` تست C#/u);
  assert.match(checkpoint, /`15\/15` assertion/u);
  assert.match(checkpoint, /`71\/71` تست قراردادی Node/u);
  assert.match(checkpoint, /`139\/139` تست Web/u);
  assert.match(checkpoint, /`382` فایل C#/u);
  assert.match(checkpoint, /`274` endpoint، `204` mutation و `5`/u);
  assert.match(checkpoint, /Restore Drill کامل `46`[\s\S]*Migration/u);
  assert.match(checkpoint, /`7\/7` Suite و `12\/12` Command/u);
  assert.match(checkpoint, /Qualification artifact: `10613446342`/u);
  assert.match(checkpoint, /sha256:b254f06e6663f243ed2a69042d625e4467f03d0a1570bddddbee53f806eef55b/u);
  assert.match(checkpoint, /Integration artifact: `10613605950`/u);
  assert.match(checkpoint, /sha256:908054eb99778c981befe78e2c960bae7ef46ec2f1df291ca39a8a3e59d4f9d5/u);
  assert.match(checkpoint, /UI-E2E artifact: `10613036659`/u);
  assert.match(checkpoint, /sha256:a72471664fb355edfd74a66a2afd17e17a2168826f7c515654385be3df8fbce9/u);
  assert.match(checkpoint, /Phase1Enabled=false[\s\S]*PdfLicense=Unconfigured/u);
  assert.match(checkpoint, /Safe Resume Point اکنون `PMCS-V1\.1-RPT1-S07-MS13-C1`/u);
  assert.match(checkpoint, /Micro-Step بعدی باید فقط با DoR[\s\S]*`RPT1-F05`/u);
  assert.match(roadmap, /نسخه سند: `1\.51\.0`/u);
  assert.match(roadmap, /\| `1\.46\.0` \| ثبت Safe Checkpoint `S07-MS13`/u);
  assert.match(registry, /Slice 07 MS13[\s\S]*4c48c03aad126a594e5328fc7995a72728ba2274[\s\S]*Run 169/u);
  assert.match(canonical, /Safe Resume Point قطعی فعلی آن `PMCS-V1\.1-RPT1-S07-MS17-C1`/u);
  assert.match(canonical, /F04 Connected Source MS13:[\s\S]*Run 169/u);
  assert.match(architecture, /نسخه: `1\.31\.0`[\s\S]*Run 169/u);
  assert.match(contract, /نسخه: `1\.3\.1`[\s\S]*Run 169/u);
  assert.match(baseline, /Slice 07 Micro-Step 13[\s\S]*Run 169/u);
  assert.match(matrix, /## ۳۵\.[\s\S]*Run 169/u);
  assert.match(matrix, /نسخه: `1\.34\.0`/u);
  assert.match(api, /خانواده F04 روی API متصل — Safe Checkpoint/u);
  assert.match(security, /نسخه: `1\.16\.0`[\s\S]*Run 169/u);
  assert.match(rootReadme, /PMCS-V1\.1-RPT1-S07-MS17-C1[\s\S]*F01 تا F05 checkpoint متصل/u);
  assert.match(validator, /docs\/checkpoints\/v1\.1-rpt1-slice-07-ms13-candidate\.md/u);
});

test("RPT1-F05 keeps the certified financial position contract aligned with its connected Runtime and Renderer", () => {
  const contract = read(
    "docs/architecture/pmcs-v1.1-rpt1-f05-financial-position-semantic-contract.md",
  );
  const canonical = read("docs/PMCS-CANONICAL-PROJECT-REFERENCE.md");
  const architecture = read("docs/architecture/pmcs-v1.1-reporting-center-phase1.md");
  const api = read("docs/api/reporting-v1.md");
  const security = read("docs/security/pmcs-v1.1-reporting-security.md");
  const matrix = read("docs/qa/pmcs-v1.1-rpt1-test-matrix.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  const rootReadme = read("README.md");
  const module = read(`${moduleRoot}/ReportingModule.cs`);
  const worker = read(`${moduleRoot}/Services/ReportGenerationWorker.cs`);
  const endpoints = read(`${moduleRoot}/Endpoints/ReportingEndpoints.cs`);
  const migration = read(
    `${moduleRoot}/Migrations/ProjectFinancialPositionReportCatalogMigration.cs`,
  );
  const policy = read(`${moduleRoot}/Domain/ReportDefinitionRuntimePolicy.cs`);
  const readService = read(`${moduleRoot}/Services/ReportingReadService.cs`);
  const harness = read(
    "src/backend/Pmcs.TestHarness/ReportingProjectFinancialPositionVerification.cs",
  );
  const harnessProgram = read("src/backend/Pmcs.TestHarness/Program.cs");
  const diagnostics = read("tools/qa/seed-diagnostics.sh");
  const financialStateSource = read(
    "src/backend/Pmcs.Modules.Finance/Contracts/IFinancialStateSource.cs",
  );
  const financeControlSource = read(
    "src/backend/Pmcs.Modules.Finance/Services/FinanceControlReadService.cs",
  );
  const budget = read("src/backend/Pmcs.Modules.Finance/Domain/BudgetBaseline.cs");
  const settings = JSON.parse(read("src/backend/Pmcs.Api/appsettings.json"));

  assert.match(contract, /PMCS-RPT1-F05-SEMANTIC-001/u);
  assert.match(contract, /نسخه: `1\.3\.1`/u);
  assert.match(contract, /Connected Safe Checkpoint \| F06-F10\/UI\/Production open/u);
  assert.match(contract, /Parent checkpoint: `PMCS-V1\.1-RPT1-S07-MS16-C1`/u);
  assert.match(contract, /پارامتر معنایی Client دقیقاً یک object خالی `\{\}`/u);
  assert.match(contract, /Client نمی‌تواند Budget مطلوب، ارز، bucket یا cutoff محلی را انتخاب کند/u);
  assert.match(contract, /`postedAt <= sourceCutoffUtc`/u);
  assert.match(contract, /`transactionDate <= cutoffLocalDate`/u);
  assert.match(contract, /`externalNetCash = totalReceipts - directPayments - pettyCashFunding`/u);
  assert.match(contract, /`recognizedSpend = directPayments \+ pettyCashExpenses`/u);
  assert.match(contract, /Funding تنخواه هزینه نیست/u);
  assert.match(contract, /هیچ رکورد رسمی واجد شرایط وجود نداشته باشد[\s\S]*همهٔ metricهای Cash برابر `null`/u);
  assert.match(contract, /`outstandingAmount = obligationAmount - Σ eligibleSettlementAmount`/u);
  assert.match(contract, /`NotDue`[\s\S]*`Overdue1To30`[\s\S]*`Overdue31To60`[\s\S]*`Overdue61Plus`/u);
  assert.match(contract, /Payable و Receivable همیشه جدا[\s\S]*net نمی‌شوند/u);
  assert.match(contract, /سررسید دقیقاً در روز cutoff دیرکرد نیست/u);
  assert.match(contract, /`approvedAt <= sourceCutoffUtc`/u);
  assert.match(contract, /`sourceCutoffUtc < supersededAt`/u);
  assert.match(contract, /حداکثر یک Baseline مؤثر[\s\S]*overlap[\s\S]*processing failure/u);
  assert.match(contract, /`budgetRemainingAmount = approvedBudgetAmount - recognizedSpend`/u);
  assert.match(contract, /درصد بالاتر از ۱۰۰ و مانده منفی cap/u);
  assert.match(contract, /`NotConfigured`[\s\S]*`NoData`[\s\S]*`InsufficientData`[\s\S]*`Available`/u);
  assert.match(contract, /`FinancialSourceIncomplete`[\s\S]*`ObligationSettlementLineageIncomplete`/u);
  assert.match(contract, /`financial-state\.read`[\s\S]*`finance\.records\.read`[\s\S]*`finance\.obligations\.read`[\s\S]*`budget\.baselines\.read`/u);
  assert.match(contract, /حداقل `Confidential`/u);
  assert.match(contract, /`FinanceDbContext`[\s\S]*`GET \/finance\/state`[\s\S]*`IFinancialStateSource`[\s\S]*`IFinanceControlReadService`/u);
  assert.match(contract, /`IFinancialStateSource\.GetCurrentAsync` فقط آخرین Snapshot/u);
  assert.match(contract, /`BudgetBaseline\.ReviewedAt` هنگام Supersede بازنویسی می‌شود/u);
  assert.match(contract, /`FinanceControlCalculator` Aging bucketها را میان Payable\/Receivable جمع می‌کند/u);
  assert.match(contract, /`ManagementFeePolicy`[\s\S]*خارج از Snapshot/u);
  assert.equal((contract.match(/\| `F05-[A-Z]{1,2}\d{2}` \|/gu) ?? []).length, 25);
  assert.match(contract, /Migration 47[\s\S]*strict API[\s\S]*Worker\/Renderer dispatch متصل/u);

  assert.match(architecture, /PMCS-RPT1-F05-SEMANTIC-001 v1\.3\.1/u);
  assert.match(api, /خانواده F05 روی API متصل — Safe Checkpoint/u);
  assert.match(security, /سیاست ثابت F05/u);
  assert.match(matrix, /## ۳۶\.[\s\S]*Golden matrix دقیقاً بیست‌وپنج‌سناریویی/u);
  assert.match(roadmap, /نسخه سند: `1\.51\.0`/u);
  assert.match(registry, /Slice 07 MS14[\s\S]*Run 171[\s\S]*Runtime not implemented/u);
  assert.match(canonical, /`RPT1-F01` تا `RPT1-F05` checkpoint متصل دارند[\s\S]*F06 تا F10[\s\S]*`Required \/ Not Implemented`/u);
  assert.match(rootReadme, /F05 متصل و checkpointed برای وضعیت مالی/u);
  assert.match(financialStateSource, /GetCurrentAsync/u);
  assert.doesNotMatch(financialStateSource, /sourceCutoffUtc|SourceManifest|Classification/u);
  assert.match(financeControlSource, /clock\.UtcNow/u);
  assert.match(financeControlSource, /FinanceDbContext/u);
  assert.match(budget, /void Supersede[\s\S]*ReviewedAt = reviewedAt/u);

  assert.match(migration, /public long Order => 1205/u);
  assert.match(migration, /public string Version => "20260921-006"/u);
  assert.match(migration, /project-financial-position-certified/u);
  assert.match(migration, /e6ad4cbf2559d825d70b1579e687e7f9ce15020afaf697692e263f18480f18e4/u);
  assert.match(migration, /financial-state\.read/u);
  assert.match(migration, /finance\.records\.read/u);
  assert.match(migration, /finance\.obligations\.read/u);
  assert.match(migration, /budget\.baselines\.read/u);
  assert.match(policy, /ProjectFinancialPositionReportRuntimeContract\.DefinitionCode/u);
  assert.match(policy, /ProjectFinancialPositionSourcePermissions/u);
  assert.match(module, /IProjectFinancialPositionReportRenderer, ProjectFinancialPositionReportPdfRenderer/u);
  assert.match(module, /IProjectFinancialPositionReportRenderer, ProjectFinancialPositionReportXlsxRenderer/u);
  assert.match(module, /ProjectFinancialPositionReportCatalogMigration/u);
  assert.match(worker, /IProjectFinancialPositionReportingSource/u);
  assert.match(worker, /ProjectFinancialPositionReportSnapshotBuilder\.Build/u);
  assert.match(worker, /ProjectFinancialPositionReportRendererRegistry/u);
  assert.match(worker, /ProjectFinancialPositionReportRenderSnapshot\.Parse/u);
  assert.match(worker, /ProjectFinancialPositionReportRenderRequest/u);
  assert.match(endpoints, /ParseProjectFinancialPositionParameters/u);
  assert.match(endpoints, /ProjectFinancialPositionPinnedProjectProfile\.Capture/u);
  assert.match(endpoints, /HasAllPermissionsAsync/u);
  assert.match(readService, /RequireSourcePermissions/u);
  assert.match(readService, /HasSourcePermissionAsync/u);
  assert.match(harness, /VerifyReportingProjectFinancialPositionAsync/u);
  assert.match(harness, /requires-all-source-permissions/u);
  assert.match(harness, /strict-empty-object/u);
  assert.match(harnessProgram, /verify-reporting-project-financial-position/u);
  assert.match(diagnostics, /verify-reporting-project-financial-position/u);
  assert.equal(settings.ReportingCenter.Phase1Enabled, false);
  assert.equal(settings.ReportingCenter.OutputAccessEnabled, false);
  assert.equal(settings.ReportingCenter.WorkerEnabled, false);
});

test("RPT1-F05 bounded Runtime Core is versioned and connected only through its certified API and Worker path", () => {
  const financeContract = read(
    "src/backend/Pmcs.Modules.Finance/Contracts/IProjectFinancialPositionReportingSource.cs",
  );
  const selector = read(
    "src/backend/Pmcs.Modules.Finance/Services/ProjectFinancialPositionReportingSelector.cs",
  );
  const calculator = read(
    "src/backend/Pmcs.Modules.Finance/Services/ProjectFinancialPositionReportingCalculator.cs",
  );
  const compatibility = read(
    "src/backend/Pmcs.Modules.Finance/Services/ProjectFinancialPositionReportingSource.cs",
  );
  const financeModule = read("src/backend/Pmcs.Modules.Finance/FinanceModule.cs");
  const identity = read(
    `${moduleRoot}/Domain/ProjectFinancialPositionReportRuntimeContract.cs`,
  );
  const semantic = read(
    `${moduleRoot}/Domain/ProjectFinancialPositionReportSemanticModels.cs`,
  );
  const builder = read(
    `${moduleRoot}/Services/ProjectFinancialPositionReportSnapshotBuilder.cs`,
  );
  const reportingProject = read(`${moduleRoot}/Pmcs.Modules.Reporting.csproj`);
  const module = read(`${moduleRoot}/ReportingModule.cs`);
  const worker = read(`${moduleRoot}/Services/ReportGenerationWorker.cs`);
  const endpoints = read(`${moduleRoot}/Endpoints/ReportingEndpoints.cs`);
  const tests = read("tests/Pmcs.Domain.Tests/ProjectFinancialPositionReportingTests.cs");
  const settings = JSON.parse(read("src/backend/Pmcs.Api/appsettings.json"));

  assert.match(financeContract, /pmcs\.finance\.project-financial-position-reporting\/v1/u);
  assert.match(financeContract, /pmcs\.finance\.project-financial-position-manifest\/v1/u);
  assert.match(financeContract, /MaximumFinancialRecords = 100_000/u);
  assert.match(financeContract, /MaximumObligations = 20_000/u);
  assert.match(financeContract, /MaximumSettlements = 100_000/u);
  assert.match(financeContract, /LedgerCompleteness/u);
  assert.match(financeContract, /SettlementLineageCompleteness/u);
  assert.match(selector, /item\.PostedAt!\.Value <= cutoff/u);
  assert.match(selector, /item\.TransactionDate <= cutoffLocalDate/u);
  assert.match(selector, /cutoff < item\.SupersededAt\.Value/u);
  assert.match(selector, /budget\.overlap/u);
  assert.match(selector, /record\.currency_mismatch/u);
  assert.match(selector, /settlement\.overallocated/u);
  assert.match(selector, /ProjectFinancialPositionCanonicalJson\.Sha256/u);
  assert.match(calculator, /receipts - payments - funding/u);
  assert.match(calculator, /payments \+ expenses/u);
  assert.match(calculator, /cutoffLocalDate\.DayNumber - dueDate\.DayNumber/u);
  assert.match(calculator, /MidpointRounding\.AwayFromZero/u);
  assert.match(calculator, /FinancialObligationType\.Payable/u);
  assert.match(calculator, /FinancialObligationType\.Receivable/u);
  assert.doesNotMatch(calculator, /Forecast|GeneralLedger|ManagementFee|CompositeHealth/u);
  assert.match(compatibility, /configuration_history\.unavailable/u);
  assert.match(compatibility, /budget_history\.unavailable/u);
  assert.match(compatibility, /record_history\.unavailable/u);
  assert.match(compatibility, /obligation_history\.unavailable/u);
  assert.match(financeModule, /IProjectFinancialPositionReportingSource, ProjectFinancialPositionReportingSource/u);
  assert.match(identity, /project-financial-position-certified/u);
  assert.match(identity, /pmcs\.reporting\.project-financial-position\.parameters\/v1/u);
  assert.match(identity, /pmcs\.reporting\.project-financial-position\.snapshot\/v1/u);
  assert.match(identity, /pmcs\.reporting\.project-financial-position\.project-profile\/v1/u);
  assert.match(identity, /CapturedAtUtc/u);
  assert.match(semantic, /ProjectFinancialPositionReportSemanticSnapshot/u);
  assert.match(semantic, /PayableSummary/u);
  assert.match(semantic, /ReceivableSummary/u);
  assert.match(builder, /ProjectFinancialPositionReportingContract\.Version/u);
  assert.match(builder, /CanonicalJson\.Sha256/u);
  assert.doesNotMatch(
    builder,
    /FinanceDbContext|ProjectsDbContext|IFinancialStateSource|IFinanceControlReadService|ExecuteSql/u,
  );
  assert.match(reportingProject, /Pmcs\.Modules\.Finance\/Pmcs\.Modules\.Finance\.csproj/u);
  assert.match(tests, /PostedAndTransactionDatesBothBoundOfficialCashSelection/u);
  assert.match(tests, /AgingBoundaryDaysMapToExactlyOneCanonicalBucket/u);
  assert.match(tests, /TwinRunsIgnoreQueryOrderRunIdentityAndBuildTime/u);
  assert.match(tests, /CompatibilityProjectionRejectsLegacySupersededBudgetHistory/u);

  assert.match(module, /ProjectFinancialPositionReportRendererRegistry/u);
  assert.match(worker, /IProjectFinancialPositionReportingSource/u);
  assert.match(worker, /ProjectFinancialPositionReportSnapshotBuilder\.Build/u);
  assert.match(endpoints, /ProjectFinancialPositionReportRuntimeContract\.DefinitionCode/u);
  assert.match(endpoints, /ParseProjectFinancialPositionParameters/u);
  assert.equal(settings.ReportingCenter.Phase1Enabled, false);
  assert.equal(settings.ReportingCenter.OutputAccessEnabled, false);
  assert.equal(settings.ReportingCenter.WorkerEnabled, false);
});

test("RPT1-F05 certified renderer is versioned deterministic and connected through the pinned registry", () => {
  const identity = read(
    `${moduleRoot}/Domain/ProjectFinancialPositionReportRuntimeContract.cs`,
  );
  const renderingContracts = read(
    `${moduleRoot}/Rendering/ProjectFinancialPositionReportRenderingContracts.cs`,
  );
  const pdfRenderer = read(
    `${moduleRoot}/Rendering/ProjectFinancialPositionReportPdfRenderer.cs`,
  );
  const xlsxRenderer = read(
    `${moduleRoot}/Rendering/ProjectFinancialPositionReportXlsxRenderer.cs`,
  );
  const rendererRegistry = read(`${moduleRoot}/Rendering/ReportRendererRegistry.cs`);
  const renderingTests = read(
    "tests/Pmcs.Domain.Tests/ProjectFinancialPositionReportRenderingTests.cs",
  );
  const module = read(`${moduleRoot}/ReportingModule.cs`);
  const worker = read(`${moduleRoot}/Services/ReportGenerationWorker.cs`);
  const endpoints = read(`${moduleRoot}/Endpoints/ReportingEndpoints.cs`);
  const settings = JSON.parse(read("src/backend/Pmcs.Api/appsettings.json"));

  assert.match(identity, /TemplateVersion = "1\.0\.0"/u);
  assert.match(
    identity,
    /e6ad4cbf2559d825d70b1579e687e7f9ce15020afaf697692e263f18480f18e4/u,
  );
  assert.match(
    identity,
    /pmcs\.reporting\.project-financial-position\.renderer\/v1/u,
  );
  assert.match(
    identity,
    /pmcs\.reporting\.project-financial-position\.layout\/v1/u,
  );
  assert.match(renderingContracts, /ProjectFinancialPositionReportRenderModel/u);
  assert.match(
    renderingContracts,
    /ProjectFinancialPositionReportRenderingContract\.ValidateRequest/u,
  );
  assert.match(renderingContracts, /MaximumCounterpartyLength = 200/u);
  assert.match(pdfRenderer, /PageSizes\.A4\.Landscape\(\)/u);
  assert.match(pdfRenderer, /ContentFromRightToLeft/u);
  assert.match(pdfRenderer, /GenerateImages/u);
  assert.doesNotMatch(pdfRenderer, /IFinancialStateSource|IFinanceControlReadService/u);
  assert.match(xlsxRenderer, /CompressionLevel\.NoCompression/u);
  assert.match(
    xlsxRenderer,
    /MetadataSheet\(model\)[\s\S]*LineageSheet\(model\)/u,
  );
  assert.match(xlsxRenderer, /rightToLeft/u);
  assert.match(rendererRegistry, /ProjectFinancialPositionReportRendererRegistry/u);
  assert.match(
    renderingTests,
    /CertifiedFinancialPositionXlsxIsDeterministicGoldenRtlFormulaFreeAndSemantic/u,
  );
  assert.match(
    renderingTests,
    /CertifiedFinancialPositionPdfIsDeterministicVisuallyPinnedAndWithinPerformanceBudget/u,
  );
  assert.match(
    renderingTests,
    /NoDataWorkbookKeepsFinancialSheetsHeaderOnlyAndDoesNotFabricateZeroMetrics/u,
  );
  assert.match(
    renderingTests,
    /cadb7f0dc5670f401df879f04efdd930cf799213194e7cdf43c0d5d5e75a6222/u,
  );
  assert.match(
    renderingTests,
    /25293911fd4eec21e9b2e2f62de9239d6f5d5bed8b4842a32c8d1483fc987d09/u,
  );
  assert.match(
    renderingTests,
    /44afd18ca0babb473b69911bf83d775dec57519c34c0237e471bebc9bdd439b7/u,
  );
  assert.match(
    renderingTests,
    /f8eb576d5e0cdfd267d80013d2fce3c8cb9f45ad18b54d0a37632a3b10358cbb/u,
  );

  assert.match(module, /IProjectFinancialPositionReportRenderer, ProjectFinancialPositionReportPdfRenderer/u);
  assert.match(module, /IProjectFinancialPositionReportRenderer, ProjectFinancialPositionReportXlsxRenderer/u);
  assert.match(worker, /ProjectFinancialPositionReportRendererRegistry/u);
  assert.match(worker, /ProjectFinancialPositionReportRenderRequest/u);
  assert.match(endpoints, /ProjectFinancialPositionReportRuntimeContract\.DefinitionCode/u);
  assert.equal(settings.ReportingCenter.Phase1Enabled, false);
  assert.equal(settings.ReportingCenter.OutputAccessEnabled, false);
  assert.equal(settings.ReportingCenter.WorkerEnabled, false);
});

test("RPT1-F05 Renderer and Golden record the S07-MS16 safe checkpoint without opening wiring", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-07-ms16-candidate.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  const canonical = read("docs/PMCS-CANONICAL-PROJECT-REFERENCE.md");
  const architecture = read("docs/architecture/pmcs-v1.1-reporting-center-phase1.md");
  const contract = read(
    "docs/architecture/pmcs-v1.1-rpt1-f05-financial-position-semantic-contract.md",
  );
  const baseline = read("docs/governance/pmcs-v1.1-development-baseline.md");
  const matrix = read("docs/qa/pmcs-v1.1-rpt1-test-matrix.md");
  const rootReadme = read("README.md");
  const validator = read("tools/validate-repository.mjs");

  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S07-MS16-C1/u);
  assert.match(checkpoint, /b1bfcc8b9257c11df106d55c5f0f3312685cc7ea/u);
  assert.match(checkpoint, /d5d4a17a93004b203ee35b5bb5e3ca094c5d7183/u);
  assert.match(checkpoint, /9ddf7f1d96324e7ffb22d2abec83071a6c087ec2/u);
  assert.match(checkpoint, /f873795dcb8893dc28f88d5e5fc8292c5201e1e4/u);
  assert.match(checkpoint, /72ab7827731fa763828c049be953ee9ca8c128a4/u);
  assert.match(checkpoint, /Run 178 \(`35558202348`\)/u);
  assert.match(checkpoint, /`456\/456` تست C#/u);
  assert.match(checkpoint, /شش case Renderer\/Golden تازه/u);
  assert.match(checkpoint, /`37\/37` case متمرکز F05/u);
  assert.match(checkpoint, /`77\/77` تست قراردادی Node/u);
  assert.match(checkpoint, /`139\/139` تست Web/u);
  assert.match(checkpoint, /`393` فایل C#/u);
  assert.match(checkpoint, /`274` endpoint، `204` mutation و `5`/u);
  assert.match(checkpoint, /Restore Drill کامل `46` Migration/u);
  assert.match(checkpoint, /`7\/7` Suite و `12\/12` Command/u);
  assert.match(checkpoint, /Qualification artifact: `10621253140`/u);
  assert.match(checkpoint, /sha256:2e4c0409a9cb1945f75823b2bbcc9a75a8f75279664406f4c2b3f5b2f13c21d5/u);
  assert.match(checkpoint, /Integration artifact: `10620593377`/u);
  assert.match(checkpoint, /sha256:ca287382f78bf5bd3ee07c14fe19c12afcf994ab075b811ba0f8ce01e1d7dee0/u);
  assert.match(checkpoint, /UI-E2E artifact: `10621228630`/u);
  assert.match(checkpoint, /sha256:7e508442c2c3be61b3c6dd3991883209b376ee604dc375afdd6baf69d6b5035c/u);
  assert.match(checkpoint, /cadb7f0dc5670f401df879f04efdd930cf799213194e7cdf43c0d5d5e75a6222/u);
  assert.match(checkpoint, /25293911fd4eec21e9b2e2f62de9239d6f5d5bed8b4842a32c8d1483fc987d09/u);
  assert.match(checkpoint, /44afd18ca0babb473b69911bf83d775dec57519c34c0237e471bebc9bdd439b7/u);
  assert.match(checkpoint, /f8eb576d5e0cdfd267d80013d2fce3c8cb9f45ad18b54d0a37632a3b10358cbb/u);
  assert.match(checkpoint, /هیچ endpoint تازه، Migration، Catalog\/Template seed/u);
  assert.match(checkpoint, /هیچ گزارش F05 هنوز از API قابل ایجاد، اجرا، retry، مشاهده، verify یا دانلود نیست/u);
  assert.match(checkpoint, /Safe Resume Point اکنون `PMCS-V1\.1-RPT1-S07-MS16-C1`/u);
  assert.match(checkpoint, /Micro-Step بعدی فقط Catalog\/Template[\s\S]*Worker dispatch متصل/u);
  assert.match(roadmap, /نسخه سند: `1\.51\.0`/u);
  assert.match(roadmap, /\| `1\.50\.0` \| ثبت Safe Checkpoint `S07-MS16`[\s\S]*Run 178/u);
  assert.match(registry, /Slice 07 MS16[\s\S]*9ddf7f1d96324e7ffb22d2abec83071a6c087ec2[\s\S]*Run 178/u);
  assert.match(canonical, /Safe Resume Point قطعی فعلی آن `PMCS-V1\.1-RPT1-S07-MS17-C1`/u);
  assert.match(canonical, /F05 Renderer\/Golden Source MS16:[\s\S]*Run 178/u);
  assert.match(canonical, /گام بعدی فقط DoR و قرارداد معنایی مستقل خانواده `RPT1-F06`/u);
  assert.match(architecture, /نسخه: `1\.31\.0`[\s\S]*Run 178/u);
  assert.match(contract, /نسخه: `1\.3\.1`[\s\S]*Connected Safe Checkpoint/u);
  assert.match(baseline, /Slice 07 Micro-Step 16[\s\S]*Run 178/u);
  assert.match(matrix, /## ۳۸\.[\s\S]*Run 178/u);
  assert.match(matrix, /نسخه: `1\.34\.0`/u);
  assert.match(rootReadme, /F05 Renderer\/Golden Safe Checkpoint:[^\n]*v1\.1-rpt1-slice-07-ms16-candidate/u);
  assert.match(validator, /docs\/checkpoints\/v1\.1-rpt1-slice-07-ms16-candidate\.md/u);
});

test("RPT1-F05 Catalog API and Worker record the S07-MS17 connected safe checkpoint", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-07-ms17-candidate.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  const canonical = read("docs/PMCS-CANONICAL-PROJECT-REFERENCE.md");
  const architecture = read("docs/architecture/pmcs-v1.1-reporting-center-phase1.md");
  const contract = read(
    "docs/architecture/pmcs-v1.1-rpt1-f05-financial-position-semantic-contract.md",
  );
  const baseline = read("docs/governance/pmcs-v1.1-development-baseline.md");
  const matrix = read("docs/qa/pmcs-v1.1-rpt1-test-matrix.md");
  const api = read("docs/api/reporting-v1.md");
  const security = read("docs/security/pmcs-v1.1-reporting-security.md");
  const rootReadme = read("README.md");
  const validator = read("tools/validate-repository.mjs");

  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S07-MS17-C1/u);
  assert.match(checkpoint, /81d8fb92f5ad79a402084f7eae9529fdb0fa3afc/u);
  assert.match(checkpoint, /d90e0780bfe88b60baeedecf23f03952573c957a/u);
  assert.match(checkpoint, /6de1e9ac3b457426be5e50064d1767106cd50c39/u);
  assert.match(checkpoint, /a4a8e8e655c56d05da2be5d87e7b84a9bb9a7a1f/u);
  assert.match(checkpoint, /cbd27a673b1887b2e245bef5340eb8199f480be4/u);
  assert.match(checkpoint, /Run 185 \(`35563055242`\)/u);
  assert.match(checkpoint, /`458\/458` تست C#/u);
  assert.match(checkpoint, /`39\/39` case متمرکز F05/u);
  assert.match(checkpoint, /`15\/15` assertion/u);
  assert.match(checkpoint, /`78\/78` تست قراردادی Node/u);
  assert.match(checkpoint, /`139\/139` تست Web/u);
  assert.match(checkpoint, /`394` فایل C#/u);
  assert.match(checkpoint, /`274` endpoint، `204` mutation و `5`/u);
  assert.match(checkpoint, /Restore Drill کامل `47`[\s\S]*Migration/u);
  assert.match(checkpoint, /`7\/7` Suite و `12\/12` Command/u);
  assert.match(checkpoint, /Qualification artifact: `10622488624`/u);
  assert.match(checkpoint, /sha256:afb1812b1ade77dedee3c754c92128ba11ed3e74168101c6657b0d6a1fe0617a/u);
  assert.match(checkpoint, /Integration artifact: `10622943057`/u);
  assert.match(checkpoint, /sha256:47d9e273327244374f42abf22f79cf4afed530b915a4b9f07ab3bb5a275f2793/u);
  assert.match(checkpoint, /UI-E2E artifact: `10622563776`/u);
  assert.match(checkpoint, /sha256:2b056ba9615319b3888e79d59dd5fcc0b6a57864265710bf71154544914be315/u);
  assert.match(checkpoint, /Phase1Enabled=false[\s\S]*PdfLicense=Unconfigured/u);
  assert.match(checkpoint, /Safe Resume Point اکنون `PMCS-V1\.1-RPT1-S07-MS17-C1`/u);
  assert.match(checkpoint, /Micro-Step بعدی باید فقط با DoR[\s\S]*`RPT1-F06`/u);
  assert.match(roadmap, /نسخه سند: `1\.51\.0`/u);
  assert.match(roadmap, /\| `1\.51\.0` \| ثبت Connected Safe Checkpoint `S07-MS17`[\s\S]*Run 185/u);
  assert.match(registry, /Slice 07 MS17[\s\S]*6de1e9ac3b457426be5e50064d1767106cd50c39[\s\S]*Run 185/u);
  assert.match(canonical, /Safe Resume Point قطعی فعلی آن `PMCS-V1\.1-RPT1-S07-MS17-C1`/u);
  assert.match(canonical, /F05 Connected Source MS17:[\s\S]*Run 185/u);
  assert.match(canonical, /گام بعدی فقط DoR و قرارداد معنایی مستقل خانواده `RPT1-F06`/u);
  assert.match(architecture, /نسخه: `1\.31\.0`[\s\S]*Run 185/u);
  assert.match(contract, /نسخه: `1\.3\.1`[\s\S]*Connected Safe Checkpoint/u);
  assert.match(baseline, /Slice 07 Micro-Step 17[\s\S]*Run 185/u);
  assert.match(matrix, /## ۳۹\.[\s\S]*Run 185/u);
  assert.match(matrix, /نسخه: `1\.34\.0`/u);
  assert.match(api, /خانواده F05 روی API متصل — Safe Checkpoint/u);
  assert.match(security, /نسخه: `1\.16\.0`[\s\S]*Run 185/u);
  assert.match(rootReadme, /PMCS-V1\.1-RPT1-S07-MS17-C1[\s\S]*F01 تا F05 checkpoint متصل/u);
  assert.match(validator, /docs\/checkpoints\/v1\.1-rpt1-slice-07-ms17-candidate\.md/u);
});

test("RPT1-F05 Runtime Core records the S07-MS15 safe checkpoint without opening renderer or wiring", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-07-ms15-candidate.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  const canonical = read("docs/PMCS-CANONICAL-PROJECT-REFERENCE.md");
  const architecture = read("docs/architecture/pmcs-v1.1-reporting-center-phase1.md");
  const contract = read(
    "docs/architecture/pmcs-v1.1-rpt1-f05-financial-position-semantic-contract.md",
  );
  const baseline = read("docs/governance/pmcs-v1.1-development-baseline.md");
  const matrix = read("docs/qa/pmcs-v1.1-rpt1-test-matrix.md");
  const rootReadme = read("README.md");
  const validator = read("tools/validate-repository.mjs");

  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S07-MS15-C1/u);
  assert.match(checkpoint, /ba1f00715ddb1359cfdb26acc5aa0c012e62a47e/u);
  assert.match(checkpoint, /172b7919181ea717e669b00cfee319849c00dcce/u);
  assert.match(checkpoint, /77ad46cbac12b899116516b0a58665ae888b3bf2/u);
  assert.match(checkpoint, /5c67523b0fbed8d521627fe406f74271a1bbdcfe/u);
  assert.match(checkpoint, /5f9ad7bd2bf0cb48c5a47dbfbe29ab09afc3f92c/u);
  assert.match(checkpoint, /Run 175 \(`35541740268`\)/u);
  assert.match(checkpoint, /`450\/450` تست C#/u);
  assert.match(checkpoint, /`31\/31` case متمرکز F05/u);
  assert.match(checkpoint, /`75\/75` تست قراردادی Node/u);
  assert.match(checkpoint, /`139\/139` تست Web/u);
  assert.match(checkpoint, /`390` فایل C#/u);
  assert.match(checkpoint, /`274` endpoint، `204` mutation و `5`/u);
  assert.match(checkpoint, /Restore Drill کامل `46` Migration/u);
  assert.match(checkpoint, /`7\/7` Suite و `12\/12` Command/u);
  assert.match(checkpoint, /Qualification artifact: `10615056923`/u);
  assert.match(checkpoint, /sha256:b0aab26340bb30c39005b128f643335b1a5ae6eb379972410cdc54b9119bc401/u);
  assert.match(checkpoint, /Integration artifact: `10614658300`/u);
  assert.match(checkpoint, /sha256:7326b21eb0d9858b8532ee687c69cce313f2656a153ff6f0c796d69dfdb4d5c8/u);
  assert.match(checkpoint, /UI-E2E artifact: `10615141576`/u);
  assert.match(checkpoint, /sha256:842a550a2146542ce5b7924e0c09f0fd9893cccc085f96fa7b8ec004874f3bc5/u);
  assert.match(checkpoint, /هیچ Migration، endpoint، Catalog\/Template seed، Worker dispatch، Renderer/u);
  assert.match(checkpoint, /هیچ گزارش F05 هنوز از API قابل ایجاد، اجرا، retry، مشاهده، verify یا دانلود نیست/u);
  assert.match(checkpoint, /Safe Resume Point اکنون `PMCS-V1\.1-RPT1-S07-MS15-C1`/u);
  assert.match(checkpoint, /Micro-Step بعدی فقط قرارداد[\s\S]*Template\/Renderer\/Layout/u);
  assert.match(roadmap, /نسخه سند: `1\.51\.0`/u);
  assert.match(roadmap, /\| `1\.49\.0` \| ثبت Safe Checkpoint `S07-MS15`[\s\S]*Run 175/u);
  assert.match(registry, /Slice 07 MS15[\s\S]*77ad46cbac12b899116516b0a58665ae888b3bf2[\s\S]*Run 175/u);
  assert.match(canonical, /Safe Resume Point قطعی فعلی آن `PMCS-V1\.1-RPT1-S07-MS17-C1`/u);
  assert.match(canonical, /F05 Runtime Core Source:[\s\S]*Run 175/u);
  assert.match(canonical, /گام بعدی فقط DoR و قرارداد معنایی مستقل خانواده `RPT1-F06`/u);
  assert.match(architecture, /نسخه: `1\.31\.0`[\s\S]*Run 175/u);
  assert.match(contract, /نسخه: `1\.3\.1`[\s\S]*Connected Safe Checkpoint/u);
  assert.match(baseline, /Slice 07 Micro-Step 15[\s\S]*Run 175/u);
  assert.match(matrix, /## ۳۷\.[\s\S]*Run 175/u);
  assert.match(matrix, /نسخه: `1\.34\.0`/u);
  assert.match(rootReadme, /F05 Runtime Core Safe Checkpoint:[^\n]*v1\.1-rpt1-slice-07-ms15-candidate/u);
  assert.match(validator, /docs\/checkpoints\/v1\.1-rpt1-slice-07-ms15-candidate\.md/u);
});

test("RPT1-F05 semantic contract preserves the historical S07-MS14 checkpoint", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-07-ms14-candidate.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  const canonical = read("docs/PMCS-CANONICAL-PROJECT-REFERENCE.md");
  const architecture = read("docs/architecture/pmcs-v1.1-reporting-center-phase1.md");
  const contract = read(
    "docs/architecture/pmcs-v1.1-rpt1-f05-financial-position-semantic-contract.md",
  );
  const baseline = read("docs/governance/pmcs-v1.1-development-baseline.md");
  const matrix = read("docs/qa/pmcs-v1.1-rpt1-test-matrix.md");
  const api = read("docs/api/reporting-v1.md");
  const security = read("docs/security/pmcs-v1.1-reporting-security.md");
  const rootReadme = read("README.md");
  const validator = read("tools/validate-repository.mjs");

  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S07-MS14-C1/u);
  assert.match(checkpoint, /4e50fcadeb11bf412eff9275b8fac8561fd7d097/u);
  assert.match(checkpoint, /82f09cfe36bfb725032f7397c4e3887c1ebf505c/u);
  assert.match(checkpoint, /72fa88349d01edd4c6455eb0af1aebfdeced8c35/u);
  assert.match(checkpoint, /d2722dd8fab797650ed0c9befb80df93fc0be135/u);
  assert.match(checkpoint, /6f1918ed1323fa3f6f14eeeaedcad8b2cf241ff7/u);
  assert.match(checkpoint, /Run 171 \(`35538654765`\)/u);
  assert.match(checkpoint, /`419\/419` تست C#/u);
  assert.match(checkpoint, /`73\/73` تست قراردادی Node/u);
  assert.match(checkpoint, /`139\/139` تست Web/u);
  assert.match(checkpoint, /`382` فایل C#/u);
  assert.match(checkpoint, /`274` endpoint، `204` mutation و `5`/u);
  assert.match(checkpoint, /Restore Drill کامل `46` Migration/u);
  assert.match(checkpoint, /`7\/7` Suite و `12\/12` Command/u);
  assert.match(checkpoint, /Qualification artifact: `10612744490`/u);
  assert.match(checkpoint, /sha256:88a62a1ccfd2a15b7d80d1f62fac888616762acde61c90d985da2bc5843e6c3c/u);
  assert.match(checkpoint, /Integration artifact: `10613816900`/u);
  assert.match(checkpoint, /sha256:fca6c121bebb10518db49907dae3af854f1ed33be7d0becc93695d0e2d88d417/u);
  assert.match(checkpoint, /UI-E2E artifact: `10613811669`/u);
  assert.match(checkpoint, /sha256:4adc0ac90be4059e78b3d2e2ac1860cf01979eddccd62c80d40125bff7c7f6e9/u);
  assert.match(checkpoint, /Golden matrix بیست‌وپنج‌سناریویی/u);
  assert.match(checkpoint, /هیچ Runtime Definition[\s\S]*Migration[\s\S]*Renderer F05/u);
  assert.match(checkpoint, /هیچ گزارش F05 هنوز از API قابل ایجاد، اجرا، retry، مشاهده، verify یا دانلود نیست/u);
  assert.match(checkpoint, /Safe Resume Point اکنون `PMCS-V1\.1-RPT1-S07-MS14-C1`/u);
  assert.match(checkpoint, /Micro-Step بعدی فقط Runtime identity[\s\S]*Application Contract\/Projection/u);
  assert.match(roadmap, /نسخه سند: `1\.51\.0`/u);
  assert.match(roadmap, /\| `1\.47\.0` \| ثبت Safe Checkpoint `S07-MS14`/u);
  assert.match(registry, /Slice 07 MS14[\s\S]*72fa88349d01edd4c6455eb0af1aebfdeced8c35[\s\S]*Run 171/u);
  assert.match(canonical, /Safe Resume Point قطعی فعلی آن `PMCS-V1\.1-RPT1-S07-MS17-C1`/u);
  assert.match(canonical, /F05 semantic contract Source:[\s\S]*Run 171/u);
  assert.match(canonical, /گام بعدی فقط DoR و قرارداد معنایی مستقل خانواده `RPT1-F06`/u);
  assert.match(architecture, /نسخه: `1\.31\.0`[\s\S]*Run 171/u);
  assert.match(contract, /نسخه: `1\.3\.1`[\s\S]*Connected Safe Checkpoint/u);
  assert.match(baseline, /Slice 07 Micro-Step 14[\s\S]*Run 171/u);
  assert.match(matrix, /## ۳۶\.[\s\S]*Run 171/u);
  assert.match(matrix, /نسخه: `1\.34\.0`/u);
  assert.match(api, /خانواده F05 روی API متصل — Safe Checkpoint/u);
  assert.match(security, /نسخه: `1\.16\.0`[\s\S]*Run 171/u);
  assert.match(rootReadme, /F05 Semantic Contract Safe Checkpoint:[\s\S]*v1\.1-rpt1-slice-07-ms14-candidate\.md/u);
  assert.match(validator, /docs\/checkpoints\/v1\.1-rpt1-slice-07-ms14-candidate\.md/u);
});

test("RPT1-F04 semantic contract records the S07-MS10 safe checkpoint without claiming Runtime", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-07-ms10-candidate.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  const canonical = read("docs/PMCS-CANONICAL-PROJECT-REFERENCE.md");
  const architecture = read("docs/architecture/pmcs-v1.1-reporting-center-phase1.md");
  const baseline = read("docs/governance/pmcs-v1.1-development-baseline.md");
  const matrix = read("docs/qa/pmcs-v1.1-rpt1-test-matrix.md");
  const api = read("docs/api/reporting-v1.md");
  const security = read("docs/security/pmcs-v1.1-reporting-security.md");

  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S07-MS10-C1/u);
  assert.match(checkpoint, /c59a2444f5d5dd70859441f27d05c23dea6c268e/u);
  assert.match(checkpoint, /f8829027c2ce073c207cd0e04a49c306b546c6a1/u);
  assert.match(checkpoint, /2b784f135894092ef55bf7c7df201b1f03e0c77f/u);
  assert.match(checkpoint, /80830a48ffd5b84ecdc97990b052f6d7037eda42/u);
  assert.match(checkpoint, /Run 158 \(`35522512734`\)/u);
  assert.match(checkpoint, /`387\/387` تست C#/u);
  assert.match(checkpoint, /`68\/68` تست قراردادی Node/u);
  assert.match(checkpoint, /`139\/139` تست Web/u);
  assert.match(checkpoint, /`367` فایل C#/u);
  assert.match(checkpoint, /`274` endpoint، `204` mutation و `5`/u);
  assert.match(checkpoint, /Restore Drill کامل `45` Migration/u);
  assert.match(checkpoint, /`7\/7` Suite و `12\/12` Command/u);
  assert.match(checkpoint, /Qualification artifact: `10609286665`/u);
  assert.match(checkpoint, /sha256:d3227eba54767ada7ccc45fa1636351790f47953cb110b84139e5d82d5f594e0/u);
  assert.match(checkpoint, /Integration artifact: `10608712935`/u);
  assert.match(checkpoint, /sha256:289d394c4cb99b3567c3a56211d17993e5474bae17b82c1a2c4544030cddb965/u);
  assert.match(checkpoint, /UI-E2E artifact: `10608897414`/u);
  assert.match(checkpoint, /sha256:5cc873efc7d7dc2a71637252e241435160dfae70a7d413d9f9cdd42e848d9bab/u);
  assert.match(checkpoint, /هیچ Runtime Definition[\s\S]*Migration[\s\S]*Renderer F04/u);
  assert.match(checkpoint, /هیچ گزارش F04 هنوز از API قابل اجرا یا دانلود نیست/u);
  assert.match(checkpoint, /Safe Resume Point اکنون `PMCS-V1\.1-RPT1-S07-MS10-C1`/u);
  assert.match(checkpoint, /Micro-Step بعدی فقط Runtime identity/u);
  assert.match(roadmap, /\| `1\.41\.0` \| ثبت Safe Checkpoint `S07-MS10`/u);
  assert.match(registry, /Slice 07 MS10[\s\S]*f8829027c2ce073c207cd0e04a49c306b546c6a1[\s\S]*Run 158/u);
  assert.match(canonical, /Safe Resume Point قطعی فعلی آن `PMCS-V1\.1-RPT1-S07-MS17-C1`/u);
  assert.match(canonical, /`S07-MS13`/u);
  assert.match(architecture, /نسخه: `1\.31\.0`[\s\S]*Run 158/u);
  assert.match(baseline, /Slice 07 Micro-Step 10[\s\S]*Run 158/u);
  assert.match(matrix, /## ۳۲\.[\s\S]*Run 158/u);
  assert.match(matrix, /نسخه: `1\.34\.0`/u);
  assert.match(api, /Micro-Stepهای `S07-MS10\/MS11\/MS12` قرارداد، Runtime Core و Renderer\/Golden را مستقل checkpoint/u);
  assert.match(security, /نسخه: `1\.16\.0`[\s\S]*Run 158/u);
});

test("RPT1-F03 semantic contract records the S07-MS06 safe checkpoint without claiming Runtime", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-07-ms06-candidate.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  const canonical = read("docs/PMCS-CANONICAL-PROJECT-REFERENCE.md");
  const architecture = read("docs/architecture/pmcs-v1.1-reporting-center-phase1.md");
  const baseline = read("docs/governance/pmcs-v1.1-development-baseline.md");
  const matrix = read("docs/qa/pmcs-v1.1-rpt1-test-matrix.md");
  const security = read("docs/security/pmcs-v1.1-reporting-security.md");

  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S07-MS06-C1/u);
  assert.match(checkpoint, /e3218555a38f7ba460558e51b4db3f8bc17fcd9c/u);
  assert.match(checkpoint, /1fe4cc804fdd078a71ff2633201c8c690447900e/u);
  assert.match(checkpoint, /a6987cd47bb1be917d34a94fb064aa72ec6b89c2/u);
  assert.match(checkpoint, /Run 146 \(`35500809115`\)/u);
  assert.match(checkpoint, /`355\/355` تست C#/u);
  assert.match(checkpoint, /`63\/63` تست قراردادی Node/u);
  assert.match(checkpoint, /`139\/139` تست Web/u);
  assert.match(checkpoint, /`355` فایل C#/u);
  assert.match(checkpoint, /`274` endpoint، `204` mutation و `5`/u);
  assert.match(checkpoint, /Restore Drill کامل `44` Migration/u);
  assert.match(checkpoint, /`7\/7` Suite/u);
  assert.match(checkpoint, /Qualification artifact: `10602595834`/u);
  assert.match(checkpoint, /sha256:0e4f6089342f1e5feb28b15167d99747043bc56ffe37fc8a9fca736d7195f433/u);
  assert.match(checkpoint, /Integration artifact: `10602685609`/u);
  assert.match(checkpoint, /sha256:1a88cb0c8775f053e359d0b7de5e43540c8054092b0b5cff6ed6409cbcd62d58/u);
  assert.match(checkpoint, /UI-E2E artifact: `10602237046`/u);
  assert.match(checkpoint, /sha256:6ac9521d93dd1280911a991f1ef38ef34d97516926225d72c6d74ae53a1f4999/u);
  assert.match(checkpoint, /هیچ Runtime Definition[\s\S]*Migration[\s\S]*Renderer F03/u);
  assert.match(checkpoint, /هیچ گزارش F03 هنوز از API قابل اجرا یا دانلود نیست/u);
  assert.match(checkpoint, /Safe Resume Point اکنون `PMCS-V1\.1-RPT1-S07-MS06-C1`/u);
  assert.match(roadmap, /\| `1\.33\.0` \| ثبت Safe Checkpoint `S07-MS06`/u);
  assert.match(registry, /Slice 07 MS06[\s\S]*e3218555a38f7ba460558e51b4db3f8bc17fcd9c/u);
  assert.match(checkpoint, /Safe Resume Point اکنون `PMCS-V1\.1-RPT1-S07-MS06-C1`/u);
  assert.match(checkpoint, /Micro-Step بعدی فقط Runtime identity/u);
  assert.match(architecture, /نسخه: `1\.31\.0`[\s\S]*Run 146/u);
  assert.match(baseline, /Slice 07 Micro-Step 06[\s\S]*Run 146/u);
  assert.match(matrix, /## ۲۸\.[\s\S]*Run 146/u);
  assert.match(matrix, /نسخه: `1\.34\.0`/u);
  assert.match(security, /نسخه: `1\.16\.0`[\s\S]*Run 154/u);
});

test("RPT1-F03 Runtime Core records the S07-MS07 safe checkpoint without opening renderer or wiring", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-07-ms07-candidate.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  const canonical = read("docs/PMCS-CANONICAL-PROJECT-REFERENCE.md");
  const architecture = read("docs/architecture/pmcs-v1.1-reporting-center-phase1.md");
  const baseline = read("docs/governance/pmcs-v1.1-development-baseline.md");
  const matrix = read("docs/qa/pmcs-v1.1-rpt1-test-matrix.md");
  const api = read("docs/api/reporting-v1.md");

  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S07-MS07-C1/u);
  assert.match(checkpoint, /22d5b0f91edf8d733192fae0ba946c8538c63bca/u);
  assert.match(checkpoint, /eb5ea4253a7b80e8ab3320b9ccea747624f89790/u);
  assert.match(checkpoint, /b89ca9920fc216644203d9cd5b3868cabed453fe/u);
  assert.match(checkpoint, /Run 148 \(`35507127968`\)/u);
  assert.match(checkpoint, /`377\/377` تست C#/u);
  assert.match(checkpoint, /`22\/22` case متمرکز F03/u);
  assert.match(checkpoint, /`64\/64` تست قراردادی Node/u);
  assert.match(checkpoint, /`139\/139` تست Web/u);
  assert.match(checkpoint, /`362` فایل C#/u);
  assert.match(checkpoint, /`274` endpoint، `204` mutation و `5`/u);
  assert.match(checkpoint, /Restore Drill کامل `44` Migration/u);
  assert.match(checkpoint, /`7\/7` Suite/u);
  assert.match(checkpoint, /Qualification artifact: `10604880766`/u);
  assert.match(checkpoint, /sha256:de8d89d1651d161d048552915c23dce07cb20f8407dfb8229d91d0544e626dfd/u);
  assert.match(checkpoint, /Integration artifact: `10604276963`/u);
  assert.match(checkpoint, /sha256:500e01e06889ece2c2c48ac8ae317c814728d8c0c2807280658110242be40978/u);
  assert.match(checkpoint, /UI-E2E artifact: `10604286949`/u);
  assert.match(checkpoint, /sha256:fc951ee1cfb012f0a7255f0520d4cfd21e2e2a7156e5a26ec3299b04493a1965/u);
  assert.match(checkpoint, /هیچ endpoint تازه، Migration، Catalog\/Template seed/u);
  assert.match(checkpoint, /هیچ گزارش F03 هنوز از API قابل ایجاد، retry، مشاهده یا دانلود نیست/u);
  assert.match(checkpoint, /Safe Resume Point اکنون `PMCS-V1\.1-RPT1-S07-MS07-C1`/u);
  assert.match(roadmap, /\| `1\.35\.0` \| ثبت Safe Checkpoint `S07-MS07`/u);
  assert.match(registry, /Slice 07 MS07[\s\S]*22d5b0f91edf8d733192fae0ba946c8538c63bca[\s\S]*Run 148/u);
  assert.match(canonical, /F03 Runtime Core Source:[\s\S]*22d5b0f91edf8d733192fae0ba946c8538c63bca[\s\S]*Run 148/u);
  assert.match(canonical, /Safe Resume Point قطعی فعلی آن `PMCS-V1\.1-RPT1-S07-MS17-C1`/u);
  assert.match(architecture, /نسخه: `1\.31\.0`[\s\S]*Run 148/u);
  assert.match(baseline, /Slice 07 Micro-Step 07[\s\S]*Run 148/u);
  assert.match(matrix, /## ۲۹\.[\s\S]*Run 148/u);
  assert.match(matrix, /نسخه: `1\.34\.0`/u);
  assert.match(api, /خانواده F03 روی API متصل — Safe Checkpoint/u);
});

test("RPT1-F03 Renderer and Golden record the S07-MS08 safe checkpoint without opening wiring", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-07-ms08-candidate.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  const canonical = read("docs/PMCS-CANONICAL-PROJECT-REFERENCE.md");
  const architecture = read("docs/architecture/pmcs-v1.1-reporting-center-phase1.md");
  const baseline = read("docs/governance/pmcs-v1.1-development-baseline.md");
  const matrix = read("docs/qa/pmcs-v1.1-rpt1-test-matrix.md");
  const api = read("docs/api/reporting-v1.md");
  const security = read("docs/security/pmcs-v1.1-reporting-security.md");

  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S07-MS08-C1/u);
  assert.match(checkpoint, /d9d7ddb17d222f3b53402f291bf3d0cb8a3f957f/u);
  assert.match(checkpoint, /58fb79b0ee3d7c9cfa11630635b8ebdfbcbce434/u);
  assert.match(checkpoint, /235e0a0f12b560bba06792c3290724d739117b73/u);
  assert.match(checkpoint, /Run 154 \(`35512969648`\)/u);
  assert.match(checkpoint, /`383\/383` تست C#/u);
  assert.match(checkpoint, /شش case Renderer\/Golden تازه/u);
  assert.match(checkpoint, /`28\/28` case متمرکز F03/u);
  assert.match(checkpoint, /`65\/65` تست قراردادی Node/u);
  assert.match(checkpoint, /`139\/139` تست Web/u);
  assert.match(checkpoint, /`365` فایل C#/u);
  assert.match(checkpoint, /`274` endpoint، `204` mutation و `5`/u);
  assert.match(checkpoint, /Restore Drill کامل `44` Migration/u);
  assert.match(checkpoint, /`7\/7` Suite/u);
  assert.match(checkpoint, /Qualification artifact: `10605799275`/u);
  assert.match(checkpoint, /sha256:22f72f9b6babee82d76f1900b36bafbbb9a627ac6b72009746978cda97b6eb84/u);
  assert.match(checkpoint, /Integration artifact: `10605432920`/u);
  assert.match(checkpoint, /sha256:f594d32316681f532bfed4200240c0aedceac33d9d5b2621bff5300aed1a0317/u);
  assert.match(checkpoint, /UI-E2E artifact: `10605298077`/u);
  assert.match(checkpoint, /sha256:7cc5ee459c0297500370c2dfe2c8799d06b3935fdb924e014d9c2da0ff5a7e86/u);
  assert.match(checkpoint, /e19809b6c3ffa5ff3443babe683c9f286c3b928986d176f1d515166f336cf5a3/u);
  assert.match(checkpoint, /d765dfc98873fbc07e28b7320524fd156cfa5acc80c6f4da2b2d42941c4e09d1/u);
  assert.match(checkpoint, /هیچ endpoint تازه، Migration، Catalog\/Template seed/u);
  assert.match(checkpoint, /هیچ گزارش F03 هنوز از API قابل ایجاد، retry، مشاهده، verify یا دانلود نیست/u);
  assert.match(checkpoint, /Safe Resume Point اکنون `PMCS-V1\.1-RPT1-S07-MS08-C1`/u);
  assert.match(roadmap, /\| `1\.37\.0` \| ثبت Safe Checkpoint `S07-MS08`/u);
  assert.match(registry, /Slice 07 MS08[\s\S]*d9d7ddb17d222f3b53402f291bf3d0cb8a3f957f[\s\S]*Run 154/u);
  assert.match(canonical, /Safe Resume Point قطعی فعلی آن `PMCS-V1\.1-RPT1-S07-MS17-C1`/u);
  assert.match(architecture, /نسخه: `1\.31\.0`[\s\S]*Run 154/u);
  assert.match(baseline, /Slice 07 Micro-Step 08[\s\S]*Run 154/u);
  assert.match(matrix, /## ۳۰\.[\s\S]*Run 154/u);
  assert.match(matrix, /نسخه: `1\.34\.0`/u);
  assert.match(api, /خانواده F03 روی API متصل — Safe Checkpoint/u);
  assert.match(security, /نسخه: `1\.16\.0`[\s\S]*Run 154/u);
});

test("RPT1-F02 runtime core stays bounded to identity period source resolver and semantic snapshot", () => {
  const sourceContract = read(
    "src/backend/Pmcs.Modules.FieldOperations/Contracts/IDailyReportPeriodReportingSource.cs",
  );
  const source = read(
    "src/backend/Pmcs.Modules.FieldOperations/Services/DailyReportPeriodReportingSource.cs",
  );
  const fieldModule = read("src/backend/Pmcs.Modules.FieldOperations/FieldOperationsModule.cs");
  const identity = read(
    "src/backend/Pmcs.Modules.Reporting/Domain/ProjectPeriodicReportRuntimeContract.cs",
  );
  const resolver = read(
    "src/backend/Pmcs.Modules.Reporting/Services/ProjectPeriodicReportPeriodResolver.cs",
  );
  const builder = read(
    "src/backend/Pmcs.Modules.Reporting/Services/ProjectPeriodicReportSnapshotBuilder.cs",
  );
  const projectProfile = read("src/backend/Pmcs.Modules.Projects/Contracts/IProjectDirectory.cs");
  const tests = read("tests/Pmcs.Domain.Tests/ProjectPeriodicReportingTests.cs");
  const worker = read("src/backend/Pmcs.Modules.Reporting/Services/ReportGenerationWorker.cs");

  assert.match(sourceContract, /interface IDailyReportPeriodReportingSource/u);
  assert.match(sourceContract, /LoadPeriodAsync/u);
  assert.match(sourceContract, /pmcs\.field-operations\.daily-report-period\/v1/u);
  assert.match(source, /report\.CreatedAt <= normalizedAsOf/u);
  assert.match(source, /DailyReportStatus\.Approved \|\| report\.Status == DailyReportStatus\.Superseded/u);
  assert.match(source, /versions\.Length == 0[\s\S]*?return null/u);
  assert.match(source, /field\.daily_report\.period\.duplicate_root_date/u);
  assert.match(source, /field\.daily_report\.period\.duplicate_current_official/u);
  assert.match(fieldModule, /AddScoped<IDailyReportPeriodReportingSource, DailyReportPeriodReportingSource>/u);
  assert.match(identity, /DefinitionCode = "project-periodic-certified"/u);
  assert.match(identity, /ParameterSchemaVersion = "pmcs\.reporting\.project-periodic\.parameters\/v1"/u);
  assert.match(identity, /SnapshotSchemaVersion = "pmcs\.reporting\.project-periodic\.snapshot\/v1"/u);
  assert.match(identity, /RendererContractVersion = "pmcs\.reporting\.project-periodic\.renderer\/v1"/u);
  assert.match(identity, /LayoutContractVersion = "pmcs\.reporting\.project-periodic\.layout\/v1"/u);
  assert.match(identity, /record ProjectPeriodicPinnedProjectProfile/u);
  assert.match(identity, /PinnedProjectProfileSchemaVersion/u);
  assert.match(resolver, /DayOfWeek\.Saturday/u);
  assert.match(resolver, /PersianCalendar/u);
  assert.match(resolver, /IsInvalidTime/u);
  assert.match(resolver, /IsAmbiguousTime/u);
  assert.match(resolver, /reporting\.period\.cutoff\.future/u);
  assert.match(resolver, /reporting\.period\.cutoff\.before_start/u);
  assert.match(builder, /ReportDataStatus\.NotConfigured/u);
  assert.match(builder, /ReportDataStatus\.NoData/u);
  assert.match(builder, /ReportDataStatus\.InsufficientData/u);
  assert.match(builder, /ReportDataStatus\.Available/u);
  assert.match(builder, /ProjectPeriodicReportUnitState\.UnitMissing/u);
  assert.match(builder, /StringComparer\.Ordinal/u);
  assert.match(builder, /root\.Classification/u);
  assert.match(builder, /version\.CreatedAt\.ToUniversalTime\(\) > sourceCutoffUtc/u);
  assert.match(builder, /project\.ValidateForRun/u);
  assert.doesNotMatch(builder, /FieldOperationsDbContext|field_operations\./u);
  assert.match(projectProfile, /ConfigurationVersion/u);
  assert.match(projectProfile, /DailyCutoffLocalTime/u);
  assert.match(projectProfile, /ReportingFrequency/u);
  assert.match(projectProfile, /DailyReportWorkflow/u);
  assert.match(tests, /MonthlyPeriodUsesPersianMonthBoundaries/u);
  assert.match(tests, /SemanticAndManifestHashesIgnoreQueryOrderRunIdentityAndBuildTime/u);
  assert.match(tests, /DuplicateRootDateAndDuplicateCurrentOfficialFailClosed/u);
  assert.match(worker, /IDailyReportPeriodReportingSource/u);
  assert.match(worker, /ProjectPeriodicReportSnapshotBuilder\.Build/u);
  assert.match(worker, /ProjectPeriodicPinnedProjectProfile/u);
});

test("RPT1-F02 renderer contract stays deterministic while wiring remains production-disabled", () => {
  const identity = read(
    "src/backend/Pmcs.Modules.Reporting/Domain/ProjectPeriodicReportRuntimeContract.cs",
  );
  const contract = read(
    "src/backend/Pmcs.Modules.Reporting/Rendering/ProjectPeriodicReportRenderingContracts.cs",
  );
  const pdf = read(
    "src/backend/Pmcs.Modules.Reporting/Rendering/ProjectPeriodicReportPdfRenderer.cs",
  );
  const xlsx = read(
    "src/backend/Pmcs.Modules.Reporting/Rendering/ProjectPeriodicReportXlsxRenderer.cs",
  );
  const tests = read("tests/Pmcs.Domain.Tests/ProjectPeriodicReportRenderingTests.cs");
  const module = read(`${moduleRoot}/ReportingModule.cs`);
  const worker = read(`${moduleRoot}/Services/ReportGenerationWorker.cs`);
  const endpoints = read(`${moduleRoot}/Endpoints/ReportingEndpoints.cs`);
  const settings = JSON.parse(read("src/backend/Pmcs.Api/appsettings.json"));
  const semantic = read(
    "docs/architecture/pmcs-v1.1-rpt1-f02-weekly-monthly-semantic-contract.md",
  );
  const matrix = read("docs/qa/pmcs-v1.1-rpt1-test-matrix.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  const canonical = read("docs/PMCS-CANONICAL-PROJECT-REFERENCE.md");
  const migration = read(`${moduleRoot}/Migrations/ProjectPeriodicReportCatalogMigration.cs`);
  const harness = read("src/backend/Pmcs.TestHarness/ReportingPeriodicVerification.cs");

  for (const path of [
    `${moduleRoot}/Rendering/CertifiedPdfRuntime.cs`,
    `${moduleRoot}/Rendering/ProjectPeriodicReportRenderingContracts.cs`,
    `${moduleRoot}/Rendering/ProjectPeriodicReportPdfRenderer.cs`,
    `${moduleRoot}/Rendering/ProjectPeriodicReportXlsxRenderer.cs`,
    `${moduleRoot}/Migrations/ProjectPeriodicReportCatalogMigration.cs`,
    "tests/Pmcs.Domain.Tests/ProjectPeriodicReportRenderingTests.cs",
    "src/backend/Pmcs.TestHarness/ReportingPeriodicVerification.cs",
  ]) assert.equal(existsSync(path), true, `Missing ${path}`);

  assert.match(identity, /RendererContractVersion = "pmcs\.reporting\.project-periodic\.renderer\/v1"/u);
  assert.match(identity, /LayoutContractVersion = "pmcs\.reporting\.project-periodic\.layout\/v1"/u);
  assert.match(contract, /interface IProjectPeriodicReportRenderer/u);
  assert.match(contract, /CanonicalJson\.Sha256\(canonicalSnapshot\)/u);
  assert.match(contract, /reporting\.periodic\.snapshot\.payload_invalid/u);
  assert.match(contract, /reporting\.periodic\.render_request\.invalid/u);
  assert.match(pdf, /ContentFromRightToLeft/u);
  assert.match(pdf, /GenerateImages[\s\S]*QualificationRasterDpi/u);
  assert.match(pdf, /execution\.MaximumPdfFacts/u);
  assert.match(xlsx, /rightToLeft/u);
  assert.match(xlsx, /CompressionLevel\.NoCompression/u);
  assert.match(xlsx, /execution\.MaximumXlsxRows/u);
  assert.doesNotMatch(xlsx, /<f>|WriteStartElement\("f"/u);
  assert.match(tests, /83fd80eedaa1024e84eb253bec76591379fe2f088be12c5b322573d63eb1909d/u);
  assert.match(tests, /52ec4e80c34e682f6994ef7a674b161b748a772e34b4e04ec12e27e94c98f989/u);
  assert.match(tests, /058a3da3045408a1d87dc9e5c942cd38ffdf7da1921b6594e6ee88a0aa22b396/u);
  assert.match(tests, /d61a1090d07d5f21a5d57c15b3abb197a341332b124ba3e8a98461996a42b770/u);
  assert.match(tests, /MonthlyWorkbookUsesCanonicalPersianMonthBoundariesAndIdentity/u);
  assert.match(tests, /NoDataWorkbookKeepsSemanticSheetsHeaderOnly/u);
  assert.match(tests, /NotConfiguredWorkbookCarriesExplicitReasons/u);

  assert.match(module, /IProjectPeriodicReportRenderer, ProjectPeriodicReportPdfRenderer/u);
  assert.match(module, /IProjectPeriodicReportRenderer, ProjectPeriodicReportXlsxRenderer/u);
  assert.match(module, /ProjectPeriodicReportCatalogMigration/u);
  assert.match(worker, /ProjectPeriodicReportRendererRegistry/u);
  assert.match(worker, /ProjectPeriodicReportRenderSnapshot\.Parse/u);
  assert.match(worker, /ProjectPeriodicReportRenderRequest/u);
  assert.match(endpoints, /ProjectPeriodicReportRuntimeContract\.DefinitionCode/u);
  assert.match(endpoints, /ParsePeriodicParameters/u);
  assert.match(migration, /project-periodic-certified/u);
  assert.match(harness, /verify-reporting-periodic|VerifyReportingPeriodicAsync/u);
  assert.equal(settings.ReportingCenter.Phase1Enabled, false);
  assert.equal(settings.ReportingCenter.OutputAccessEnabled, false);
  assert.equal(settings.ReportingCenter.WorkerEnabled, false);
  assert.match(semantic, /Connected Safe Checkpoint/u);
  assert.match(semantic, /83fd80eedaa1024e84eb253bec76591379fe2f088be12c5b322573d63eb1909d/u);
  assert.match(matrix, /## ۲۷\. Catalog\/API\/Worker wiring متصل خانواده F02/u);
  assert.match(roadmap, /F02 Catalog\/API\/Worker Safe Checkpoint — Slice 07 Micro-Step 05/u);
  assert.match(registry, /Slice 07 MS05[\s\S]*Run 144/u);
  assert.match(canonical, /Catalog\/API\/Worker و qualification متصل F01 تا F05 بسته/u);
});

test("RPT1-F02 Catalog API and Worker record the S07-MS05 connected safe checkpoint", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-07-ms05-candidate.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  const canonical = read("docs/PMCS-CANONICAL-PROJECT-REFERENCE.md");
  const baseline = read("docs/governance/pmcs-v1.1-development-baseline.md");
  const matrix = read("docs/qa/pmcs-v1.1-rpt1-test-matrix.md");
  const security = read("docs/security/pmcs-v1.1-reporting-security.md");

  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S07-MS05-C1/u);
  assert.match(checkpoint, /1d2af49f459dee047161dcdd631131bfe8bb5d7f/u);
  assert.match(checkpoint, /7fc55c167ad2159a31c895b32a52d78f47574df9/u);
  assert.match(checkpoint, /d665fe4cdf29369f96ec0875bc6f1535db349d55/u);
  assert.match(checkpoint, /2a91fc442a3a84a2ee3d6c59fe5186c8f0ed3efb/u);
  assert.match(checkpoint, /Run 143 \(`35498734639`\)/u);
  assert.match(checkpoint, /Run 144 \(`35498990050`\)/u);
  assert.match(checkpoint, /`355\/355` تست C#/u);
  assert.match(checkpoint, /`61\/61` تست قراردادی Node/u);
  assert.match(checkpoint, /`139\/139` تست Web/u);
  assert.match(checkpoint, /repository validator روی `355` فایل C#/u);
  assert.match(checkpoint, /`274` endpoint، `204` mutation و `5`/u);
  assert.match(checkpoint, /هارنس F02 با `13\/13` assertion/u);
  assert.match(checkpoint, /Restore Drill کامل `44` Migration/u);
  assert.match(checkpoint, /هر `7\/7` Suite/u);
  assert.match(checkpoint, /Qualification artifact: `10601457941`/u);
  assert.match(checkpoint, /sha256:3b577627970885596d83e992d04bce9093ad69e9b594e58f2919294a676206e8/u);
  assert.match(checkpoint, /Integration artifact: `10601880116`/u);
  assert.match(checkpoint, /sha256:3b582c64f4301cf1215157263d66270bf300d1009b763679e7e86e5fe5623fd7/u);
  assert.match(checkpoint, /UI-E2E artifact: `10601678972`/u);
  assert.match(checkpoint, /sha256:5d35548a4675c0515d54597702eb911cb9413c66eb5ba09989603874c4803ea7/u);
  assert.match(checkpoint, /eca353e00f07fbdb054613768399496e137ab0deda731c5d319d5cd4ebd3be6b/u);
  assert.match(checkpoint, /Phase1Enabled=false[\s\S]*PdfLicense=Unconfigured/u);
  assert.match(checkpoint, /RPT1-F03 — Executive Project State/u);
  assert.match(checkpoint, /هیچ Runtime برای F03 پیش از checkpoint قرارداد/u);
  assert.match(roadmap, /\| `1\.31\.0` \| ثبت Safe Checkpoint `S07-MS05`/u);
  assert.match(registry, /Slice 07 MS05[\s\S]*7fc55c167ad2159a31c895b32a52d78f47574df9[\s\S]*Run 144/u);
  assert.match(canonical, /F02 Catalog\/API\/Worker Source:[\s\S]*Run 144/u);
  assert.match(baseline, /Slice 07 Micro-Step 05[\s\S]*Run 144/u);
  assert.match(matrix, /## ۲۷\.[\s\S]*Run 144/u);
  assert.match(security, /Checkpoint `S07-MS05`[\s\S]*Run 144/u);
});

test("RPT1-F02 renderer and Golden record the S07-MS04 safe checkpoint without opening wiring", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-07-ms04-candidate.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  const canonical = read("docs/PMCS-CANONICAL-PROJECT-REFERENCE.md");
  const baseline = read("docs/governance/pmcs-v1.1-development-baseline.md");
  const matrix = read("docs/qa/pmcs-v1.1-rpt1-test-matrix.md");
  const security = read("docs/security/pmcs-v1.1-reporting-security.md");

  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S07-MS04-C1/u);
  assert.match(checkpoint, /4f68f57de2c2a79b654a19128894d9c89878ab65/u);
  assert.match(checkpoint, /f4b592c72ea65974c00b936ca59c0428eb47f981/u);
  assert.match(checkpoint, /Run 141 \(`35495791821`\)/u);
  assert.match(checkpoint, /`353\/353` تست C#/u);
  assert.match(checkpoint, /`60\/60` تست قراردادی Node/u);
  assert.match(checkpoint, /`139\/139` تست Web/u);
  assert.match(checkpoint, /repository validator روی `354` فایل C#/u);
  assert.match(checkpoint, /`274` endpoint، `204` mutation و `5`/u);
  assert.match(checkpoint, /Restore Drill کامل `43` Migration/u);
  assert.match(checkpoint, /هر `7\/7` Suite/u);
  assert.match(checkpoint, /Qualification artifact: `10600109655`/u);
  assert.match(checkpoint, /sha256:621bfda27a59f3d21fcc4793bd8d1820cd67b5238a32e0e647bfa6766e5cd19e/u);
  assert.match(checkpoint, /Integration artifact: `10600910298`/u);
  assert.match(checkpoint, /sha256:9a614a04f57af341dcb9838e84eed57716bfa654a50221af17cced27d08fd1b1/u);
  assert.match(checkpoint, /UI-E2E artifact: `10599559968`/u);
  assert.match(checkpoint, /sha256:e64d16a6e0e8e98d4226e14fecb904162c7671a88e57d1c17f0fde4cb673808d/u);
  assert.match(checkpoint, /83fd80eedaa1024e84eb253bec76591379fe2f088be12c5b322573d63eb1909d/u);
  assert.match(checkpoint, /52ec4e80c34e682f6994ef7a674b161b748a772e34b4e04ec12e27e94c98f989/u);
  assert.match(checkpoint, /هیچ endpoint،[\s\S]*Catalog\/Template seed[\s\S]*DI registration/u);
  assert.match(checkpoint, /Safe Resume Point اکنون `PMCS-V1\.1-RPT1-S07-MS04-C1`/u);
  assert.match(roadmap, /\| `1\.29\.0` \| ثبت Safe Checkpoint `S07-MS04`/u);
  assert.match(registry, /Slice 07 MS04[\s\S]*4f68f57de2c2a79b654a19128894d9c89878ab65[\s\S]*Run 141/u);
  assert.match(canonical, /F02 Renderer\/Golden Source:[\s\S]*Run 141/u);
  assert.match(baseline, /Slice 07 Micro-Step 04[\s\S]*Run 141/u);
  assert.match(matrix, /## ۲۶\.[\s\S]*Run 141/u);
  assert.match(security, /formula-safety[\s\S]*Run 141/u);
});

test("RPT1-F02 semantic contract records the S07-MS02 safe checkpoint without claiming Runtime", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-07-ms02-candidate.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  const canonical = read("docs/PMCS-CANONICAL-PROJECT-REFERENCE.md");
  const baseline = read("docs/governance/pmcs-v1.1-development-baseline.md");
  const matrix = read("docs/qa/pmcs-v1.1-rpt1-test-matrix.md");

  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S07-MS02-C1/u);
  assert.match(checkpoint, /b4a59fa966320a1da4b53759814224e21893c01e/u);
  assert.match(checkpoint, /6b5b486dace3c07b0b4e0385413bf1add5aee7a3/u);
  assert.match(checkpoint, /Run 137 \(`35474388839`\)/u);
  assert.match(checkpoint, /`330\/330` تست C#/u);
  assert.match(checkpoint, /`56\/56` تست قراردادی Node/u);
  assert.match(checkpoint, /`139\/139` تست Web/u);
  assert.match(checkpoint, /`344` فایل C#/u);
  assert.match(checkpoint, /`274` endpoint، `204` mutation و `5`/u);
  assert.match(checkpoint, /Restore Drill کامل `43` Migration/u);
  assert.match(checkpoint, /`7\/7` Suite/u);
  assert.match(checkpoint, /Qualification artifact: `10593113029`/u);
  assert.match(checkpoint, /sha256:fa9054d784ffa5139e7d2de466d93c9992777930373af115c037a7a520c6bcbc/u);
  assert.match(checkpoint, /Integration artifact: `10593968226`/u);
  assert.match(checkpoint, /sha256:89b1986b252258093172ad2973b0ece4a9cb7f56320f81fc00a0918b6ae26cb4/u);
  assert.match(checkpoint, /UI-E2E artifact: `10594217426`/u);
  assert.match(checkpoint, /sha256:51b125bc7c442ae9a76890ad2ce69c63b7505cb284d6e36f81e3110dd28a7a85/u);
  assert.match(checkpoint, /هیچ Runtime Definition[\s\S]*Migration[\s\S]*Renderer/u);
  assert.match(checkpoint, /هیچ گزارش هفتگی\/ماهانه‌ای هنوز از API قابل اجرا یا دانلود نیست/u);
  assert.match(checkpoint, /Safe Resume Point اکنون `PMCS-V1\.1-RPT1-S07-MS02-C1`/u);
  assert.match(roadmap, /\| `1\.25\.0` \| ثبت Safe Checkpoint `S07-MS02`/u);
  assert.match(registry, /Slice 07 MS02[\s\S]*b4a59fa966320a1da4b53759814224e21893c01e/u);
  assert.match(canonical, /F02 semantic Checkpoint:[\s\S]*42e607ee98e2bbdedafaec892c8af47b9f947aa1[\s\S]*Run 138/u);
  assert.match(baseline, /Slice 07 Micro-Step 02[\s\S]*Run 137/u);
  assert.match(matrix, /## ۲۴\.[\s\S]*Run 137/u);
});

test("RPT1-F02 runtime core records the S07-MS03 safe checkpoint without opening API or renderer", () => {
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-slice-07-ms03-candidate.md");
  const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
  const registry = read("docs/roadmaps/README.md");
  const canonical = read("docs/PMCS-CANONICAL-PROJECT-REFERENCE.md");
  const baseline = read("docs/governance/pmcs-v1.1-development-baseline.md");
  const matrix = read("docs/qa/pmcs-v1.1-rpt1-test-matrix.md");

  assert.match(checkpoint, /PMCS-V1\.1-RPT1-S07-MS03-C1/u);
  assert.match(checkpoint, /6fc28cf54a6df820c49a2365eab76e3550ae421a/u);
  assert.match(checkpoint, /5188dac79fe5187b319e6aa727da89163fa37c1b/u);
  assert.match(checkpoint, /Run 139 \(`35477179493`\)/u);
  assert.match(checkpoint, /`346\/346` تست C#/u);
  assert.match(checkpoint, /`58\/58` تست قراردادی Node/u);
  assert.match(checkpoint, /`139\/139` تست Web/u);
  assert.match(checkpoint, /`350` فایل C#/u);
  assert.match(checkpoint, /`274` endpoint، `204` mutation و `5`/u);
  assert.match(checkpoint, /Restore Drill کامل `43` Migration/u);
  assert.match(checkpoint, /`7\/7` Suite/u);
  assert.match(checkpoint, /Qualification artifact: `10594807015`/u);
  assert.match(checkpoint, /sha256:6a8e20ff54435ab596a607415ebe2e4fa9862ba5c1e77ba22dc3a12ce9b10ba7/u);
  assert.match(checkpoint, /Integration artifact: `10595160446`/u);
  assert.match(checkpoint, /sha256:436fd7019a1320e0ba769cecc9627c2b6265b82df88df9c64605d1394e85bb80/u);
  assert.match(checkpoint, /UI-E2E artifact: `10594547342`/u);
  assert.match(checkpoint, /sha256:9b9d994d404d79dd6330f3b9c539c0855fba0c741422ab00cf49eb10e4f37a10/u);
  assert.match(checkpoint, /هیچ endpoint، Migration، Catalog\/Template seed/u);
  assert.match(checkpoint, /گزارش هفتگی\/ماهانه هنوز از API قابل ایجاد، retry، مشاهده یا دانلود نیست/u);
  assert.match(checkpoint, /Safe Resume Point اکنون `PMCS-V1\.1-RPT1-S07-MS03-C1`/u);
  assert.match(roadmap, /\| `1\.27\.0` \| ثبت Safe Checkpoint `S07-MS03`/u);
  assert.match(registry, /Slice 07 MS03[\s\S]*6fc28cf54a6df820c49a2365eab76e3550ae421a/u);
  assert.match(canonical, /F02 Runtime Core Source:[\s\S]*6fc28cf54a6df820c49a2365eab76e3550ae421a/u);
  assert.match(baseline, /Slice 07 Micro-Step 03[\s\S]*Run 139/u);
  assert.match(matrix, /## ۲۵\.[\s\S]*Run 139/u);
});

function read(path) {
  return readFileSync(path, "utf8");
}

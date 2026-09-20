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
    `${moduleRoot}/Contracts/IReportingReadService.cs`,
    `${moduleRoot}/Endpoints/ReportingEndpoints.cs`,
    `${moduleRoot}/Migrations/ReportingInitialMigration.cs`,
    `${moduleRoot}/Migrations/ProjectPeriodicReportCatalogMigration.cs`,
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

test("migrations 42 through 44 own reporting schema verification and F02 catalog", () => {
  const migration = read(`${moduleRoot}/Migrations/ReportingInitialMigration.cs`);
  const verificationMigration = read(`${moduleRoot}/Migrations/ReportingVerificationCodeIndexMigration.cs`);
  const periodicMigration = read(`${moduleRoot}/Migrations/ProjectPeriodicReportCatalogMigration.cs`);
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
  assert.match(dbContext, /HasIndex\(item => item\.VerificationCode\);/u);
  assert.doesNotMatch(dbContext, /HasIndex\(item => item\.VerificationCode\)\.IsUnique/u);
  assert.match(read("tools/qa/verify-database.sh"), /canonical migration ledger size[\s\S]*?"44"/u);
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
  assert.match(canonical, /`RPT1-F01` و `RPT1-F02` checkpoint متصل دارند/u);
});

test("RPT1-F03 keeps its semantic contract while bounding the Runtime Core candidate", () => {
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
  const migration = read(`${moduleRoot}/Migrations/ProjectPeriodicReportCatalogMigration.cs`);
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

  assert.match(contract, /PMCS-RPT1-F03-SEMANTIC-001/u);
  assert.match(contract, /نسخه: `1\.1\.0`/u);
  assert.match(contract, /Runtime Core Candidate \| Renderer\/Wiring Not Implemented/u);
  assert.match(contract, /Parent checkpoint: `PMCS-V1\.1-RPT1-S07-MS06-C1`/u);
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
  assert.match(contract, /هیچ API، Migration، Catalog seed، Renderer، feature flag/u);

  assert.match(architecture, /PMCS-RPT1-F03-SEMANTIC-001 v1\.0\.0/u);
  assert.match(api, /قرارداد checkpointed F03 بدون تغییر API/u);
  assert.match(security, /سیاست ثابت F03/u);
  assert.match(matrix, /## ۲۸\.[\s\S]*Golden matrix هفده‌سناریویی/u);
  assert.match(roadmap, /نسخه سند: `1\.34\.0`/u);
  assert.match(registry, /Slice 07 MS06[\s\S]*Run 146[\s\S]*Runtime not implemented/u);
  assert.match(canonical, /PMCS-RPT1-F03-SEMANTIC-001 v1\.0\.0[\s\S]*Run 146/u);
  assert.match(rootReadme, /قرارداد checkpointed F03 برای Executive Project State/u);

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

  for (const source of [module, worker, endpoints, migration]) {
    assert.doesNotMatch(source, /ExecutiveProjectState|executive-project-state|RPT1-F03/u);
  }
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
  assert.match(canonical, /Safe Resume Point قطعی فعلی آن `PMCS-V1\.1-RPT1-S07-MS06-C1`/u);
  assert.match(checkpoint, /Micro-Step بعدی فقط Runtime identity/u);
  assert.match(architecture, /نسخه: `1\.18\.0`[\s\S]*Run 146/u);
  assert.match(baseline, /Slice 07 Micro-Step 06[\s\S]*Run 146/u);
  assert.match(matrix, /## ۲۸\.[\s\S]*Run 146/u);
  assert.match(matrix, /نسخه: `1\.19\.0`/u);
  assert.match(security, /نسخه: `1\.8\.1`[\s\S]*Run 146/u);
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
  assert.match(canonical, /Catalog\/API\/Worker و qualification متصل F02 و قرارداد معنایی F03 بسته شده‌اند/u);
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

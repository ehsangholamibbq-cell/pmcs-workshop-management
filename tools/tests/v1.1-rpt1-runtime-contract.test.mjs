import assert from "node:assert/strict";
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
    `${moduleRoot}/Services/ReportGenerationWorker.cs`,
    `${moduleRoot}/Services/ReportingWorkerHealthCheck.cs`,
    `${moduleRoot}/Services/ReportingWorkerTelemetry.cs`,
    `${moduleRoot}/ReportingExecutionOptions.cs`,
    `${moduleRoot}/ReportingOrphanRemediationOptions.cs`,
    `${moduleRoot}/Services/ReportOutputOrphanRemediationWorker.cs`,
    `${moduleRoot}/Services/ReportRunAdvisoryLock.cs`,
    "src/backend/Pmcs.TestHarness/ReportingCancellationVerification.cs",
    "src/backend/Pmcs.TestHarness/ReportingGoldenVerification.cs",
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

test("migration 42 owns reporting schema and migration 43 preserves deterministic verification", () => {
  const migration = read(`${moduleRoot}/Migrations/ReportingInitialMigration.cs`);
  const verificationMigration = read(`${moduleRoot}/Migrations/ReportingVerificationCodeIndexMigration.cs`);
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
  assert.match(dbContext, /HasIndex\(item => item\.VerificationCode\);/u);
  assert.doesNotMatch(dbContext, /HasIndex\(item => item\.VerificationCode\)\.IsUnique/u);
  assert.match(read("tools/qa/verify-database.sh"), /canonical migration ledger size[\s\S]*?"43"/u);
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
  const xlsx = read(`${moduleRoot}/Rendering/DailyReportXlsxRenderer.cs`);
  const endpoints = read(`${moduleRoot}/Endpoints/ReportingEndpoints.cs`);
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
  assert.match(pdf, /File\.OpenRead[\s\S]*FontManager\.RegisterFont\(/u);
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
  assert.match(seed, /-- verify-reporting-golden/u);
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
  assert.match(seed, /ReportingCenter__PdfLicense=Unconfigured/u);
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
  assert.match(roadmap, /نسخه سند: `1\.20\.0`/u);
  assert.match(roadmap, /\| `1\.20\.0` \| ثبت Safe Checkpoint `S06-MS05`/u);
  assert.match(registry, /PMCS-RM-POST-V1-001 v1\.20\.0/u);
  assert.match(registry, /V1\.1 RPT1 Slice 06 MS05[\s\S]*38a03f33f4747d0b6a76696705877633acd17678/u);
  assert.match(runbook, /## Semantic\/XLSX Golden verification/u);
  assert.match(runbook, /Run 130 \(`35449387794`\)/u);
});

function read(path) {
  return readFileSync(path, "utf8");
}

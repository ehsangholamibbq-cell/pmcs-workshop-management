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
  assert.match(worker, /IDailyReportReportingSource/u);
  assert.doesNotMatch(worker, /FieldOperations\.Persistence|field_operations\./u);
  assert.match(worker, /for update skip locked/u);
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
  assert.match(pdf, /File\.OpenRead[\s\S]*FontManager\.RegisterFontFromStream/u);
  assert.match(xlsx, /rightToLeft/u);
  assert.match(xlsx, /CompressionLevel\.NoCompression/u);
  assert.doesNotMatch(xlsx, /<f>|WriteStartElement\("f"/u);
  assert.match(endpoints, /outputs\/\{outputId:guid\}\/content/u);
  assert.match(endpoints, /outputs\/\{outputId:guid\}\/verify/u);
  assert.match(endpoints, /DocumentOwnerType\.ReportOutput/u);
  assert.match(endpoints, /reporting\.output\.integrity_failed/u);
  assert.match(endpoints, /CertifiedReportOutputIntegrityFailed/u);
});

test("connected RPT1 qualification covers API, worker, storage and database evidence", () => {
  const harness = read("src/backend/Pmcs.TestHarness/ReportingVerification.cs");
  const seed = read("tools/qa/seed-diagnostics.sh");
  const database = read("tools/qa/verify-database.sh");
  assert.match(harness, /reporting\.xlsx\.create\.idempotent-replay/u);
  assert.match(harness, /reporting\.xlsx\.download\.integrity/u);
  assert.match(harness, /reporting\.xlsx\.verify\.valid/u);
  assert.match(harness, /reporting\.pdf\.license\.fail-closed/u);
  assert.match(harness, /reporting\.pdf\.retry\.bounded/u);
  assert.match(seed, /ReportingCenter__PdfLicense=Unconfigured/u);
  assert.match(seed, /-- verify-reporting/u);
  assert.match(database, /certified output is a released governed document/u);
  assert.match(database, /certified reporting transactional outbox coverage/u);
});

test("RPT1 is disabled by default until renderers and qualification are complete", () => {
  const settings = JSON.parse(read("src/backend/Pmcs.Api/appsettings.json"));
  assert.equal(settings.ReportingCenter.Phase1Enabled, false);
  assert.equal(settings.ReportingCenter.OutputAccessEnabled, false);
  assert.equal(settings.ReportingCenter.WorkerEnabled, false);
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
  assert.match(checkpoint, /1cb7e2856e72f8cdf51a6b08e24cc0190f9313b5/u);
  assert.match(checkpoint, /6c7f0714490066ac1259e05e9e6af6da1d3067af/u);
  assert.match(checkpoint, /39\/39 passed/u);
  assert.match(checkpoint, /139\/139 passed/u);
  assert.match(checkpoint, /۳۳۴ فایل C# ماژولی/u);
  assert.match(checkpoint, /dotnet[\s\S]*پاس‌شده اعلام نمی‌شوند/u);
  assert.match(checkpoint, /PdfLicense[\s\S]*Unconfigured/u);
  assert.match(checkpoint, /هفت Stage Agent/u);
  assert.match(checkpoint, /هنوز `Feature Complete`[\s\S]*نیست/u);
});

function read(path) {
  return readFileSync(path, "utf8");
}

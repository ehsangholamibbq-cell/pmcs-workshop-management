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
});

test("migration 42 owns reporting schema and seeds only the certified definition", () => {
  const migration = read(`${moduleRoot}/Migrations/ReportingInitialMigration.cs`);
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
  assert.match(read("tools/qa/verify-database.sh"), /canonical migration ledger size[\s\S]*?"42"/u);
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

test("RPT1 is disabled by default until renderers and qualification are complete", () => {
  const settings = JSON.parse(read("src/backend/Pmcs.Api/appsettings.json"));
  assert.equal(settings.ReportingCenter.Phase1Enabled, false);
  assert.equal(settings.ReportingCenter.WorkerEnabled, false);
  const options = read(`${moduleRoot}/ReportingRuntimeOptions.cs`);
  assert.match(options, /workerEnabled = phase1Enabled &&/u);
});

function read(path) {
  return readFileSync(path, "utf8");
}

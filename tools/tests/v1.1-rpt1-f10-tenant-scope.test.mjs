import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const read = (path) => readFileSync(path, "utf8");

test("F10 tenant rows have nullable project identity and SQL scope constraints", () => {
  const migration = read("src/backend/Pmcs.Modules.Reporting/Migrations/PortfolioReportScopeMigration.cs");
  const run = read("src/backend/Pmcs.Modules.Reporting/Domain/ReportRun.cs");
  const snapshot = read("src/backend/Pmcs.Modules.Reporting/Domain/ReportSnapshot.cs");
  const output = read("src/backend/Pmcs.Modules.Reporting/Domain/ReportOutput.cs");
  assert.match(migration, /20260927-011[\s\S]*ck_reporting_run_scope_project[\s\S]*ck_reporting_snapshot_scope_project[\s\S]*ck_reporting_output_scope_project/u);
  assert.match(migration, /scope = 'Portfolio' and project_id is null[\s\S]*jsonb_typeof\(pinned_portfolio_cohort\) = 'object'/u);
  for (const domain of [run, snapshot, output]) {
    assert.match(domain, /public Guid\? ProjectId/u);
    assert.match(domain, /ReportDefinitionScope\.Portfolio/u);
  }
  assert.match(run, /QueuePortfolio[\s\S]*PinnedPortfolioCohortJson/u);
  assert.match(snapshot, /CreatePortfolio/u);
  assert.match(output, /CreatePortfolio/u);
});

test("F10 document ownership cannot enter generic document routes or project worker", () => {
  const migration = read("src/backend/Pmcs.Modules.Documents/Migrations/TenantReportOutputOwnerMigration.cs");
  const publisher = read("src/backend/Pmcs.Modules.Documents/Services/GeneratedDocumentPublisher.cs");
  const endpoints = read("src/backend/Pmcs.Modules.Documents/Endpoints/DocumentEndpoints.cs");
  const worker = read("src/backend/Pmcs.Modules.Reporting/Services/ReportGenerationWorker.cs");
  assert.match(migration, /TenantReportOutput'[\s\S]*project_id is null/u);
  assert.match(publisher, /request\.ProjectId\.HasValue[\s\S]*DocumentOwnerType\.TenantReportOutput/u);
  assert.match(publisher, /"portfolio"[\s\S]*tenants\/\{request\.TenantId:N\}/u);
  assert.match(endpoints, /asset\.OwnerType is DocumentOwnerType\.ReportOutput or DocumentOwnerType\.TenantReportOutput[\s\S]*return false/u);
  assert.match(endpoints, /request\.OwnerType is DocumentOwnerType\.ReportOutput or DocumentOwnerType\.TenantReportOutput/u);
  assert.match(worker, /where candidate\.scope = 'Project'[\s\S]*where candidate\.scope = 'Project'/u);
});

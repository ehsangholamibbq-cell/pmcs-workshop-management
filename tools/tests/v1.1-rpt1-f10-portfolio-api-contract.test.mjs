import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const read = (path) => readFileSync(path, "utf8");

test("F10 catalog is Portfolio scoped and the project catalog remains F01-F09", () => {
  const migration = read("src/backend/Pmcs.Modules.Reporting/Migrations/PortfolioSummaryReportCatalogMigration.cs");
  const policy = read("src/backend/Pmcs.Modules.Reporting/Domain/ReportDefinitionRuntimePolicy.cs");
  const db = read("tools/qa/verify-database.sh");
  assert.match(migration, /Version => "20260927-012"[\s\S]*'portfolio-summary-certified'[\s\S]*'Portfolio'/u);
  assert.match(migration, /portfolio\.read[\s\S]*project-state\.read[\s\S]*pmcs\.reporting\.portfolio-summary\.parameters\/v1/u);
  assert.doesNotMatch(policy, /PortfolioSummaryReportRuntimeContract\.DefinitionCode/u);
  assert.match(db, /canonical migration ledger size[\s\S]*?"56"[\s\S]*portfolio summary catalog migration identity/u);
});

test("F10 tenant API pins cohort and mask, strictly accepts empty parameters and hides other runs", () => {
  const endpoint = read("src/backend/Pmcs.Modules.Reporting/Endpoints/PortfolioReportingEndpoints.cs");
  const source = read("src/backend/Pmcs.Modules.Reporting/Services/PortfolioSummaryReportSource.cs");
  assert.match(endpoint, /"\/api\/v1\/portfolio\/reports"[\s\S]*MapPost\("\/runs", CreateRunAsync\)/u);
  assert.match(endpoint, /Parameters\.ValueKind != JsonValueKind\.Object[\s\S]*Parameters\.EnumerateObject\(\)\.Any\(\)/u);
  assert.match(endpoint, /source\.PinAsync[\s\S]*pinnedCohortSha256[\s\S]*ReportRun\.QueuePortfolio/u);
  assert.match(endpoint, /existing\.RequestedBy != actor\.UserId[\s\S]*source\.CanAccessPinnedAsync[\s\S]*requestedAsOfUtc = request\.AsOfUtc/u);
  assert.match(endpoint, /item\.Scope == ReportDefinitionScope\.Portfolio[\s\S]*source\.CanAccessPinnedAsync/u);
  assert.match(source, /LoadPinnedAsync[\s\S]*CanAccessPinnedAsync[\s\S]*SelectProfilesAsync/u);
  assert.match(source, /entry\.FinancialAuthorized && !financialAllowed[\s\S]*financialAllowed &= entry\.FinancialAuthorized/u);
  assert.doesNotMatch(endpoint, /projectId:guid|ProjectId!\.Value|GetPortfolioAsync/u);
});

import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const read = (path) => readFileSync(path, "utf8");

test("F10 selects only the authorized cohort through owner contracts", () => {
  const source = read("src/backend/Pmcs.Modules.Reporting/Services/PortfolioSummaryReportSource.cs");
  assert.match(source, /HasTenantPermissionAsync[\s\S]*"portfolio\.read"[\s\S]*GetProjectScopeAsync[\s\S]*"project-state\.read"/u);
  assert.match(source, /allowedIds[\s\S]*ListProfilesAsync\(tenantId, allowedIds/u);
  assert.match(source, /MaximumProjects[\s\S]*cohort\.invalid/u);
  assert.match(source, /FinancialPermissions\.All[\s\S]*CommercialPermissions\.All/u);
  assert.match(source, /IProjectStateReportingSource[\s\S]*IProjectFinancialPositionReportingSource[\s\S]*IProjectCommercialProcurementSupplyReportingSource/u);
  assert.doesNotMatch(source, /DbContext|SELECT\s|FromSql|GetPortfolioAsync|command-center/iu);
});

test("F10 runtime combines independently validated owner selections without FX", () => {
  const source = read("src/backend/Pmcs.Modules.Reporting/Services/PortfolioSummaryReportSource.cs");
  const builder = read("src/backend/Pmcs.Modules.Reporting/Services/PortfolioSummaryReportSnapshotBuilder.cs");
  const model = read("src/backend/Pmcs.Modules.Reporting/Domain/PortfolioSummaryReportModels.cs");
  assert.match(source, /ExecutiveProjectStateReportSnapshotBuilder\.Build[\s\S]*ProjectFinancialPositionReportSnapshotBuilder\.Build[\s\S]*ProjectCommercialProcurementSupplyReportSnapshotBuilder\.Build/u);
  assert.match(source, /sourceCutoffUtc\.ToUniversalTime[\s\S]*ConfigurationChangedAt\.Value\.ToUniversalTime\(\) <= cutoff/u);
  assert.match(builder, /Distinct\(StringComparer\.Ordinal\)[\s\S]*OrderBy\(item => item, StringComparer\.Ordinal\)/u);
  assert.match(builder, /financeIncomplete[\s\S]*commercialIncomplete/u);
  assert.match(model, /MaximumProjects = 200[\s\S]*PortfolioCurrencyGroup/u);
  assert.doesNotMatch(builder, /exchangeRate|fxRate|OverallHealth/iu);
});

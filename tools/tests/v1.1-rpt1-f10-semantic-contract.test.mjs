import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const read = (path) => readFileSync(path, "utf8");
const contract = read("docs/architecture/pmcs-v1.1-rpt1-f10-portfolio-summary-semantic-contract.md");
const roadmap = read("docs/roadmaps/pmcs-post-v1-product-evolution.md");
const adr = read("docs/adr/0010-portfolio-state-without-composite-health.md");
const permission = read("src/backend/Pmcs.BuildingBlocks/Application/IProjectPermissionService.cs");
const run = read("src/backend/Pmcs.Modules.Reporting/Domain/ReportRun.cs");
const publisher = read("src/backend/Pmcs.Modules.Documents/Contracts/IGeneratedDocumentPublisher.cs");

test("F10 owns a tenant-scoped portfolio rather than a disguised project report", () => {
  assert.match(roadmap, /Portfolio Summary به تفکیک دسترسی و ارز، بدون تبدیل پنهان/u);
  assert.match(contract, /PMCS-RPT1-F10-SEMANTIC-001[\s\S]*scope=Portfolio/u);
  assert.match(contract, /ProjectId` آن \*\*nullable\*\*[\s\S]*GUID ساختگی/u);
  assert.match(contract, /\/api\/v1\/portfolio\/reports/u);
  assert.match(run, /public Guid\? ProjectId/u);
  assert.match(publisher, /Guid\? ProjectId/u);
  assert.match(contract, /MS37 contract-only Runtime \/ Renderer \/ Migration \/ Catalog \/ API \/ Worker change: None/u);
});

test("F10 pins authorization, cutoff and distinct currency totals", () => {
  assert.match(permission, /HasTenantPermissionAsync[\s\S]*GetProjectScopeAsync/u);
  assert.match(contract, /`portfolio\.read`[\s\S]*`project-state\.read`[\s\S]*تقاطع ورود/u);
  assert.match(contract, /ListProfilesAsync[\s\S]*scope مجاز/u);
  assert.match(contract, /revoke هر permission پین‌شده کل Run/u);
  assert.match(contract, /`NotAuthorized`[\s\S]*بدون مبلغ، تعداد/u);
  assert.match(contract, /cutoff UTC[\s\S]*Time Zone[\s\S]*cutoffLocalDate/u);
  assert.match(adr, /currencyCode[\s\S]*تبدیل ارز/u);
  assert.match(contract, /IRR و USD هرگز بدون FX policy نسخه‌دار تبدیل، جمع/u);
  assert.match(contract, /OverallHealth[\s\S]*ممنوع/u);
});

test("F10 acceptance matrix is independent, bounded and ready for MS38", () => {
  const ids = [...contract.matchAll(/^\| `(F10-[A-Z]\d{2})` \|/gmu)].map((match) => match[1]);
  assert.equal(ids.length, 30);
  assert.equal(new Set(ids).size, ids.length);
  for (const id of ["F10-P01", "F10-C01", "F10-T01", "F10-S01", "F10-B01", "F10-O01", "F10-G01"]) {
    assert.ok(ids.includes(id), `Missing ${id}`);
  }
  assert.match(contract, /S07-MS38` فقط[\s\S]*Documents owner contract/u);
  assert.match(contract, /MaximumProjects=200[\s\S]*Take\(200\)/u);
});

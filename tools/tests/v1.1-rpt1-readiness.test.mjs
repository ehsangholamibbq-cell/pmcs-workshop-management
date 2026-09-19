import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import test from "node:test";

const required = [
  "docs/audits/pmcs-continuity-audit-2026-09-18.md",
  "docs/adr/0029-certified-reporting-core.md",
  "docs/architecture/pmcs-v1.1-reporting-center-phase1.md",
  "docs/api/reporting-v1.md",
  "docs/security/pmcs-v1.1-reporting-security.md",
  "docs/qa/pmcs-v1.1-rpt1-test-matrix.md",
  "docs/runbooks/reporting-center.md",
  "docs/checkpoints/v1.1-rpt1-readiness.md",
];

test("RPT1 readiness pack is complete without claiming runtime completion", () => {
  for (const path of required) assert.equal(existsSync(path), true, `Missing ${path}`);
  const checkpoint = read("docs/checkpoints/v1.1-rpt1-readiness.md");
  assert.match(checkpoint, /Ready for Implementation/u);
  assert.match(checkpoint, /Runtime change: None/u);
  assert.match(checkpoint, /PMCS V1\.1 همچنان `Feature Complete`، `Qualified`، `Final` یا `Baseline Locked` نیست/u);
});

test("RPT1 preserves module ownership and permission-aware semantic reporting", () => {
  const architecture = read("docs/architecture/pmcs-v1.1-reporting-center-phase1.md");
  const adr = read("docs/adr/0029-certified-reporting-core.md");
  for (const requiredText of [
    "reporting.center",
    "Semantic Snapshot",
    "IDailyReportReportingSource",
    "reporting.catalog.read",
    "reporting.run.create",
    "reporting.output.download",
    "reporting.template.publish",
    "NoData/NotConfigured/InsufficientData",
    "reporting.report.completed.v1",
  ]) {
    assert.match(`${architecture}\n${adr}`, new RegExp(requiredText.replaceAll(".", "\\."), "u"));
  }
  assert.match(adr, /Query مستقیم Reporting به Schemaهای ماژول/u);
  assert.match(adr, /generic Documents download برای ReportOutput/u);
  assert.match(adr, /LLM در محاسبه، Snapshot، جمع، KPI، انتخاب Fact یا تولید رقم گزارش نقشی ندارد/u);
});

test("RPT1 API is closed to SQL, arbitrary templates and public report downloads", () => {
  const api = read("docs/api/reporting-v1.md");
  assert.match(api, /POST \/api\/v1\/projects\/\{projectId\}\/reports\/runs/u);
  assert.match(api, /GET \/api\/v1\/projects\/\{projectId\}\/reports\/outputs\/\{outputId\}\/content/u);
  assert.match(api, /Idempotency-Key/u);
  assert.match(api, /Source permission/u);
  assert.match(api, /generic Documents endpoint[\s\S]*`ReportOutput` پاسخ content نمی‌دهد/u);
  assert.match(api, /هیچ endpoint عمومی Query\/SQL\/Template executable دریافت نمی‌کند/u);
});

test("RPT1 test contract includes deterministic, visual, security and recovery gates", () => {
  const matrix = read("docs/qa/pmcs-v1.1-rpt1-test-matrix.md");
  for (const gate of [
    "Golden semantic dataset",
    "PDF Golden",
    "XLSX Golden",
    "Permission و Security",
    "Load/Soak",
    "تمام ۷ Suite V1",
    "backup/restore",
  ]) assert.match(matrix, new RegExp(gate, "u"));
  assert.match(matrix, /Draft v3 غایب است/u);
  assert.match(matrix, /generic Documents download برای ReportOutput مسدود باشد/u);
});

test("continuity audit retains seven Agent stages and separates open visual gates", () => {
  const audit = read("docs/audits/pmcs-continuity-audit-2026-09-18.md");
  for (let stage = 1; stage <= 7; stage += 1) {
    assert.match(audit, new RegExp(`AGENT-S${stage}`, "u"));
  }
  for (const gate of ["VX-G1", "VX-G3", "VX-G4", "VX-G5"]) {
    assert.match(audit, new RegExp(gate, "u"));
  }
  assert.match(audit, /RPT1 Active/u);
  assert.match(audit, /346fbb778aa5c4475fd48df3241b700341e96d83/u);
  assert.match(audit, /Run 113 \(`35437832281`\)/u);
  assert.match(audit, /Gate خروج باز است/u);
});

function read(path) {
  return readFileSync(path, "utf8");
}

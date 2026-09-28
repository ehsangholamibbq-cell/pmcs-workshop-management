import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import test from "node:test";
import { compileTypography } from "../typography/sync.mjs";

const read = (path) => readFileSync(path, "utf8");
const contract = JSON.parse(read("assets/typography/pmcs-fonts.json"));

test("one versioned font contract serves active UI, offline, PDF, XLSX and print inheritance", () => {
  const generated = compileTypography(contract);
  assert.match(generated.css, /--pmcs-font-family/u);
  assert.match(read("src/web/app/layout.tsx"), /typography\.generated\.css/u);
  assert.equal(read("src/web/app/typography.generated.css"), read("src/web/public/typography/pmcs-fonts.css"));
  assert.match(read("src/web/public/offline.html"), /\/typography\/pmcs-fonts\.css/u);
  assert.match(read("src/web/app/globals.css"), /font-family: var\(--pmcs-font-family/u);
  assert.match(read("src/web/public/offline.html"), /font-family: var\(--pmcs-font-family/u);
  assert.match(read("src/web/public/sw.js"), /\/typography\/pmcs-fonts\.css/u);
  assert.match(read("src/backend/Pmcs.Modules.Reporting/Rendering/CertifiedPdfRuntimeContract.cs"),
    /PmcsTypographyContract\.PdfFamily/u);
  assert.match(read("src/backend/Pmcs.Modules.Reporting/Rendering/ReportingRendererOptions.cs"),
    /PmcsTypographyContract\.PdfRegularFileName/u);
  assert.match(read("deploy/docker/api.Dockerfile"), /COPY assets\/reporting\/fonts\/ \/app\/fonts\//u);
  assert.match(read("tools/qa/seed-diagnostics.sh"), /\. tools\/qa\/typography-env\.sh/u);
  const renderers = ["DailyReport", "ProjectPeriodicReport", "ExecutiveProjectStateReport",
    "ProjectProgressReport", "ProjectFinancialPositionReport", "ProjectCommercialProcurementSupplyReport",
    "ProjectTechnicalOfficeReport", "ProjectQualityHseReport", "ProjectGovernanceActionReport",
    "PortfolioSummaryReport"];
  for (const name of renderers) {
    assert.match(read(`src/backend/Pmcs.Modules.Reporting/Rendering/${name}XlsxRenderer.cs`),
      /PmcsTypographyContract\.XlsxFamily/u);
  }
  assert.doesNotMatch(read("src/backend/Pmcs.Api/appsettings.json"), /PdfRegularFontSha256|PdfBoldFontSha256/u);
});

test("a future font swap changes generated UI, offline cache, PDF and XLSX from one manifest", () => {
  const candidate = { ...contract, contractVersion: "2.0.0",
    web: { ...contract.web, family: "New Persian UI" },
    pdf: { ...contract.pdf, family: "New Persian PDF" },
    xlsx: { family: "New Persian XLSX" } };
  const generated = compileTypography(candidate);
  assert.match(generated.css, /"New Persian UI"/u);
  assert.match(generated.cs, /PdfFamily = "New Persian PDF"/u);
  assert.match(generated.cs, /XlsxFamily = "New Persian XLSX"/u);
  assert.match(generated.manifest, /"family": "New Persian PDF"/u);
  assert.equal(generated.cacheName, "pmcs-public-shell-font-v2.0.0");
  const selfHosted = compileTypography({ ...candidate, web: { ...candidate.web,
    asset: { file: "NewPersian.woff2", sha256: "a".repeat(64), weight: "100 900" } } });
  assert.match(selfHosted.css, /@font-face[\s\S]*font-display: swap/u);
  assert.ok(selfHosted.assets.includes("/typography/NewPersian.woff2"));
  assert.throws(() => compileTypography({ ...candidate, pdf: { ...candidate.pdf,
    regular: { ...candidate.pdf.regular, sha256: "incorrect" } } }), /Invalid versioned/u);
  assert.throws(() => compileTypography({ ...candidate,
    web: { ...candidate.web, family: "font\"; evil: true" } }), /Invalid versioned/u);
});

test("generated font surfaces and pinned PDF file bytes have no drift", () => {
  assert.match(execFileSync(process.execPath, ["tools/typography/sync.mjs", "--check"],
    { encoding: "utf8" }), /verified/u);
});

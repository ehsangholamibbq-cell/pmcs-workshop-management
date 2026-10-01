import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const read = (path) => readFileSync(path, "utf8");
const root = "src/backend/Pmcs.Modules.Reporting/Rendering/";

test("F10 render contract revalidates immutable snapshot, manifest, identity and classification", () => {
  const contract = read(`${root}PortfolioSummaryReportRenderingContracts.cs`);
  assert.match(contract, /PortfolioSummaryReportSnapshotBuilder\.Build/u);
  assert.match(contract, /CanonicalJson\.Serialize\(snapshot\) != rebuilt\.PayloadJson/u);
  assert.match(contract, /CanonicalJson\.Serialize\(manifest\) != rebuilt\.SourceManifestJson/u);
  assert.match(contract, /request\.Classification != rebuilt\.Classification/u);
  assert.match(contract, /TemplateContentDigest[\s\S]*SnapshotSha256[\s\S]*SourceManifestSha256/u);
  assert.doesNotMatch(contract, /DbContext|GetProjectScopeAsync|\.LoadAsync\(/u);
});

test("F10 PDF and XLSX keep currency dimensions separate, gaps explicit and output deterministic", () => {
  const pdf = read(`${root}PortfolioSummaryReportPdfRenderer.cs`);
  const xlsx = read(`${root}PortfolioSummaryReportXlsxRenderer.cs`);
  const formatting = read(`${root}PortfolioSummaryReportFormatting.cs`);
  assert.match(pdf, /RecognizedSpendSubtotal[\s\S]*ExternalNetCashSubtotal[\s\S]*TotalCommittedSubtotal[\s\S]*OpenCommitmentSubtotal/u);
  assert.match(xlsx, /"Currencies"[\s\S]*"Projects"[\s\S]*"Finance"[\s\S]*"Commercial"/u);
  assert.match(xlsx, /CompressionLevel\.NoCompression[\s\S]*LastWriteTime = new DateTimeOffset\(1980/u);
  assert.match(xlsx, /SafeSpreadsheetText/u);
  assert.match(formatting, /NotAuthorized => "بدون مجوز"[\s\S]*InsufficientData => "داده ناکافی؛ صفر فرض نشود"/u);
  assert.doesNotMatch(pdf + xlsx, /exchangeRate|fxRate|OverallHealth|TotalPortfolio/iu);
});

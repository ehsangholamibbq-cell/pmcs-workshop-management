import assert from "node:assert/strict";
import test from "node:test";
import { createHash } from "node:crypto";
import { loadPortfolioReportingCenter, type PortfolioReportRunView } from
  "../lib/portfolio-reporting-center.ts";
import { downloadPortfolioReportOutput, PortfolioReportOutputAccessError } from
  "../lib/portfolio-reporting-output.ts";

const id = "10000000-0000-4000-8000-000000000088";
const pdf = "%PDF-1.7\nPMCS portfolio verified\n";
const bytes = new TextEncoder().encode(pdf);
const output = { id, format: "Pdf", fileName: "portfolio.pdf", contentType: "application/pdf",
  sizeBytes: bytes.byteLength, sha256: createHash("sha256").update(bytes).digest("hex"),
  verificationCode: "verified" };
const run: PortfolioReportRunView = { id: "10000000-0000-4000-8000-000000000099",
  definitionCode: "portfolio-summary-certified", status: "Succeeded",
  pipelineStage: "Complete", dataStatus: "Available",
  createdAt: "2026-09-28T00:00:00Z", outputs: [output] };

test("portfolio output reads gated tenant bytes and verifies digest", async () => {
  const previous = globalThis.fetch;
  try {
    globalThis.fetch = async (url, init) => {
      assert.equal(String(url), `/api/pmcs/api/v1/portfolio/reports/outputs/${id}/content`);
      assert.equal(init?.cache, "no-store");
      return new Response(pdf, { headers: { "content-type": "application/pdf" } });
    };
    const file = await downloadPortfolioReportOutput("/api/pmcs", run, output);
    assert.equal(file.size, output.sizeBytes);
    assert.equal(file.type, "application/pdf");
  } finally { globalThis.fetch = previous; }
});

test("portfolio output refuses revocation, changed bytes and output outside the run", async () => {
  const previous = globalThis.fetch;
  let reads = 0;
  try {
    globalThis.fetch = async () => { reads += 1; return new Response(null, { status: 403 }); };
    await assert.rejects(downloadPortfolioReportOutput("/api/pmcs",
      { ...run, outputs: [] }, output), /مشخصات خروجی/u);
    assert.equal(reads, 0);
    await assert.rejects(downloadPortfolioReportOutput("/api/pmcs", run, output),
      (error: unknown) => error instanceof PortfolioReportOutputAccessError && error.status === 403);
    globalThis.fetch = async () => new Response(pdf.replace("verified", "tampered"), {
      headers: { "content-type": "application/pdf" },
    });
    await assert.rejects(downloadPortfolioReportOutput("/api/pmcs", run, output), /صحت خروجی/u);
  } finally { globalThis.fetch = previous; }
});

test("portfolio history rejects unsafe output metadata before download", async () => {
  const previous = globalThis.fetch;
  try {
    let reads = 0;
    globalThis.fetch = async () => Response.json(++reads === 1 ?
      [{ code: "portfolio-summary-certified", scope: "Portfolio", title: "سبد",
        description: "گزارش", templateVersion: "1.0.0",
        supportedFormats: ["Pdf"], dataStatuses: ["Available"] }] :
      [{ ...run, outputs: [{ ...output, fileName: "../portfolio.pdf" }] }]);
    await assert.rejects(loadPortfolioReportingCenter("/api/pmcs"), /سابقهٔ گزارش‌های سبد/u);
  } finally { globalThis.fetch = previous; }
});

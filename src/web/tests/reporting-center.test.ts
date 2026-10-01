import assert from "node:assert/strict";
import test from "node:test";
import { createHash } from "node:crypto";
import { downloadProjectReportOutput, loadProjectReportingCenter,
  ReportOutputAccessError, type ReportRunView } from "../lib/reporting-center.ts";

const projectId = "10000000-0000-4000-8000-000000000001";

test("reporting center reads only the current project's authorized catalog and runs", async () => {
  const previous = globalThis.fetch;
  const urls: string[] = [];
  try {
    globalThis.fetch = async (input, init) => {
      assert.equal(init?.cache, "no-store");
      urls.push(String(input));
      return Response.json(urls.length === 1 ? [{ code: "project-progress-certified", title: "پیشرفت",
        description: "واقعیت تأییدشده", templateVersion: "1.0.0", supportedFormats: ["Pdf"],
        dataStatuses: ["Available", "NoData"] }] : [{ id: "run", projectId,
        definitionCode: "project-progress-certified", status: "Succeeded", pipelineStage: "Complete",
        dataStatus: "Available", createdAt: "2026-09-28T00:00:00Z", outputs: [] }]);
    };
    const result = await loadProjectReportingCenter("/api/pmcs", projectId);
    assert.equal(result.kind, "ready");
    assert.deepEqual(urls, [
      `/api/pmcs/api/v1/projects/${projectId}/reports/catalog`,
      `/api/pmcs/api/v1/projects/${projectId}/reports/runs?limit=50`,
    ]);
  } finally { globalThis.fetch = previous; }
});

test("reporting center remains closed when disabled, revoked or cross-project", async () => {
  const previous = globalThis.fetch;
  try {
    globalThis.fetch = async () => new Response(null, { status: 404 });
    assert.equal((await loadProjectReportingCenter("/api/pmcs", projectId)).kind, "unavailable");
    globalThis.fetch = async () => new Response(null, { status: 403 });
    assert.equal((await loadProjectReportingCenter("/api/pmcs", projectId)).kind, "forbidden");
    let calls = 0;
    globalThis.fetch = async () => Response.json(++calls === 1 ? [] : [{ id: "run",
      projectId: "20000000-0000-4000-8000-000000000002", definitionCode: "unexpected",
      status: "Succeeded", pipelineStage: "Complete", createdAt: "2026-09-28T00:00:00Z",
      outputs: [] }]);
    await assert.rejects(loadProjectReportingCenter("/api/pmcs", projectId), /محدودهٔ پروژه/u);
  } finally { globalThis.fetch = previous; }
});

const outputId = "10000000-0000-4000-8000-000000000088";
const runId = "10000000-0000-4000-8000-000000000099";
const pdf = "%PDF-1.7\nPMCS verified output\n";
const pdfBytes = new TextEncoder().encode(pdf);
const output = { id: outputId, format: "Pdf", fileName: "گزارش.pdf", contentType: "application/pdf",
  sizeBytes: pdfBytes.byteLength, sha256: createHash("sha256").update(pdfBytes).digest("hex"),
  verificationCode: "verified" };
const run: ReportRunView = { id: runId, projectId, definitionCode: "project-progress-certified",
  status: "Succeeded", pipelineStage: "Complete", dataStatus: "Available",
  createdAt: "2026-09-28T00:00:00Z", outputs: [output] };

test("reporting output reads scoped gated bytes and verifies the registered digest", async () => {
  const previous = globalThis.fetch;
  try {
    globalThis.fetch = async (input, init) => {
      assert.equal(String(input), `/api/pmcs/api/v1/projects/${projectId}/reports/outputs/${outputId}/content`);
      assert.equal(init?.cache, "no-store");
      return new Response(pdf, { headers: { "content-type": "application/pdf" } });
    };
    const file = await downloadProjectReportOutput("/api/pmcs", projectId, run, output);
    assert.equal(file.size, output.sizeBytes);
    assert.equal(file.type, "application/pdf");
  } finally { globalThis.fetch = previous; }
});

test("reporting output fails closed on revocation, project mismatch, and changed bytes", async () => {
  const previous = globalThis.fetch;
  let reads = 0;
  try {
    globalThis.fetch = async () => { reads += 1; return new Response(null, { status: 403 }); };
    await assert.rejects(downloadProjectReportOutput("/api/pmcs", projectId,
      { ...run, projectId: "20000000-0000-4000-8000-000000000002" }, output), /مشخصات خروجی/u);
    assert.equal(reads, 0);
    await assert.rejects(downloadProjectReportOutput("/api/pmcs", projectId, run, output),
      (error: unknown) => error instanceof ReportOutputAccessError && error.status === 403);
    globalThis.fetch = async () => new Response(pdf.replace("verified", "tampered"), {
      headers: { "content-type": "application/pdf" },
    });
    await assert.rejects(downloadProjectReportOutput("/api/pmcs", projectId, run, output),
      /صحت خروجی/u);
    globalThis.fetch = async () => new Response(null, { status: 404 });
    await assert.rejects(downloadProjectReportOutput("/api/pmcs", projectId, run, output),
      (error: unknown) => error instanceof ReportOutputAccessError && error.status === 404);
  } finally { globalThis.fetch = previous; }
});

test("reporting history rejects unsafe output metadata before any download", async () => {
  const previous = globalThis.fetch;
  try {
    let reads = 0;
    globalThis.fetch = async () => Response.json(++reads === 1 ? [] : [{
      ...run, outputs: [{ ...output, fileName: "../گزارش.pdf" }],
    }]);
    await assert.rejects(loadProjectReportingCenter("/api/pmcs", projectId), /سابقهٔ گزارش‌ها/u);
  } finally { globalThis.fetch = previous; }
});

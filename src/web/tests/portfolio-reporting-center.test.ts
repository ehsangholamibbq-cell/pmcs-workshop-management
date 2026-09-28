import assert from "node:assert/strict";
import test from "node:test";
import { loadPortfolioReportingCenter } from "../lib/portfolio-reporting-center.ts";

const id = "10000000-0000-4000-8000-000000000099";
const definition = { code: "portfolio-summary-certified", scope: "Portfolio",
  title: "گزارش سبد پروژه‌ها", description: "وضعیت‌های رسمی پروژه‌ها",
  templateVersion: "1.0.0", supportedFormats: ["Pdf", "Xlsx"],
  dataStatuses: ["Available", "NoData"] };
const run = { id, definitionCode: definition.code, status: "Succeeded",
  pipelineStage: "Complete", dataStatus: "Available", createdAt: "2026-09-28T00:00:00Z",
  outputs: [] };

test("portfolio reports read only tenant-scoped catalog and history", async () => {
  const previous = globalThis.fetch;
  const urls: string[] = [];
  try {
    globalThis.fetch = async (input, init) => {
      assert.equal(init?.cache, "no-store");
      urls.push(String(input));
      return Response.json(urls.length === 1 ? [definition] : [run]);
    };
    const result = await loadPortfolioReportingCenter("/api/pmcs");
    assert.equal(result.kind, "ready");
    assert.deepEqual(urls, ["/api/pmcs/api/v1/portfolio/reports/catalog",
      "/api/pmcs/api/v1/portfolio/reports/runs?limit=50"]);
  } finally { globalThis.fetch = previous; }
});

test("portfolio reporting closes before history on disabled and denied catalog", async () => {
  const previous = globalThis.fetch;
  try {
    let calls = 0;
    globalThis.fetch = async () => { calls += 1; return new Response(null, { status: 404 }); };
    assert.equal((await loadPortfolioReportingCenter("/api/pmcs")).kind, "unavailable");
    globalThis.fetch = async () => { calls += 1; return new Response(null, { status: 403 }); };
    assert.equal((await loadPortfolioReportingCenter("/api/pmcs")).kind, "forbidden");
    assert.equal(calls, 2);
  } finally { globalThis.fetch = previous; }
});

test("portfolio reporting rejects project-scoped runs and unexpected definitions", async () => {
  const previous = globalThis.fetch;
  try {
    let calls = 0;
    globalThis.fetch = async () => Response.json(++calls === 1 ? [definition] :
      [{ ...run, projectId: "20000000-0000-4000-8000-000000000002" }]);
    await assert.rejects(loadPortfolioReportingCenter("/api/pmcs"), /محدودهٔ سازمان/u);
    globalThis.fetch = async () => Response.json([{ ...definition, scope: "Project" }]);
    await assert.rejects(loadPortfolioReportingCenter("/api/pmcs"), /فهرست گزارش‌های سبد/u);
  } finally { globalThis.fetch = previous; }
});

import assert from "node:assert/strict";
import test from "node:test";
import { PortfolioReportRequestAccessError, requestPortfolioReport } from
  "../lib/portfolio-reporting-run-request.ts";

const input = { clientGeneratedId: "10000000-0000-4000-8000-000000000099",
  definitionCode: "portfolio-summary-certified" as const, templateVersion: "1.0.0",
  format: "Pdf" as const };

test("portfolio request keeps identity and empty parameters through a transport retry", async () => {
  const previous = globalThis.fetch;
  const attempts: Array<{ url: string; key: string | null; body: unknown }> = [];
  try {
    globalThis.fetch = async (url, init) => {
      attempts.push({ url: String(url), key: new Headers(init?.headers).get("Idempotency-Key"),
        body: JSON.parse(String(init?.body)) as unknown });
      if (attempts.length === 1) return new Response(null, { status: 503 });
      return Response.json({ id: input.clientGeneratedId, definitionCode: input.definitionCode,
        status: "Queued", pipelineStage: "Queued", outputs: [] }, { status: 202 });
    };
    await assert.rejects(requestPortfolioReport("/api/pmcs", input), /پذیرفته نشد/u);
    const run = await requestPortfolioReport("/api/pmcs", input);
    assert.equal(run.id, input.clientGeneratedId);
    assert.deepEqual(attempts[0], attempts[1]);
    assert.equal(attempts[0].url, "/api/pmcs/api/v1/portfolio/reports/runs");
    assert.equal(attempts[0].key, input.clientGeneratedId);
    assert.deepEqual(attempts[0].body, { clientGeneratedId: input.clientGeneratedId,
      definitionCode: input.definitionCode, templateVersion: input.templateVersion,
      asOfUtc: null, formats: ["Pdf"], parameters: {} });
  } finally { globalThis.fetch = previous; }
});

test("portfolio request fails closed on revocation and project-scoped responses", async () => {
  const previous = globalThis.fetch;
  try {
    globalThis.fetch = async () => new Response(null, { status: 403 });
    await assert.rejects(requestPortfolioReport("/api/pmcs", input),
      (error: unknown) => error instanceof PortfolioReportRequestAccessError && error.status === 403);
    globalThis.fetch = async () => Response.json({ id: input.clientGeneratedId,
      definitionCode: input.definitionCode, projectId: "20000000-0000-4000-8000-000000000002",
      outputs: [] }, { status: 202 });
    await assert.rejects(requestPortfolioReport("/api/pmcs", input), /محدودهٔ سازمان/u);
  } finally { globalThis.fetch = previous; }
});

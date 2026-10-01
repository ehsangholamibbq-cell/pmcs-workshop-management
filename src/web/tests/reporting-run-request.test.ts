import assert from "node:assert/strict";
import test from "node:test";
import {
  isValidProjectPeriodStart, ReportRequestAccessError, requestProjectReport,
} from "../lib/reporting-run-request.ts";

const projectId = "10000000-0000-4000-8000-000000000001";
const clientGeneratedId = "20000000-0000-4000-8000-000000000002";
const input = { projectId, clientGeneratedId, definitionCode: "project-progress-certified",
  templateVersion: "1.0.0", format: "Pdf" as const };

test("report request uses the stable client identity and strict parameter shape", async () => {
  const previous = globalThis.fetch;
  try {
    globalThis.fetch = async (url, init) => {
      assert.equal(String(url), `/api/pmcs/api/v1/projects/${projectId}/reports/runs`);
      assert.equal(init?.method, "POST");
      assert.equal((init?.headers as Record<string, string>)["Idempotency-Key"], clientGeneratedId);
      assert.deepEqual(JSON.parse(String(init?.body)), {
        clientGeneratedId, definitionCode: input.definitionCode, templateVersion: "1.0.0",
        asOfUtc: null, formats: ["Pdf"], parameters: {},
      });
      return Response.json({ id: clientGeneratedId, projectId,
        definitionCode: input.definitionCode, outputs: [] }, { status: 202 });
    };
    assert.equal((await requestProjectReport("/api/pmcs", input)).id, clientGeneratedId);
    await assert.rejects(requestProjectReport("/api/pmcs", { ...input,
      definitionCode: "daily-report-certified" }), /ورودی روز/u);
  } finally { globalThis.fetch = previous; }
});

test("daily and periodic report requests pin only their approved input contracts", async () => {
  const previous = globalThis.fetch;
  const parameters: unknown[] = [];
  try {
    globalThis.fetch = async (_url, init) => {
      const body = JSON.parse(String(init?.body)) as { parameters: unknown; definitionCode: string };
      parameters.push(body.parameters);
      return Response.json({ id: clientGeneratedId, projectId,
        definitionCode: body.definitionCode, outputs: [] }, { status: 202 });
    };
    await requestProjectReport("/api/pmcs", { ...input, definitionCode: "daily-report-certified",
      parameters: { dailyReportId: "30000000-0000-4000-8000-000000000003", includeRevisionChain: true } });
    await requestProjectReport("/api/pmcs", { ...input, definitionCode: "project-periodic-certified",
      parameters: { periodKind: "Monthly", periodStartLocalDate: "2026-09-23" } });
    assert.deepEqual(parameters, [
      { dailyReportId: "30000000-0000-4000-8000-000000000003", includeRevisionChain: true },
      { periodKind: "Monthly", periodStartLocalDate: "2026-09-23" },
    ]);
    assert.equal(isValidProjectPeriodStart("Weekly", "2026-09-26"), true);
    assert.equal(isValidProjectPeriodStart("Monthly", "2026-09-28"), false);
    await assert.rejects(requestProjectReport("/api/pmcs", { ...input,
      definitionCode: "project-periodic-certified", parameters: {
        periodKind: "Weekly", periodStartLocalDate: "not-a-date" } }), /ورودی روز/u);
  } finally { globalThis.fetch = previous; }
});

test("report request fails closed on revocation or another project's response", async () => {
  const previous = globalThis.fetch;
  try {
    globalThis.fetch = async () => new Response(null, { status: 403 });
    await assert.rejects(requestProjectReport("/api/pmcs", input),
      (error: unknown) => error instanceof ReportRequestAccessError && error.status === 403);
    globalThis.fetch = async () => Response.json({ id: clientGeneratedId,
      projectId: "30000000-0000-4000-8000-000000000003", definitionCode: input.definitionCode,
      outputs: [] }, { status: 202 });
    await assert.rejects(requestProjectReport("/api/pmcs", input), /پروژه مطابقت ندارد/u);
  } finally { globalThis.fetch = previous; }
});

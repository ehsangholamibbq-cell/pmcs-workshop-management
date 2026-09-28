import assert from "node:assert/strict";
import test from "node:test";
import { loadProjectReportingCenter } from "../lib/reporting-center.ts";

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

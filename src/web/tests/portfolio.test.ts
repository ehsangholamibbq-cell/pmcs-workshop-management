import assert from "node:assert/strict";
import test from "node:test";
import { getPortfolioCommandCenter } from "../lib/portfolio.ts";

test("portfolio command center is tenant and user scoped", async () => {
  const originalFetch = globalThis.fetch;
  let capturedUrl = "";
  let capturedHeaders: HeadersInit | undefined;
  globalThis.fetch = async (input, init) => {
    capturedUrl = String(input);
    capturedHeaders = init?.headers;
    return Response.json({
      contractVersion: "portfolio-command-center-v1",
      generatedAt: "2026-09-10T08:00:00Z",
      header: {},
      projects: [],
      actionExceptions: [],
    });
  };

  try {
    const model = await getPortfolioCommandCenter(
      "https://pmcs.test/",
      { tenantId: "tenant-id", userId: "user-id" },
    );
    const headers = new Headers(capturedHeaders);

    assert.equal(capturedUrl, "https://pmcs.test/api/v1/portfolio/command-center");
    assert.equal(headers.get("X-Tenant-Id"), "tenant-id");
    assert.equal(headers.get("X-User-Id"), "user-id");
    assert.equal(model.contractVersion, "portfolio-command-center-v1");
  } finally {
    globalThis.fetch = originalFetch;
  }
});

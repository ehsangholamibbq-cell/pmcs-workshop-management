import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const output = resolve(root, "src/web/artifacts/ms76-portfolio-refresh-truth");

test("portfolio refresh hides an old aggregate until a new authorized response", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  async function capture(name: string, width: number, height: number) {
    await page.evaluate(() => document.fonts.ready);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    const bytes = await page.screenshot({ animations: "disabled", caret: "hide" });
    expect(bytes.readUInt32BE(16)).toBe(width);
    expect(bytes.readUInt32BE(20)).toBe(height);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"),
      bytes: bytes.length, width, height });
  }

  let reads = 0;
  let releaseRefresh: () => void = () => {};
  const refreshGate = new Promise<void>(resolve => { releaseRefresh = resolve; });
  const aggregate = { contractVersion: "portfolio-command-center-v1", generatedAt: "2026-09-29T12:00:00Z",
    header: { projectCount: 7, activeProjectCount: 5, onHoldProjectCount: 0, closingProjectCount: 0,
      stableProjectCount: 0, watchProjectCount: 0, atRiskProjectCount: 0, criticalProjectCount: 0,
      insufficientDataProjectCount: 0, noDataProjectCount: 0, staleProjectCount: 0, outdatedProjectCount: 0,
      actionVisibleProjectCount: 0, openActionCount: 0, overdueActionCount: 0,
      pendingCommercialApprovalCount: 0, latestSnapshotAt: null, currencyExposures: [] },
    projects: [], actionExceptions: [] };
  await page.route("**/api/pmcs/api/v1/portfolio/command-center", async route => {
    reads += 1;
    if (reads === 2) await refreshGate;
    if (reads === 2) return route.fulfill({ status: 503, contentType: "application/problem+json",
      body: JSON.stringify({ status: 503, code: "service.unavailable", title: "Sensitive upstream error" }) });
    if (reads === 3) return route.fulfill({ status: 403, contentType: "application/problem+json",
      body: JSON.stringify({ status: 403, title: "Sensitive permission error" }) });
    return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(aggregate) });
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/portfolio");
  const workspace = page.locator(".portfolio-workspace");
  await expect(workspace).toHaveAttribute("data-read-state", "current");
  await expect(page.locator(".portfolio-kpis")).toContainText("۷");
  await capture("portfolio-390-current.png", 390, 844);

  await page.getByRole("button", { name: "تازه‌سازی" }).click();
  await expect(workspace).toHaveAttribute("data-read-state", "loading");
  await expect(page.locator(".portfolio-loading-preview")).toBeVisible();
  await expect(page.locator(".portfolio-kpis")).toHaveCount(0);
  await page.setViewportSize({ width: 320, height: 720 });
  await capture("portfolio-320-refreshing.png", 320, 720);
  releaseRefresh();
  await expect(workspace).toHaveAttribute("data-read-state", "error");
  await expect(page.locator(".portfolio-kpis")).toHaveCount(0);
  await expect(workspace.getByRole("alert")).not.toContainText("Sensitive upstream error");
  await capture("portfolio-320-read-error.png", 320, 720);

  await page.getByRole("button", { name: "تلاش دوباره" }).click();
  await expect(workspace).toHaveAttribute("data-read-state", "error");
  await expect(page.locator(".portfolio-kpis")).toHaveCount(0);
  await expect(workspace.getByRole("alert")).not.toContainText("Sensitive permission error");
  await capture("portfolio-320-no-permission.png", 320, 720);

  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    route: "/portfolio", owner: "VX-G4 portfolio aggregate refresh truth", files,
  }, null, 2) + "\n");
});

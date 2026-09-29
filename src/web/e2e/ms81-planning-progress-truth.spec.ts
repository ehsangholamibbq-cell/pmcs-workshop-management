import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";
import { projectId } from "./support";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const output = resolve(root, "src/web/artifacts/ms81-planning-progress-truth");
const itemId = "70000000-0000-4000-8000-000000000081";

test("progress ledger hides an old item on refresh, failed read and access loss", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  async function capture(name: string, width: number, height: number,
    state: "current" | "loading" | "unavailable" | "forbidden") {
    const panel = page.getByTestId("planning-progress");
    let bytes: Buffer | null = null;
    for (let attempt = 0; attempt < 8; attempt += 1) {
      await expect(panel).toHaveAttribute("data-read-state", state);
      await page.evaluate(() => document.fonts.ready);
      await panel.evaluate((element) => window.scrollTo({
        top: Math.max(0, window.scrollY + element.getBoundingClientRect().top - 150),
        behavior: "instant",
      }));
      const headingTop = () => panel.getByRole("heading", { name: "دفتر مستقل اندازه‌گیری پیشرفت" })
        .evaluate((element) => element.getBoundingClientRect().top);
      await expect.poll(async () => {
        const top = await headingTop();
        if (top < 120 || top >= 250) await panel.evaluate((element) =>
          window.scrollTo({ top: Math.max(0, window.scrollY + element.getBoundingClientRect().top - 150),
            behavior: "instant" }));
        await page.waitForTimeout(150);
        const settled = await headingTop();
        return settled >= 120 && settled < 250;
      }, { timeout: 10_000 }).toBe(true);
      await page.waitForTimeout(400);
      if (await panel.getAttribute("data-read-state") !== state) continue;
      const candidate = await page.screenshot({ animations: "disabled", caret: "hide" });
      if (await panel.getAttribute("data-read-state") !== state || candidate.length < 10_000) continue;
      bytes = candidate;
      break;
    }
    if (!bytes) throw new Error(`Progress panel did not remain ${state} during ${name}`);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    expect(bytes.readUInt32BE(16)).toBe(width);
    expect(bytes.readUInt32BE(20)).toBe(height);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"),
      bytes: bytes.length, width, height });
  }

  const item = { id: itemId, code: "MEAS-81", title: "قلم شاهد برنامه", unit: "مترمکعب",
    targetQuantity: 100, notes: null, status: "Active", revision: 1,
    lastModifiedAt: "2026-09-29T10:00:00Z" };
  const ledger = { planningMode: "None", measurementBasisState: "TargetsAvailable",
    officialProgressBasisState: "NotConfigured", scheduleBasisState: "NotConfigured",
    approvedBaselineId: null, approvedBaselineVersion: null, approvedBaselineKind: null,
    officialOverallPhysicalPercent: null, plannedOverallPhysicalPercent: null,
    scheduleVariancePercent: null, forecastCompletionDate: null, missingActualEntryCount: 0,
    approvedFactCount: 0, provisionalFactCount: 0, unlinkedApprovedFactCount: 0,
    unlinkedProvisionalFactCount: 0, items: [{ measurementItemId: itemId, code: item.code,
      title: item.title, unit: item.unit, targetQuantity: 100, isActive: true,
      approvedQuantity: null, provisionalQuantity: null, approvedCompletionPercent: null,
      latestApprovedReportDate: null }], milestones: [] };
  let responseStatus = 200;
  let releaseFailure: () => void = () => {};
  const failureGate = new Promise<void>(resolve => { releaseFailure = resolve; });
  for (const [endpoint, body] of [["measurement-items", [item]], ["progress", ledger]] as const) {
    await page.route(`**/api/pmcs/api/v1/projects/${projectId}/planning/${endpoint}`, async route => {
      if (responseStatus === 503) await failureGate;
      return route.fulfill(responseStatus === 200
        ? { status: 200, contentType: "application/json", body: JSON.stringify(body) }
        : { status: responseStatus, contentType: "application/problem+json",
          body: JSON.stringify({ status: responseStatus, title: "Sensitive progress detail" }) });
    });
  }

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/projects/${projectId}`);
  const panel = page.getByTestId("planning-progress");
  await expect(panel).toHaveAttribute("data-read-state", "current");
  await expect(panel.getByText(item.title)).toBeVisible();
  await capture("progress-390-current.png", 390, 844, "current");

  responseStatus = 503;
  await page.reload();
  await expect(panel).toHaveAttribute("data-read-state", "loading");
  await expect(panel.getByText(item.title)).toHaveCount(0);
  await page.setViewportSize({ width: 320, height: 720 });
  await capture("progress-320-refreshing.png", 320, 720, "loading");
  releaseFailure();
  await expect(panel).toHaveAttribute("data-read-state", "unavailable");
  await expect(panel.getByRole("alert")).not.toContainText("Sensitive progress detail");
  await expect(panel.getByRole("alert")).toHaveCSS("background-color", "rgb(255, 240, 237)");
  await expect(panel.getByText(item.title)).toHaveCount(0);
  await expect(panel.getByRole("button", { name: "افزودن قلم" })).toHaveCount(0);
  await capture("progress-320-read-error.png", 320, 720, "unavailable");

  responseStatus = 403;
  await page.reload();
  await expect(panel).toHaveAttribute("data-read-state", "forbidden");
  await expect(panel.getByRole("alert")).toContainText("دادهٔ قبلی نمایش داده نمی‌شود");
  await expect(panel.getByRole("alert")).toHaveCSS("background-color", "rgb(255, 240, 237)");
  await expect(panel.getByText(item.title)).toHaveCount(0);
  await capture("progress-320-access-revoked.png", 320, 720, "forbidden");

  responseStatus = 200;
  await page.reload();
  await expect(panel).toHaveAttribute("data-read-state", "current");
  await expect(panel.getByText(item.title)).toBeVisible();
  await capture("progress-320-restored.png", 320, 720, "current");

  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    route: `/projects/${projectId}`, owner: "VX-G4 planning progress read truth", files,
  }, null, 2) + "\n");
});

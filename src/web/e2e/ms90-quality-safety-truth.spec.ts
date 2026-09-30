import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";
import { projectId } from "./support";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const output = resolve(root, "src/web/artifacts/ms90-quality-safety-truth");

test("quality and safety withholds sensitive state and formal commands until a fresh authorized read", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  async function capture(name: string, width: number, height: number,
    state: "current" | "loading" | "offline" | "unavailable" | "forbidden") {
    const panel = page.getByTestId("quality-safety-panel");
    await expect(panel).toHaveAttribute("data-read-state", state);
    await page.evaluate(() => document.fonts.ready);
    await expect.poll(async () => {
      const top = await panel.getByRole("heading", { name: "کیفیت و ایمنی، بهداشت و محیط‌زیست (HSE)" })
        .evaluate(element => element.getBoundingClientRect().top);
      if (top < 120 || top >= 250) await panel.evaluate(element =>
        window.scrollTo({ top: Math.max(0, window.scrollY + element.getBoundingClientRect().top - 150), behavior: "instant" }));
      return top >= 120 && top < 250;
    }, { timeout: 10_000 }).toBe(true);
    await page.waitForTimeout(300);
    await expect(panel).toHaveAttribute("data-read-state", state);
    const bytes = await page.screenshot({ animations: "disabled", caret: "hide" });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    expect(bytes.readUInt32BE(16)).toBe(width);
    expect(bytes.readUInt32BE(20)).toBe(height);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"), bytes: bytes.length, width, height });
  }

  const state = { qualityState: "Available", hseState: "Available", configuration: null,
    matrices: [], intakes: [], inspections: [], nonConformances: [], defects: [], incidents: [],
    correctiveActions: [], permits: [], toolboxTalks: [], inspectionTestPlans: [], checklistTemplates: [],
    testRecords: [], competencyRecords: [], exposureHours: [], openQualityCount: 17, openHseCount: 5,
    overdueCorrectiveActionCount: 0, incidentRatesAvailable: false,
    incidentRateUnavailableReason: null, reportedIncidentFrequencyPerTwoHundredThousandHours: null };
  let status = 200;
  let releaseFailure: () => void = () => {};
  const failureGate = new Promise<void>(resolve => { releaseFailure = resolve; });
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/quality-safety/state`, async route => {
    if (status === 503) await failureGate;
    return route.fulfill(status === 200
      ? { status: 200, contentType: "application/json", body: JSON.stringify(state) }
      : { status, contentType: "application/problem+json", body: JSON.stringify({ status, title: "Sensitive safety detail" }) });
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/projects/${projectId}`);
  const panel = page.getByTestId("quality-safety-panel");
  await expect(panel).toHaveAttribute("data-read-state", "current");
  await expect(panel).toContainText("۱۷ پرونده باز");
  await capture("quality-safety-390-current.png", 390, 844, "current");

  await page.context().setOffline(true);
  await expect(panel).toHaveAttribute("data-read-state", "offline");
  await expect(panel).not.toContainText("۱۷ پرونده باز");
  await expect(panel.getByRole("button", { name: "ثبت اولیه غیررسمی" })).toBeEnabled();
  await expect(panel.getByRole("button", { name: "ثبت درخواست مستقل از نتیجه" })).toHaveCount(0);
  await page.setViewportSize({ width: 320, height: 720 });
  await capture("quality-safety-320-offline-intake.png", 320, 720, "offline");
  await page.context().setOffline(false);
  await expect(panel).toHaveAttribute("data-read-state", "current");

  status = 503;
  await page.context().setOffline(true);
  await expect(panel).toHaveAttribute("data-read-state", "offline");
  await page.context().setOffline(false);
  await expect(panel).toHaveAttribute("data-read-state", "loading");
  await expect(panel).not.toContainText("۱۷ پرونده باز");
  await page.setViewportSize({ width: 320, height: 720 });
  await capture("quality-safety-320-refreshing.png", 320, 720, "loading");
  releaseFailure();
  await expect(panel).toHaveAttribute("data-read-state", "unavailable");
  await expect(panel.getByRole("alert")).not.toContainText("Sensitive safety detail");
  await expect(panel).not.toContainText("۱۷ پرونده باز");
  await expect(panel.getByRole("button", { name: "ثبت اولیه غیررسمی" })).toBeDisabled();
  await expect(panel.getByRole("button", { name: "ثبت درخواست مستقل از نتیجه" })).toHaveCount(0);
  await capture("quality-safety-320-read-error.png", 320, 720, "unavailable");

  status = 403;
  await panel.getByRole("button", { name: "تلاش دوباره برای دریافت وضعیت کیفیت و ایمنی" }).click();
  await expect(panel).toHaveAttribute("data-read-state", "forbidden");
  await expect(panel.getByRole("alert")).toContainText("دادهٔ قبلی نمایش داده نمی‌شود");
  await expect(panel).not.toContainText("۱۷ پرونده باز");
  await capture("quality-safety-320-access-revoked.png", 320, 720, "forbidden");

  status = 200;
  await panel.getByRole("button", { name: "تلاش دوباره برای دریافت وضعیت کیفیت و ایمنی" }).click();
  await expect(panel).toHaveAttribute("data-read-state", "current");
  await expect(panel).toContainText("۱۷ پرونده باز");
  await capture("quality-safety-320-restored.png", 320, 720, "current");

  writeFileSync(resolve(output, "index.json"), JSON.stringify({ contractVersion: 1,
    source: process.env.PMCS_SOURCE_HEAD_SHA ?? null, route: `/projects/${projectId}`,
    owner: "VX-G4 quality and safety read truth", files }, null, 2) + "\n");
});

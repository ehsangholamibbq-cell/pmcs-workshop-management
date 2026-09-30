import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";
import { projectId } from "./support";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const output = resolve(root, "src/web/artifacts/ms91-advisory-read-truth");

test("advisory insight hides an old private analysis and review commands after failed or forbidden read", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  async function capture(name: string, width: number, height: number,
    state: "current" | "loading" | "unavailable" | "forbidden") {
    const panel = page.getByTestId("advisory-insights");
    await expect(panel).toHaveAttribute("data-read-state", state);
    await page.evaluate(() => document.fonts.ready);
    await expect.poll(async () => {
      const top = await panel.getByRole("heading", { name: "تحلیل مشورتی مستند" })
        .evaluate(element => element.getBoundingClientRect().top);
      if (top < 120 || top >= 250) await panel.evaluate(element =>
        window.scrollTo({ top: Math.max(0, window.scrollY + element.getBoundingClientRect().top - 150), behavior: "instant" }));
      return top >= 120 && top < 250;
    }, { timeout: 10_000 }).toBe(true);
    await page.waitForTimeout(350);
    await expect(panel).toHaveAttribute("data-read-state", state);
    const bytes = await page.screenshot({ animations: "disabled", caret: "hide" });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    expect(bytes.readUInt32BE(16)).toBe(width);
    expect(bytes.readUInt32BE(20)).toBe(height);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"), bytes: bytes.length, width, height });
  }

  const result = { providerConfigured: true, canGenerate: true, canReview: true,
    activeRequests: [], recentRequests: [], insights: [{ insightId: "70000000-0000-4000-8000-000000000091",
      projectId, generationRequestId: "70000000-0000-4000-8000-000000000092",
      snapshotId: "70000000-0000-4000-8000-000000000093", includesFinancialData: false,
      includesCommercialData: false, includesActionData: false,
      generatedAt: "2026-09-30T00:00:00Z", expiresAt: "2026-10-01T00:00:00Z",
      isStale: false, reviewStatus: "NeedsReview", reviewedAt: null, reviewComment: null, revision: 1,
      output: { insightType: "EmergingRisk", statement: "هشدار مشورتی اختصاصی پروژه",
        evidenceReferences: ["project-state:70000000-0000-4000-8000-000000000093"],
        factsUsed: ["فقط شواهد مجاز"], assumptions: [], dataGaps: [], confidenceBand: "Low",
        potentialImpact: "نیازمند بررسی انسانی", suggestedActions: [], suggestedOwnerRole: "مدیر پروژه" } }] };
  let status = 200;
  let releaseFailure: () => void = () => {};
  const failureGate = new Promise<void>(resolve => { releaseFailure = resolve; });
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/insights`, async route => {
    if (status === 503) await failureGate;
    return route.fulfill(status === 200
      ? { status: 200, contentType: "application/json", body: JSON.stringify(result) }
      : { status, contentType: "application/problem+json", body: JSON.stringify({ status, title: "Sensitive advisory detail" }) });
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/projects/${projectId}`);
  const panel = page.getByTestId("advisory-insights");
  await expect(panel).toHaveAttribute("data-read-state", "current");
  await expect(panel).toContainText("هشدار مشورتی اختصاصی پروژه");
  await capture("advisory-390-current.png", 390, 844, "current");

  status = 503;
  await page.context().setOffline(true);
  await expect(panel).toHaveAttribute("data-read-state", "offline");
  await page.context().setOffline(false);
  await expect(panel).toHaveAttribute("data-read-state", "loading");
  await expect(panel).not.toContainText("هشدار مشورتی اختصاصی پروژه");
  await page.setViewportSize({ width: 320, height: 720 });
  await capture("advisory-320-refreshing.png", 320, 720, "loading");
  releaseFailure();
  await expect(panel).toHaveAttribute("data-read-state", "unavailable");
  await expect(panel.getByRole("alert")).not.toContainText("Sensitive advisory detail");
  await expect(panel).not.toContainText("هشدار مشورتی اختصاصی پروژه");
  await expect(panel.getByRole("button", { name: "پذیرش پس از بررسی" })).toHaveCount(0);
  await capture("advisory-320-read-error.png", 320, 720, "unavailable");

  status = 403;
  await panel.getByRole("button", { name: "تلاش دوباره برای دریافت تحلیل مشورتی" }).click();
  await expect(panel).toHaveAttribute("data-read-state", "forbidden");
  await expect(panel.getByRole("alert")).toContainText("دادهٔ قبلی نمایش داده نمی‌شود");
  await capture("advisory-320-access-revoked.png", 320, 720, "forbidden");

  status = 200;
  await panel.getByRole("button", { name: "تلاش دوباره برای دریافت تحلیل مشورتی" }).click();
  await expect(panel).toHaveAttribute("data-read-state", "current");
  await expect(panel).toContainText("هشدار مشورتی اختصاصی پروژه");
  await capture("advisory-320-restored.png", 320, 720, "current");

  writeFileSync(resolve(output, "index.json"), JSON.stringify({ contractVersion: 1,
    source: process.env.PMCS_SOURCE_HEAD_SHA ?? null, route: `/projects/${projectId}`,
    owner: "VX-G4 advisory read truth", files }, null, 2) + "\n");
});

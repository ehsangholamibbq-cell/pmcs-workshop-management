import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";
import { projectId, userId } from "./support";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const output = resolve(root, "src/web/artifacts/ms80-daily-history-truth");
const reportId = "70000000-0000-4000-8000-000000000080";

test("daily report history hides an old version on refresh, failure and access loss", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  async function capture(name: string, width: number, height: number,
    expectedState: "current" | "loading" | "unavailable" | "forbidden") {
    const history = page.getByTestId("daily-report-history");
    let bytes: Buffer | null = null;
    for (let attempt = 0; attempt < 8; attempt += 1) {
      await expect(history).toHaveAttribute("data-read-state", expectedState);
      await page.evaluate(() => document.fonts.ready);
      await history.evaluate((element) => window.scrollTo({
        top: Math.max(0, window.scrollY + element.getBoundingClientRect().top - 150),
        behavior: "instant",
      }));
      const headingTop = () => history.getByRole("heading", { name: "نسخه‌های گزارش روزانه" })
        .evaluate((element) => element.getBoundingClientRect().top);
      await page.waitForTimeout(400);
      const top = await headingTop();
      if (top < 120 || top >= 250) continue;
      if (await history.getAttribute("data-read-state") !== expectedState) continue;
      const candidate = await page.screenshot({ animations: "disabled", caret: "hide" });
      if (await history.getAttribute("data-read-state") !== expectedState) continue;
      // A settled DOM can precede the browser's painted frame after scrolling.
      if (candidate.length < 10_000) continue;
      bytes = candidate;
      break;
    }
    if (!bytes) throw new Error(`Report history did not remain ${expectedState} during ${name}`);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    expect(bytes.readUInt32BE(16)).toBe(width);
    expect(bytes.readUInt32BE(20)).toBe(height);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"),
      bytes: bytes.length, width, height });
  }

  const report = { id: reportId, projectId, reportDate: "2026-09-28", locationName: "کارگاه نمونه",
    narrative: "گزارش نمونه", status: "Approved", revision: 1, createdBy: userId, factCount: 2,
    reviewedBy: userId, reviewedAt: "2026-09-29T10:00:00Z", reviewComment: null,
    rootReportId: reportId, versionNumber: 1, supersedesReportId: null, supersededByReportId: null,
    supersededAt: null, correctionReason: null, correctionInitiatedBy: null };
  let responseStatus = 200;
  let releaseFailure: () => void = () => {};
  const failureGate = new Promise<void>(resolve => { releaseFailure = resolve; });
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/daily-reports`, async route => {
    if (responseStatus === 503) await failureGate;
    return route.fulfill(responseStatus === 200
      ? { status: 200, contentType: "application/json", body: JSON.stringify([report]) }
      : { status: responseStatus, contentType: "application/problem+json",
        body: JSON.stringify({ status: responseStatus, title: "Sensitive report detail" }) });
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/projects/${projectId}`);
  const history = page.getByTestId("daily-report-history");
  await expect(history).toHaveAttribute("data-read-state", "current");
  await expect(history.getByTestId("daily-report-version")).toHaveCount(1);
  await capture("history-390-current.png", 390, 844, "current");

  responseStatus = 503;
  await page.reload();
  await expect(history).toHaveAttribute("data-read-state", "loading");
  await expect(history.getByTestId("daily-report-version")).toHaveCount(0);
  await page.setViewportSize({ width: 320, height: 720 });
  await capture("history-320-refreshing.png", 320, 720, "loading");
  releaseFailure();
  await expect(history).toHaveAttribute("data-read-state", "unavailable");
  await expect(history.locator(".count-badge")).toHaveText("—");
  await expect(history.getByRole("alert")).not.toContainText("Sensitive report detail");
  await expect(history.getByRole("alert")).toHaveCSS("background-color", "rgb(255, 240, 237)");
  await capture("history-320-read-error.png", 320, 720, "unavailable");

  responseStatus = 403;
  await page.reload();
  await expect(history).toHaveAttribute("data-read-state", "forbidden");
  await expect(history.getByRole("alert")).toContainText("دادهٔ قبلی نمایش داده نمی‌شود");
  await expect(history.getByRole("alert")).toHaveCSS("background-color", "rgb(255, 240, 237)");
  await expect(history.getByTestId("daily-report-version")).toHaveCount(0);
  await capture("history-320-access-revoked.png", 320, 720, "forbidden");

  responseStatus = 200;
  await page.reload();
  await expect(history).toHaveAttribute("data-read-state", "current");
  await expect(history.getByTestId("daily-report-version")).toHaveCount(1);
  await capture("history-320-restored.png", 320, 720, "current");

  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    route: `/projects/${projectId}`, owner: "VX-G4 daily report history read truth", files,
  }, null, 2) + "\n");
});

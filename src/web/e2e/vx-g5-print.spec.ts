import { expect, test } from "@playwright/test";
import { execFileSync } from "node:child_process";
import { projectId, projectPath } from "./support";
import { evidence } from "./vx-g5-support";

test("every browser hides interactive workspace controls in print media", async ({ page }, testInfo) => {
  const report = evidence(testInfo, "print-media");
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(projectPath);
  await expect(page.locator(".project-print-shell")).toHaveAttribute("data-command-read-state", "current");
  await page.emulateMedia({ media: "print", reducedMotion: "reduce" });
  await expect(page.locator(".project-print-sheet")).toBeVisible();
  await expect(page.locator(".sidebar")).toBeHidden();
  await expect(page.locator(".workspace")).toBeHidden();
  expect(await page.locator("button,input,select,textarea,a").evaluateAll(elements => elements.filter(element => {
    const box = element.getBoundingClientRect(); return box.width > 0 && box.height > 0;
  }).length)).toBe(0);
  await report.capture(page, "print-media-1440.png");
});

test("independent no-snapshot, current, outdated, cached and denied A4/A3 print matrix", async ({ page, context }, testInfo) => {
  test.skip(testInfo.project.name !== "chromium", "Playwright PDF generation is supported by Chromium; print media is audited in all engines.");
  test.setTimeout(240_000);
  const report = evidence(testInfo, "print-matrix");
  await page.setViewportSize({ width: 1440, height: 900 });
  let state = "no-snapshot";
  const snapshotId = "a5000000-0000-4000-8000-000000000001";
  let projectName = "";
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/command-center`, async route => {
    if (state === "denied") return route.fulfill({ status: 403 });
    const response = await route.fetch();
    expect(response.ok()).toBe(true);
    const model = await response.json();
    projectName = model.projectName;
    const snapshot = state === "no-snapshot" ? null : {
      snapshotId, calculationVersion: "1.0.0", projectConfigurationRevision: 1,
      asOfDate: "2026-09-30", windowStart: "2026-09-24", windowEnd: "2026-09-30",
      calculatedAt: "2026-09-30T12:00:00Z", assessmentScope: "ApprovedDailyOperations",
      isPartial: true, operationalStatus: "Watch", coverageStatus: "Insufficient",
      freshnessStatus: "Current", confidenceStatus: "Low", coverageBasis: "SevenCalendarDays",
      coveragePercent: 50, expectedReportDays: 7, approvedReportDays: 1, lastApprovedReportDate: "2026-09-30",
      approvedFactCount: 0, progressFactCount: 0, laborFactCount: 0, equipmentFactCount: 0,
      materialFactCount: 0, issueCount: 0, stoppageCount: 0, highImpactCount: 0, criticalImpactCount: 0,
      oldestAttentionAgeDays: null, sourceMaxChangedAt: "2026-09-30T12:00:00Z", attentionItems: [],
    };
    return route.fulfill({ json: { ...model, hasSnapshot: Boolean(snapshot), snapshot,
      isOutdated: state === "outdated" || state === "cached", trend: [] } });
  });
  for (const nextState of ["no-snapshot", "current", "outdated", "cached", "denied"]) {
    state = nextState;
    await page.emulateMedia({ media: "screen", reducedMotion: "reduce" });
    if (state === "cached") {
      await context.setOffline(true);
      await expect(page.locator(".project-print-shell")).toHaveAttribute("data-command-read-state", "cached");
    } else {
      await context.setOffline(false);
      await page.goto(projectPath);
      await expect(page.locator(".project-print-shell")).toHaveAttribute("data-command-read-state", state === "denied" ? "forbidden" : "current");
    }
    await page.emulateMedia({ media: "print", reducedMotion: "reduce" });
    await page.evaluate(() => document.fonts.ready);
    const sheet = page.locator(".project-print-sheet");
    await expect(page.locator(".workspace")).toBeHidden();
    await expect(page.locator(".sidebar")).toBeHidden();
    if (state === "denied") {
      await expect(sheet).toHaveCount(0);
      await expect(page.getByText(projectName, { exact: true })).toHaveCount(0);
    } else {
      await expect(sheet).toBeVisible();
      await expect(sheet).toContainText("گزارش رسمی امضاشده یا خروجی مرکز گزارش‌ها نیست");
      if (state === "no-snapshot") {
        await expect(page.locator(".project-print-absence")).toBeVisible();
        await expect(page.locator(".project-print-facts")).toHaveCount(0);
      } else await expect(sheet).toContainText(snapshotId);
      if (state === "outdated" || state === "cached") await expect(page.locator(".project-print-warning")).toBeVisible();
      if (state === "cached") await expect(page.locator(".project-print-source")).toContainText("نسخه ذخیره‌شده روی دستگاه");
    }
    expect(await page.locator("button,input,select,textarea,a").evaluateAll(elements => elements.filter(element => {
      const box = element.getBoundingClientRect(); return box.width > 0 && box.height > 0;
    }).length)).toBe(0);
    await report.capture(page, `${state}-print-media-1440.png`);
    for (const paper of ["A4", "A3"] as const) for (const landscape of [false, true]) {
      let bytes: Buffer | null = null;
      let attempts = 0;
      for (; attempts < 3; attempts++) {
        if (state !== "denied") {
          await expect(page.locator(".project-print-shell")).toHaveAttribute("data-command-read-state",
            state === "cached" ? "cached" : "current");
          await expect(sheet).toContainText(projectName);
          if (state === "no-snapshot") await expect(page.locator(".project-print-absence")).toBeVisible();
          else await expect(sheet).toContainText(snapshotId);
        }
        const candidate = await page.pdf({ format: paper, landscape, printBackground: true,
          margin: { top: "12mm", right: "12mm", bottom: "12mm", left: "12mm" } });
        const extracted = execFileSync("pdftotext", ["-layout", "-", "-"],
          { input: candidate, encoding: "utf8" });
        const valid = state === "denied" ? extracted.trim() === "" :
          !extracted.includes("مشخصات پروژه هنوز دریافت نشده است") &&
          (state === "no-snapshot" ? !extracted.includes(snapshotId) : extracted.includes(snapshotId));
        if (valid) { bytes = candidate; break; }
      }
      expect(bytes, `print content for ${state}/${paper}/${landscape ? "landscape" : "portrait"}`).not.toBeNull();
      report.record(`${state}-${paper}-${landscape ? "landscape" : "portrait"}:attempts`, attempts + 1);
      if (!bytes) throw new Error("Print content did not match the selected state.");
      expect(bytes.subarray(0, 5).toString()).toBe("%PDF-");
      report.file(`${state}-${paper}-${landscape ? "landscape" : "portrait"}.pdf`, bytes,
        { state, paper, orientation: landscape ? "landscape" : "portrait",
          expectedProjectName: state === "denied" ? null : projectName });
    }
  }
  await context.setOffline(false);
});

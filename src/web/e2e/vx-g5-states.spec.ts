import { expect, test } from "@playwright/test";
import { projectId, projectPath, userId } from "./support";
import { auditSurface, evidence, viewports } from "./vx-g5-support";

test("enabled Chat retains six readable conversion forms and truthful loading, error, offline and denied states in every engine", async ({ page, context }, testInfo) => {
  test.setTimeout(600_000);
  await page.emulateMedia({ reducedMotion: "reduce" });
  const report = evidence(testInfo, "chat-states");
  const messageId = "a5000000-0000-4000-8000-000000000010";
  const documentId = "a5000000-0000-4000-8000-000000000011";
  const reportId = "a5000000-0000-4000-8000-000000000012";
  const locationId = "a5000000-0000-4000-8000-000000000013";
  const base = `/api/pmcs/api/v1/projects/${projectId}`;
  const message = `${base}/collaboration/messages/${messageId}`;
  let state = "current";
  let release: () => void = () => {};
  const gate = new Promise<void>(resolve => { release = resolve; });
  let commands = 0;
  await page.route(`**${base}/collaboration`, async route => {
    if (state === "loading") await gate;
    if (state === "error") return route.fulfill({ status: 503 });
    if (state === "denied") return route.fulfill({ status: 403 });
    return route.fulfill({ json: { projectId, lastSequence: 60, canUpload: true, canConvert: true,
      canConvertAction: true, canConvertIssue: true, canConvertRfi: true,
      canConvertDailyFact: true, canConvertEvidence: true, canConvertTechnicalDocument: true } });
  });
  await page.route(new RegExp(`${base}/collaboration/messages\\?after=0$`, "u"), route => route.fulfill({ json: {
    nextSequence: 60, messages: Array.from({ length: 60 }, (_, index) => ({
      id: index === 0 ? messageId : `a5000000-0000-4000-8000-${String(index + 100).padStart(12, "0")}`,
      projectId, sequence: index + 1, revision: 1, authorUserId: userId,
      body: `پیام کارگاه ${index + 1}؛ شرح بلند برای بررسی خوانایی و شکستن متن ${"project-reference-".repeat(8)}`,
      createdAt: "2026-09-30T12:00:00Z",
    })) } }));
  await page.route(new RegExp(`${base}/collaboration/events\\?`, "u"), route => route.fulfill({ status: 503 }));
  await page.route(`**${base}/collaboration/unread`, route => route.fulfill({ json: { lastReadSequence: 0, unreadCount: 60 } }));
  await page.route(`**${message}/reactions`, route => route.fulfill({ json: { messageId, canReact: true, reactions: [] } }));
  await page.route(`**${message}/attachments`, route => route.fulfill({ json: [{ messageId, documentId,
    originalFileName: "مدرک-کارگاه.pdf", contentType: "application/pdf", sizeBytes: 10,
    sha256: "a".repeat(64), versionNumber: 1, classification: "Internal", retentionPolicy: "Standard",
    legalHold: false, releasedAt: "2026-09-30T12:00:00Z",
    contentUrl: `/api/v1/projects/${projectId}/collaboration/messages/${messageId}/attachments/${documentId}/content` }] }));
  await page.route(`**${message}/conversions`, route => {
    if (route.request().method() === "GET") return route.fulfill({ json: [] });
    commands++; return route.fulfill({ status: 503 });
  });
  const dailyReport = { id: reportId, projectId, reportDate: "2099-01-03", locationName: "کارگاه", status: "Draft", revision: 1, facts: [] };
  await page.route(`**${base}/daily-reports`, route => route.fulfill({ json: [dailyReport] }));
  await page.route(`**${base}/daily-reports/${reportId}`, route => route.fulfill({ json: dailyReport }));
  await page.route(`**${base}/locations`, route => route.fulfill({ json: [{ id: locationId, projectId, code: "SITE", name: "کارگاه", status: "Active" }] }));
  await page.setViewportSize(viewports[0]);
  await page.goto(`${projectPath}/collaboration`);
  await expect(page.locator(".collaboration-message")).toHaveCount(60);
  const first = page.locator(".collaboration-message").first();
  await first.getByRole("button", { name: "واکنش‌ها", exact: true }).click();
  await first.getByRole("button", { name: "پیوست‌ها", exact: true }).click();
  await first.getByRole("button", { name: "تبدیل‌های رسمی پیام", exact: true }).click();
  const conversions = ["ساخت اقدام رسمی از پیام", "ساخت مسئلهٔ رسمی از پیام", "ساخت RFI رسمی از پیام",
    "ساخت واقعیت روزانه از پیام", "ساخت مدرک رسمی از فایل پیام", "ساخت سند فنی رسمی از فایل پیام"];
  for (const button of conversions) await first.getByRole("button", { name: button, exact: true }).click();
  await expect(first.locator(".collaboration-edit form")).toHaveCount(6);
  await first.getByLabel("افزودن فایل به پیام خود").setInputFiles({ name: "ردشده.exe", mimeType: "application/octet-stream", buffer: Buffer.from("invalid") });
  await first.getByRole("button", { name: "نگهداری فایل در صف دستگاه" }).click();
  await expect(first.getByText("ردشده.exe", { exact: true })).toBeVisible();
  const fileNameBox = await first.locator(".pmcs-file-input-name").boundingBox();
  expect(fileNameBox!.width).toBeGreaterThanOrEqual(144);
  expect(fileNameBox!.height).toBeLessThanOrEqual(80);
  report.record("mobileFileNameBox", fileNameBox);
  for (const viewport of viewports) {
    await page.setViewportSize(viewport);
    await auditSurface(page, report, `enabled-dense-${viewport.width}`);
    await page.evaluate(() => window.scrollTo({ top: 0, behavior: "instant" }));
    await report.capture(page, `enabled-dense-${viewport.width}.png`);
  }
  await page.setViewportSize(viewports[0]);
  for (let index = 0; index < 6; index++) {
    await first.locator(".collaboration-edit form").nth(index).evaluate(element => window.scrollTo({
      top: Math.max(0, window.scrollY + element.getBoundingClientRect().top - 120), behavior: "instant" }));
    await report.capture(page, `conversion-${index + 1}-320.png`);
  }
  await context.setOffline(true);
  await expect(page.getByText("اتصال قطع است", { exact: false })).toBeVisible();
  await auditSurface(page, report, "offline-320");
  await report.capture(page, "offline-320.png");
  state = "loading";
  await context.setOffline(false);
  await expect(page.getByText("در حال دریافت گفت‌وگوی پروژه…", { exact: true })).toBeVisible();
  await expect(first).toBeHidden();
  await auditSurface(page, report, "loading-320");
  await report.capture(page, "loading-320.png");
  state = "error"; release();
  await expect(page.getByRole("heading", { name: "دریافت گفت‌وگو کامل نشد", exact: true })).toBeVisible();
  await expect(first).toBeHidden();
  await auditSurface(page, report, "error-320");
  await report.capture(page, "error-320.png");
  state = "denied";
  await page.getByRole("button", { name: "تلاش دوباره", exact: true }).click();
  await expect(page.getByRole("heading", { name: "دسترسی به گفت‌وگو ندارید", exact: true })).toBeVisible();
  await expect(page.locator(".collaboration-message")).toHaveCount(0);
  await auditSurface(page, report, "denied-320");
  await report.capture(page, "denied-320.png");
  expect(commands).toBe(0);
});

test("Reporting cards remain readable under dense history, then loading, error and permission denial", async ({ page }, testInfo) => {
  test.setTimeout(360_000);
  await page.emulateMedia({ reducedMotion: "reduce" });
  const report = evidence(testInfo, "reporting-states");
  const base = `/api/pmcs/api/v1/projects/${projectId}/reports`;
  let state = "current";
  let release: () => void = () => {};
  const gate = new Promise<void>(resolve => { release = resolve; });
  await page.route(`**${base}/catalog`, async route => {
    if (state === "loading") await gate;
    if (state === "error") return route.fulfill({ status: 503 });
    if (state === "denied") return route.fulfill({ status: 403 });
    return route.fulfill({ json: [{ code: "project-progress-certified", title: "گزارش پیشرفت پروژه",
      description: "واقعیت‌های تأییدشده تا زمان قطع", templateVersion: "1.0.0", supportedFormats: ["Pdf", "Xlsx"], dataStatuses: ["Available", "InsufficientData"] }] });
  });
  await page.route(new RegExp(`${base}/runs\\?limit=50$`, "u"), route => route.fulfill({ json: Array.from({ length: 50 }, (_, index) => ({
    id: `a5000000-0000-4000-8000-${String(index + 200).padStart(12, "0")}`, projectId,
    definitionCode: "project-progress-certified", status: "Succeeded", pipelineStage: "Complete",
    dataStatus: "InsufficientData", createdAt: "2026-09-30T12:00:00Z", outputs: [],
  })) }));
  await page.goto(`${projectPath}/reports`);
  await expect(page.locator(".reporting-card")).toHaveCount(51);
  for (const viewport of viewports) {
    await page.setViewportSize(viewport);
    await auditSurface(page, report, `dense-${viewport.width}`);
    await page.evaluate(() => window.scrollTo({ top: 0, behavior: "instant" }));
    await report.capture(page, `dense-${viewport.width}.png`);
  }
  await page.setViewportSize(viewports[0]);
  state = "loading";
  await page.getByRole("button", { name: "تازه‌سازی", exact: true }).click();
  await expect(page.locator(".reporting-card")).toHaveCount(0);
  await auditSurface(page, report, "loading-320");
  await report.capture(page, "loading-320.png");
  state = "error"; release();
  await expect(page.getByRole("heading", { name: "دریافت گزارش‌ها کامل نشد", exact: true })).toBeVisible();
  await auditSurface(page, report, "error-320");
  await report.capture(page, "error-320.png");
  state = "denied";
  await page.getByRole("button", { name: "تلاش دوباره", exact: true }).click();
  await expect(page.getByRole("heading", { name: "دسترسی به گزارش‌ها ندارید", exact: true })).toBeVisible();
  await expect(page.locator(".reporting-card")).toHaveCount(0);
  await auditSurface(page, report, "denied-320");
  await report.capture(page, "denied-320.png");
});

test("the active operational trend communicates all six statuses and values independently of color", async ({ page }, testInfo) => {
  test.setTimeout(360_000);
  await page.emulateMedia({ reducedMotion: "reduce" });
  const report = evidence(testInfo, "trend");
  const statuses = ["NoData", "InsufficientData", "Stable", "Watch", "AtRisk", "Critical"];
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/command-center`, async route => {
    const response = await route.fetch(); expect(response.ok()).toBe(true);
    return route.fulfill({ json: { ...await response.json(), trend: statuses.map((operationalStatus, index) => ({
      snapshotId: `a5000000-0000-4000-8000-${String(index + 300).padStart(12, "0")}`,
      asOfDate: `2026-09-${String(24 + index).padStart(2, "0")}`, calculatedAt: "2026-09-30T12:00:00Z",
      operationalStatus, coveragePercent: index * 20, freshnessStatus: "Current", attentionCount: index,
    })) } });
  });
  await page.goto(projectPath);
  await expect(page.locator(".project-print-shell")).toHaveAttribute("data-command-read-state", "current");
  const trend = page.locator(".operational-trend");
  await expect(trend.getByRole("listitem")).toHaveCount(6);
  await expect(trend.locator("strong")).toHaveText(["وضعیت عملیاتی بحرانی است", "عملیات در معرض ریسک است",
    "عملیات نیازمند پایش است", "عملیات در محدوده پایدار است", "داده برای ارزیابی رسمی کافی نیست", "هنوز داده تأییدشده وجود ندارد"]);
  await expect(trend.locator("time")).toHaveCount(6);
  await expect(trend.locator('[aria-hidden="true"]')).toHaveCount(6);
  for (const viewport of viewports) {
    await page.setViewportSize(viewport);
    await auditSurface(page, report, `trend-${viewport.width}`);
    await trend.evaluate(element => window.scrollTo({ top: Math.max(0,
      window.scrollY + element.getBoundingClientRect().top - 100), behavior: "instant" }));
    await report.capture(page, `trend-${viewport.width}.png`);
  }
});

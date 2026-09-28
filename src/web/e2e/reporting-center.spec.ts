import { expect, test } from "@playwright/test";
import { projectId } from "./support";

const path = `/projects/${projectId}/reports`;
const catalog = `**/api/pmcs/api/v1/projects/${projectId}/reports/catalog`;
const runs = new RegExp(`/api/pmcs/api/v1/projects/${projectId}/reports/runs\\?limit=50$`, "u");

test("Reporting Center respects the default-off gate", async ({ page }) => {
  await page.goto(path);
  await expect(page.getByRole("heading", { name: "مرکز گزارش‌های پروژه" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "گزارش‌گیری در این پروژه در دسترس نیست" })).toBeVisible();
  await expect(page.locator(".reporting-card")).toHaveCount(0);
});

test("Reporting Center denies the catalog without reading runs", async ({ page }) => {
  let runReads = 0;
  await page.route(catalog, (route) => route.fulfill({ status: 403 }));
  await page.route(runs, (route) => { runReads += 1; return route.fulfill({ status: 200, body: "[]" }); });
  await page.goto(path);
  await expect(page.getByRole("heading", { name: "دسترسی به گزارش‌ها ندارید" })).toBeVisible();
  expect(runReads).toBe(0);
});

test("Reporting Center displays only catalog and runs authorized for the same project", async ({ page }) => {
  await page.route(catalog, (route) => route.fulfill({ status: 200, contentType: "application/json",
    body: JSON.stringify([{ code: "project-progress-certified", title: "گزارش پیشرفت پروژه",
      description: "واقعیت‌های تأییدشده تا زمان قطع", templateVersion: "1.0.0",
      supportedFormats: ["Pdf", "Xlsx"], dataStatuses: ["Available", "InsufficientData"] }]) }));
  await page.route(runs, (route) => route.fulfill({ status: 200, contentType: "application/json",
    body: JSON.stringify([{ id: "10000000-0000-4000-8000-000000000099", projectId,
      definitionCode: "project-progress-certified", status: "Succeeded", pipelineStage: "Complete",
      dataStatus: "InsufficientData", createdAt: "2026-09-28T00:00:00Z", outputs: [] }]) }));
  await page.goto(path);
  await expect(page.getByRole("heading", { name: "گزارش پیشرفت پروژه" })).toBeVisible();
  await expect(page.getByText("داده ناکافی")).toBeVisible();
  await expect(page.getByText("خروجی ثبت نشده است")).toBeVisible();
  await expect(page.locator(".reporting-card")).toHaveCount(2);
});

test("Reporting Center refuses a cross-project run even when catalog was authorized", async ({ page }) => {
  await page.route(catalog, (route) => route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  await page.route(runs, (route) => route.fulfill({ status: 200, contentType: "application/json",
    body: JSON.stringify([{ id: "10000000-0000-4000-8000-000000000099",
      projectId: "40000000-0000-4000-8000-000000000004", definitionCode: "other",
      status: "Succeeded", pipelineStage: "Complete", createdAt: "2026-09-28T00:00:00Z", outputs: [] }]) }));
  await page.goto(path);
  await expect(page.getByRole("heading", { name: "دریافت گزارش‌ها کامل نشد" })).toBeVisible();
  await expect(page.locator(".reporting-card")).toHaveCount(0);
});

test("a certified report request uses one client identity across a transport retry", async ({ page }) => {
  const attempts: Array<{ key: string | undefined; id: string; parameters: unknown }> = [];
  let created = false;
  await page.route(catalog, (route) => route.fulfill({ status: 200, contentType: "application/json",
    body: JSON.stringify([{ code: "project-progress-certified", title: "گزارش پیشرفت پروژه",
      description: "واقعیت تأییدشده", templateVersion: "1.0.0",
      supportedFormats: ["Pdf", "Xlsx"], dataStatuses: ["Available"] }]) }));
  await page.route(runs, (route) => route.fulfill({ status: 200, contentType: "application/json",
    body: JSON.stringify(created ? [{ id: attempts[0].id, projectId,
      definitionCode: "project-progress-certified", status: "Queued", pipelineStage: "Queued",
      dataStatus: null, createdAt: "2026-09-28T00:00:00Z", outputs: [] }] : []) }));
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/reports/runs`, (route) => {
    const payload = route.request().postDataJSON() as { clientGeneratedId: string; parameters: unknown };
    attempts.push({ key: route.request().headers()["idempotency-key"], id: payload.clientGeneratedId,
      parameters: payload.parameters });
    if (attempts.length === 1) return route.fulfill({ status: 503 });
    created = true;
    return route.fulfill({ status: 202, contentType: "application/json", body: JSON.stringify({
      id: payload.clientGeneratedId, projectId, definitionCode: "project-progress-certified", outputs: [],
    }) });
  });
  await page.goto(path);
  await page.getByRole("button", { name: "درخواست گزارش" }).click();
  await expect(page.getByRole("button", { name: "تلاش دوباره با همان درخواست" })).toBeVisible();
  await page.reload();
  await expect(page.getByRole("button", { name: "تلاش دوباره با همان درخواست" })).toBeVisible();
  await page.getByRole("button", { name: "تلاش دوباره با همان درخواست" }).click();
  await expect(page.getByText("درخواست گزارش پذیرفته شد؛ وضعیت آن در سابقه نمایش داده می‌شود.")).toBeVisible();
  await expect(page.getByText("در صف")).toBeVisible();
  expect(attempts).toHaveLength(2);
  expect(attempts[0]).toEqual(attempts[1]);
  expect(attempts[0].key).toBe(attempts[0].id);
  expect(attempts[0].parameters).toEqual({});
});

test("daily and periodic certified reports use scoped day and Persian period inputs", async ({ page }) => {
  const dailyReportId = "10000000-0000-4000-8000-000000000077";
  const requests: Array<{ code: string; parameters: unknown }> = [];
  await page.route(catalog, (route) => route.fulfill({ status: 200, contentType: "application/json",
    body: JSON.stringify([
      { code: "daily-report-certified", title: "گزارش روزانهٔ کارگاه", description: "روز تأییدشده",
        templateVersion: "1.0.0", supportedFormats: ["Pdf"], dataStatuses: ["Available"] },
      { code: "project-periodic-certified", title: "گزارش دوره‌ای پروژه", description: "هفته یا ماه",
        templateVersion: "1.0.0", supportedFormats: ["Xlsx"], dataStatuses: ["Available"] },
    ]) }));
  await page.route(runs, (route) => route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/daily-reports`, (route) => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify([
      { id: dailyReportId, projectId, reportDate: "2026-09-28", versionNumber: 1,
        status: "Approved", supersededByReportId: null },
      { id: "10000000-0000-4000-8000-000000000078", projectId, reportDate: "2026-09-27",
        versionNumber: 1, status: "Draft", supersededByReportId: null },
    ]) }));
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/reports/runs`, (route) => {
    const payload = route.request().postDataJSON() as {
      clientGeneratedId: string; definitionCode: string; parameters: unknown;
    };
    requests.push({ code: payload.definitionCode, parameters: payload.parameters });
    return route.fulfill({ status: 202, contentType: "application/json", body: JSON.stringify({
      id: payload.clientGeneratedId, projectId, definitionCode: payload.definitionCode, outputs: [],
    }) });
  });
  await page.goto(path);
  const dailyCard = page.locator(".reporting-card").filter({ has: page.getByRole("heading", { name: "گزارش روزانهٔ کارگاه" }) });
  await expect(dailyCard.getByLabel("گزارش روزانهٔ تأییدشده").locator("option")).toHaveCount(2);
  await dailyCard.getByLabel("گزارش روزانهٔ تأییدشده").selectOption(dailyReportId);
  await dailyCard.getByRole("button", { name: "درخواست گزارش" }).click();
  await expect(page.getByText("درخواست گزارش پذیرفته شد؛ وضعیت آن در سابقه نمایش داده می‌شود.")).toBeVisible();

  const periodCard = page.locator(".reporting-card").filter({ has: page.getByRole("heading", { name: "گزارش دوره‌ای پروژه" }) });
  await periodCard.getByLabel("دورهٔ گزارش").selectOption("Monthly");
  await periodCard.getByLabel("شروع دوره شمسی").fill("۱۴۰۵/۰۷/۰۱");
  await periodCard.getByLabel("شروع دوره شمسی").press("Tab");
  await periodCard.getByRole("button", { name: "درخواست گزارش" }).click();
  await expect.poll(() => requests.length).toBe(2);
  expect(requests).toEqual([
    { code: "daily-report-certified", parameters: { dailyReportId, includeRevisionChain: true } },
    { code: "project-periodic-certified", parameters: {
      periodKind: "Monthly", periodStartLocalDate: "2026-09-23",
    } },
  ]);
});

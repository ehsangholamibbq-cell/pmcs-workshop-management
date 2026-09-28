import { expect, test } from "@playwright/test";

const path = "/portfolio/reports";
const catalog = "**/api/pmcs/api/v1/portfolio/reports/catalog";
const runs = /\/api\/pmcs\/api\/v1\/portfolio\/reports\/runs\?limit=50$/u;
const definition = { code: "portfolio-summary-certified", scope: "Portfolio",
  title: "گزارش سبد پروژه‌ها", description: "وضعیت‌های رسمی پروژه‌ها",
  templateVersion: "1.0.0", supportedFormats: ["Pdf", "Xlsx"],
  dataStatuses: ["Available", "NoData"] };
const run = { id: "10000000-0000-4000-8000-000000000099",
  definitionCode: definition.code, status: "Succeeded", pipelineStage: "Complete",
  dataStatus: "NoData", createdAt: "2026-09-28T00:00:00Z", outputs: [] };

test("Portfolio Reporting Center respects the default-off gate", async ({ page }) => {
  await page.goto(path);
  await expect(page.getByRole("heading", { name: "مرکز گزارش‌های سبد پروژه‌ها" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "گزارش‌های سبد در دسترس نیستند" })).toBeVisible();
  await expect(page.locator(".reporting-card")).toHaveCount(0);
});

test("Portfolio Reporting Center stops before history on denied tenant catalog", async ({ page }) => {
  let historyReads = 0;
  await page.route(catalog, (route) => route.fulfill({ status: 403 }));
  await page.route(runs, (route) => {
    historyReads += 1;
    return route.fulfill({ status: 200, body: "[]" });
  });
  await page.goto(path);
  await expect(page.getByRole("heading", { name: "دسترسی به گزارش‌های سبد ندارید" })).toBeVisible();
  expect(historyReads).toBe(0);
});

test("Portfolio Reporting Center displays only the authorized tenant catalog and run", async ({ page }) => {
  await page.route(catalog, (route) => route.fulfill({ status: 200,
    contentType: "application/json", body: JSON.stringify([definition]) }));
  await page.route(runs, (route) => route.fulfill({ status: 200,
    contentType: "application/json", body: JSON.stringify([run]) }));
  await page.goto(path);
  await expect(page.getByRole("heading", { name: definition.title })).toBeVisible();
  await expect(page.getByText("بدون داده")).toBeVisible();
  await expect(page.getByText("خروجی ثبت نشده است")).toBeVisible();
  await expect(page.locator(".reporting-card")).toHaveCount(2);
});

test("Portfolio Reporting Center rejects a project-scoped run", async ({ page }) => {
  await page.route(catalog, (route) => route.fulfill({ status: 200,
    contentType: "application/json", body: JSON.stringify([definition]) }));
  await page.route(runs, (route) => route.fulfill({ status: 200,
    contentType: "application/json", body: JSON.stringify([
      { ...run, projectId: "20000000-0000-4000-8000-000000000002" },
    ]) }));
  await page.goto(path);
  await expect(page.getByRole("heading", { name: "دریافت گزارش‌ها کامل نشد" })).toBeVisible();
  await expect(page.locator(".reporting-card")).toHaveCount(0);
});

test("portfolio request preserves one identity and payload after retry and reload", async ({ page }) => {
  const attempts: Array<{ key: string | undefined; id: string; body: unknown }> = [];
  let created = false;
  await page.route(catalog, (route) => route.fulfill({ status: 200, contentType: "application/json",
    body: JSON.stringify([definition]) }));
  await page.route(runs, (route) => route.fulfill({ status: 200, contentType: "application/json",
    body: JSON.stringify(created ? [{ ...run, id: attempts[0].id, status: "Queued",
      pipelineStage: "Queued", dataStatus: null }] : []) }));
  await page.route("**/api/pmcs/api/v1/portfolio/reports/runs", (route) => {
    const body = route.request().postDataJSON() as { clientGeneratedId: string };
    attempts.push({ key: route.request().headers()["idempotency-key"], id: body.clientGeneratedId,
      body });
    if (attempts.length === 1) return route.fulfill({ status: 503 });
    created = true;
    return route.fulfill({ status: 202, contentType: "application/json",
      body: JSON.stringify({ id: body.clientGeneratedId, definitionCode: definition.code,
        status: "Queued", pipelineStage: "Queued", outputs: [] }) });
  });
  await page.goto(path);
  await page.getByRole("button", { name: "درخواست گزارش سبد" }).click();
  await expect(page.getByRole("button", { name: "تلاش دوباره با همان درخواست" })).toBeVisible();
  await page.reload();
  await expect(page.getByRole("button", { name: "تلاش دوباره با همان درخواست" })).toBeVisible();
  await page.getByRole("button", { name: "تلاش دوباره با همان درخواست" }).click();
  await expect(page.getByText("درخواست گزارش سبد پذیرفته شد؛ وضعیت آن در سابقه نمایش داده می‌شود.")).toBeVisible();
  await expect(page.getByText("در صف")).toBeVisible();
  expect(attempts).toHaveLength(2);
  expect(attempts[0]).toEqual(attempts[1]);
  expect(attempts[0].key).toBe(attempts[0].id);
  expect(attempts[0].body).toEqual({ clientGeneratedId: attempts[0].id,
    definitionCode: definition.code, templateVersion: definition.templateVersion,
    asOfUtc: null, formats: ["Pdf"], parameters: {} });
});

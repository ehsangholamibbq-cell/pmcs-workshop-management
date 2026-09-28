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

import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";
import { projectId } from "./support";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const output = resolve(root, "src/web/artifacts/ms73-reporting-feedback");
const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];

async function capture(page: import("@playwright/test").Page, name: string, width: number, height: number) {
  await page.evaluate(() => document.fonts.ready);
  const bytes = await page.screenshot({ animations: "disabled", caret: "hide" });
  expect(bytes.readUInt32BE(16)).toBe(width);
  expect(bytes.readUInt32BE(20)).toBe(height);
  writeFileSync(resolve(output, name), bytes);
  files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"),
    bytes: bytes.length, width, height });
}

test("project report keeps its retry identity and distinguishes a command error from a fresh history", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const path = `/projects/${projectId}/reports`;
  const base = `**/api/pmcs/api/v1/projects/${projectId}/reports`;
  let catalogReads = 0;
  let releaseRefresh: () => void = () => {};
  const refreshGate = new Promise<void>(resolve => { releaseRefresh = resolve; });
  await page.route(`${base}/catalog`, async route => {
    catalogReads += 1;
    if (catalogReads === 2) await refreshGate;
    return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([
      { code: "project-progress-certified", title: "گزارش پیشرفت پروژه", description: "واقعیت تأییدشده",
        templateVersion: "1.0.0", supportedFormats: ["Pdf"], dataStatuses: ["Available"] },
    ]) });
  });
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/reports/runs\\?limit=50$`, "u"),
    route => route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  const identities: string[] = [];
  await page.route(`${base}/runs`, route => {
    const request = route.request().postDataJSON() as { clientGeneratedId: string };
    identities.push(request.clientGeneratedId);
    if (identities.length === 1) return route.fulfill({ status: 503,
      contentType: "application/problem+json", body: JSON.stringify({ title: "Sensitive upstream error" }) });
    return route.fulfill({ status: 202, contentType: "application/json", body: JSON.stringify({
      id: request.clientGeneratedId, projectId, definitionCode: "project-progress-certified", outputs: [],
    }) });
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(path);
  await expect(page.getByRole("heading", { name: "گزارش پیشرفت پروژه" })).toBeVisible();
  await page.getByRole("button", { name: "درخواست گزارش" }).click();
  const error = page.locator(".reporting-notice.error");
  await expect(error).toHaveAttribute("role", "alert");
  await expect(error).not.toContainText("Sensitive upstream error");
  await expect(page.getByRole("button", { name: "تلاش دوباره با همان درخواست" })).toBeVisible();
  await capture(page, "project-390-request-error.png", 390, 844);

  await page.getByRole("button", { name: "تلاش دوباره با همان درخواست" }).click();
  await expect(page.getByText("در حال دریافت مرکز گزارش‌ها…")).toBeVisible();
  await expect(page.getByRole("heading", { name: "گزارش پیشرفت پروژه" })).toHaveCount(0);
  await expect(error).toHaveCount(0);
  await page.setViewportSize({ width: 320, height: 720 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await capture(page, "project-320-refreshing.png", 320, 720);
  releaseRefresh();
  await expect(page.getByRole("heading", { name: "گزارش پیشرفت پروژه" })).toBeVisible();
  await expect(page.locator(".reporting-notice.status")).toContainText("درخواست گزارش پذیرفته شد");
  expect(identities).toHaveLength(2);
  expect(identities[0]).toBe(identities[1]);
});

test("portfolio report retries without exposing stale cards or an upstream error", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const base = "**/api/pmcs/api/v1/portfolio/reports";
  let catalogReads = 0;
  let releaseRefresh: () => void = () => {};
  const refreshGate = new Promise<void>(resolve => { releaseRefresh = resolve; });
  await page.route(`${base}/catalog`, async route => {
    catalogReads += 1;
    if (catalogReads === 2) await refreshGate;
    return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([
      { code: "portfolio-summary-certified", scope: "Portfolio", title: "گزارش سبد پروژه‌ها",
        description: "وضعیت رسمی", templateVersion: "1.0.0", supportedFormats: ["Pdf"],
        dataStatuses: ["Available"] },
    ]) });
  });
  await page.route(/\/api\/pmcs\/api\/v1\/portfolio\/reports\/runs\?limit=50$/u,
    route => route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  const identities: string[] = [];
  await page.route(`${base}/runs`, route => {
    const request = route.request().postDataJSON() as { clientGeneratedId: string };
    identities.push(request.clientGeneratedId);
    if (identities.length === 1) return route.fulfill({ status: 503,
      contentType: "application/problem+json", body: JSON.stringify({ title: "Sensitive upstream error" }) });
    return route.fulfill({ status: 202, contentType: "application/json", body: JSON.stringify({
      id: request.clientGeneratedId, definitionCode: "portfolio-summary-certified", outputs: [],
    }) });
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/portfolio/reports");
  await expect(page.getByRole("heading", { name: "گزارش سبد پروژه‌ها", exact: true })).toBeVisible();
  await page.getByRole("button", { name: "درخواست گزارش سبد" }).click();
  const error = page.locator(".reporting-notice.error");
  await expect(error).toHaveAttribute("role", "alert");
  await expect(error).not.toContainText("Sensitive upstream error");
  await expect(page.getByRole("button", { name: "تلاش دوباره با همان درخواست" })).toBeVisible();
  await capture(page, "portfolio-390-request-error.png", 390, 844);

  await page.getByRole("button", { name: "تلاش دوباره با همان درخواست" }).click();
  await expect(page.getByText("در حال دریافت مرکز گزارش‌های سبد…")).toBeVisible();
  await expect(page.getByRole("heading", { name: "گزارش سبد پروژه‌ها", exact: true })).toHaveCount(0);
  await page.setViewportSize({ width: 320, height: 720 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await capture(page, "portfolio-320-refreshing.png", 320, 720);
  releaseRefresh();
  await expect(page.getByRole("heading", { name: "گزارش سبد پروژه‌ها", exact: true })).toBeVisible();
  await expect(page.locator(".reporting-notice.status")).toContainText("درخواست گزارش سبد پذیرفته شد");
  expect(identities).toHaveLength(2);
  expect(identities[0]).toBe(identities[1]);
  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    routes: [`/projects/${projectId}/reports`, "/portfolio/reports"],
    owner: "VX-G4 reporting feedback", files,
  }, null, 2) + "\n");
});

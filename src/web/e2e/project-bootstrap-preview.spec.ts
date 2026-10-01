import { expect, test } from "@playwright/test";
import { createHash } from "node:crypto";
import { projectId } from "./support";
import { captureVisualBaseline } from "./visual-baseline";

test("project bootstrap preview shows conflicts and blocks execution before confirmation", async ({ page }) => {
  const previewDigest = createHash("sha256").update("pmcs-ms36-preview-fixture").digest("hex");
  let createRequests = 0;
  let executeRequests = 0;
  await page.route("**/api/pmcs/api/v1/project-bootstraps", async (route) => {
    expect(route.request().method()).toBe("POST");
    const body = route.request().postDataJSON() as {
      sourceProjectId: string;
      target: { code: string; name: string; offlinePolicyAccepted: boolean };
      conflictPolicy: string;
    };
    expect(body.sourceProjectId).toBe(projectId);
    expect(body.target).toMatchObject({ code: "PREVIEW-01", name: "پروژه مقصد آزمایشی", offlinePolicyAccepted: true });
    expect(body.conflictPolicy).toBe("FailOnConflict");
    expect(route.request().headers()["idempotency-key"]).toBeTruthy();
    createRequests += 1;
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
      planId: "44444444-4444-4444-8444-444444444444",
      planRevision: 1,
      sourceProjectId: projectId,
      sourceProjectCode: "DEMO-01",
      targetProjectId: "55555555-5555-4555-8555-555555555555",
      targetProject: { id: "55555555-5555-4555-8555-555555555555", code: "PREVIEW-01",
        name: "پروژه مقصد آزمایشی", status: "Draft", revision: 1 },
      status: "PreviewReady",
      conflictPolicy: "FailOnConflict",
      selectedCategories: ["BaseSettings", "Locations"],
      contributorCatalogVersion: "qa-preview-v1",
      previewDigest,
      previewedAt: "2026-09-28T00:00:00Z",
      previewExpiresAt: "2026-09-29T00:00:00Z",
      contributors: [],
      items: [
        { contributorId: "settings", category: "BaseSettings", disposition: "Added", code: "settings",
          title: "تنظیمات پایه", detail: "مقدار مستقل مقصد", sourceReference: null, targetReference: null },
        { contributorId: "locations", category: "Locations", disposition: "Conflict", code: "location-conflict",
          title: "تعارض مکان", detail: "نیازمند برنامهٔ تازه", sourceReference: null, targetReference: null },
        { contributorId: "locations", category: "Locations", disposition: "Blocked", code: "location-blocked",
          title: "انتقال مسدود", detail: "بدون اجرای حدسی", sourceReference: null, targetReference: null },
      ],
      summary: { added: 1, skipped: 0, conflicts: 1, blocked: 1 },
      alwaysExcluded: ["دادهٔ عملیاتی", "تاریخچهٔ پیام‌ها"],
    }) });
  });
  await page.route("**/api/pmcs/api/v1/project-bootstraps/*/execute", async (route) => {
    executeRequests += 1;
    await route.fulfill({ status: 500 });
  });

  await page.goto("/project-bootstraps");
  await expect(page.getByRole("option", { name: /پروژه نمونه ساختمان اداری–تجاری/u })).toBeAttached();
  await page.getByLabel("نام پروژه مقصد").fill("پروژه مقصد آزمایشی");
  await page.getByLabel("کد مستقل مقصد").fill("PREVIEW-01");
  await page.getByLabel("استان یا منطقه").fill("قزوین");
  const dates = page.locator(".bootstrap-form-grid .persian-date-input input");
  await dates.nth(0).fill("۱۴۰۵/۰۷/۰۶");
  await dates.nth(1).fill("۱۴۰۶/۰۷/۰۶");
  await page.getByLabel("شرح کوتاه").fill("مقصد مستقل آزمایشی برای سنجش پیش‌نمایش");
  await page.locator(".bootstrap-form-grid .checkbox-row input").check();
  await page.getByRole("button", { name: "مرحله بعد" }).click();
  await expect(page.getByRole("heading", { name: "چه چیزهایی بررسی و منتقل شوند؟" })).toBeVisible();
  await page.getByRole("navigation", { name: "مراحل ساخت پروژه" }).getByRole("button", { name: /اعضا/u }).click();
  await expect(page.getByRole("heading", { name: "اعضای انتخاب‌شده و نقش مقصد" })).toBeVisible();
  expect(createRequests).toBe(0);
  await page.getByRole("button", { name: "ایجاد مقصد پیش‌نویس و نمایش پیش‌نمایش" }).click();

  await expect(page.getByRole("heading", { name: "پیش‌نمایش انتقال" })).toBeVisible();
  await expect(page.getByText("تعارض مکان")).toBeVisible();
  await expect(page.getByText("انتقال مسدود")).toBeVisible();
  const blocked = page.getByText("پیش‌نمایش دارای مانع اجرایی است", { exact: false });
  await expect(blocked).toBeVisible();
  await page.locator(".bootstrap-confirm input").check();
  const executeButton = page.getByRole("button", { name: "تأیید و اجرای کنترل‌شده" });
  await expect(executeButton).toBeDisabled();
  await blocked.scrollIntoViewIfNeeded();
  await expect(blocked).toBeInViewport();
  await captureVisualBaseline(page, "41-bootstrap-preview-blocked");
  const backgroundBeforeHover = await executeButton.evaluate((button) => getComputedStyle(button).backgroundColor);
  await executeButton.hover({ force: true });
  await expect(executeButton).toBeDisabled();
  expect(await executeButton.evaluate((button) => getComputedStyle(button).backgroundColor)).toBe(backgroundBeforeHover);
  await captureVisualBaseline(page, "45-bootstrap-disabled-hover");
  expect(createRequests).toBe(1);
  expect(executeRequests).toBe(0);
});

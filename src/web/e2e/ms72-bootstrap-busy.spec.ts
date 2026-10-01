import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const output = resolve(root, "src/web/artifacts/ms72-bootstrap-busy");

test("bootstrap form and step controls stay locked during a pending preview command", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  async function capture(name: string, width: number, height: number, selector: string) {
    await page.locator(selector).scrollIntoViewIfNeeded();
    await page.evaluate(() => document.fonts.ready);
    const bytes = await page.screenshot({ animations: "disabled", caret: "hide" });
    expect(bytes.readUInt32BE(16)).toBe(width);
    expect(bytes.readUInt32BE(20)).toBe(height);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"),
      bytes: bytes.length, width, height });
  }

  let releasePreview: () => void = () => {};
  const previewGate = new Promise<void>(resolve => { releasePreview = resolve; });
  let createRequests = 0;
  await page.route("**/api/pmcs/api/v1/project-bootstraps", async route => {
    expect(route.request().method()).toBe("POST");
    createRequests += 1;
    await previewGate;
    await route.fulfill({ status: 503, contentType: "application/problem+json",
      body: JSON.stringify({ status: 503, title: "Sensitive upstream error" }) });
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/project-bootstraps");
  await expect(page.getByRole("option", { name: /پروژه نمونه ساختمان اداری–تجاری/u })).toBeAttached();
  const form = page.locator(".bootstrap-workspace");
  await page.getByLabel("نام پروژه مقصد").fill("پروژه مقصد آزمون وضعیت");
  await page.getByLabel("کد مستقل مقصد").fill("BUSY-01");
  await page.getByLabel("استان یا منطقه").fill("قزوین");
  const dates = page.locator(".bootstrap-form-grid .persian-date-input input");
  await dates.nth(0).fill("۱۴۰۵/۰۷/۰۶");
  await dates.nth(1).fill("۱۴۰۶/۰۷/۰۶");
  await page.getByLabel("شرح کوتاه").fill("مقصد مستقل آزمایشی برای وضعیت فرم");
  await page.locator(".bootstrap-form-grid .checkbox-row input").check();
  await page.getByRole("navigation", { name: "مراحل ساخت پروژه" }).getByRole("button", { name: /اعضا/u }).click();
  await expect(page.getByRole("option", { name: "سرپرست کارگاه" }).first()).toBeAttached();
  await expect(page.locator(".bootstrap-member-list")).not.toContainText("Active");
  const submit = form.getByRole("button", { name: "ایجاد مقصد پیش‌نویس و نمایش پیش‌نمایش" });
  await expect(submit).toBeEnabled();
  await submit.click();
  await expect(form).toHaveAttribute("aria-busy", "true");
  await expect(form.locator("fieldset")).toHaveAttribute("disabled", "");
  await expect(form.locator(".bootstrap-member-list input[type=checkbox]").first()).toBeDisabled();
  await expect(page.getByRole("navigation", { name: "مراحل ساخت پروژه" }).getByRole("button", { name: /مبدأ و مقصد/u })).toBeDisabled();
  await expect(page.locator(".bootstrap-message")).toHaveAttribute("role", "status");
  await capture("bootstrap-390-busy.png", 390, 844, ".bootstrap-actions");

  await page.setViewportSize({ width: 320, height: 720 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await capture("bootstrap-320-busy.png", 320, 720, ".bootstrap-actions");
  releasePreview();
  await expect(form).toHaveAttribute("aria-busy", "false");
  await expect(form.locator("fieldset")).not.toHaveAttribute("disabled");
  await expect(page.locator(".bootstrap-message")).toHaveAttribute("role", "alert");
  await expect(page.locator(".bootstrap-message")).not.toContainText("Sensitive upstream error");
  await page.getByRole("navigation", { name: "مراحل ساخت پروژه" }).getByRole("button", { name: /مبدأ و مقصد/u }).click();
  await expect(page.getByLabel("نام پروژه مقصد")).toHaveValue("پروژه مقصد آزمون وضعیت");
  await capture("bootstrap-320-error-preserved.png", 320, 720, ".bootstrap-message");
  expect(createRequests).toBe(1);
  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    route: "/project-bootstraps", owner: "VX-G4 bootstrap busy command", files,
  }, null, 2) + "\n");
});

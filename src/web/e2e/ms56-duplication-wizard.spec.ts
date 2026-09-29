import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { expect, test } from "@playwright/test";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const prototype = resolve(root, "docs/ux/prototypes/ms56/index.html");
const output = resolve(root, "src/web/artifacts/ms56-duplication-wizard");
const digest = (bytes: Buffer) => createHash("sha256").update(bytes).digest("hex");

test("MS56 selection, conflict and review never imply an operational transfer", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const requests: string[] = [];
  page.on("request", request => { if (/^https?:/u.test(request.url())) requests.push(request.url()); });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  async function capture(name: string, width: number, height: number) {
    const bytes = await page.screenshot({ fullPage: true, animations: "disabled", caret: "hide" });
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: digest(bytes), bytes: bytes.length, width, height });
  }

  for (const [width, height] of [[1280, 800], [390, 844], [320, 720]] as const) {
    await page.setViewportSize({ width, height });
    await page.goto(pathToFileURL(prototype).href);
    await page.evaluate(() => document.fonts.ready);
    expect(await page.evaluate(() => document.fonts.check("16px Vazirmatn"))).toBe(true);
    expect(await page.locator(".brand").evaluate((image: HTMLImageElement) => image.naturalWidth)).toBeGreaterThan(0);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    const prepare = page.getByRole("button", { name: "محاسبهٔ نمایشی پیش‌نمایش" });
    const execute = page.locator("#execute"), reviewed = page.locator("#reviewed"), reviewButton = page.locator("#review-button");
    const policy = page.locator("#policy"), scenario = page.locator("#scenario"), rows = page.locator("#rows .row");
    await expect(execute).toBeDisabled();
    await expect(page.locator("#result")).toBeHidden();
    await prepare.click();
    await expect(page.locator("body")).toHaveAttribute("data-state", "ready");
    await expect(rows).toHaveCount(2);
    await expect(page.locator("#summary")).toContainText("افزودنی: 2");
    await expect(reviewed).toBeEnabled();
    await expect(reviewButton).toBeDisabled();
    if (width === 1280 || width === 390) await capture(`wizard-${width}-ready.png`, width, height);
    await reviewed.check();
    await reviewButton.click();
    await expect(page.locator("#review-dialog")).toBeVisible();
    await expect(page.locator("#acknowledge")).toBeFocused();
    await page.keyboard.press("Escape");
    await expect(page.locator("#review-dialog")).toBeHidden();
    await expect(reviewButton).toBeFocused();
    await reviewButton.click();
    await page.getByRole("button", { name: "ثبت مرور محلی" }).click();
    await expect(page.locator("#step-confirm")).toHaveAttribute("data-active", "true");
    await expect(page.locator("#confirmation-reason")).toContainText("هیچ انتقالی انجام نشده است");
    await expect(execute).toBeDisabled();

    await scenario.selectOption("conflict");
    await expect(page.locator("body")).toHaveAttribute("data-state", "unprepared");
    await expect(reviewed).toBeDisabled();
    await expect(page.locator("#result")).toBeHidden();
    await prepare.click();
    await expect(page.locator('#rows .row[data-kind="conflict"]')).toHaveCount(1);
    await expect(reviewed).toBeDisabled();
    await expect(execute).toBeDisabled();
    if (width === 1280 || width === 320) await capture(`wizard-${width}-conflict.png`, width, height);

    await policy.selectOption("skip");
    await expect(page.locator("body")).toHaveAttribute("data-state", "unprepared");
    await prepare.click();
    await expect(page.locator('#rows .row[data-kind="skipped"]')).toHaveCount(1);
    await expect(page.locator("#summary")).toContainText("ردشده: 1");
    await expect(reviewed).toBeEnabled();
    await expect(execute).toBeDisabled();
    if (width === 1280) await capture(`wizard-${width}-skipped.png`, width, height);

    await scenario.selectOption("blocked");
    await prepare.click();
    await expect(page.locator('#rows .row[data-kind="blocked"]')).toHaveCount(1);
    await expect(reviewed).toBeDisabled();
    await expect(execute).toBeDisabled();
    if (width === 1280 || width === 320) await capture(`wizard-${width}-blocked.png`, width, height);

    for (const [key, title] of [
      ["loading", "در حال محاسبه"], ["empty", "قلمی برای انتقال نیست"],
      ["error", "محاسبهٔ پیش‌نمایش ناموفق بود"], ["offline", "اتصال برقرار نیست"],
      ["permission", "مجوز کافی نیست"], ["expired", "نسخهٔ پیش‌نمایش منقضی شد"],
    ] as const) {
      await scenario.selectOption(key);
      await prepare.click();
      await expect(page.locator("body")).toHaveAttribute("data-state", key);
      await expect(page.locator("#status-title")).toHaveText(title);
      await expect(page.locator("#result")).toBeHidden();
      await expect(reviewed).toBeDisabled();
      await expect(execute).toBeDisabled();
      if (key === "loading") await expect(page.locator("#skeleton")).toBeVisible();
      else await expect(page.locator("#skeleton")).toBeHidden();
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `${width}px ${key}`).toBe(true);
      if (width === 1280) await capture(`wizard-${width}-${key}.png`, width, height);
    }
    await scenario.selectOption("ready");
    for (const box of await page.locator("#categories input").all()) await box.uncheck();
    await prepare.click();
    await expect(page.locator("body")).toHaveAttribute("data-state", "empty");
    await expect(execute).toBeDisabled();
    if (width === 320) await capture(`wizard-${width}-empty.png`, width, height);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  }
  expect(requests).toEqual([]);
  expect(new Set(files.map(file => file.name)).size).toBe(files.length);
  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    prototype: "UX2-MS56", prototypeSha256: digest(readFileSync(prototype)), files,
  }, null, 2) + "\n");
});

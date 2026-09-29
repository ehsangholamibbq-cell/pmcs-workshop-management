import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { expect, test } from "@playwright/test";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const prototype = resolve(root, "docs/ux/prototypes/ms55/index.html");
const output = resolve(root, "src/web/artifacts/ms55-profile-privacy");
const digest = (bytes: Buffer) => createHash("sha256").update(bytes).digest("hex");

test("MS55 profile image states preserve privacy and crop preview semantics", async ({ page }) => {
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
    const state = page.locator("#state"), photo = page.locator("#photo"), initials = page.locator("#initials");
    const crop = page.getByRole("button", { name: "بازبینی قاب تصویر نمونه" });
    const remove = page.getByRole("button", { name: "نمایش حالت بدون تصویر" });
    for (const [key, image, editable, message] of [
      ["fallback", false, false, "بدون تصویر"],
      ["default", true, true, "تصویر نمونه"],
      ["loading", false, false, "در حال بررسی تصویر"],
      ["error", false, false, "تصویر پذیرفته نشد"],
      ["offline", true, false, "اتصال برقرار نیست"],
      ["permission", false, false, "اجازهٔ ویرایش تصویر ندارید"],
      ["conflict", true, false, "نسخهٔ نمایه تغییر کرده است"],
      ["success", true, true, "پیش‌نمایش محلی تأیید شد"],
    ] as const) {
      await state.selectOption(key);
      await expect(page.locator("body")).toHaveAttribute("data-state", key);
      await expect(page.locator("#status-title")).toHaveText(message);
      if (image) { await expect(photo).toBeVisible(); await expect(initials).toBeHidden(); }
      else { await expect(photo).toBeHidden(); await expect(initials).toBeVisible(); }
      if (editable) { await expect(crop).toBeEnabled(); await expect(remove).toBeEnabled(); }
      else { await expect(crop).toBeDisabled(); await expect(remove).toBeDisabled(); }
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `${width}px ${key}`).toBe(true);
      if (width === 1280 || (width === 320 && ["fallback", "permission"].includes(key))) {
        await capture(`profile-${width}-${key}.png`, width, height);
      }
    }
    await state.selectOption("loading");
    await expect(page.locator("#skeleton")).toBeVisible();
    await state.selectOption("error");
    await expect(page.locator("#status")).toHaveAttribute("role", "alert");
    await state.selectOption("permission");
    await expect(page.locator("#privacy")).toHaveText("نمایش تصویر محدود شده");
    await state.selectOption("default");
    await crop.click();
    await expect(page.locator("#crop-dialog")).toBeVisible();
    await expect(page.locator("#crop-x")).toBeFocused();
    await page.keyboard.press("Escape");
    await expect(page.locator("#crop-dialog")).toBeHidden();
    await expect(crop).toBeFocused();
    await crop.click();
    await page.locator("#crop-x").evaluate((input: HTMLInputElement) => { input.value = "20"; input.dispatchEvent(new Event("input", { bubbles: true })); });
    await page.locator("#crop-y").evaluate((input: HTMLInputElement) => { input.value = "15"; input.dispatchEvent(new Event("input", { bubbles: true })); });
    await page.getByRole("button", { name: "تأیید پیش‌نمایش محلی" }).click();
    await expect(page.locator("body")).toHaveAttribute("data-state", "success");
    await expect(photo).toHaveCSS("--crop-x", "10px");
    await expect(crop).toBeFocused();
    await capture(`profile-${width}-success-crop.png`, width, height);
    await remove.click();
    await expect(photo).toBeHidden();
  }
  expect(requests).toEqual([]);
  expect(new Set(files.map(file => file.name)).size).toBe(files.length);
  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    prototype: "UX2-MS55", prototypeSha256: digest(readFileSync(prototype)), files,
  }, null, 2) + "\n");
});

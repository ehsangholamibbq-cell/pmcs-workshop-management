import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { expect, test } from "@playwright/test";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const prototype = resolve(root, "docs/ux/prototypes/ms57/index.html");
const output = resolve(root, "src/web/artifacts/ms57-attachment-evidence");
const digest = (bytes: Buffer) => createHash("sha256").update(bytes).digest("hex");

test("MS57 attachment and Evidence sample never promotes an unverified file", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const requests: string[] = [];
  page.on("request", request => { if (/^https?:/u.test(request.url())) requests.push(request.url()); });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  async function capture(name: string, width: number) {
    const bytes = await page.screenshot({ fullPage: true, animations: "disabled", caret: "hide" });
    const imageWidth = bytes.readUInt32BE(16);
    const imageHeight = bytes.readUInt32BE(20);
    expect(imageWidth).toBe(width);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: digest(bytes), bytes: bytes.length, width: imageWidth, height: imageHeight });
  }

  for (const [width, height] of [[1280, 800], [390, 844], [320, 720]] as const) {
    await page.setViewportSize({ width, height });
    await page.goto(pathToFileURL(prototype).href);
    await page.evaluate(() => document.fonts.ready);
    expect(await page.evaluate(() => document.fonts.check("16px Vazirmatn"))).toBe(true);
    expect(await page.locator(".brand").evaluate((image: HTMLImageElement) => image.naturalWidth)).toBeGreaterThan(0);
    const state = page.locator("#state"), file = page.locator("#file"), review = page.locator("#review");
    const attach = page.locator("#attach"), download = page.locator("#download");
    for (const [key, title, visible, reviewable] of [
      ["queued", "در صف امن دستگاه", true, false],
      ["pending", "در حال ارسال و بررسی", true, false],
      ["quarantined", "در قرنطینه", true, false],
      ["released", "آزادشده، هنوز متصل نشده", true, true],
      ["rejected", "ردشده در کنترل امنیتی", true, false],
      ["offline", "اتصال برقرار نیست", true, false],
      ["permission", "دسترسی به پیوست مجاز نیست", false, false],
      ["error", "دریافت وضعیت کامل نشد", false, false],
      ["conflict", "نسخهٔ پیام تغییر کرده است", true, false],
      ["empty", "پیوستی ثبت نشده است", false, false],
    ] as const) {
      await state.selectOption(key);
      await expect(page.locator("body")).toHaveAttribute("data-state", key);
      await expect(page.locator("#status-title")).toHaveText(title);
      if (visible) await expect(file).toBeVisible(); else await expect(file).toBeHidden();
      if (reviewable) await expect(review).toBeEnabled(); else await expect(review).toBeDisabled();
      await expect(attach).toBeDisabled(); await expect(download).toBeDisabled();
      if (key === "permission") await expect(page.getByText("برگه-نمونه.pdf")).toBeHidden();
      if (key === "pending") await expect(page.locator("#skeleton")).toBeVisible();
      else await expect(page.locator("#skeleton")).toBeHidden();
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `${width}px ${key}`).toBe(true);
      if (width === 1280 || (width === 390 && ["released", "quarantined"].includes(key)) ||
          (width === 320 && ["permission", "rejected", "queued"].includes(key))) {
        await capture(`attachment-${width}-${key}.png`, width);
      }
    }
    await state.selectOption("released");
    await review.click();
    await expect(page.locator("#review-dialog")).toBeVisible();
    await expect(page.locator("#acknowledge")).toBeFocused();
    await page.keyboard.press("Escape");
    await expect(page.locator("#review-dialog")).toBeHidden();
    await expect(review).toBeFocused();
    await review.click();
    await page.getByRole("button", { name: "ثبت مرور محلی" }).click();
    await expect(page.locator("#review-result")).toContainText("هیچ اتصال یا مدرک رسمی ساخته نشده است");
    await expect(attach).toBeDisabled(); await expect(download).toBeDisabled();
    await state.selectOption("conflict");
    await expect(page.locator("#review-result")).not.toContainText("مرور محلی ثبت شد");
  }
  expect(requests).toEqual([]);
  expect(new Set(files.map(file => file.name)).size).toBe(files.length);
  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    prototype: "UX2-MS57", prototypeSha256: digest(readFileSync(prototype)), files,
  }, null, 2) + "\n");
});

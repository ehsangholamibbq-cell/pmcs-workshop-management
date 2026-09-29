import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { expect, test } from "@playwright/test";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const review = resolve(root, "docs/ux/review/ms61/index.html");
const output = resolve(root, "src/web/artifacts/ms61-owner-review");
const digest = (bytes: Buffer) => createHash("sha256").update(bytes).digest("hex");

test("MS61 owner decision pack has four criteria and linked evidence", async ({ page }) => {
  mkdirSync(output, { recursive: true });
  const requests: string[] = [];
  page.on("request", request => { if (/^https?:/u.test(request.url())) requests.push(request.url()); });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  for (const [width, height] of [[1280, 800], [390, 844], [320, 720]] as const) {
    await page.setViewportSize({ width, height });
    await page.goto(pathToFileURL(review).href);
    await page.evaluate(() => document.fonts.ready);
    expect(await page.evaluate(() => document.fonts.check("16px Vazirmatn"))).toBe(true);
    expect(await page.locator(".brand").evaluate((image: HTMLImageElement) => image.naturalWidth)).toBeGreaterThan(0);
    await expect(page.locator(".criterion")).toHaveCount(4);
    await expect(page.locator("#identity")).toContainText("وزیرمتن ۲٫۰٫۰");
    await expect(page.locator("#truth")).toContainText("پیشنهاد آزمایشی");
    await expect(page.locator("#interaction")).toContainText("صفحه‌کلید");
    await expect(page.locator("#print")).toContainText("A4/A3");
    await expect(page.locator(".foot")).toContainText("VX-G3 باز");
    const broken = await page.locator("a[href]").evaluateAll(anchors => anchors.filter(anchor => {
      const url = new URL((anchor as HTMLAnchorElement).href);
      return url.protocol !== "file:" || !url.pathname || !anchor.textContent?.trim();
    }).length);
    expect(broken).toBe(0);
    for (const link of await page.locator("a[href]").all()) {
      const href = await link.getAttribute("href");
      if (href?.startsWith("#")) continue;
      const target = new URL(href!, pathToFileURL(review).href);
      expect(readFileSync(fileURLToPath(target)).length).toBeGreaterThan(0);
    }
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    const name = `review-${width}.png`;
    const bytes = await page.screenshot({ fullPage: true, animations: "disabled", caret: "hide" });
    const imageWidth = bytes.readUInt32BE(16), imageHeight = bytes.readUInt32BE(20);
    expect(imageWidth).toBe(width);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: digest(bytes), bytes: bytes.length, width: imageWidth, height: imageHeight });
  }
  expect(requests).toEqual([]);
  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    prototype: "UX2-MS61-owner-review", prototypeSha256: digest(readFileSync(review)), files,
  }, null, 2) + "\n");
});

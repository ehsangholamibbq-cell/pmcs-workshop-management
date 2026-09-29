import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { expect, test } from "@playwright/test";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const prototype = resolve(root, "docs/ux/prototypes/ms62/index.html");
const fixture = resolve(root, "docs/ux/prototypes/ms62/fixture.js");
const output = resolve(root, "src/web/artifacts/ms62-dense-print");
const digest = (bytes: Buffer) => createHash("sha256").update(bytes).digest("hex");

test("MS62 keeps all dense rows and the same total through mobile pagination and four print sizes", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const requests: string[] = [];
  page.on("request", request => { if (/^https?:/u.test(request.url())) requests.push(request.url()); });
  const files: Array<{ name: string; sha256: string; bytes: number; width?: number; height?: number }> = [];
  async function capture(name: string, width: number) {
    await page.evaluate(() => window.scrollTo(0, 0));
    const bytes = await page.screenshot({ fullPage: true, animations: "disabled", caret: "hide" });
    const imageWidth = bytes.readUInt32BE(16), imageHeight = bytes.readUInt32BE(20);
    expect(imageWidth).toBe(width);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: digest(bytes), bytes: bytes.length, width: imageWidth, height: imageHeight });
  }
  const fa = (value: number) => new Intl.NumberFormat("fa-IR", { useGrouping: false }).format(value);
  const money = (value: number) => new Intl.NumberFormat("fa-IR").format(value);
  for (const [width, height] of [[1280, 800], [768, 1024], [390, 844], [320, 720]] as const) {
    await page.setViewportSize({ width, height });
    await page.goto(pathToFileURL(prototype).href);
    await page.evaluate(() => document.fonts.ready);
    expect(await page.evaluate(() => document.fonts.check("16px Vazirmatn"))).toBe(true);
    expect(await page.locator(".brand").evaluate((img: HTMLImageElement) => img.naturalWidth)).toBeGreaterThan(0);
    await expect(page.locator("#rows tr")).toHaveCount(48);
    await expect(page.locator("#grand-total")).toHaveAttribute("data-rial", "1482000000");
    await expect(page.locator("#grand-total")).toHaveText(money(1482000000));
    const size = width <= 390 ? 6 : 12;
    await page.locator("#page-size").selectOption(String(size));
    await expect(page.locator("#previous")).toBeDisabled();
    const seen: number[] = [];
    for (let start = 0; start < 48; start += size) {
      const visible = page.locator("#rows tr:visible");
      await expect(visible).toHaveCount(size);
      const numbers = await visible.evaluateAll(elements => elements.map(element => Number((element as HTMLElement).dataset.number)));
      expect(numbers).toEqual(Array.from({ length: size }, (_, i) => start + i + 1));
      seen.push(...numbers);
      for (let i = 0; i < size; i++) {
        const number = start + i + 1;
        await expect(visible.nth(i).locator(".row-number")).toHaveText(fa(number).padStart(3, "۰"));
        await expect(visible.nth(i).locator(".amount")).toHaveText(money(number * 1250000 + ((number - 1) % 3) * 250000));
        await expect(visible.nth(i).locator(".description")).toBeVisible();
        await expect(visible.nth(i).locator(".badge")).toBeVisible();
      }
      const overflow = await page.evaluate(() => [...document.querySelectorAll<HTMLElement>("#rows tr:not([data-page-hidden='true']) td, #rows tr:not([data-page-hidden='true']) th")]
        .filter(element => element.scrollWidth > element.clientWidth + 1).map(element => element.textContent));
      expect(overflow, `${width}px cells`).toEqual([]);
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `${width}px document`).toBe(true);
      if (start === 0) await capture(`dense-${width}-first.png`, width);
      if (start + size < 48) {
        await page.locator("#next").focus();
        await page.keyboard.press("Enter");
      }
    }
    expect(seen).toEqual(Array.from({ length: 48 }, (_, i) => i + 1));
    await expect(page.locator("#next")).toBeDisabled();
    await expect(page.locator("#previous")).toBeFocused();
    await capture(`dense-${width}-last.png`, width);
    await page.keyboard.press("Enter");
    await expect(page.locator("#rows tr:visible").first()).toHaveAttribute("data-number", String(49 - 2 * size));
    await page.locator("#page-size").selectOption("24");
    await expect(page.locator("#rows tr:visible")).toHaveCount(24);
    await expect(page.locator("#previous")).toBeDisabled();
    await expect(page.locator("#rows tr:visible").first()).toHaveAttribute("data-number", "1");
  }
  await page.setViewportSize({ width: 1280, height: 800 });
  await page.locator("#page-size").selectOption("12");
  await page.locator("#next").click();
  await expect(page.locator("#rows tr:visible").first()).toHaveAttribute("data-number", "13");
  for (const [size, landscape] of [["A4", false], ["A4", true], ["A3", false], ["A3", true]] as const) {
    await page.emulateMedia({ media: "print", reducedMotion: "reduce" });
    await expect(page.locator(".screen:visible")).toHaveCount(0);
    await expect(page.locator("#rows tr:visible")).toHaveCount(48);
    await expect(page.locator(".cell-label:visible")).toHaveCount(0);
    expect(await page.locator("thead").evaluate(element => getComputedStyle(element).display)).toBe("table-header-group");
    const bytes = await page.pdf({ format: size, landscape, printBackground: true, preferCSSPageSize: false,
      displayHeaderFooter: true, headerTemplate: "<span></span>",
      footerTemplate: '<div style="width:100%;text-align:center;font-family:Arial,sans-serif;font-size:9px;color:#5c6971"><span class="pageNumber"></span> / <span class="totalPages"></span></div>',
    });
    expect(bytes.subarray(0, 4).toString()).toBe("%PDF");
    expect((bytes.toString("latin1").match(/\/Type\s*\/Page\b/gu) ?? []).length).toBeGreaterThan(1);
    const name = `dense-${size.toLowerCase()}-${landscape ? "landscape" : "portrait"}.pdf`;
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: digest(bytes), bytes: bytes.length });
    await page.emulateMedia({ media: "screen" });
    await expect(page.locator("#rows tr:visible")).toHaveCount(12);
    await expect(page.locator("#rows tr:visible").first()).toHaveAttribute("data-number", "13");
  }
  expect(requests).toEqual([]);
  expect(new Set(files.map(file => file.name)).size).toBe(files.length);
  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    prototype: "UX2-MS62", prototypeSha256: digest(readFileSync(prototype)), fixtureSha256: digest(readFileSync(fixture)),
    rowCount: 48, totalRial: 1482000000, files,
  }, null, 2) + "\n");
});

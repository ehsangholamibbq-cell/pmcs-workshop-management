import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { expect, test } from "@playwright/test";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const prototype = resolve(root, "docs/ux/prototypes/ms52/index.html");
const output = resolve(root, "src/web/artifacts/ms52-composition");
const states = ["default", "loading", "empty", "error", "permission", "offline"] as const;

test("MS52 Login Shell and Chart composition is truthful and keyboard reachable", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const network: string[] = [];
  const files: Array<{ name: string; sha256: string; bytes: number; width?: number; height?: number }> = [];
  page.on("request", (request) => { if (/^https?:/u.test(request.url())) network.push(request.url()); });
  async function capture(name: string, width: number, height: number) {
    await page.setViewportSize({ width, height });
    await page.evaluate(() => document.fonts.ready);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), name).toBe(true);
    const bytes = await page.screenshot({ fullPage: true, animations: "disabled", caret: "hide" });
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"), bytes: bytes.length, width, height });
  }
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(pathToFileURL(prototype).href);
  await expect(page.getByRole("heading", { name: "ترکیب ورود، پوسته و نمودار" })).toBeVisible();
  expect(await page.locator(".brand").evaluate((image: HTMLImageElement) => image.naturalWidth)).toBeGreaterThan(0);
  await page.evaluate(() => document.fonts.ready);
  expect(await page.evaluate(() => document.fonts.check("16px Vazirmatn"))).toBe(true);
  await expect(page.locator(".login-form input")).toHaveCount(0);
  await expect(page.locator("#chart-layout svg[role=img]")).toBeVisible();
  await expect(page.locator("#chart-layout table tbody tr")).toHaveCount(6);
  const select = page.locator("#state"), chart = page.locator("#chart-layout"), message = page.locator("#chart-message");
  for (const state of states) {
    await select.selectOption(state);
    await expect(page.locator("body")).toHaveAttribute("data-state", state);
    if (state === "default") {
      await expect(chart).toBeVisible();
      await expect(message).toBeHidden();
    } else {
      await expect(chart).toBeHidden();
      await expect(message).toBeVisible();
      await expect(page.locator("#message-title")).not.toBeEmpty();
      await expect(page.locator("#message-text")).not.toBeEmpty();
      if (state === "loading") await expect(page.locator("#skeleton")).toBeVisible();
      else await expect(page.locator("#skeleton")).toBeHidden();
      if (["error", "permission"].includes(state)) await expect(message).toHaveAttribute("role", "alert");
    }
  }
  const shots = [
    ["default", "desktop", 1440, 900], ["default", "tablet", 768, 1024], ["default", "mobile", 390, 844],
    ["loading", "mobile", 390, 844], ["empty", "mobile", 390, 844], ["error", "desktop", 1440, 900],
    ["permission", "mobile", 390, 844], ["offline", "mobile", 390, 844],
  ] as const;
  for (const [state, viewport, width, height] of shots) {
    await select.selectOption(state);
    await capture(`${state}-${viewport}.png`, width, height);
  }
  await select.selectOption("default");
  await page.setViewportSize({ width: 390, height: 844 });
  const toggle = page.locator("#nav-toggle"), nav = page.locator("#shell-nav");
  await expect(toggle).toBeVisible();
  await expect(toggle).toHaveAttribute("aria-expanded", "false");
  await toggle.click();
  await expect(toggle).toHaveAttribute("aria-expanded", "true");
  await expect(nav.getByRole("link", { name: "مرکز فرمان" })).toBeFocused();
  await capture("navigation-mobile.png", 390, 844);
  await page.keyboard.press("Escape");
  await expect(toggle).toHaveAttribute("aria-expanded", "false");
  await expect(toggle).toBeFocused();
  await toggle.click();
  await nav.getByRole("link", { name: "روند نمونه" }).click();
  await expect(toggle).toHaveAttribute("aria-expanded", "false");
  await page.setViewportSize({ width: 320, height: 720 });
  for (const state of states) {
    await select.selectOption(state);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), state).toBe(true);
  }
  await select.selectOption("default");
  await page.emulateMedia({ media: "print" });
  const pdf = await page.pdf({ format: "A4", printBackground: true });
  writeFileSync(resolve(output, "composition-a4.pdf"), pdf);
  files.push({ name: "composition-a4.pdf", sha256: createHash("sha256").update(pdf).digest("hex"), bytes: pdf.length });
  expect(network).toEqual([]);
  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    prototype: "UX2-MS52", states, files,
    prototypeSha256: createHash("sha256").update(readFileSync(prototype)).digest("hex"),
  }, null, 2) + "\n");
});

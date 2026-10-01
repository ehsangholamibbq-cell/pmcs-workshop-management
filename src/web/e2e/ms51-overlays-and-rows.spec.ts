import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { expect, test } from "@playwright/test";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const prototype = resolve(root, "docs/ux/prototypes/ms51/index.html");
const output = resolve(root, "src/web/artifacts/ms51-overlays-and-rows");
const states = ["default", "loading", "empty", "error", "permission", "offline", "conflict", "success"] as const;

test("MS51 overlays, feedback and mobile rows preserve focus and state truth", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const network: string[] = [];
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
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
  await expect(page.getByRole("heading", { name: "لایه‌ها و ردیف موبایل" })).toBeVisible();
  expect(await page.locator(".brand").evaluate((image: HTMLImageElement) => image.naturalWidth)).toBeGreaterThan(0);
  await page.evaluate(() => document.fonts.ready);
  expect(await page.evaluate(() => document.fonts.check("16px Vazirmatn"))).toBe(true);
  const select = page.locator("#state"), rows = page.locator("#rows"), skeleton = page.locator("#skeleton"), toastTrigger = page.locator("#toast-trigger");
  for (const state of states) {
    await select.selectOption(state);
    await expect(page.locator("body")).toHaveAttribute("data-state", state);
    await expect(page.locator("#feedback-title")).not.toBeEmpty();
    await expect(page.locator("#feedback-text")).not.toBeEmpty();
    if (["default", "success"].includes(state)) await expect(rows).toBeVisible();
    else await expect(rows).toBeHidden();
    if (state === "loading") {
      await expect(skeleton).toBeVisible();
      await expect(skeleton).toHaveAttribute("aria-hidden", "true");
    } else await expect(skeleton).toBeHidden();
    if (["loading", "error", "permission", "offline", "conflict"].includes(state)) await expect(toastTrigger).toBeDisabled();
    else await expect(toastTrigger).toBeEnabled();
    if (["error", "conflict"].includes(state)) await expect(page.locator("#feedback")).toHaveAttribute("role", "alert");
  }
  await select.selectOption("default");
  const popoverTrigger = page.locator("#popover-trigger"), popover = page.locator("#popover");
  await popoverTrigger.click();
  await expect(popoverTrigger).toHaveAttribute("aria-expanded", "true");
  await expect(page.locator("#popover-link")).toBeFocused();
  await page.keyboard.press("Escape");
  await expect(popover).toBeHidden();
  await expect(popoverTrigger).toBeFocused();
  await page.locator("#drawer-trigger").click();
  await expect(page.locator("#drawer")).toBeVisible();
  await expect(page.locator("#drawer-close")).toBeFocused();
  await page.keyboard.press("Tab");
  await expect(page.locator("#drawer-close")).toBeFocused();
  await page.keyboard.press("Escape");
  await expect(page.locator("#drawer")).toBeHidden();
  await expect(page.locator("#drawer-trigger")).toBeFocused();
  await toastTrigger.click();
  await expect(page.locator("#toast")).toContainText("درخواستی صادر نشد");
  await page.locator("#toast-close").click();
  await expect(page.locator("#toast")).toBeHidden();
  await expect(toastTrigger).toBeFocused();

  const shots = [
    ["default", "desktop", 1440, 900], ["default", "tablet", 768, 1024],
    ["default", "mobile", 390, 844], ["loading", "mobile", 390, 844],
    ["empty", "mobile", 390, 844], ["error", "mobile", 390, 844],
    ["permission", "desktop", 1440, 900], ["offline", "mobile", 390, 844],
    ["conflict", "desktop", 1440, 900], ["success", "desktop", 1440, 900],
  ] as const;
  for (const [state, viewport, width, height] of shots) {
    await select.selectOption(state);
    await capture(`${state}-${viewport}.png`, width, height);
    if (width <= 720 && ["default", "success"].includes(state)) {
      await expect(page.locator(".mobile-rows .row")).toHaveCount(2);
      await expect(page.locator(".desktop-table")).toBeHidden();
    }
  }
  await select.selectOption("default");
  await page.setViewportSize({ width: 390, height: 844 });
  await popoverTrigger.click();
  await capture("popover-mobile.png", 390, 844);
  await page.keyboard.press("Escape");
  await page.locator("#drawer-trigger").click();
  await capture("drawer-mobile.png", 390, 844);
  await page.keyboard.press("Escape");
  await page.setViewportSize({ width: 320, height: 720 });
  for (const state of states) {
    await select.selectOption(state);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), state).toBe(true);
  }
  expect(network).toEqual([]);
  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    prototype: "UX2-MS51", states, files,
    prototypeSha256: createHash("sha256").update(readFileSync(prototype)).digest("hex"),
  }, null, 2) + "\n");
});

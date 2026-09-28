import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { expect, test } from "@playwright/test";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const prototype = resolve(root, "docs/ux/prototypes/ms49/index.html");
const output = resolve(root, "src/web/artifacts/ms49-system-states");
const states = ["default", "loading", "error", "permission", "offline", "conflict", "success"] as const;

test("MS49 navigation, feedback and confirmation states remain accessible at desktop and mobile", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  const network: string[] = [];
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
  await expect(page.getByRole("heading", { name: "ناوبری، بازخورد و تأیید" })).toBeVisible();
  expect(await page.locator(".brand").evaluate((image: HTMLImageElement) => image.naturalWidth)).toBeGreaterThan(0);
  await page.evaluate(() => document.fonts.ready);
  expect(await page.evaluate(() => document.fonts.check("16px Vazirmatn"))).toBe(true);
  const select = page.locator("#state"), open = page.locator("#open-dialog"), dialog = page.locator("#review-dialog");
  for (const state of states) {
    await select.selectOption(state);
    await expect(page.locator("body")).toHaveAttribute("data-state", state);
    await expect(page.locator("#feedback-title")).not.toBeEmpty();
    await expect(page.locator("#feedback-text")).not.toBeEmpty();
    if (["loading", "error", "permission", "offline"].includes(state)) await expect(open).toBeDisabled();
    else await expect(open).toBeEnabled();
    if (["error", "conflict"].includes(state)) await expect(page.locator("#feedback")).toHaveAttribute("role", "alert");
  }
  await select.selectOption("conflict");
  await open.click();
  await expect(dialog).toBeVisible();
  await expect(page.locator("#close-dialog")).toBeFocused();
  await expect(page.locator("#confirm-action")).toBeDisabled();
  await page.keyboard.press("Tab");
  expect(await page.evaluate(() => {
    const modal = document.getElementById("review-dialog");
    return document.activeElement === modal || modal?.contains(document.activeElement);
  })).toBe(true);
  await page.locator("#confirm").check();
  await expect(page.locator("#confirm-action")).toBeEnabled();
  await page.locator("#confirm-action").click();
  await expect(page.locator("#dialog-result")).toContainText("هیچ درخواستی صادر نشد");
  await page.keyboard.press("Escape");
  await expect(dialog).not.toBeVisible();
  await expect(open).toBeFocused();
  await open.click();
  await expect(page.locator("#confirm-action")).toBeDisabled();
  await page.locator("#close-dialog").click();
  await select.selectOption("offline");
  await expect(open).toBeDisabled();
  await expect(page.locator("#action-reason")).toContainText("اتصال");

  const shots = [
    ["default", "desktop", 1440, 900], ["default", "tablet", 768, 1024],
    ["default", "mobile", 390, 844], ["loading", "desktop", 1440, 900],
    ["error", "mobile", 390, 844], ["permission", "desktop", 1440, 900],
    ["offline", "mobile", 390, 844], ["conflict", "desktop", 1440, 900],
    ["success", "desktop", 1440, 900],
  ] as const;
  for (const [state, viewport, width, height] of shots) {
    await select.selectOption(state);
    await capture(`${state}-${viewport}.png`, width, height);
  }
  await select.selectOption("conflict");
  await open.click();
  await capture("dialog-desktop.png", 1440, 900);
  await capture("dialog-mobile.png", 390, 844);
  await page.keyboard.press("Escape");
  await page.setViewportSize({ width: 320, height: 720 });
  const toggle = page.locator("#nav-toggle");
  await expect(toggle).toBeVisible();
  await expect(toggle).toHaveAttribute("aria-expanded", "false");
  await toggle.click();
  await expect(toggle).toHaveAttribute("aria-expanded", "true");
  await expect(page.getByRole("navigation", { name: "بخش‌های نمونه" }).getByRole("link", { name: "منبع و نسخه" })).toBeVisible();
  await page.getByRole("navigation", { name: "بخش‌های نمونه" }).getByRole("link", { name: "منبع و نسخه" }).click();
  await expect(toggle).toHaveAttribute("aria-expanded", "false");
  for (const state of states) {
    await select.selectOption(state);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `${state} at 320px`).toBe(true);
  }
  expect(network).toEqual([]);
  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    prototype: "UX2-MS49", states, files,
    prototypeSha256: createHash("sha256").update(readFileSync(prototype)).digest("hex"),
  }, null, 2) + "\n");
});

import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { expect, test } from "@playwright/test";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const prototype = resolve(root, "docs/ux/prototypes/ms47/index.html");
const output = resolve(root, "src/web/artifacts/ms47-component-states");
const states = ["default", "hover", "focus", "pressed", "disabled", "loading", "error", "success", "offline"] as const;

test("MS47 component states retain semantics, keyboard access and responsive review evidence", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  async function capture(name: string, width: number, height: number) {
    await page.setViewportSize({ width, height });
    await page.evaluate(() => document.fonts.ready);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), name).toBe(true);
    const bytes = await page.screenshot({ fullPage: true, animations: "disabled", caret: "hide" });
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"), bytes: bytes.length, width, height });
  }
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(pathToFileURL(prototype).href);
  await expect(page.getByRole("heading", { name: "PMCS · Stateهای اجزای مشترک" })).toBeVisible();
  expect(await page.locator(".brand").evaluate((image: HTMLImageElement) => image.naturalWidth)).toBeGreaterThan(0);
  const select = page.locator("#state"), primary = page.locator("#primary"), secondary = page.locator("#secondary");
  const field = page.locator("#sample-field");
  for (const state of states) {
    await select.selectOption(state);
    await expect(page.locator("body")).toHaveAttribute("data-state", state);
    await expect(page.locator("#feedback-title")).not.toBeEmpty();
    await expect(page.locator("#feedback-text")).not.toBeEmpty();
    if (["disabled", "loading", "offline"].includes(state)) {
      await expect(primary).toBeDisabled();
      await expect(field).toBeDisabled();
    } else {
      await expect(primary).toBeEnabled();
    }
    if (state === "error") {
      await expect(field).toHaveAttribute("aria-invalid", "true");
      await expect(field).toHaveAttribute("aria-describedby", "field-help");
      await expect(page.locator("#feedback")).toHaveAttribute("role", "alert");
      await expect(page.locator("#field-help")).toContainText("خطا:");
    }
    if (state === "loading") await expect(primary).toHaveAttribute("aria-busy", "true");
    if (state === "pressed") await expect(secondary).toHaveAttribute("aria-pressed", "true");
    if (state === "offline") await expect(page.locator("#action-reason")).toContainText("ارتباط قطع است");
  }
  await select.selectOption("default");
  const defaultBackground = await primary.evaluate((button) => getComputedStyle(button).backgroundColor);
  await primary.hover();
  const hoverBackground = await primary.evaluate((button) => getComputedStyle(button).backgroundColor);
  expect(hoverBackground).not.toBe(defaultBackground);
  await select.selectOption("disabled");
  const disabledBackground = await primary.evaluate((button) => getComputedStyle(button).backgroundColor);
  await primary.hover({ force: true });
  expect(await primary.evaluate((button) => getComputedStyle(button).backgroundColor)).toBe(disabledBackground);
  await select.selectOption("focus");
  await expect(primary).toBeFocused();
  await page.keyboard.press("Tab");
  await page.keyboard.press("Shift+Tab");
  await expect(primary).toBeFocused();
  expect(await primary.evaluate((button) => getComputedStyle(button).outlineStyle)).not.toBe("none");
  await select.selectOption("pressed");
  await secondary.click();
  await expect(secondary).toHaveAttribute("aria-pressed", "false");
  await select.selectOption("default");
  await primary.click();
  await expect(page.locator("#action-reason")).toContainText("درخواستی صادر نشد");
  await page.reload();

  const shots = [
    ["default", "desktop", 1440, 900], ["default", "tablet", 768, 1024],
    ["default", "mobile", 390, 844], ["focus", "desktop", 1440, 900],
    ["pressed", "desktop", 1440, 900], ["disabled", "desktop", 1440, 900],
    ["loading", "desktop", 1440, 900], ["error", "desktop", 1440, 900],
    ["error", "mobile", 390, 844], ["success", "desktop", 1440, 900],
    ["offline", "mobile", 390, 844],
  ] as const;
  for (const [state, viewport, width, height] of shots) {
    await select.selectOption(state);
    await capture(`${state}-${viewport}.png`, width, height);
  }
  await page.setViewportSize({ width: 320, height: 720 });
  for (const state of states) {
    await select.selectOption(state);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `${state} at 320px`).toBe(true);
  }
  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1,
    source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    prototype: "UX2-MS47", states, files,
    prototypeSha256: createHash("sha256").update(readFileSync(prototype)).digest("hex"),
  }, null, 2) + "\n");
});

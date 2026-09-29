import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { expect, test } from "@playwright/test";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const baseline = resolve(root, "docs/ux/prototypes/ms52/index.html");
const prototype = resolve(root, "docs/ux/prototypes/ms54/index.html");
const output = resolve(root, "src/web/artifacts/ms54-owner-followups");
const states = ["default", "loading", "empty", "error", "permission", "offline"] as const;
const digest = (bytes: Buffer) => createHash("sha256").update(bytes).digest("hex");

test("MS54 owner follow-ups shorten mobile composition without losing state truth", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const requests: string[] = [];
  page.on("request", request => { if (/^https?:/u.test(request.url())) requests.push(request.url()); });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  const heights: Array<{ width: number; baseline: number; candidate: number }> = [];

  for (const [width, height] of [[390, 844], [320, 720]] as const) {
    await page.setViewportSize({ width, height });
    await page.goto(pathToFileURL(baseline).href);
    await page.evaluate(() => document.fonts.ready);
    const baselineHeight = await page.evaluate(() => document.documentElement.scrollHeight);
    await page.goto(pathToFileURL(prototype).href);
    await page.evaluate(() => document.fonts.ready);
    expect(await page.evaluate(() => document.fonts.check("16px Vazirmatn"))).toBe(true);
    expect(await page.locator(".brand").evaluate((image: HTMLImageElement) => image.naturalWidth)).toBeGreaterThan(0);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `${width}px overflow`).toBe(true);
    const candidateHeight = await page.evaluate(() => document.documentElement.scrollHeight);
    expect(candidateHeight, `${width}px should save at least 80px`).toBeLessThanOrEqual(baselineHeight - 80);
    heights.push({ width, baseline: baselineHeight, candidate: candidateHeight });

    const visibleText = await page.locator("body").innerText();
    expect(visibleText).not.toMatch(/\b(?:Motion|Context|Disclosure|Snapshot|Prototype|Login|Shell|Permission|Chart|Source|Fact)\b/u);
    expect(visibleText).toContain("پوسته و زمینهٔ پروژه");
    expect(visibleText).toContain("تحلیل هوشمند");
    await expect(page.locator(".login-form input")).toHaveCount(0);
    await expect(page.locator("#chart-layout svg[role=img]")).toBeVisible();
    await expect(page.locator("#chart-layout table tbody tr")).toHaveCount(6);

    const stateSelect = page.locator("#state"), chart = page.locator("#chart-layout"), message = page.locator("#chart-message");
    for (const state of states) {
      await stateSelect.selectOption(state);
      await expect(page.locator("body")).toHaveAttribute("data-state", state);
      if (state === "default") {
        await expect(chart).toBeVisible();
        await expect(message).toBeHidden();
      } else {
        await expect(chart).toBeHidden();
        await expect(message).toBeVisible();
        await expect(page.locator("#message-title")).not.toBeEmpty();
        if (["error", "permission"].includes(state)) await expect(message).toHaveAttribute("role", "alert");
      }
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `${width}px ${state}`).toBe(true);
    }
    await stateSelect.selectOption("default");
    const navToggle = page.locator("#nav-toggle"), nav = page.locator("#shell-nav");
    await expect(navToggle).toBeVisible();
    expect(await navToggle.evaluate((button: HTMLElement) => button.getBoundingClientRect().height)).toBeGreaterThanOrEqual(44);
    await navToggle.click();
    await expect(navToggle).toHaveAttribute("aria-expanded", "true");
    await expect(nav.getByRole("link", { name: "مرکز فرمان" })).toBeFocused();
    await page.keyboard.press("Escape");
    await expect(navToggle).toBeFocused();
    await expect(navToggle).toHaveAttribute("aria-expanded", "false");
    const bytes = await page.screenshot({ fullPage: true, animations: "disabled", caret: "hide" });
    const name = `refined-${width}.png`;
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: digest(bytes), bytes: bytes.length, width, height });
  }
  expect(requests).toEqual([]);
  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    prototype: "UX2-MS54", baselineSha256: digest(readFileSync(baseline)),
    prototypeSha256: digest(readFileSync(prototype)), heights, files,
  }, null, 2) + "\n");
});

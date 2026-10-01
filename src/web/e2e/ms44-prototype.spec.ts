import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { expect, test } from "@playwright/test";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const prototype = resolve(root, "docs/ux/prototypes/ms44/index.html");
const output = resolve(root, "src/web/artifacts/ms44-prototype");
const fonts = {
  vazirmatn: ["Vazirmatn Review", "4e3fa217d38fdafc1fea4414ceb58ca5e662cf0ab5fa735a8c8c20e8b42cad92"],
  estedad: ["Estedad Review", "18a2278ad5c9c2f60e034270ea2d5856d04c4e984e47c65483aa63b41e3c5a1e"],
} as const;
const scenarios = ["portfolio", "command", "bootstrap", "work", "table", "chat", "report", "agent", "login", "profile"];
const states = ["default", "empty", "loading", "error", "permission", "offline", "conflict"];

test("MS44 independent review prototype covers scenarios, states, font files, mobile and print", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number }> = [];
  async function record(name: string, bytes: Buffer) {
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"), bytes: bytes.length });
  }
  for (const [name, [, expected]] of Object.entries(fonts)) {
    const bytes = readFileSync(resolve(root, `docs/ux/prototypes/ms44/fonts/${name === "vazirmatn" ? "Vazirmatn" : "Estedad"}-variable.woff2`));
    expect(createHash("sha256").update(bytes).digest("hex")).toBe(expected);
  }
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(pathToFileURL(prototype).href);
  await expect(page.getByRole("heading", { name: "PMCS · بازبینی UX2-MS44" })).toBeVisible();
  expect(await page.locator(".side img").evaluate((image: HTMLImageElement) => image.naturalWidth)).toBeGreaterThan(0);
  expect(await page.locator(".side img").evaluate((image: HTMLImageElement) => getComputedStyle(image).backgroundColor)).toBe("rgb(251, 248, 241)");
  for (const scenario of scenarios) {
    await page.locator("#scenario").selectOption(scenario);
    for (const state of states) {
      await page.locator("#state").selectOption(state);
      await expect(page.locator("#surface h2")).toHaveText(await page.locator("#top-title").textContent() ?? "");
      await expect(page.locator("#surface .state")).toHaveCount(state === "default" ? 0 : 1);
      if (state === "loading") await expect(page.locator("#surface .skeleton[aria-hidden='true']")).toHaveCount(2);
    }
  }
  await page.locator("#state").selectOption("default");
  await page.locator("#scenario").selectOption("command");
  await expect(page.locator("#surface")).toContainText("هنوز ساخته نشده است");
  for (const [name, [family]] of Object.entries(fonts)) {
    await page.locator("#font").selectOption(name);
    await page.evaluate(async (value) => { await document.fonts.load(`400 16px "${value}"`); }, family);
    expect(await page.evaluate((value) => document.fonts.check(`400 16px "${value}"`), family)).toBe(true);
    await record(`${name}-command-desktop.png`, await page.screenshot({ fullPage: true, animations: "disabled" }));
    await record(`${name}-command-print.pdf`, await page.pdf({ format: "A4", preferCSSPageSize: true, printBackground: true }));
    await page.setViewportSize({ width: 768, height: 1024 });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
    await record(`${name}-command-tablet.png`, await page.screenshot({ fullPage: true, animations: "disabled" }));
    await page.setViewportSize({ width: 390, height: 844 });
    await expect.poll(() => page.locator(".side img").evaluate((image: HTMLImageElement) =>
      image.currentSrc.endsWith("bbq-official-symbol.png") && image.naturalWidth > 0)).toBe(true);
    await record(`${name}-command-mobile.png`, await page.screenshot({ fullPage: true, animations: "disabled" }));
    await page.setViewportSize({ width: 1440, height: 900 });
  }
  await page.locator("#font").selectOption("baseline");
  await record("baseline-command-desktop.png", await page.screenshot({ fullPage: true, animations: "disabled" }));
  await page.setViewportSize({ width: 768, height: 1024 });
  await record("baseline-command-tablet.png", await page.screenshot({ fullPage: true, animations: "disabled" }));
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.locator("#scenario").selectOption("portfolio");
  await page.locator("#state").selectOption("error");
  await record("baseline-portfolio-error.png", await page.screenshot({ fullPage: true, animations: "disabled" }));
  await page.setViewportSize({ width: 320, height: 720 });
  for (const scenario of scenarios) {
    await page.locator("#scenario").selectOption(scenario);
    for (const state of states) {
      await page.locator("#state").selectOption(state);
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), `${scenario}/${state} overflow`).toBe(true);
    }
  }
  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    prototype: "UX2-MS44", scenarios, states, fonts: Object.keys(fonts), files,
  }, null, 2) + "\n");
});

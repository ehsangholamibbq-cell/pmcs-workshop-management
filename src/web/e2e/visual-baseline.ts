import { readFile, mkdir } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { expect, type Page } from "@playwright/test";

interface Capture {
  readonly id: string;
  readonly route: string;
  readonly viewport: "desktop" | "tablet" | "mobile";
}

const manifestPath = fileURLToPath(new URL("./visual-baseline.json", import.meta.url));
const manifest = JSON.parse(await readFile(manifestPath, "utf8")) as {
  viewports: Record<Capture["viewport"], { width: number; height: number }>;
  captures: Capture[];
};
const directory = path.resolve("artifacts/visual-tour");

export async function captureVisualBaseline(page: Page, id: string): Promise<void> {
  const entry = manifest.captures.find((item) => item.id === id);
  if (!entry) throw new Error(`Undeclared visual baseline: ${id}`);
  const actualPath = new URL(page.url()).pathname;
  const routePattern = new RegExp(`^${entry.route.replace(/\[projectId\]/gu,
    "[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}")}$`, "iu");
  expect(actualPath, `Visual baseline route ${id}`).toMatch(routePattern);
  await page.setViewportSize(manifest.viewports[entry.viewport]);
  await page.emulateMedia({ reducedMotion: "reduce", colorScheme: "light" });
  await expect(page.locator("html")).toHaveAttribute("lang", "fa");
  await expect(page.locator("html")).toHaveAttribute("dir", "rtl");
  await page.evaluate(() => document.fonts.ready);
  await mkdir(directory, { recursive: true });
  await page.screenshot({ path: path.join(directory, `${id}.png`), animations: "disabled", caret: "hide" });
}

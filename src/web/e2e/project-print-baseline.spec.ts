import { expect, test } from "@playwright/test";
import { mkdir, writeFile } from "node:fs/promises";
import path from "node:path";
import { projectPath } from "./support";
import { captureVisualBaseline } from "./visual-baseline";

test("capture the current Project Command Center browser print output for audit", async ({ page }) => {
  await page.goto(projectPath);
  await expect(page.getByRole("heading", { name: "مرکز فرمان پروژه" })).toBeVisible();
  await expect(page.locator("#today")).toBeVisible();
  await page.emulateMedia({ media: "print", reducedMotion: "reduce" });
  await expect.poll(() => page.evaluate(() => matchMedia("print").matches)).toBe(true);
  await captureVisualBaseline(page, "43-project-print-preview");
  const pdf = await page.pdf({ format: "A4", printBackground: true });
  expect(pdf.subarray(0, 5).toString("ascii")).toBe("%PDF-");
  await mkdir("artifacts/visual-tour", { recursive: true });
  await writeFile(path.join("artifacts/visual-tour", "43-project-print-preview.pdf"), pdf);
});

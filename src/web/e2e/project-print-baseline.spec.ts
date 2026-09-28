import { expect, test } from "@playwright/test";
import { mkdir, writeFile } from "node:fs/promises";
import path from "node:path";
import { projectPath } from "./support";
import { captureVisualBaseline } from "./visual-baseline";

test("capture the current Project Command Center browser print output for audit", async ({ page }) => {
  await page.goto(projectPath);
  await expect(page.getByRole("heading", { name: "مرکز فرمان پروژه" })).toBeVisible();
  await expect(page.locator(".workspace").getByText("پروژه نمونه ساختمان اداری–تجاری")).toBeVisible();
  await expect(page.locator("#today")).toBeVisible();
  await page.emulateMedia({ media: "print", reducedMotion: "reduce" });
  await expect.poll(() => page.evaluate(() => matchMedia("print").matches)).toBe(true);
  await expect(page.locator(".project-print-sheet")).toBeVisible();
  await expect(page.locator(".project-print-shell > .sidebar")).toBeHidden();
  await expect(page.locator(".project-print-shell > .workspace")).toBeHidden();
  await expect(page.locator(".project-print-sheet input, .project-print-sheet button, .project-print-sheet select")).toHaveCount(0);
  await expect.poll(() => page.locator(".project-print-logo").evaluate((image: HTMLImageElement) =>
    image.complete && image.naturalWidth > 0)).toBe(true);
  await expect(page.locator(".project-print-sheet")).toContainText("گزارش رسمی امضاشده یا خروجی مرکز گزارش‌ها نیست");
  await captureVisualBaseline(page, "43-project-print-preview");
  const pdf = await page.pdf({ format: "A4", printBackground: true });
  expect(pdf.subarray(0, 5).toString("ascii")).toBe("%PDF-");
  await mkdir("artifacts/visual-tour", { recursive: true });
  await writeFile(path.join("artifacts/visual-tour", "43-project-print-preview.pdf"), pdf);
});

import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";
import { projectId } from "./support";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const output = resolve(root, "src/web/artifacts/ms77-project-access-truth");

test("revoked project access clears a saved command snapshot before showing project modules", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  async function capture(name: string, width: number, height: number) {
    await page.evaluate(() => document.fonts.ready);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    const bytes = await page.screenshot({ animations: "disabled", caret: "hide" });
    expect(bytes.readUInt32BE(16)).toBe(width);
    expect(bytes.readUInt32BE(20)).toBe(height);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"),
      bytes: bytes.length, width, height });
  }

  let responseStatus = 200;
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/command-center`, route =>
    responseStatus === 200 ? route.continue() : route.fulfill({ status: responseStatus,
      contentType: "application/problem+json",
      body: JSON.stringify({ status: responseStatus, title: "Sensitive permission detail" }) }));

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/projects/${projectId}`);
  const main = page.locator(".project-print-shell");
  await expect(main).toHaveAttribute("data-command-read-state", "current");
  const projectName = await page.locator(".project-print-heading h1").textContent();
  expect(projectName).toBeTruthy();
  await capture("project-390-current.png", 390, 844);
  const cacheBefore = await page.evaluate(() => Object.keys(localStorage).filter(key =>
    key.includes("pmcs-command-center:")));
  expect(cacheBefore.length).toBeGreaterThan(0);

  responseStatus = 403;
  await page.setViewportSize({ width: 320, height: 720 });
  await page.reload();
  await expect(main).toHaveAttribute("data-command-read-state", "forbidden");
  await expect(page.getByRole("heading", { name: "دسترسی به پروژه تأیید نشد" })).toBeVisible();
  await expect(page.locator(".project-print-sheet")).toHaveCount(0);
  await expect(page.getByText(projectName ?? "", { exact: true })).toHaveCount(0);
  await expect(main.getByRole("alert")).not.toContainText("Sensitive permission detail");
  const cacheAfter = await page.evaluate(() => Object.keys(localStorage).filter(key =>
    key.includes("pmcs-command-center:") || key.includes("pmcs-project-locations:")));
  expect(cacheAfter).toEqual([]);
  await capture("project-320-access-revoked.png", 320, 720);

  responseStatus = 200;
  await page.reload();
  await expect(main).toHaveAttribute("data-command-read-state", "current");
  await expect(page.locator(".hero-grid")).toBeVisible();
  await expect(page.locator(".project-print-sheet")).toHaveCount(1);
  await expect(page.locator(".project-print-sheet")).toBeHidden();
  await capture("project-320-access-restored.png", 320, 720);

  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    route: `/projects/${projectId}`, owner: "VX-G4 project command access truth", files,
  }, null, 2) + "\n");
});

import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";
import { projectId } from "./support";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const output = resolve(root, "src/web/artifacts/ms79-technical-read-truth");

test("technical office hides prior documents and commands after a failed or forbidden read", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  async function capture(name: string, width: number, height: number, expectedState: "current" | "cached" | "forbidden") {
    await page.evaluate(() => document.fonts.ready);
    await page.locator("#technical-office").evaluate((element) => {
      window.scrollTo({
        top: Math.max(0, window.scrollY + element.getBoundingClientRect().top - 150),
        behavior: "instant",
      });
    });
    const titleTop = () => page.locator("#technical-office .section-title")
      .evaluate((element) => element.getBoundingClientRect().top);
    await expect.poll(async () => {
      const top = await titleTop();
      if (top < 120 || top >= 250) await page.locator("#technical-office").evaluate((element) =>
        window.scrollTo({ top: Math.max(0, window.scrollY + element.getBoundingClientRect().top - 150),
          behavior: "instant" }));
      await page.waitForTimeout(150);
      const settled = await titleTop();
      return settled >= 120 && settled < 250;
    }, { timeout: 10_000 }).toBe(true);
    await expect.poll(async () => {
      const before = await page.locator("#technical-office").getAttribute("data-read-state");
      await page.waitForTimeout(400);
      const after = await page.locator("#technical-office").getAttribute("data-read-state");
      return before === expectedState && after === expectedState;
    }, { timeout: 10_000 }).toBe(true);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    const bytes = await page.screenshot({ animations: "disabled", caret: "hide" });
    expect(await page.locator("#technical-office").getAttribute("data-read-state")).toBe(expectedState);
    expect(bytes.readUInt32BE(16)).toBe(width);
    expect(bytes.readUInt32BE(20)).toBe(height);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"),
      bytes: bytes.length, width, height });
  }

  let responseStatus = 200;
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/technical-office/state`, route =>
    responseStatus === 200 ? route.continue() : route.fulfill({ status: responseStatus,
      contentType: "application/problem+json",
      body: JSON.stringify({ status: responseStatus, title: "Sensitive technical detail" }) }));

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/projects/${projectId}`);
  const office = page.locator("#technical-office");
  await expect(office).toHaveAttribute("data-read-state", "current");
  await expect(office.locator(".technical-editor").first()).toBeVisible();
  await capture("technical-390-current.png", 390, 844, "current");

  responseStatus = 503;
  await page.reload();
  await expect(office).toHaveAttribute("data-read-state", "cached");
  await expect(office.locator(".collaboration-state.cached")).toHaveCSS("border-top-color", "rgb(153, 96, 23)");
  await expect(office.getByRole("alert")).not.toContainText("Sensitive technical detail");
  await expect(office.locator(".technical-editor")).toHaveCount(0);
  await expect(office.locator(".technical-summary-grid")).toHaveCount(0);
  await page.setViewportSize({ width: 320, height: 720 });
  await capture("technical-320-cached-error.png", 320, 720, "cached");

  responseStatus = 403;
  await page.reload();
  await expect(office).toHaveAttribute("data-read-state", "forbidden");
  await expect(office.locator(".collaboration-state.forbidden")).toHaveCSS("border-top-color", "rgb(170, 55, 64)");
  await expect(office.getByRole("alert")).toContainText("دادهٔ ذخیره‌شده نمایش داده نمی‌شود");
  await expect(office.locator(".technical-editor")).toHaveCount(0);
  const keys = await page.evaluate(() => Object.keys(localStorage).filter(key => key.includes("pmcs-technical-office:")));
  expect(keys).toEqual([]);
  await capture("technical-320-access-revoked.png", 320, 720, "forbidden");

  responseStatus = 200;
  await page.reload();
  await expect(office).toHaveAttribute("data-read-state", "current");
  await expect(office.locator(".technical-editor").first()).toBeVisible();
  await capture("technical-320-access-restored.png", 320, 720, "current");

  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    route: `/projects/${projectId}`, owner: "VX-G4 technical office read truth", files,
  }, null, 2) + "\n");
});

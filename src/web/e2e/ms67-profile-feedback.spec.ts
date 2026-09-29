import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";
import type { MemberProfileModel } from "../lib/member-profile";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const output = resolve(root, "src/web/artifacts/ms67-profile-feedback");

test("active Profile distinguishes loading, validation error and saved state", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];

  async function capture(name: string, width: number, height: number) {
    await page.locator(".profile-feedback").scrollIntoViewIfNeeded();
    await page.evaluate(() => document.fonts.ready);
    const bytes = await page.screenshot({ animations: "disabled", caret: "hide" });
    expect(bytes.readUInt32BE(16)).toBe(width);
    expect(bytes.readUInt32BE(20)).toBe(height);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"),
      bytes: bytes.length, width, height });
  }

  let releaseLoad: () => void = () => {};
  const loadGate = new Promise<void>(resolve => { releaseLoad = resolve; });
  await page.route("**/api/pmcs/api/v1/member-profile", async route => {
    if (route.request().method() === "GET") await loadGate;
    await route.continue();
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/profile");
  const feedback = page.locator(".profile-feedback");
  const form = page.locator(".profile-workspace form");
  await expect(feedback).toHaveAttribute("role", "status");
  await expect(feedback).toContainText("در حال دریافت پروفایل…");
  await expect(form).toHaveAttribute("aria-busy", "false");
  await expect(page.getByLabel("انتخاب تصویر")).toBeDisabled();
  await expect(page.locator(".profile-photo-controls .file-button")).toHaveAttribute("aria-disabled", "true");
  await capture("profile-390-loading.png", 390, 844);
  releaseLoad();
  await expect(page.getByRole("heading", { name: "مشخصات کاری من" })).toBeVisible();
  await expect(feedback).toHaveCount(0);
  await expect(page.getByLabel("انتخاب تصویر")).toBeEnabled();

  const invalidImage = { name: "too-large.png", mimeType: "image/png",
    buffer: Buffer.alloc(5 * 1024 * 1024 + 1) };
  await page.getByLabel("انتخاب تصویر").setInputFiles(invalidImage);
  await expect(feedback).toHaveAttribute("role", "alert");
  await expect(feedback).toContainText("حداکثر ۵ مگابایت");
  await expect(form).toHaveAttribute("aria-busy", "false");
  await capture("profile-390-error.png", 390, 844);
  await page.setViewportSize({ width: 320, height: 720 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await capture("profile-320-error.png", 320, 720);

  const response = await page.request.get("/api/pmcs/api/v1/member-profile");
  expect(response.ok()).toBe(true);
  const current = await response.json() as MemberProfileModel;
  let releaseSave: () => void = () => {};
  const saveGate = new Promise<void>(resolve => { releaseSave = resolve; });
  await page.unroute("**/api/pmcs/api/v1/member-profile");
  await page.route("**/api/pmcs/api/v1/member-profile", async route => {
    if (route.request().method() !== "PUT") return route.continue();
    await saveGate;
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
      ...current, userRevision: current.userRevision + 1,
      profileRevision: current.profileRevision + 1,
    }) });
  });
  await page.getByRole("button", { name: "ذخیره پروفایل" }).click();
  await expect(form).toHaveAttribute("aria-busy", "true");
  await expect(feedback).toHaveAttribute("role", "status");
  await expect(feedback).toContainText("در حال ذخیره تغییرات…");
  await capture("profile-320-saving.png", 320, 720);
  releaseSave();
  await expect(form).toHaveAttribute("aria-busy", "false");
  await expect(feedback).toHaveAttribute("role", "status");
  await expect(feedback).toContainText("پروفایل با موفقیت به‌روزرسانی شد.");
  await capture("profile-320-success.png", 320, 720);

  expect(new Set(files.map(file => file.name)).size).toBe(5);
  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    route: "/profile", owner: "VX-G4 active feedback wave", files,
  }, null, 2) + "\n");
});

import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const output = resolve(root, "src/web/artifacts/ms68-identity-feedback");

test("identity directory and invitation retain truthful loading, failure and success states", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];

  async function capture(name: string, width: number, height: number) {
    await page.locator(".identity-feedback").scrollIntoViewIfNeeded();
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
  await page.route("**/api/pmcs/api/v1/identity/directory", async route => {
    await loadGate;
    await route.fulfill({ status: 503, contentType: "application/problem+json",
      body: JSON.stringify({ status: 503, code: "service.unavailable", title: "Sensitive upstream error" }) });
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/admin/users");
  const feedback = page.locator(".identity-feedback");
  await expect(feedback).toHaveAttribute("role", "status");
  await expect(feedback).toContainText("در حال دریافت فهرست کاربران و دعوت‌ها…");
  await expect(page.getByRole("heading", { name: "افزودن کاربر" })).toHaveCount(0);
  await capture("identity-390-loading.png", 390, 844);
  releaseLoad();
  await expect(feedback).toHaveAttribute("role", "alert");
  await expect(feedback).toContainText("در حال حاضر مشکلی در سرور رخ داده است");
  await expect(feedback).not.toContainText("Sensitive upstream error");
  await expect(page.getByRole("heading", { name: "افزودن کاربر" })).toHaveCount(0);
  await capture("identity-390-load-error.png", 390, 844);
  await page.setViewportSize({ width: 320, height: 720 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await capture("identity-320-load-error.png", 320, 720);

  await page.unroute("**/api/pmcs/api/v1/identity/directory");
  await page.getByRole("button", { name: "تازه‌سازی" }).click();
  await expect(page.getByRole("heading", { name: "افزودن کاربر" })).toBeVisible();
  await expect(feedback).toHaveAttribute("role", "status");
  const form = page.locator(".identity-form");
  await form.getByLabel("نام و نام خانوادگی").fill("کاربر آزمون بازخورد");
  await form.getByLabel("نشانی ایمیل").fill("ms68-feedback@example.invalid");
  const commandKeys: string[] = [];
  await page.route("**/api/pmcs/api/v1/identity/invitations", async route => {
    commandKeys.push(route.request().headers()["idempotency-key"] ?? "");
    await route.fulfill({ status: 503, contentType: "application/problem+json",
      body: JSON.stringify({ status: 503, code: "service.unavailable", title: "Sensitive upstream error" }) });
  });
  await form.getByRole("button", { name: "ثبت و ارسال دعوت امن" }).click();
  await expect(feedback).toHaveAttribute("role", "alert");
  await expect(form.getByLabel("نام و نام خانوادگی")).toHaveValue("کاربر آزمون بازخورد");
  await expect(form.getByLabel("نشانی ایمیل")).toHaveValue("ms68-feedback@example.invalid");
  await expect(form).toHaveAttribute("aria-busy", "false");
  await capture("identity-320-invite-error.png", 320, 720);

  await page.unroute("**/api/pmcs/api/v1/identity/invitations");
  let releaseSave: () => void = () => {};
  const saveGate = new Promise<void>(resolve => { releaseSave = resolve; });
  await page.route("**/api/pmcs/api/v1/identity/invitations", async route => {
    commandKeys.push(route.request().headers()["idempotency-key"] ?? "");
    await saveGate;
    await route.fulfill({ status: 200, contentType: "application/json", body: "{}" });
  });
  await form.getByRole("button", { name: "ثبت و ارسال دعوت امن" }).click();
  await expect(form).toHaveAttribute("aria-busy", "true");
  await expect(form.getByLabel("نام و نام خانوادگی")).toBeDisabled();
  await capture("identity-320-invite-saving.png", 320, 720);
  releaseSave();
  await expect(feedback).toHaveAttribute("role", "status");
  await expect(feedback).toContainText("دعوت در صف پایدار ثبت شد");
  await expect(form.getByLabel("نام و نام خانوادگی")).toHaveValue("");
  await expect(form.getByLabel("نشانی ایمیل")).toHaveValue("");
  expect(commandKeys).toHaveLength(2);
  expect(commandKeys[0]).toBeTruthy();
  expect(commandKeys[1]).toBe(commandKeys[0]);
  await capture("identity-320-invite-success.png", 320, 720);

  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    route: "/admin/users", owner: "VX-G4 active identity feedback wave", files,
  }, null, 2) + "\n");
});

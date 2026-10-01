import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";
import { projectId } from "./support";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const output = resolve(root, "src/web/artifacts/ms69-permission-preview");

test("permission preview never presents a prior role's result as current", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];

  async function capture(name: string, width: number, height: number) {
    await page.locator(".membership-editor").first().scrollIntoViewIfNeeded();
    await page.evaluate(() => document.fonts.ready);
    const bytes = await page.screenshot({ animations: "disabled", caret: "hide" });
    expect(bytes.readUInt32BE(16)).toBe(width);
    expect(bytes.readUInt32BE(20)).toBe(height);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"),
      bytes: bytes.length, width, height });
  }

  let releasePreview: () => void = () => {};
  const previewGate = new Promise<void>(resolve => { releasePreview = resolve; });
  let requests = 0;
  await page.route("**/api/pmcs/api/v1/projects", async route => {
    await route.fulfill({ status: 200, contentType: "application/json",
      body: JSON.stringify([{ id: projectId, code: "MS69-QA", name: "پروژه نمونهٔ پیش‌نمایش" }]) });
  });
  await page.route(/\/identity\/permissions\/preview\?/u, async route => {
    requests += 1;
    if (requests === 1) {
      await previewGate;
      const query = new URL(route.request().url()).searchParams;
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
        userId: query.get("userId"), projectId: query.get("projectId"),
        accountStatus: "Active", tenantRole: "Member", projectRole: query.get("proposedRoleCode"),
        policyVersion: "ms69-browser-sample", evaluatedAt: new Date().toISOString(),
        decisions: [{ operation: "projects.read", allowed: true, source: "role", scope: "Project",
          condition: "active date-bounded membership", denyReason: null, expiresAt: null, delegationEffect: "None" }],
      }) });
    } else {
      await route.fulfill({ status: 503, contentType: "application/problem+json",
        body: JSON.stringify({ status: 503, code: "service.unavailable", title: "Sensitive upstream error" }) });
    }
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/admin/users");
  const card = page.locator(".identity-user-card").filter({ has: page.locator(".membership-editor") }).first();
  await expect(card).toBeVisible();
  const editor = card.locator(".membership-editor");
  const project = editor.getByRole("combobox", { name: "پروژه", exact: true });
  const role = editor.getByRole("combobox", { name: "نقش پروژه", exact: true });
  await expect(project).toContainText("پروژه نمونهٔ پیش‌نمایش");
  await project.selectOption(projectId, { timeout: 10_000 });
  await editor.getByRole("button", { name: "پیش‌نمایش نقش انتخابی" }).click();
  await expect(card.getByRole("status")).toContainText("در حال محاسبهٔ مجوز مؤثر…");
  await expect(card.locator(".permission-preview")).toHaveCount(0);
  await expect(project).toBeDisabled();
  await expect(role).toBeDisabled();
  await capture("preview-390-loading.png", 390, 844);

  releasePreview();
  await expect(card.locator(".permission-preview")).toContainText("ms69-browser-sample");
  await expect(card.getByRole("status")).toContainText("مجوز جدیدی ایجاد یا ذخیره نمی‌کند");
  await expect(project).toBeEnabled();
  await capture("preview-390-current.png", 390, 844);

  await page.setViewportSize({ width: 320, height: 720 });
  await role.selectOption("ProjectManager");
  await expect(card.locator(".permission-preview")).toHaveCount(0);
  await expect(card.locator(".permission-preview-message")).toHaveCount(0);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await capture("preview-320-role-changed.png", 320, 720);
  await editor.getByRole("button", { name: "پیش‌نمایش نقش انتخابی" }).click();
  await expect(card.getByRole("alert")).toContainText("در حال حاضر مشکلی در سرور رخ داده است");
  await expect(card.locator(".permission-preview")).toHaveCount(0);
  await capture("preview-320-error.png", 320, 720);
  expect(requests).toBe(2);

  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    route: "/admin/users", owner: "VX-G4 effective-permission preview truth", files,
  }, null, 2) + "\n");
});

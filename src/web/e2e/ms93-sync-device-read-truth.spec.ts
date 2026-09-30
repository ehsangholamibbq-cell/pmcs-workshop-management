import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";
import { projectId } from "./support";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const output = resolve(root, "src/web/artifacts/ms93-sync-device-read-truth");

test("local sync diagnostics remain distinct from current account devices and revocation", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  async function capture(name: string, width: number, height: number,
    state: "current" | "loading" | "offline" | "unavailable" | "forbidden") {
    const panel = page.getByTestId("sync-recovery-center");
    await expect(panel).toHaveAttribute("data-device-read-state", state);
    await page.evaluate(() => document.fonts.ready);
    const message = panel.locator(".device-read-message");
    await expect.poll(async () => {
      await message.evaluate(element => window.scrollTo({
        top: Math.max(0, window.scrollY + element.getBoundingClientRect().top - 360),
        behavior: "instant",
      })).catch(() => undefined);
      await page.waitForTimeout(150);
      const box = await message.boundingBox();
      return Boolean(box && box.y >= 240 && box.y + box.height <= height - 40);
    }, { timeout: 15_000 }).toBe(true);
    await expect(panel).toHaveAttribute("data-device-read-state", state);
    const bytes = await page.screenshot({ animations: "disabled", caret: "hide" });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    expect(bytes.length).toBeGreaterThan(10_000);
    expect(bytes.readUInt32BE(16)).toBe(width);
    expect(bytes.readUInt32BE(20)).toBe(height);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"),
      bytes: bytes.length, width, height });
  }

  const device = { registrationId: "93000000-0000-4000-8000-000000000001", deviceId: "remote-device-ms93",
    displayName: "دستگاه آزمایشی دیگر", platform: "Web", appVersion: "1.1", status: "Active",
    registeredAt: "2026-09-29T00:00:00Z", lastSeenAt: "2026-09-30T00:00:00Z", revision: 1 };
  let status = 200;
  let revokeCount = 0;
  let releaseFailure: () => void = () => {};
  const failureGate = new Promise<void>(resolve => { releaseFailure = resolve; });
  await page.route("**/api/pmcs/api/v1/sync/devices", async route => {
    if (status === 503) await failureGate;
    return route.fulfill(status === 200
      ? { status: 200, contentType: "application/json", body: JSON.stringify([device]) }
      : { status, contentType: "application/problem+json", body: JSON.stringify({ status, title: "Sensitive device detail" }) });
  });
  await page.route(`**/api/pmcs/api/v1/sync/devices/${device.registrationId}/revoke`, route => {
    revokeCount += 1;
    return route.fulfill({ status: 403, contentType: "application/problem+json",
      body: JSON.stringify({ status: 403, title: "Sensitive revoke detail" }) });
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/projects/${projectId}`);
  const panel = page.getByTestId("sync-recovery-center");
  await expect(panel).toHaveAttribute("data-device-read-state", "current");
  await expect(panel).toContainText("دستگاه‌های حساب با پاسخ جاری سرور تأیید شدند");
  await panel.getByText("دستگاه‌های ثبت‌شده این حساب").click();
  await expect(panel).toContainText("دستگاه آزمایشی دیگر");
  await capture("sync-device-390-current.png", 390, 844, "current");

  await page.context().setOffline(true);
  await expect(panel).toHaveAttribute("data-device-read-state", "offline");
  await expect(panel).not.toContainText("دستگاه آزمایشی دیگر");
  await expect(panel).toContainText("تعارض یا عملیات ردشده‌ای روی این دستگاه وجود ندارد");
  await page.setViewportSize({ width: 320, height: 720 });
  await capture("sync-device-320-offline-local.png", 320, 720, "offline");

  status = 503;
  await page.context().setOffline(false);
  await expect(panel).toHaveAttribute("data-device-read-state", "loading");
  await expect(panel).not.toContainText("دستگاه آزمایشی دیگر");
  await capture("sync-device-320-refreshing.png", 320, 720, "loading");
  releaseFailure();
  await expect(panel).toHaveAttribute("data-device-read-state", "unavailable");
  await expect(panel.locator(".device-read-message[role=\"alert\"]")).not.toContainText("Sensitive device detail");
  await capture("sync-device-320-read-error.png", 320, 720, "unavailable");

  status = 403;
  await panel.getByRole("button", { name: "تلاش دوباره برای دریافت دستگاه‌ها" }).click();
  await expect(panel).toHaveAttribute("data-device-read-state", "forbidden");
  await expect(panel).not.toContainText("دستگاه آزمایشی دیگر");
  await capture("sync-device-320-access-revoked.png", 320, 720, "forbidden");

  status = 200;
  await panel.getByRole("button", { name: "تلاش دوباره برای دریافت دستگاه‌ها" }).click();
  await expect(panel).toHaveAttribute("data-device-read-state", "current");
  await panel.getByText("دستگاه‌های ثبت‌شده این حساب").click();
  await expect(panel).toContainText("دستگاه آزمایشی دیگر");
  await capture("sync-device-320-restored.png", 320, 720, "current");
  await panel.getByRole("button", { name: "لغو نشست و مجوز آفلاین" }).click();
  await expect(panel).toHaveAttribute("data-device-read-state", "forbidden");
  expect(revokeCount).toBe(1);
  await capture("sync-device-320-command-revoked.png", 320, 720, "forbidden");

  writeFileSync(resolve(output, "index.json"), JSON.stringify({ contractVersion: 1,
    source: process.env.PMCS_SOURCE_HEAD_SHA ?? null, route: `/projects/${projectId}`,
    owner: "VX-G4 sync device read truth", files }, null, 2) + "\n");
});

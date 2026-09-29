import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";
import { projectId } from "./support";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const output = resolve(root, "src/web/artifacts/ms74-my-work-truth");
const actionId = "70000000-0000-4000-8000-000000000074";
const noticeId = "70000000-0000-4000-8000-000000000075";

test("work and notifications distinguish current, cached and revoked data", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  async function capture(name: string, width: number, height: number) {
    await page.getByTestId("my-work-center").scrollIntoViewIfNeeded();
    const workPanel = await page.getByTestId("my-work-panel").boundingBox();
    const notificationPanel = await page.getByTestId("notification-panel").boundingBox();
    expect(workPanel).not.toBeNull();
    expect(notificationPanel).not.toBeNull();
    if (workPanel && notificationPanel) {
      expect(workPanel.y + workPanel.height).toBeLessThanOrEqual(notificationPanel.y + 1);
      expect(workPanel.x).toBeGreaterThanOrEqual(-1);
      expect(workPanel.x + workPanel.width).toBeLessThanOrEqual(width + 1);
      expect(notificationPanel.x + notificationPanel.width).toBeLessThanOrEqual(width + 1);
    }
    await page.evaluate(() => document.fonts.ready);
    const bytes = await page.screenshot({ animations: "disabled", caret: "hide" });
    expect(bytes.readUInt32BE(16)).toBe(width);
    expect(bytes.readUInt32BE(20)).toBe(height);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"),
      bytes: bytes.length, width, height });
  }

  let responseStatus = 200;
  const work = { calculatedAt: "2026-09-29T12:00:00Z", unreadNotificationCount: 1,
    items: [{ id: actionId, kind: "ManagementAction", title: "اقدام نمونهٔ کارتابل",
      description: null, dueDate: "2026-10-01", referenceDate: null, priority: "High",
      status: "Open", targetType: "ManagementAction", targetId: actionId,
      changedAt: "2026-09-29T12:00:00Z", revision: 1, isOverdue: false }] };
  const notifications = [{ id: noticeId, projectId, category: "Action", title: "اعلان نمونهٔ پروژه",
    body: "برای اقدام نمونه", targetType: "ManagementAction", targetId: actionId,
    occurredAt: "2026-09-29T12:00:00Z", readAt: null, acknowledgedAt: null, revision: 1 }];
  for (const [suffix, data] of [["my-work", work], ["notifications", notifications]] as const) {
    await page.route(`**/api/pmcs/api/v1/projects/${projectId}/${suffix}`, route =>
      route.fulfill(responseStatus === 200
        ? { status: 200, contentType: "application/json", body: JSON.stringify(data) }
        : { status: responseStatus, contentType: "application/problem+json",
          body: JSON.stringify({ status: responseStatus, title: "Sensitive upstream error" }) }));
  }

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/projects/${projectId}`);
  const center = page.getByTestId("my-work-center");
  await expect(center).toHaveAttribute("data-read-state", "current");
  await expect(center.getByText("اقدام نمونهٔ کارتابل")).toBeVisible();
  await expect(center.getByTestId("my-work-action-done")).toBeEnabled();

  responseStatus = 503;
  await page.reload();
  await expect(center).toHaveAttribute("data-read-state", "cached");
  await expect(center.getByRole("alert")).toContainText("فقط نسخهٔ ذخیره‌شده");
  await expect(center.getByRole("alert")).not.toContainText("Sensitive upstream error");
  await expect(center.getByText("اقدام نمونهٔ کارتابل")).toBeVisible();
  await expect(center.getByTestId("my-work-action-done")).toBeDisabled();
  await expect(center.getByTestId("notification-acknowledge")).toBeDisabled();
  await capture("my-work-390-cached-error.png", 390, 844);

  responseStatus = 200;
  await center.getByRole("button", { name: "تلاش دوباره برای دریافت کارتابل" }).click();
  await expect(center).toHaveAttribute("data-read-state", "current");
  await expect(center.getByTestId("my-work-action-done")).toBeEnabled();
  await page.setViewportSize({ width: 320, height: 720 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await capture("my-work-320-current.png", 320, 720);

  responseStatus = 403;
  await page.reload();
  await expect(center).toHaveAttribute("data-read-state", "forbidden");
  await expect(center.getByRole("alert")).toContainText("دادهٔ ذخیره‌شده نمایش داده نمی‌شود");
  await expect(center.getByText("اقدام نمونهٔ کارتابل")).toHaveCount(0);
  await expect(center.getByText("اعلان نمونهٔ پروژه")).toHaveCount(0);
  await expect(center.getByTestId("my-work-panel").locator(".count-badge")).toHaveText("—");
  await capture("my-work-320-access-revoked.png", 320, 720);
  const cacheKeys = await page.evaluate(() => Object.keys(localStorage).filter(key =>
    key.startsWith("pmcs-my-work:") || key.startsWith("pmcs-notifications:")));
  expect(cacheKeys).toEqual([]);

  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    route: `/projects/${projectId}`, owner: "VX-G4 my-work state truth", files,
  }, null, 2) + "\n");
});

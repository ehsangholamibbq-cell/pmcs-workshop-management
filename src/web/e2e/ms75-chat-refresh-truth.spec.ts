import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";
import { projectId, userId } from "./support";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const output = resolve(root, "src/web/artifacts/ms75-chat-refresh-truth");
const messageId = "70000000-0000-4000-8000-000000000075";

test("project Chat clears stale room and search data during refresh, failure and revoked access", async ({ page }) => {
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

  let reads = 0;
  let releaseRefresh: () => void = () => {};
  const refreshGate = new Promise<void>(resolve => { releaseRefresh = resolve; });
  const base = `**/api/pmcs/api/v1/projects/${projectId}/collaboration`;
  await page.route(base, async route => {
    reads += 1;
    if (reads === 2) await refreshGate;
    if (reads === 2) return route.fulfill({ status: 503, contentType: "application/problem+json",
      body: JSON.stringify({ title: "Sensitive upstream error" }) });
    if (reads === 3) return route.fulfill({ status: 403 });
    return route.fulfill({ status: 200, contentType: "application/json",
      body: JSON.stringify({ projectId, lastSequence: 1 }) });
  });
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/messages\\?after=0$`, "u"),
    route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
      nextSequence: 1, messages: [{ id: messageId, projectId, sequence: 1,
        authorUserId: userId, body: "پیام خصوصی همین گروه پروژه",
        createdAt: "2026-09-29T12:00:00Z" }],
    }) }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/events\\?`, "u"),
    route => route.fulfill({ status: 503 }));
  await page.route(`${base}/unread`, route => route.fulfill({ status: 200,
    contentType: "application/json", body: JSON.stringify({ lastReadSequence: 0, unreadCount: 0 }) }));
  let searches = 0;
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/messages/search\\?`, "u"),
    route => {
      searches += 1;
      if (searches === 2) return route.fulfill({ status: 503, contentType: "application/problem+json",
        body: JSON.stringify({ title: "Sensitive search error" }) });
      return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([
        { id: messageId, projectId, sequence: 1, body: "پیام خصوصی همین گروه پروژه" },
      ]) });
    });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/projects/${projectId}/collaboration`);
  await expect(page.locator(".collaboration-message")).toHaveCount(1);
  await page.getByLabel("جست‌وجو در پیام‌های همین پروژه").fill("گروه پروژه");
  await page.getByRole("button", { name: "جست‌وجو", exact: true }).click();
  await expect(page.getByRole("region", { name: "نتیجه‌های جست‌وجوی پروژه" })).toBeVisible();
  await page.getByLabel("جست‌وجو در پیام‌های همین پروژه").fill("عبارت تازه");
  await expect(page.getByRole("region", { name: "نتیجه‌های جست‌وجوی پروژه" })).toHaveCount(0);
  await page.getByRole("button", { name: "جست‌وجو", exact: true }).click();
  const searchAlert = page.locator(".collaboration-room p[role='alert']").filter({
    hasText: "عملیات گفت‌وگوی پروژه کامل نشد",
  });
  await expect(searchAlert).toBeVisible();
  await expect(searchAlert).not.toContainText("Sensitive search error");
  await capture("chat-390-search-error.png", 390, 844);

  await page.getByRole("button", { name: "تازه‌سازی" }).click();
  await expect(page.getByText("در حال دریافت گفت‌وگوی پروژه…")).toBeVisible();
  await expect(page.getByText("پیام خصوصی همین گروه پروژه")).toBeHidden();
  await expect(page.getByLabel("پیام به گروه همین پروژه")).toBeHidden();
  await expect(page.locator('.collaboration-composer button[type="submit"]')).toBeDisabled();
  await page.setViewportSize({ width: 320, height: 720 });
  await capture("chat-320-refreshing.png", 320, 720);
  releaseRefresh();
  await expect(page.getByRole("heading", { name: "دریافت گفت‌وگو کامل نشد" })).toBeVisible();
  await expect(page.locator(".collaboration-state[role='alert']")).not.toContainText("Sensitive upstream error");
  await expect(page.locator(".collaboration-message")).toBeHidden();
  await expect(page.locator('.collaboration-composer button[type="submit"]')).toBeDisabled();
  await capture("chat-320-read-error.png", 320, 720);

  await page.getByRole("button", { name: "تلاش دوباره" }).click();
  await expect(page.getByRole("heading", { name: "دسترسی به گفت‌وگو ندارید" })).toBeVisible();
  await expect(page.getByText("پیام خصوصی همین گروه پروژه")).toHaveCount(0);
  await expect(page.getByLabel("پیام به گروه همین پروژه")).toHaveCount(0);
  await capture("chat-320-access-revoked.png", 320, 720);

  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    route: `/projects/${projectId}/collaboration`, owner: "VX-G4 project-group Chat read and search truth", files,
  }, null, 2) + "\n");
});

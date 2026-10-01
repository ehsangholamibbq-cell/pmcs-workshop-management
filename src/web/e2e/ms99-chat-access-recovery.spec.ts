import { expect, test } from "@playwright/test";
import { projectId, userId } from "./support";
import { chatEvidence } from "./ms99-chat-support";

test("automatic reconnect cannot undo explicit Chat revocation and event connection state remains truthful", async ({ page, context }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const capture = chatEvidence(page, "access");
  const messageId = "99000000-0000-4000-8000-000000000021";
  let revoked = false;
  let reads = 0;
  let eventPhase: "waiting" | "current" | "error" = "waiting";
  let release: () => void = () => {};
  const gate = new Promise<void>(resolve => { release = resolve; });
  const base = `/api/pmcs/api/v1/projects/${projectId}/collaboration`;
  await page.route(`**${base}`, route => {
    reads++;
    return route.fulfill(revoked ? { status: 403 } : { json: { projectId, lastSequence: 1 } });
  });
  await page.route(new RegExp(`${base}/messages\\?after=0$`, "u"), route => route.fulfill({ json: {
    nextSequence: 1, messages: [{ id: messageId, projectId, sequence: 1, revision: 1,
      authorUserId: userId, body: "پیام محدود به اعضای پروژه", createdAt: "2026-09-30T12:00:00Z" }],
  } }));
  await page.route(`**${base}/unread`, route => route.fulfill({ json: { lastReadSequence: 0, unreadCount: 0 } }));
  await page.route(new RegExp(`${base}/events\\?`, "u"), async route => {
    if (eventPhase === "waiting") await gate;
    if (eventPhase === "error") return route.fulfill({ status: 503 });
    await new Promise(resolve => setTimeout(resolve, 100));
    return route.fulfill({ json: { nextSequence: 1, events: [] } });
  });
  await page.goto(`/projects/${projectId}/collaboration`);
  await expect(page.getByText("در حال برقراری اتصال رویدادهای پروژه…", { exact: true })).toBeVisible();
  await capture("chat-390-events-connecting.png", page.getByText("در حال برقراری اتصال رویدادهای پروژه…", { exact: true }));
  eventPhase = "current"; release();
  await expect(page.getByText("رویدادهای پروژه در اتصال جاری بررسی شده‌اند.", { exact: true })).toBeVisible();
  await capture("chat-390-events-current.png", page.getByText("رویدادهای پروژه در اتصال جاری بررسی شده‌اند.", { exact: true }));
  eventPhase = "error";
  await page.setViewportSize({ width: 320, height: 900 });
  await expect(page.getByText("بررسی رویدادها کامل نشد", { exact: false })).toBeVisible();
  await capture("chat-320-events-retrying.png", page.getByText("بررسی رویدادها کامل نشد", { exact: false }));
  revoked = true;
  await page.getByRole("button", { name: "تازه‌سازی", exact: true }).click();
  await expect(page.getByRole("heading", { name: "دسترسی به گفت‌وگو ندارید" })).toBeVisible();
  await capture("chat-320-revoked.png", page.getByRole("heading", { name: "دسترسی به گفت‌وگو ندارید" }));
  const previousReads = reads;
  revoked = false; eventPhase = "current";
  await context.setOffline(true); await context.setOffline(false);
  await page.waitForTimeout(300);
  expect(reads).toBe(previousReads);
  await expect(page.getByText("پیام محدود به اعضای پروژه", { exact: true })).toHaveCount(0);
  await page.getByRole("button", { name: "تازه‌سازی", exact: true }).click();
  await expect(page.getByText("پیام محدود به اعضای پروژه", { exact: true })).toBeVisible();
  await capture("chat-320-explicit-recovery.png", page.getByText("پیام محدود به اعضای پروژه", { exact: true }));
});

import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";
import { projectId, userId } from "./support";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const output = resolve(root, "src/web/artifacts/ms96-chat-event-refresh-truth");

test("live project event hides the old room while retaining an unsent group draft", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  async function capture(name: string, width: number, height: number, focusText: string) {
    await page.evaluate(() => document.fonts.ready);
    const focus = page.getByText(focusText, { exact: true });
    await expect.poll(async () => {
      await focus.evaluate(element => window.scrollTo({
        top: Math.max(0, window.scrollY + element.getBoundingClientRect().top - 240), behavior: "instant",
      })).catch(() => undefined);
      await page.waitForTimeout(150);
      const box = await focus.boundingBox();
      return Boolean(box && box.y >= 130 && box.y + box.height <= height - 30);
    }, { timeout: 15_000 }).toBe(true);
    let bytes: Buffer | null = null;
    for (let attempt = 0; attempt < 10; attempt += 1) {
      await page.waitForTimeout(150);
      const candidate = await page.screenshot({ animations: "disabled", caret: "hide" });
      if (candidate.length > 10_000) { bytes = candidate; break; }
    }
    if (!bytes) throw new Error(`Chat state did not paint during ${name}`);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    expect(bytes.readUInt32BE(16)).toBe(width);
    expect(bytes.readUInt32BE(20)).toBe(height);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"),
      bytes: bytes.length, width, height });
  }

  let reads = 0;
  let lastSequence = 1;
  let releaseEvent: () => void = () => {};
  let releaseRead: () => void = () => {};
  const eventGate = new Promise<void>(resolve => { releaseEvent = resolve; });
  const readGate = new Promise<void>(resolve => { releaseRead = resolve; });
  const base = `**/api/pmcs/api/v1/projects/${projectId}/collaboration`;
  await page.route(base, async route => {
    reads += 1;
    if (reads === 2) await readGate;
    return route.fulfill({ status: 200, contentType: "application/json",
      body: JSON.stringify({ projectId, lastSequence }) });
  });
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/messages\\?after=0$`, "u"),
    route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
      nextSequence: lastSequence, messages: [{ id: lastSequence === 1
        ? "95000000-0000-4000-8000-000000000001" : "95000000-0000-4000-8000-000000000002",
      projectId, sequence: lastSequence, authorUserId: userId,
      body: lastSequence === 1 ? "پیام قدیمی گروه پروژه" : "پیام تازهٔ گروه پروژه",
      createdAt: "2026-09-29T12:00:00Z" }],
    }) }));
  let eventSent = false;
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/events\\?`, "u"), async route => {
    if (eventSent) return route.fulfill({ status: 503 });
    await eventGate;
    eventSent = true;
    lastSequence = 2;
    return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
      nextSequence: 2, events: [{ messageId: "95000000-0000-4000-8000-000000000002",
        projectId, sequence: 2, createdAt: "2026-09-29T12:01:00Z" }],
    }) });
  });
  await page.route(`${base}/unread`, route => route.fulfill({ status: 200,
    contentType: "application/json", body: JSON.stringify({ lastReadSequence: 0, unreadCount: 0 }) }));

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/projects/${projectId}/collaboration`);
  const room = page.getByRole("region", { name: "پیام‌های اخیر پروژه" });
  await expect(page.getByText("پیام قدیمی گروه پروژه")).toBeVisible();
  const composer = page.getByLabel("پیام به گروه همین پروژه");
  await composer.fill("پیش‌نویس ارسال‌نشدهٔ گروه");
  await capture("chat-390-current.png", 390, 844, "پیام قدیمی گروه پروژه");

  releaseEvent();
  await expect.poll(() => reads).toBe(2);
  await expect(page.getByText("در حال دریافت گفت‌وگوی پروژه…")).toBeVisible();
  await expect(room).toBeHidden();
  await expect(page.getByText("پیام قدیمی گروه پروژه")).toBeHidden();
  await expect(composer).toHaveValue("پیش‌نویس ارسال‌نشدهٔ گروه");
  await page.setViewportSize({ width: 320, height: 720 });
  await capture("chat-320-event-refreshing.png", 320, 720, "در حال دریافت گفت‌وگوی پروژه…");

  releaseRead();
  await expect(page.getByText("پیام تازهٔ گروه پروژه")).toBeVisible();
  await expect(page.getByText("پیام قدیمی گروه پروژه")).toHaveCount(0);
  await expect(room).toBeVisible();
  await expect(composer).toHaveValue("پیش‌نویس ارسال‌نشدهٔ گروه");
  await capture("chat-320-restored-draft.png", 320, 720, "پیام تازهٔ گروه پروژه");

  writeFileSync(resolve(output, "index.json"), JSON.stringify({ contractVersion: 1,
    source: process.env.PMCS_SOURCE_HEAD_SHA ?? null, route: `/projects/${projectId}/collaboration`,
    owner: "VX-G4 project-group Chat event refresh truth", files }, null, 2) + "\n");
});

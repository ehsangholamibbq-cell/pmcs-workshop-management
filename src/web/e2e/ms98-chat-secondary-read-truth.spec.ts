import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";
import { projectId, userId } from "./support";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const output = resolve(root, "src/web/artifacts/ms98-chat-secondary-read-truth");
const messageId = "98000000-0000-4000-8000-000000000001";
const oldDocumentId = "98000000-0000-4000-8000-000000000002";
const newDocumentId = "98000000-0000-4000-8000-000000000003";

test("group message reactions and attachments discard stale reads across event, failure and access loss", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  async function capture(name: string, width: number, height: number, focusText: string) {
    await page.evaluate(() => document.fonts.ready);
    const focus = page.getByText(focusText, { exact: false }).first();
    await expect.poll(async () => {
      await focus.evaluate(element => window.scrollTo({ top: Math.max(0,
        window.scrollY + element.getBoundingClientRect().top - 230), behavior: "instant" })).catch(() => undefined);
      await page.waitForTimeout(150);
      const box = await focus.boundingBox();
      return Boolean(box && box.y >= 120 && box.y + box.height <= height - 25);
    }, { timeout: 15_000 }).toBe(true);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    const bytes = await page.screenshot({ animations: "disabled", caret: "hide" });
    expect(bytes.readUInt32BE(16)).toBe(width);
    expect(bytes.readUInt32BE(20)).toBe(height);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"),
      bytes: bytes.length, width, height });
  }

  let sequence = 1;
  let phase: "old" | "error" | "new" = "old";
  let revoked = false;
  let reads = 0;
  let releaseEvent: () => void = () => {};
  let releaseRead: () => void = () => {};
  const eventGate = new Promise<void>(resolveEvent => { releaseEvent = resolveEvent; });
  const readGate = new Promise<void>(resolveRead => { releaseRead = resolveRead; });
  const base = `**/api/pmcs/api/v1/projects/${projectId}/collaboration`;
  await page.route(base, async route => {
    reads += 1;
    if (reads === 2) await readGate;
    return route.fulfill(revoked ? { status: 403 } : {
      status: 200, contentType: "application/json", body: JSON.stringify({ projectId, lastSequence: sequence }),
    });
  });
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/messages\\?after=0$`, "u"),
    route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
      nextSequence: sequence, messages: [{ id: messageId, projectId, sequence: 1,
        authorUserId: userId, body: "پیام گروه با واکنش و پیوست", createdAt: "2026-09-29T12:00:00Z" }],
    }) }));
  let eventSent = false;
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/events\\?`, "u"), async route => {
    if (eventSent) return route.fulfill({ status: 503 });
    await eventGate;
    eventSent = true;
    sequence = 2;
    phase = "error";
    return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
      nextSequence: 2, events: [{ messageId, projectId, sequence: 2, createdAt: "2026-09-29T12:01:00Z" }],
    }) });
  });
  await page.route(`${base}/unread`, route => route.fulfill({ status: 200,
    contentType: "application/json", body: JSON.stringify({ lastReadSequence: 0, unreadCount: 0 }) }));
  const reactions = `**/api/pmcs/api/v1/projects/${projectId}/collaboration/messages/${messageId}/reactions`;
  await page.route(reactions, route => route.fulfill(phase === "error" ? { status: 503 } : {
    status: 200, contentType: "application/json", body: JSON.stringify({ canReact: false,
      reactions: phase === "old" ? [{ emoji: "👍", count: 1, reactedByMe: false }]
        : [{ emoji: "✅", count: 2, reactedByMe: false }] }),
  }));
  const attachmentPath = `/api/v1/projects/${projectId}/collaboration/messages/${messageId}/attachments`;
  await page.route(`**/api/pmcs${attachmentPath}`, route => {
    if (phase === "error") return route.fulfill({ status: 503 });
    const id = phase === "old" ? oldDocumentId : newDocumentId;
    const name = phase === "old" ? "old-project.pdf" : "current-project.pdf";
    return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([{
      messageId, documentId: id, originalFileName: name, contentType: "application/pdf",
      sizeBytes: 12, sha256: "a".repeat(64), classification: "Internal", retentionPolicy: "Standard",
      legalHold: false, releasedAt: "2026-09-29T12:00:00Z", versionNumber: 1,
      contentUrl: `${attachmentPath}/${id}/content`,
    }]) });
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/projects/${projectId}/collaboration`);
  await expect(page.getByText("پیام گروه با واکنش و پیوست")).toBeVisible();
  await page.getByRole("button", { name: "واکنش‌ها" }).click();
  await page.getByRole("button", { name: "پیوست‌ها" }).click();
  await expect(page.getByRole("button", { name: /👍، ۱ واکنش/u })).toBeVisible();
  await expect(page.getByText("old-project.pdf", { exact: false })).toBeVisible();
  await capture("chat-390-old-secondary.png", 390, 844, "old-project.pdf");

  releaseEvent();
  await expect.poll(() => reads).toBe(2);
  await expect(page.getByText("در حال دریافت گفت‌وگوی پروژه…")).toBeVisible();
  await expect(page.getByText("old-project.pdf", { exact: false })).toBeHidden();
  releaseRead();
  await expect(page.getByText("پیام گروه با واکنش و پیوست")).toBeVisible();
  await expect(page.getByRole("button", { name: "واکنش‌ها" })).toHaveAttribute("aria-expanded", "false");
  await page.setViewportSize({ width: 320, height: 720 });
  await page.getByRole("button", { name: "واکنش‌ها" }).click();
  await page.getByRole("button", { name: "پیوست‌ها" }).click();
  await expect(page.getByText("دریافت واکنش‌های پیام کامل نشد")).toBeVisible();
  await expect(page.getByText("دریافت پیوست‌های پیام کامل نشد")).toBeVisible();
  await expect(page.getByText("old-project.pdf", { exact: false })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /👍، ۱ واکنش/u })).toHaveCount(0);
  await capture("chat-320-secondary-error.png", 320, 720, "دریافت پیوست‌های پیام کامل نشد");

  phase = "new";
  await page.getByRole("button", { name: "واکنش‌ها" }).click();
  await page.getByRole("button", { name: "پیوست‌ها" }).click();
  await page.getByRole("button", { name: "واکنش‌ها" }).click();
  await page.getByRole("button", { name: "پیوست‌ها" }).click();
  await expect(page.getByRole("button", { name: /✅، ۲ واکنش/u })).toBeVisible();
  await expect(page.getByText("current-project.pdf", { exact: false })).toBeVisible();
  await capture("chat-320-secondary-current.png", 320, 720, "current-project.pdf");

  revoked = true;
  await page.getByRole("button", { name: "تازه‌سازی" }).click();
  await expect(page.getByRole("heading", { name: "دسترسی به گفت‌وگو ندارید" })).toBeVisible();
  await expect(page.getByText("current-project.pdf", { exact: false })).toHaveCount(0);
  await capture("chat-320-secondary-revoked.png", 320, 720, "دسترسی به گفت‌وگو ندارید");

  writeFileSync(resolve(output, "index.json"), JSON.stringify({ contractVersion: 1,
    source: process.env.PMCS_SOURCE_HEAD_SHA ?? null, route: `/projects/${projectId}/collaboration`,
    owner: "VX-G4 Chat secondary read truth", files }, null, 2) + "\n");
});

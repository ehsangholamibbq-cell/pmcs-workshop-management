import { expect, test } from "@playwright/test";
import { projectId, projectPath, tenantId, userId } from "./support";
import { evidence } from "./vx-g5-support";

test.use({ serviceWorkers: "block" });

test("leaving Chat while file hashing is pending cannot create a late local upload", async ({ page }, testInfo) => {
  const report = evidence(testInfo, "navigation-queue");
  const messageId = "a5000000-0000-4000-8000-000000000040";
  const base = `/api/pmcs/api/v1/projects/${projectId}/collaboration`;
  await page.route(`**${base}`, route => route.fulfill({ json: { projectId, lastSequence: 1, canUpload: true } }));
  await page.route(new RegExp(`${base}/messages\\?after=0$`, "u"), route => route.fulfill({ json: {
    nextSequence: 1, messages: [{ id: messageId, projectId, sequence: 1, revision: 1,
      authorUserId: userId, body: "فایل در انتظار محاسبهٔ هش", createdAt: "2026-09-30T12:00:00Z" }],
  } }));
  await page.route(new RegExp(`${base}/events\\?`, "u"), route => route.fulfill({ status: 503 }));
  await page.route(`**${base}/unread`, route => route.fulfill({ json: { lastReadSequence: 0, unreadCount: 0 } }));
  await page.route(`**${base}/messages/${messageId}/attachments`, route => route.fulfill({ json: [] }));
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(`${projectPath}/collaboration`);
  await page.getByRole("button", { name: "پیوست‌ها", exact: true }).click();
  await expect(page.getByLabel("افزودن فایل به پیام خود")).toBeVisible();
  await page.evaluate(() => {
    const state = { started: false, opened: false, released: false, release: () => {} };
    (window as unknown as { g5PendingHash: typeof state }).g5PendingHash = state;
    const originalDigest = crypto.subtle.digest.bind(crypto.subtle);
    Object.defineProperty(crypto.subtle, "digest", { configurable: true,
      value: async (algorithm: AlgorithmIdentifier, data: BufferSource) => {
        if (new TextDecoder().decode(data) !== "%PDF-1.7\n%%EOF") return originalDigest(algorithm, data);
        const result = await originalDigest(algorithm, data);
        state.started = true;
        await new Promise<void>(resolve => { state.release = resolve; });
        return result;
      } });
    const originalOpen = IDBFactory.prototype.open;
    IDBFactory.prototype.open = function (...args: Parameters<IDBFactory["open"]>) {
      const request = originalOpen.apply(this, args);
      if (state.released && args[0].startsWith("pmcs-field-v3-")) request.addEventListener("success", () => { state.opened = true; });
      return request;
    };
  });
  await page.getByLabel("افزودن فایل به پیام خود").setInputFiles({ name: "late-navigation.pdf",
    mimeType: "application/pdf", buffer: Buffer.from("%PDF-1.7\n%%EOF") });
  await page.getByRole("button", { name: "نگهداری فایل در صف دستگاه", exact: true }).click();
  await expect.poll(() => page.evaluate(() => (window as unknown as { g5PendingHash: { started: boolean } }).g5PendingHash.started)).toBe(true);
  await page.getByRole("link", { name: "مرکز فرمان پروژه", exact: true }).click();
  await expect(page).toHaveURL(projectPath);
  await expect(page.getByRole("heading", { name: "مرکز فرمان پروژه", exact: true })).toBeVisible();
  await expect(page.locator(".project-print-shell")).toHaveAttribute("data-command-read-state", "current");
  await expect.poll(() => page.locator('#fact-location option').count()).toBeGreaterThan(1);
  await page.evaluate(() => {
    const state = (window as unknown as { g5PendingHash: { released: boolean; opened: boolean; release: () => void } }).g5PendingHash;
    state.opened = false; state.released = true; state.release();
  });
  await expect.poll(() => page.evaluate(() => (window as unknown as { g5PendingHash: { opened: boolean } }).g5PendingHash.opened)).toBe(true);
  const count = await page.evaluate(async ({ tenantId, userId }) => new Promise<number>((resolve, reject) => {
    const request = indexedDB.open(`pmcs-field-v3-${tenantId}-${userId}`);
    request.onerror = () => reject(request.error);
    request.onsuccess = () => {
      const database = request.result;
      const items = database.transaction("document-uploads", "readonly").objectStore("document-uploads").getAll();
      items.onerror = () => reject(items.error);
      items.onsuccess = () => { database.close(); resolve(items.result.filter(item => item.originalFileName === "late-navigation.pdf").length); };
    };
  }), { tenantId, userId });
  report.record("lateLocalUploadsAfterNavigation", count);
  expect(count).toBe(0);
  await report.capture(page, "no-late-upload-1440.png");
});

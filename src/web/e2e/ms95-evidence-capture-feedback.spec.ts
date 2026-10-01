import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";
import { projectId, tenantId, userId, waitForProjectReady } from "./support";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const output = resolve(root, "src/web/artifacts/ms95-evidence-capture-feedback");

test("evidence preserves a failed selection and queues once with explicit local status", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  const evidence = page.getByTestId("evidence-capture");
  const input = evidence.getByLabel("عکس یا سند پی‌دی‌اف", { exact: true });
  const save = evidence.getByRole("button", { name: "ذخیره مدرک روی این دستگاه", exact: true });
  async function capture(name: string, width: number, height: number) {
    await page.evaluate(() => document.fonts.ready);
    await evidence.scrollIntoViewIfNeeded();
    await evidence.evaluate(element => window.scrollTo({
      top: Math.max(0, window.scrollY + element.getBoundingClientRect().top - 170), behavior: "instant",
    }));
    await expect.poll(async () => {
      const box = await evidence.boundingBox();
      return Boolean(box && box.y >= 130 && box.y + box.height < height - 15);
    }).toBe(true);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    const controlBox = await evidence.locator(".pmcs-file-input-control").boundingBox();
    const nameBox = await evidence.locator(".pmcs-file-input-name").boundingBox();
    expect(controlBox?.width).toBeGreaterThan(180);
    expect(nameBox?.height).toBeLessThan(80);
    const bytes = await page.screenshot({ animations: "disabled", caret: "hide" });
    expect(bytes.readUInt32BE(16)).toBe(width);
    expect(bytes.readUInt32BE(20)).toBe(height);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"),
      bytes: bytes.length, width, height });
  }
  const readAttachments = () => page.evaluate(async ({ tenantId, userId }) => {
    return new Promise<Array<{ originalFileName: string; sha256: string; status: string; dailyFactId: string }>>((resolve, reject) => {
      const request = indexedDB.open(`pmcs-field-v3-${tenantId}-${userId}`);
      request.onerror = () => reject(request.error);
      request.onsuccess = () => {
        const database = request.result;
        const transaction = database.transaction("attachments", "readonly");
        const all = transaction.objectStore("attachments").getAll();
        all.onsuccess = () => resolve(all.result);
        all.onerror = () => reject(all.error);
        transaction.oncomplete = () => database.close();
      };
    });
  }, { tenantId, userId });
  const setLeaseExpiry = (expiry: string) => page.evaluate(async ({ tenantId, userId, projectId, expiry }) => {
    return new Promise<string>((resolve, reject) => {
      const request = indexedDB.open(`pmcs-field-v3-${tenantId}-${userId}`);
      request.onerror = () => reject(request.error);
      request.onsuccess = () => {
        const database = request.result;
        const transaction = database.transaction("sync-metadata", "readwrite");
        const store = transaction.objectStore("sync-metadata");
        const read = store.get(`manifest:${projectId}`);
        let previous = "";
        read.onsuccess = () => {
          previous = read.result.leaseExpiresAt as string;
          store.put({ ...read.result, leaseExpiresAt: expiry });
        };
        transaction.oncomplete = () => { database.close(); resolve(previous); };
        transaction.onerror = () => { database.close(); reject(transaction.error); };
      };
    });
  }, { tenantId, userId, projectId, expiry });

  await page.setViewportSize({ width: 390, height: 844 });
  await waitForProjectReady(page);
  await expect(page.getByTestId("reality-capture-form")).toHaveAttribute("data-location-read-state", "current");
  await page.context().setOffline(true);
  await expect(page.getByTestId("reality-capture-form")).toHaveAttribute("data-location-read-state", "cached");
  await page.evaluate(() => {
    for (const key of Object.keys(localStorage)) if (key.includes("pmcs-daily-report:")) localStorage.removeItem(key);
  });
  await expect(save).toBeDisabled();
  const png = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=", "base64");
  await input.setInputFiles({ name: "مدرک-محلی.png", mimeType: "image/png", buffer: png });
  await expect(evidence.locator(".pmcs-file-input-name")).toHaveText("مدرک-محلی.png");
  await capture("evidence-390-selected.png", 390, 844);
  await save.click();
  await expect(evidence.getByRole("alert")).toContainText("ابتدا حداقل یک واقعیت");
  await expect(evidence.locator(".pmcs-file-input-name")).toHaveText("مدرک-محلی.png");
  expect(await readAttachments()).toHaveLength(0);
  await page.setViewportSize({ width: 320, height: 720 });
  await capture("evidence-320-no-report.png", 320, 720);

  const fact = page.getByTestId("reality-capture-form");
  await fact.locator("#fact-location").selectOption({ index: 1 });
  await fact.locator("#fact-category").fill("فعالیت آزمون مدرک");
  await fact.locator("#fact-quantity").fill("1");
  await fact.locator("#fact-unit").fill("متر");
  await fact.locator("#fact-description").fill("واقعیت محلی برای آزمون حفظ مدرک");
  await fact.getByRole("button", { name: "ذخیره پیش‌نویس آفلاین" }).click();
  await expect(fact.locator("output")).toContainText("روی این دستگاه ذخیره شد");

  await input.setInputFiles({ name: "نامعتبر.txt", mimeType: "text/plain", buffer: Buffer.from("invalid") });
  await save.click();
  await expect(evidence.getByRole("alert")).toContainText("فقط تصویر");
  await expect(evidence.locator(".pmcs-file-input-name")).toHaveText("نامعتبر.txt");
  expect(await readAttachments()).toHaveLength(0);
  await capture("evidence-320-invalid-file.png", 320, 720);

  await input.setInputFiles({ name: "مدرک-محلی.png", mimeType: "image/png", buffer: png });
  const leaseExpiry = await setLeaseExpiry("2000-01-01T00:00:00Z");
  await save.click();
  await expect(evidence.getByRole("alert")).toContainText("مجوز آفلاین این پروژه منقضی");
  await expect(evidence.locator(".pmcs-file-input-name")).toHaveText("مدرک-محلی.png");
  expect(await readAttachments()).toHaveLength(0);
  await capture("evidence-320-expired-lease.png", 320, 720);
  await setLeaseExpiry(leaseExpiry);

  await page.evaluate(() => {
    const original = crypto.subtle.digest.bind(crypto.subtle);
    let release: () => void = () => undefined;
    const gate = new Promise<void>(resolve => { release = resolve; });
    (window as typeof window & { releaseEvidenceDigest: () => void }).releaseEvidenceDigest = release;
    Object.defineProperty(crypto.subtle, "digest", { configurable: true,
      value: async (algorithm: AlgorithmIdentifier, data: BufferSource) => {
        await gate;
        return original(algorithm, data);
      } });
  });
  await save.click();
  await expect(evidence).toHaveAttribute("aria-busy", "true");
  await expect(input).toBeDisabled();
  await expect(evidence.getByRole("button", { name: "حذف انتخاب" })).toBeDisabled();
  await evidence.dispatchEvent("submit");
  await capture("evidence-320-saving.png", 320, 720);
  await page.evaluate(() => (window as typeof window & { releaseEvidenceDigest: () => void }).releaseEvidenceDigest());
  await expect(evidence).toHaveAttribute("aria-busy", "false");
  await expect(evidence.locator(".evidence-feedback[role='status']"))
    .toContainText("تا پذیرش سرور رسمی نیست");
  await expect(evidence.locator(".pmcs-file-input-name")).toHaveText("فایلی انتخاب نشده");
  await expect(save).toBeDisabled();
  const attachments = await readAttachments();
  expect(attachments).toHaveLength(1);
  expect(attachments[0]).toMatchObject({ originalFileName: "مدرک-محلی.png", status: "queued",
    sha256: createHash("sha256").update(png).digest("hex") });
  expect(attachments[0].dailyFactId).toMatch(/^[a-f0-9-]{36}$/u);
  await capture("evidence-320-locally-queued.png", 320, 720);

  writeFileSync(resolve(output, "index.json"), JSON.stringify({ contractVersion: 1,
    source: process.env.PMCS_SOURCE_HEAD_SHA ?? null, route: `/projects/${projectId}`,
    owner: "VX-G4 evidence local feedback and retained selection", files }, null, 2) + "\n");
});

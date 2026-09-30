import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";
import { projectId, tenantId, userId, waitForProjectReady } from "./support";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const output = resolve(root, "src/web/artifacts/ms97-local-evidence-access-truth");
const sample = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL/nwAAAABJRU5ErkJggg==", "base64");

test("local evidence stays provisional and revoked project access stops new attachments", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  const evidence = page.getByTestId("evidence-capture");
  const input = evidence.getByLabel("عکس یا سند پی‌دی‌اف", { exact: true });
  const save = evidence.getByRole("button", { name: "ذخیره مدرک روی این دستگاه", exact: true });
  async function capture(name: string, width: number, height: number, state: string) {
    await expect(evidence).toHaveAttribute("data-project-read-state", state);
    await page.evaluate(() => document.fonts.ready);
    await expect.poll(async () => {
      await evidence.evaluate(element => window.scrollTo({
        top: Math.max(0, window.scrollY + element.getBoundingClientRect().top - 260), behavior: "instant",
      })).catch(() => undefined);
      await page.waitForTimeout(150);
      const box = await evidence.boundingBox();
      return Boolean(box && box.y >= 140 && box.y + box.height <= height - 20);
    }, { timeout: 15_000 }).toBe(true);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    const bytes = await page.screenshot({ animations: "disabled", caret: "hide" });
    expect(bytes.readUInt32BE(16)).toBe(width);
    expect(bytes.readUInt32BE(20)).toBe(height);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"),
      bytes: bytes.length, width, height });
  }
  async function attachmentCount() {
    return page.evaluate(async ({ tenant, user }) => new Promise<number>((resolveCount, reject) => {
      const request = indexedDB.open(`pmcs-field-v3-${tenant}-${user}`);
      request.onerror = () => reject(request.error ?? new Error("IndexedDB unavailable"));
      request.onsuccess = () => {
        const db = request.result;
        if (!db.objectStoreNames.contains("attachments")) { db.close(); resolveCount(0); return; }
        const transaction = db.transaction("attachments", "readonly");
        const count = transaction.objectStore("attachments").count();
        count.onerror = () => reject(count.error ?? new Error("Attachment read failed"));
        count.onsuccess = () => { db.close(); resolveCount(count.result); };
      };
    }), { tenant: tenantId, user: userId });
  }
  async function attach(name: string) {
    await input.setInputFiles({ name, mimeType: "image/png", buffer: sample });
    await save.click();
    await expect.poll(attachmentCount).toBeGreaterThan(0);
  }

  let readStatus = 200;
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/command-center`, async route => {
    if (readStatus === 403) return route.fulfill({ status: 403, contentType: "application/problem+json",
      body: JSON.stringify({ status: 403, title: "Sensitive project detail" }) });
    return route.continue();
  });
  await page.setViewportSize({ width: 390, height: 844 });
  await waitForProjectReady(page);
  await expect(evidence).toHaveAttribute("data-project-read-state", "current");
  const form = page.getByTestId("reality-capture-form");
  const selectedLocation = await form.locator("#fact-location option").nth(1).getAttribute("value");
  if (!selectedLocation) throw new Error("No project location available");
  await form.locator("#fact-location").selectOption(selectedLocation);
  await form.locator("#fact-category").fill("مدرک دسترسی محلی");
  await form.locator("#fact-quantity").fill("1");
  await form.locator("#fact-unit").fill("عدد");
  await form.locator("#fact-description").fill(`مشاهده مدرک ${Date.now()}`);
  await form.getByRole("button", { name: "ذخیره پیش‌نویس آفلاین" }).click();
  await expect(form.locator("output")).toContainText("روی این دستگاه ذخیره شد");
  await attach("current-proof.png");
  await expect.poll(attachmentCount).toBe(1);
  await capture("evidence-390-current.png", 390, 844, "current");

  await page.context().setOffline(true);
  await expect(evidence).toHaveAttribute("data-project-read-state", "cached");
  await expect(evidence.locator(".field-help[role='status']")).toContainText("پیش‌نویس محلی");
  await page.setViewportSize({ width: 320, height: 720 });
  await attach("offline-proof.png");
  await expect.poll(attachmentCount).toBe(2);
  await capture("evidence-320-cached.png", 320, 720, "cached");

  readStatus = 403;
  await page.context().setOffline(false);
  await expect(evidence).toHaveAttribute("data-project-read-state", "forbidden");
  await expect(input).toBeDisabled();
  await expect(evidence.getByRole("button", { name: "ذخیره مدرک غیرفعال است" })).toBeDisabled();
  await expect(evidence.getByRole("alert")).toContainText("مدرک تازه روی این دستگاه صف نمی‌شود");
  expect(await attachmentCount()).toBe(2);
  await capture("evidence-320-access-revoked.png", 320, 720, "forbidden");

  readStatus = 200;
  await page.reload();
  await expect(evidence).toHaveAttribute("data-project-read-state", "current");
  await expect(input).toBeEnabled();
  await attach("restored-proof.png");
  await expect.poll(attachmentCount).toBe(3);
  await capture("evidence-320-restored.png", 320, 720, "current");

  writeFileSync(resolve(output, "index.json"), JSON.stringify({ contractVersion: 1,
    source: process.env.PMCS_SOURCE_HEAD_SHA ?? null, route: `/projects/${projectId}`,
    owner: "VX-G4 local evidence access truth", files }, null, 2) + "\n");
});

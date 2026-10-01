import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";
import { projectId, readStoredOperations } from "./support";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const output = resolve(root, "src/web/artifacts/ms94-reality-capture-location-truth");

test("local fact keeps its draft but never queues a retired or unverified location", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  const form = page.getByTestId("reality-capture-form");
  async function capture(name: string, width: number, height: number, state: string) {
    await expect(form).toHaveAttribute("data-location-read-state", state);
    await page.evaluate(() => document.fonts.ready);
    const location = form.locator("#fact-location");
    let bytes: Buffer = Buffer.alloc(0);
    await expect.poll(async () => {
      await location.evaluate(element => window.scrollTo({
        top: Math.max(0, window.scrollY + element.getBoundingClientRect().top - 300), behavior: "instant",
      })).catch(() => undefined);
      await page.waitForTimeout(200);
      const box = await location.boundingBox();
      if (!box || box.y < 120 || box.y + box.height > height - 40 ||
        await form.getAttribute("data-location-read-state") !== state) return 0;
      bytes = await page.screenshot({ animations: "disabled", caret: "hide" });
      return bytes.length;
    }, { timeout: 15_000 }).toBeGreaterThan(10_000);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    expect(bytes.readUInt32BE(16)).toBe(width);
    expect(bytes.readUInt32BE(20)).toBe(height);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"),
      bytes: bytes.length, width, height });
  }

  const rootLocation = { id: "71000000-0000-4000-8000-000000000094", projectId, code: "ROOT",
    name: "ریشه پروژه", parentLocationId: null, status: "Active", revision: 1 };
  const site = { id: "72000000-0000-4000-8000-000000000094", projectId, code: "SITE",
    name: "کارگاه", parentLocationId: rootLocation.id, status: "Active", revision: 1 };
  let locations = [rootLocation, site];
  let status = 200;
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/locations`, route => route.fulfill(status === 200
    ? { status: 200, contentType: "application/json", body: JSON.stringify(locations) }
    : { status, contentType: "application/problem+json", body: JSON.stringify({ status, title: "Sensitive location detail" }) }));

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/projects/${projectId}`);
  await expect(form).toHaveAttribute("data-location-read-state", "current");
  await form.locator("#fact-location").selectOption(site.id);
  await form.locator("#fact-category").fill("فعالیت آزمون محل");
  await form.locator("#fact-quantity").fill("1");
  await form.locator("#fact-unit").fill("متر");
  const description = `مشاهده محل آزمون ${Date.now()}`;
  await form.locator("#fact-description").fill(description);
  await capture("capture-390-current.png", 390, 844, "current");

  await page.context().setOffline(true);
  await expect(form).toHaveAttribute("data-location-read-state", "cached");
  await expect(form.locator(".field-help[role='status']").first()).toContainText("نسخهٔ ذخیره‌شده");
  await page.setViewportSize({ width: 320, height: 720 });
  await capture("capture-320-cached.png", 320, 720, "cached");

  locations = [rootLocation];
  await page.context().setOffline(false);
  await expect(form).toHaveAttribute("data-location-read-state", "current");
  await expect(form.getByRole("alert")).toContainText("محل قبلی در فهرست فعال فعلی نیست");
  await expect(form.locator("#fact-description")).toHaveValue(description);
  await form.getByRole("button", { name: "ذخیره پیش‌نویس آفلاین" }).click();
  await expect(form.locator("output")).toContainText("دوباره انتخاب کنید");
  expect((await readStoredOperations(page)).filter(operation => operation.description === description)).toHaveLength(0);
  await capture("capture-320-stale-location.png", 320, 720, "current");

  status = 403;
  await page.context().setOffline(true);
  await expect(form).toHaveAttribute("data-location-read-state", "cached");
  await page.context().setOffline(false);
  await expect(form).toHaveAttribute("data-location-read-state", "forbidden");
  await expect(form.locator("#fact-description")).toHaveValue(description);
  await form.getByRole("button", { name: "ذخیره پیش‌نویس آفلاین" }).click();
  await expect(form.locator("output")).toContainText("فهرست محل‌های مجاز در دسترس نیست");
  expect((await readStoredOperations(page)).filter(operation => operation.description === description)).toHaveLength(0);
  await capture("capture-320-access-revoked.png", 320, 720, "forbidden");

  status = 200;
  await page.getByTestId("project-location-settings")
    .getByRole("button", { name: "تلاش دوباره برای دریافت مکان‌ها" }).click();
  await expect(form).toHaveAttribute("data-location-read-state", "current");
  await form.locator("#fact-location").selectOption(rootLocation.id);
  await form.getByRole("button", { name: "ذخیره پیش‌نویس آفلاین" }).click();
  await expect(form.locator("output")).toContainText("روی این دستگاه ذخیره شد");
  await expect.poll(async () => (await readStoredOperations(page))
    .filter(operation => operation.description === description).length).toBe(1);
  await capture("capture-320-reselected.png", 320, 720, "current");

  writeFileSync(resolve(output, "index.json"), JSON.stringify({ contractVersion: 1,
    source: process.env.PMCS_SOURCE_HEAD_SHA ?? null, route: `/projects/${projectId}`,
    owner: "VX-G4 reality capture location truth", files }, null, 2) + "\n");
});

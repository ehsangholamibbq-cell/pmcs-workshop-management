import { createHash } from "node:crypto";
import { expect, test } from "@playwright/test";

const fontPath = "/typography/Vazirmatn-variable.woff2";
const fontSha256 = "4e3fa217d38fdafc1fea4414ceb58ca5e662cf0ab5fa735a8c8c20e8b42cad92";
const cacheName = "pmcs-public-shell-font-v2.0.0";

test("MS48 versioned Vazirmatn loads in the active UI, print and offline shell", async ({ page, context }) => {
  await page.goto("/portfolio");
  await expect(page.locator("body")).toHaveCSS("font-family", /Vazirmatn/u);
  const font = await page.request.get(fontPath);
  expect(font.status()).toBe(200);
  expect(createHash("sha256").update(await font.body()).digest("hex")).toBe(fontSha256);

  await page.evaluate(async () => {
    await document.fonts.load('400 16px "Vazirmatn"');
    await navigator.serviceWorker.ready;
  });
  expect(await page.evaluate(() => document.fonts.check('400 16px "Vazirmatn"'))).toBe(true);
  expect(await page.evaluate(async (name) => {
    if (!(await caches.keys()).includes(name)) return false;
    return Boolean(await (await caches.open(name)).match("/offline.html")) &&
      Boolean(await (await caches.open(name)).match("/typography/pmcs-fonts.css")) &&
      Boolean(await (await caches.open(name)).match("/typography/Vazirmatn-variable.woff2"));
  }, cacheName)).toBe(true);

  await page.emulateMedia({ media: "print" });
  await expect(page.locator("body")).toHaveCSS("font-family", /Vazirmatn/u);
  await page.emulateMedia({ media: "screen" });

  try {
    await context.setOffline(true);
    await page.goto("/portfolio?font-offline=1");
    await expect(page.getByRole("heading", { name: "اتصال به سامانه برقرار نیست" })).toBeVisible();
    await expect(page.locator("body")).toHaveCSS("font-family", /Vazirmatn/u);
    expect(await page.evaluate(() => document.fonts.check('400 16px "Vazirmatn"'))).toBe(true);
  } finally {
    await context.setOffline(false);
  }
});

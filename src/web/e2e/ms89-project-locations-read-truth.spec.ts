import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { expect, test } from "@playwright/test";
import { projectId } from "./support";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const output = resolve(root, "src/web/artifacts/ms89-project-locations-read-truth");

test("location cache is labeled local and cannot authorize create or retire", async ({ page }) => {
  test.setTimeout(180_000);
  mkdirSync(output, { recursive: true });
  const files: Array<{ name: string; sha256: string; bytes: number; width: number; height: number }> = [];
  const panel = page.getByTestId("project-location-settings");
  async function capture(name: string, width: number, height: number,
    state: "current" | "loading" | "cached" | "forbidden", showCommandFeedback = false) {
    await expect(panel).toHaveAttribute("data-read-state", state);
    await page.evaluate(() => document.fonts.ready);
    await expect.poll(async () => {
      const heading = panel.getByRole("heading", { name: "ساختار مکان پروژه" });
      const top = await heading.evaluate((element) => element.getBoundingClientRect().top);
      if (top < 120 || top >= 250) await panel.evaluate((element) =>
        window.scrollTo({ top: Math.max(0, window.scrollY + element.getBoundingClientRect().top - 150),
          behavior: "instant" }));
      await page.waitForTimeout(150);
      const settled = await heading.evaluate((element) => element.getBoundingClientRect().top);
      return settled >= 120 && settled < 250;
    }, { timeout: 10_000 }).toBe(true);
    if (showCommandFeedback) {
      await panel.locator(".calculation-note").evaluate((element) => window.scrollTo({
        top: Math.max(0, window.scrollY + element.getBoundingClientRect().top - 680),
        behavior: "instant",
      }));
    }
    await page.waitForTimeout(300);
    await expect(panel).toHaveAttribute("data-read-state", state);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    const bytes = await page.screenshot({ animations: "disabled", caret: "hide" });
    expect(bytes.readUInt32BE(16)).toBe(width);
    expect(bytes.readUInt32BE(20)).toBe(height);
    writeFileSync(resolve(output, name), bytes);
    files.push({ name, sha256: createHash("sha256").update(bytes).digest("hex"),
      bytes: bytes.length, width, height });
  }

  const locations = [
    { id: "71000000-0000-4000-8000-000000000089", projectId, code: "ROOT", name: "ریشه پروژه",
      parentLocationId: null, status: "Active", revision: 1 },
    { id: "72000000-0000-4000-8000-000000000089", projectId, code: "SITE", name: "کارگاه",
      parentLocationId: "71000000-0000-4000-8000-000000000089", status: "Active", revision: 1 },
  ];
  let responseStatus = 200;
  let commands = 0;
  let releaseFailure: () => void = () => {};
  const failureGate = new Promise<void>((resolve) => { releaseFailure = resolve; });
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/locations`, async (route) => {
    if (route.request().method() !== "GET") {
      commands += 1;
      return route.fulfill({ status: 503, contentType: "application/problem+json",
        body: JSON.stringify({ status: 503, title: "Sensitive location detail" }) });
    }
    if (responseStatus === 503) await failureGate;
    return route.fulfill(responseStatus === 200
      ? { status: 200, contentType: "application/json", body: JSON.stringify(locations) }
      : { status: responseStatus, contentType: "application/problem+json",
        body: JSON.stringify({ status: responseStatus, title: "Sensitive location detail" }) });
  });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`/projects/${projectId}`);
  await expect(panel).toHaveAttribute("data-read-state", "current");
  await expect(panel.getByRole("button", { name: "غیرفعال‌کردن" })).toBeVisible();
  await capture("locations-390-current.png", 390, 844, "current");

  await panel.getByLabel("کد محل").fill("NEW");
  await panel.getByLabel("نام محل").fill("محل جدید");
  await panel.getByRole("button", { name: "افزودن محل" }).click();
  await expect(panel.getByRole("button", { name: "افزودن محل" })).toBeEnabled();
  await expect(panel.getByLabel("کد محل")).toHaveValue("NEW");
  await expect(panel.getByLabel("نام محل")).toHaveValue("محل جدید");
  await expect(panel.locator(".calculation-note")).toContainText("سرویس موردنیاز هنوز آماده نیست");
  await expect(panel.locator(".calculation-note")).not.toContainText("Sensitive location detail");
  await capture("locations-390-command-error.png", 390, 844, "current", true);

  await page.context().setOffline(true);
  await expect(panel).toHaveAttribute("data-read-state", "cached");
  await expect(panel.getByText("نسخهٔ محلی · فعال").first()).toBeVisible();
  await expect(panel.getByRole("button", { name: "غیرفعال‌کردن" })).toHaveCount(0);
  await expect(panel.getByRole("button", { name: "افزودن محل" })).toBeDisabled();
  await page.setViewportSize({ width: 320, height: 720 });
  await capture("locations-320-cached.png", 320, 720, "cached");
  expect(commands).toBe(1);

  responseStatus = 503;
  await page.context().setOffline(false);
  await expect(panel).toHaveAttribute("data-read-state", "loading");
  await expect(panel.getByRole("button", { name: "غیرفعال‌کردن" })).toHaveCount(0);
  await capture("locations-320-refreshing.png", 320, 720, "loading");
  releaseFailure();
  await expect(panel).toHaveAttribute("data-read-state", "cached");
  await expect(panel.getByRole("button", { name: "افزودن محل" })).toBeDisabled();
  await capture("locations-320-read-error-cached.png", 320, 720, "cached");

  responseStatus = 403;
  await panel.getByRole("button", { name: "تلاش دوباره برای دریافت مکان‌ها" }).click();
  await expect(panel).toHaveAttribute("data-read-state", "forbidden");
  await expect(panel.getByRole("alert")).not.toContainText("Sensitive location detail");
  await expect(panel.getByText("SITE · کارگاه")).toHaveCount(0);
  await capture("locations-320-access-revoked.png", 320, 720, "forbidden");

  responseStatus = 200;
  await panel.getByRole("button", { name: "تلاش دوباره برای دریافت مکان‌ها" }).click();
  await expect(panel).toHaveAttribute("data-read-state", "current");
  await expect(panel.getByRole("button", { name: "غیرفعال‌کردن" })).toBeVisible();
  await capture("locations-320-restored.png", 320, 720, "current");

  writeFileSync(resolve(output, "index.json"), JSON.stringify({
    contractVersion: 1, source: process.env.PMCS_SOURCE_HEAD_SHA ?? null,
    route: `/projects/${projectId}`, owner: "VX-G4 project location read truth", files,
  }, null, 2) + "\n");
});

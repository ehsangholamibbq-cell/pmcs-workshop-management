import { expect, test } from "@playwright/test";
import { auditSurface, evidence, routeReadyBudget, viewports } from "./vx-g5-support";

test.use({ storageState: { cookies: [], origins: [] } });

test("guest Login meets independent RTL, keyboard, reduced-motion and accessibility checks", async ({ page }, testInfo) => {
  const report = evidence(testInfo, "login");
  await page.emulateMedia({ reducedMotion: "reduce" });
  for (const viewport of viewports) {
    await page.setViewportSize(viewport);
    const started = Date.now();
    await page.goto("/login");
    await expect(page.getByRole("button", { name: "ورود امن", exact: true })).toBeEnabled();
    const ready = Date.now() - started;
    report.record(`login-${viewport.width}:routeReadyMilliseconds`, ready);
    expect(ready).toBeLessThanOrEqual(routeReadyBudget);
    await auditSurface(page, report, `login-${viewport.width}`);
    await report.capture(page, `login-${viewport.width}.png`);
  }
});

test("identity error preserves a working login and missing official image retains the organization name", async ({ page }, testInfo) => {
  const report = evidence(testInfo, "login");
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.setViewportSize({ width: 320, height: 900 });
  await page.goto("/login?error=identity");
  await expect(page.getByRole("alert")).toContainText("ورود کامل نشد");
  await expect(page.getByRole("button", { name: "ورود امن", exact: true })).toBeEnabled();
  await auditSurface(page, report, "identity-error-320");
  await report.capture(page, "identity-error-320.png");
  await page.route("**/brand/bbq-official-symbol.png", route => route.abort("failed"));
  await page.goto("/login");
  await expect(page.locator(".login-brand-lockup")).toContainText("بتن بسپار قزوین");
  await expect(page.getByRole("button", { name: "ورود امن", exact: true })).toBeEnabled();
  await expect(page.getByRole("img", { name: "نشان رسمی بتن بسپار قزوین", exact: true })).toHaveCount(0);
  await report.capture(page, "official-brand-missing-320.png");
});

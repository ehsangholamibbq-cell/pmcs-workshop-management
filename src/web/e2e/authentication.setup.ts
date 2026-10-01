import { test as setup, expect } from "@playwright/test";
import { authenticationState } from "../playwright.config";
import { loginThroughOidc, tenantId, userId } from "./support";
import { captureVisualBaseline } from "./visual-baseline";

setup("OIDC login establishes the scoped BFF session", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto("/login?returnTo=%2Fportfolio");
  const loginShell = page.locator(".login-shell");
  await expect(loginShell).toBeVisible();
  await expect(loginShell).toHaveAttribute("data-composition", /^(BlueprintSplit|MonolithFocus|WarmMinimal)$/u);
  await expect(loginShell).toHaveAttribute("data-surface", /^(WarmStone|WarmIvory|DeepNavy)$/u);
  await expect(loginShell).toHaveAttribute("data-accent", /^(CorporateNavyGreen|NavySilver|GreenStone)$/u);
  await expect(loginShell).toHaveAttribute("data-motion", /^(Calm|Balanced|Expressive)$/u);
  await expect(page.locator("html")).toHaveAttribute("lang", "fa");
  await expect(page.locator("html")).toHaveAttribute("dir", "rtl");
  await expect(page.getByRole("heading", { name: "ورود به حساب کاربری" })).toBeVisible();
  await expect(page.getByRole("button", { name: "ورود امن" })).toBeEnabled();
  await captureVisualBaseline(page, "01-login");
  await expect(page.locator(".blueprint-grid path").first()).toHaveCSS("animation-name", "none");
  await page.emulateMedia({ reducedMotion: "no-preference" });
  await expect(page.locator(".blueprint-grid path").first()).toHaveCSS("animation-name", "login-draw");

  const descriptorResponse = await page.request.get("/api/login-experience");
  expect(descriptorResponse.status()).toBe(200);
  expect(descriptorResponse.headers()["cache-control"]).toMatch(/no-store/u);
  const descriptor = await descriptorResponse.json() as { logoUrl: string | null };
  const descriptorBody = JSON.stringify(descriptor);
  expect(descriptorBody).not.toMatch(/clientSecret|authorizationUrl|redirectUri|issuer/u);
  if (!descriptor.logoUrl) {
    const officialMark = page.getByRole("img", { name: "نشان رسمی بتن بسپار قزوین" });
    await expect(officialMark).toBeVisible();
    await expect(officialMark).toHaveAttribute("src", /\/brand\/bbq-official-symbol\.png/u);
    await expect.poll(() => officialMark.evaluate((image: HTMLImageElement) => image.naturalWidth)).toBeGreaterThan(0);
  }

  await page.goto("/login?error=identity");
  await expect(page.locator(".login-card .login-error[role='alert']"))
    .toHaveText("ورود کامل نشد؛ اطلاعات حساب یا اتصال سرویس هویت را بررسی کنید.");
  await expect(page.getByRole("button", { name: "ورود امن" })).toBeEnabled();
  await captureVisualBaseline(page, "42-login-identity-error");

  await loginThroughOidc(page);

  const sessionResponse = await page.request.get("/api/pmcs/api/v1/session");
  expect(sessionResponse.status()).toBe(200);
  expect(sessionResponse.headers()["cache-control"]).toMatch(/no-store/u);
  const session = await sessionResponse.json() as {
    tenantId: string;
    userId: string;
    authentication: string;
  };
  expect(session).toMatchObject({
    tenantId,
    userId,
    authentication: "oidc-access-token",
  });

  await page.context().storageState({ path: authenticationState, indexedDB: true });
});

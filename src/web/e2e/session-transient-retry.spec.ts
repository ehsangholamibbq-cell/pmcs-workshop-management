import { expect, test } from "@playwright/test";

test("a transient session failure recovers without losing the authenticated page", async ({ page }) => {
  let attempts = 0;
  await page.route("**/api/pmcs/api/v1/session", async (route) => {
    attempts += 1;
    if (attempts <= 2) {
      await route.fulfill({ status: 503, contentType: "application/problem+json",
        body: JSON.stringify({ status: 503, title: "Temporary session service failure" }) });
      return;
    }
    await route.continue();
  });

  await page.goto("/portfolio");
  await expect(page.getByRole("heading", { name: "مرکز فرمان سبد پروژه‌ها" })).toBeVisible();
  expect(attempts).toBe(3);
});

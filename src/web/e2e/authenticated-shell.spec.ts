import { test, expect } from "@playwright/test";
import { projectId, projectPath, tenantId, userId } from "./support";

test("authenticated cold start keeps the Persian RTL tenant and project boundary", async ({ page }) => {
  await page.goto("/portfolio");

  await expect(page).toHaveURL(/\/portfolio(?:[?#]|$)/u);
  await expect(page.getByRole("heading", { name: "مرکز فرمان سبد پروژه‌ها" })).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("lang", "fa");
  await expect(page.locator("html")).toHaveAttribute("dir", "rtl");

  const sessionResponse = await page.request.get("/api/pmcs/api/v1/session");
  expect(sessionResponse.status()).toBe(200);
  expect(await sessionResponse.json()).toMatchObject({ tenantId, userId });

  const projectLink = page.getByRole("link", { name: "ورود به مرکز فرمان پروژه" });
  await expect(projectLink).toHaveAttribute("href", projectPath);

  const search = page.getByPlaceholder("نام، کد یا مدیر پروژه");
  await search.fill("پروژه‌ای که وجود ندارد");
  await expect(page.getByText("پروژه‌ای مطابق فیلترهای انتخاب‌شده پیدا نشد.")).toBeVisible();
  await search.clear();
  await expect(projectLink).toBeVisible();

  await projectLink.click();
  await expect(page).toHaveURL(new RegExp(`/projects/${projectId}$`, "u"));
  await expect(page.getByRole("heading", { name: "مرکز فرمان پروژه" })).toBeVisible();
  await expect(page.getByText("پروژه نمونه ساختمان اداری–تجاری")).toBeVisible();

  const calendarTrigger = page.getByRole("button", { name: "باز کردن تقویم شمسی" }).first();
  await calendarTrigger.click();
  const calendar = page.getByRole("dialog", { name: "انتخاب تاریخ شمسی" });
  await expect(calendar).toBeVisible();
  await expect(calendar.getByRole("gridcell")).toHaveCount(42);
  await expect(calendar.getByRole("button", { name: "امروز" })).toBeVisible();

  for (const viewport of [
    { width: 1440, height: 900 },
    { width: 820, height: 1180 },
    { width: 390, height: 844 },
  ]) {
    await page.setViewportSize(viewport);
    await expect.poll(() => page.evaluate(() => ({
      clientWidth: document.documentElement.clientWidth,
      scrollWidth: document.documentElement.scrollWidth,
    }))).toEqual({ clientWidth: viewport.width, scrollWidth: viewport.width });
    await expect(page.getByRole("navigation")).toBeVisible();
  }
});

test("loading and failure states stay explicit and localized", async ({ page }) => {
  let releaseRequest: (() => void) | undefined;
  const delayed = new Promise<void>((resolve) => { releaseRequest = resolve; });
  await page.route("**/api/pmcs/api/v1/portfolio/command-center", async (route) => {
    await delayed;
    await route.fulfill({
      status: 503,
      contentType: "application/problem+json",
      body: JSON.stringify({
        status: 503,
        code: "service.unavailable",
        title: "Sensitive upstream failure must not reach the user",
      }),
    });
  });

  await page.goto("/portfolio");
  await expect(page.getByText("در حال ساخت نمای مدیریتی از آخرین وضعیت‌های رسمی…")).toBeVisible();
  releaseRequest?.();

  await expect(page.getByRole("heading", { name: "داده سبد در دسترس نیست" })).toBeVisible();
  await expect(page.getByText("Sensitive upstream failure must not reach the user")).toHaveCount(0);
  await expect(page.locator(".portfolio-empty-panel").getByText(/در حال حاضر مشکلی در سرور رخ داده است/u)).toBeVisible();
});

test("member profile and login presentation administration work through the real UI", async ({ page }) => {
  await page.goto("/profile");
  await expect(page.getByRole("heading", { name: "مشخصات کاری من" })).toBeVisible();
  await expect(page.getByLabel("ایمیل سازمانی")).toBeDisabled();
  await expect(page.getByLabel("واحد سازمانی")).toBeDisabled();
  await expect(page.getByText("یک پروفایل، چند پروژه")).toBeVisible();

  await expect(page.getByRole("button", { name: "ذخیره پروفایل" })).toBeEnabled();
  await page.getByLabel("عنوان شغلی").fill("مدیر سامانه آزمون");
  await page.getByRole("button", { name: "ذخیره پروفایل" }).click();
  await expect(page.getByText("پروفایل با موفقیت به‌روزرسانی شد.")).toBeVisible();

  await page.goto("/admin/login-experience");
  await expect(page.getByRole("heading", { name: "مدیریت ظاهر صفحه ورود" })).toBeVisible();
  await expect(page.getByText("بدون کد اجرایی دلخواه")).toBeVisible();
  await page.getByLabel("تیتر اصلی").fill("مرکز فرمان حرفه‌ای پروژه");
  await page.getByLabel("شدت حرکت").selectOption("Calm");
  await page.getByRole("button", { name: "ساخت نسخه پیش‌نویس" }).click();
  await expect(page.getByText("نسخه پیش‌نویس ساخته شد؛ پس از بازبینی می‌توانید آن را منتشر کنید.")).toBeVisible();

  const draft = page.locator(".login-version-list article").filter({ hasText: "مرکز فرمان حرفه‌ای پروژه" });
  await expect(draft).toContainText("پیش‌نویس");
  await draft.getByRole("button", { name: "انتشار" }).click();
  await expect(page.getByText("نسخه جدید منتشر شد.")).toBeVisible();
  await expect(draft).toContainText("فعال");

  const published = await page.request.get("/api/login-experience");
  expect(published.status()).toBe(200);
  expect(await published.json()).toMatchObject({
    fallbackUsed: false,
    headline: "مرکز فرمان حرفه‌ای پروژه",
    motionPolicy: "Calm",
  });
});

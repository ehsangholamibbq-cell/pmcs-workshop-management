import { test, expect } from "@playwright/test";
import { projectId, projectPath, tenantId, userId } from "./support";
import { captureVisualBaseline } from "./visual-baseline";

test("authenticated cold start keeps the Persian RTL tenant and project boundary", async ({ page }) => {
  await page.goto("/portfolio");

  await expect(page).toHaveURL(/\/portfolio(?:[?#]|$)/u);
  await expect(page.getByRole("heading", { name: "مرکز فرمان سبد پروژه‌ها" })).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("lang", "fa");
  await expect(page.locator("html")).toHaveAttribute("dir", "rtl");
  const brand = page.locator('.brand-mark[aria-label="بتن بسپار قزوین، سامانه مدیریت پروژه"] img');
  await expect(brand).toHaveAttribute("src", /\/brand\/bbq-official-symbol\.png/u);
  await expect.poll(() => brand.evaluate((image: HTMLImageElement) => image.naturalWidth)).toBeGreaterThan(0);

  const sessionResponse = await page.request.get("/api/pmcs/api/v1/session");
  expect(sessionResponse.status()).toBe(200);
  expect(await sessionResponse.json()).toMatchObject({ tenantId, userId });

  const projectLink = page.getByRole("link", { name: "ورود به مرکز فرمان پروژه" });
  await expect(projectLink).toHaveAttribute("href", projectPath);

  const search = page.getByPlaceholder("نام، کد یا مدیر پروژه");
  await search.fill("پروژه‌ای که وجود ندارد");
  await expect(page.getByText("پروژه‌ای مطابق فیلترهای انتخاب‌شده پیدا نشد.")).toBeVisible();
  await captureVisualBaseline(page, "25-portfolio-empty");
  await search.clear();
  await expect(projectLink).toBeVisible();

  await projectLink.click();
  await expect(page).toHaveURL(new RegExp(`/projects/${projectId}$`, "u"));
  await expect(page.getByRole("heading", { name: "مرکز فرمان پروژه" })).toBeVisible();
  await expect(page.locator(".workspace").getByText("پروژه نمونه ساختمان اداری–تجاری")).toBeVisible();

  await page.setViewportSize({ width: 1440, height: 900 });
  const sidebar = page.locator(".sidebar");
  const mark = sidebar.locator(".brand-mark");
  await expect(mark).toBeVisible();
  await expect.poll(() => mark.evaluate((element) => element.getBoundingClientRect().height))
    .toBeGreaterThan(48);
  await expect.poll(() => sidebar.evaluate((element) => element.scrollHeight > element.clientHeight))
    .toBe(true);
  const lastNavigation = sidebar.getByRole("link", { name: "ظاهر صفحه ورود" });
  await sidebar.getByRole("link", { name: "کاربران و دسترسی‌ها" }).focus();
  await page.keyboard.press("Tab");
  await expect(lastNavigation).toBeFocused();
  await expect(lastNavigation).toBeInViewport();
  await expect(lastNavigation).toHaveCSS("outline-style", "solid");
  await expect.poll(() => sidebar.evaluate((element) => element.scrollTop)).toBeGreaterThan(0);
  await captureVisualBaseline(page, "39-desktop-navigation-end");
  await sidebar.evaluate((element) => { element.scrollTop = 0; });

  const calendarTrigger = page.getByRole("button", { name: "باز کردن تقویم شمسی" }).first();
  await calendarTrigger.click();
  const calendar = page.getByRole("dialog", { name: "انتخاب تاریخ شمسی" });
  await expect(calendar).toBeVisible();
  await expect(calendar.getByRole("gridcell")).toHaveCount(42);
  await expect(calendar.getByRole("button", { name: "امروز" })).toBeVisible();
  await captureVisualBaseline(page, "29-calendar-dialog");
  await calendarTrigger.click();
  await expect(calendar).toBeHidden();

  for (const viewport of [
    { width: 1440, height: 900 },
    { width: 820, height: 1180 },
    { width: 390, height: 844 },
    { width: 320, height: 720 },
  ]) {
    await page.setViewportSize(viewport);
    if (viewport.width === 320) {
      const overflow = await page.evaluate(() => {
        const width = document.documentElement.clientWidth;
        if (document.documentElement.scrollWidth <= width) return null;
        const elements = [...document.querySelectorAll("body *")]
          .filter((element) => {
            const box = element.getBoundingClientRect();
            if (box.left >= -0.1 && box.right <= width + 0.1) return false;
            for (let parent = element.parentElement; parent; parent = parent.parentElement) {
              if (/^(auto|scroll|hidden|clip)$/u.test(getComputedStyle(parent).overflowX)) return false;
            }
            return true;
          })
          .slice(0, 24)
          .map((element) => {
            const box = element.getBoundingClientRect();
            return { tag: element.tagName, className: String(element.className),
              left: Math.round(box.left * 10) / 10, right: Math.round(box.right * 10) / 10 };
          });
        const focused = document.activeElement;
        const focusBox = focused?.getBoundingClientRect();
        return { width, scrollWidth: document.documentElement.scrollWidth,
          bodyScrollWidth: document.body.scrollWidth, elements,
          focus: focused ? { tag: focused.tagName, className: String(focused.className),
            left: focusBox?.left, right: focusBox?.right,
            outline: getComputedStyle(focused).outline } : null };
      });
      if (overflow) console.log("Narrow viewport overflow", JSON.stringify(overflow));
    }
    await expect.poll(() => page.evaluate(() => ({
      clientWidth: document.documentElement.clientWidth,
      scrollWidth: document.documentElement.scrollWidth,
    }))).toEqual({ clientWidth: viewport.width, scrollWidth: viewport.width });
    if (viewport.width > 980) await expect(page.getByRole("navigation")).toBeVisible();
    else await expect(page.getByRole("button", { name: "باز کردن فهرست بخش‌ها" })).toBeVisible();
  }

  const mobileSidebar = page.locator(".disclosure-sidebar");
  await expect.poll(() => mobileSidebar.evaluate((element) =>
    element.scrollWidth <= element.clientWidth)).toBe(true);
  const toggle = mobileSidebar.getByRole("button", { name: "باز کردن فهرست بخش‌ها" });
  await toggle.click();
  await expect(mobileSidebar.getByRole("navigation", { name: "بخش‌های مرکز فرمان پروژه" })).toBeVisible();
  const profileNavigation = mobileSidebar.getByRole("link", { name: "پروفایل من" });
  await profileNavigation.focus();
  await page.keyboard.press("Tab");
  await page.keyboard.press("Shift+Tab");
  await expect(profileNavigation).toHaveCSS("outline-style", "solid");
  await expect(profileNavigation).toHaveCSS("white-space", "normal");
  await page.keyboard.press("Escape");
  await expect(toggle).toBeFocused();
  await expect(toggle).toBeInViewport();
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
  const loadingPreview = page.locator(".portfolio-loading-preview");
  await expect(loadingPreview).toBeVisible();
  await expect(loadingPreview).toHaveAttribute("aria-hidden", "true");
  await expect(loadingPreview.locator(".portfolio-loading-card")).toHaveCount(5);
  await expect(page.locator(".portfolio-kpis")).toHaveCount(0);
  await captureVisualBaseline(page, "26-portfolio-loading");
  releaseRequest?.();

  await expect(page.getByRole("heading", { name: "داده سبد در دسترس نیست" })).toBeVisible();
  await expect(loadingPreview).toHaveCount(0);
  await expect(page.getByText("Sensitive upstream failure must not reach the user")).toHaveCount(0);
  await expect(page.locator(".portfolio-empty-panel").getByText(/در حال حاضر مشکلی در سرور رخ داده است/u)).toBeVisible();
  await captureVisualBaseline(page, "27-portfolio-failure");
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

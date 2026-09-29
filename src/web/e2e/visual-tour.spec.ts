import { expect, test } from "@playwright/test";
import { projectPath } from "./support";
import { captureVisualBaseline } from "./visual-baseline";

test("capture the actual authenticated PMCS interface with isolated QA data", async ({ page }) => {
  test.setTimeout(240_000);
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.emulateMedia({ reducedMotion: "reduce" });

  await page.goto("/");
  await expect(page.getByRole("heading", { name: "پروژه‌های در دسترس" })).toBeVisible();
  await captureVisualBaseline(page, "02-projects");

  await page.goto("/portfolio");
  await expect(page.getByRole("heading", { name: "مرکز فرمان سبد پروژه‌ها" })).toBeVisible();
  await expect(page.getByRole("link", { name: "ورود به مرکز فرمان پروژه" })).toBeVisible();
  await captureVisualBaseline(page, "03-portfolio");
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByRole("button", { name: "باز کردن فهرست بخش‌ها" })).toBeVisible();
  await captureVisualBaseline(page, "20-mobile-portfolio");
  await page.setViewportSize({ width: 1440, height: 900 });

  await page.goto(projectPath);
  await expect(page.getByRole("heading", { name: "مرکز فرمان پروژه" })).toBeVisible();
  await expect(page.locator(".workspace").getByText("پروژه نمونه ساختمان اداری–تجاری")).toBeVisible();
  await page.addStyleTag({ content: "html { scroll-behavior: auto !important; }" });
  await captureVisualBaseline(page, "04-project-command");

  const projectSections = [
    ["today", "05-today"],
    ["progress", "06-progress"],
    ["technical-office", "07-technical-office"],
    ["actions", "08-my-work"],
    ["finance", "09-finance"],
    ["commercial", "10-commercial"],
    ["supply", "11-supply"],
    ["quality-safety", "12-quality-safety"],
    ["governance", "13-governance"],
    ["advisory", "14-advisory"],
    ["setup", "15-project-settings"],
  ] as const;

  for (const [sectionId, baselineId] of projectSections) {
    const section = page.locator(`#${sectionId}`);
    await expect(section).toBeVisible();
    await section.evaluate((element) => element.scrollIntoView({ block: "start" }));
    await captureVisualBaseline(page, baselineId);
  }

  await captureVisualBaseline(page, "28-tablet-project");

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByRole("button", { name: "باز کردن فهرست بخش‌ها" })).toBeVisible();
  await captureVisualBaseline(page, "21-mobile-project");

  await page.goto(`${projectPath}/collaboration`);
  await expect(page.getByRole("heading", { name: "گفت‌وگو در این پروژه در دسترس نیست" })).toBeVisible();
  await expect.poll(() => page.locator(".collaboration-shell .sidebar").evaluate((element) =>
    element.getBoundingClientRect().height)).toBeLessThan(120);
  await expect(page.locator(".mobile-nav-hint")).toBeVisible();
  await captureVisualBaseline(page, "22-project-chat-default-off");

  await page.goto(`${projectPath}/reports`);
  await expect(page.getByRole("heading", { name: "گزارش‌گیری در این پروژه در دسترس نیست" })).toBeVisible();
  await expect.poll(() => page.locator(".reporting-shell .sidebar").evaluate((element) =>
    element.getBoundingClientRect().height)).toBeLessThan(120);
  await expect(page.locator(".mobile-nav-hint")).toBeVisible();
  await captureVisualBaseline(page, "23-project-reporting-default-off");

  await page.goto("/portfolio/reports");
  await expect(page.getByRole("heading", { name: "گزارش‌های سبد در دسترس نیستند" })).toBeVisible();
  await captureVisualBaseline(page, "24-portfolio-reporting-default-off");

  // The preceding full project visit loads many independent API panels. Allow the
  // isolated fixture's short API rate window to clear before opening admin pages.
  await page.goto("about:blank");
  await page.waitForTimeout(65_000);
  await page.setViewportSize({ width: 1440, height: 900 });

  await page.goto("/profile");
  await expect(page.getByRole("heading", { name: "مشخصات کاری من" })).toBeVisible();
  await captureVisualBaseline(page, "16-profile");

  await page.goto("/admin/users");
  await expect(page.getByRole("heading", { name: "کاربران، دعوت‌ها و عضویت پروژه" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "دعوت‌های اخیر" })).toBeVisible();
  await captureVisualBaseline(page, "17-users");

  await page.goto("/admin/login-experience");
  await expect(page.getByRole("heading", { name: "مدیریت ظاهر صفحه ورود" })).toBeVisible();
  await expect(page.locator(".pmcs-file-input-name")).toHaveText(["فایلی انتخاب نشده", "فایلی انتخاب نشده"]);
  await captureVisualBaseline(page, "18-login-appearance");
  const logoInput = page.getByLabel("لوگو اختیاری");
  await logoInput.setInputFiles({ name: "نمونه.png", mimeType: "image/png", buffer: Buffer.from(
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/lXcAAAAASUVORK5CYII=", "base64") });
  await expect(page.locator(".pmcs-file-input-name").first()).toHaveText("نمونه.png");
  await captureVisualBaseline(page, "44-login-asset-selected");
  await logoInput.focus();
  await expect(page.locator(".pmcs-file-input-control").first()).toHaveCSS("outline-style", "solid");
  await page.getByRole("button", { name: "حذف انتخاب" }).click();
  await expect(page.locator(".pmcs-file-input-name").first()).toHaveText("فایلی انتخاب نشده");

  await page.goto("/project-bootstraps");
  await expect(page.getByRole("heading", { name: "ساخت از روی پروژهٔ موجود" })).toBeVisible();
  await expect(page.getByRole("option", { name: /پروژه نمونه ساختمان اداری–تجاری/u })).toBeAttached();
  const accountContrast = await page.locator(".project-bootstrap-account .session-badge").evaluate((card) =>
    [...card.querySelectorAll("strong, span, small, button")].map((element) => {
      const channels = getComputedStyle(element).color.match(/\d+/gu)?.slice(0, 3).map(Number) ?? [];
      const linear = channels.map((value) => {
        const channel = value / 255;
        return channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4;
      });
      const luminance = linear[0] * 0.2126 + linear[1] * 0.7152 + linear[2] * 0.0722;
      return 1.05 / (luminance + 0.05);
    }));
  expect(accountContrast.length).toBeGreaterThanOrEqual(3);
  for (const ratio of accountContrast) expect(ratio).toBeGreaterThanOrEqual(4.5);
  await captureVisualBaseline(page, "19-project-bootstrap");

  await page.goto("/missing-visual-baseline-route");
  await expect(page.getByRole("heading", { name: "نشانی واردشده در دسترس نیست" })).toBeVisible();
  await captureVisualBaseline(page, "32-not-found");
});

import { mkdir } from "node:fs/promises";
import path from "node:path";
import { expect, test, type Page } from "@playwright/test";
import { projectPath } from "./support";

const tourDirectory = path.resolve("artifacts/visual-tour");

async function capture(page: Page, fileName: string): Promise<void> {
  await page.screenshot({ path: path.join(tourDirectory, fileName), animations: "disabled" });
}

test("capture the actual authenticated PMCS interface with isolated QA data", async ({ page }) => {
  test.setTimeout(180_000);
  await mkdir(tourDirectory, { recursive: true });
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.emulateMedia({ reducedMotion: "reduce" });

  await page.goto("/");
  await expect(page.getByRole("heading", { name: "پروژه‌های در دسترس" })).toBeVisible();
  await capture(page, "02-projects.png");

  await page.goto("/portfolio");
  await expect(page.getByRole("heading", { name: "مرکز فرمان سبد پروژه‌ها" })).toBeVisible();
  await expect(page.getByRole("link", { name: "ورود به مرکز فرمان پروژه" })).toBeVisible();
  await capture(page, "03-portfolio.png");

  await page.goto(projectPath);
  await expect(page.getByRole("heading", { name: "مرکز فرمان پروژه" })).toBeVisible();
  await expect(page.getByText("پروژه نمونه ساختمان اداری–تجاری")).toBeVisible();
  await page.addStyleTag({ content: "html { scroll-behavior: auto !important; }" });
  await capture(page, "04-project-command.png");

  const projectSections = [
    ["today", "05-today.png"],
    ["progress", "06-progress.png"],
    ["technical-office", "07-technical-office.png"],
    ["actions", "08-my-work.png"],
    ["finance", "09-finance.png"],
    ["commercial", "10-commercial.png"],
    ["supply", "11-supply.png"],
    ["quality-safety", "12-quality-safety.png"],
    ["governance", "13-governance.png"],
    ["advisory", "14-advisory.png"],
    ["setup", "15-project-settings.png"],
  ] as const;

  for (const [sectionId, fileName] of projectSections) {
    const section = page.locator(`#${sectionId}`);
    await expect(section).toBeVisible();
    await section.evaluate((element) => element.scrollIntoView({ block: "start" }));
    await capture(page, fileName);
  }

  await page.goto("/profile");
  await expect(page.getByRole("heading", { name: "مشخصات کاری من" })).toBeVisible();
  await capture(page, "16-profile.png");

  await page.goto("/admin/users");
  await expect(page.getByRole("heading", { name: "کاربران، دعوت‌ها و عضویت پروژه" })).toBeVisible();
  await capture(page, "17-users.png");

  await page.goto("/admin/login-experience");
  await expect(page.getByRole("heading", { name: "مدیریت ظاهر صفحه ورود" })).toBeVisible();
  await capture(page, "18-login-appearance.png");

  await page.goto("/project-bootstraps");
  await expect(page.getByRole("heading", { name: "ساخت از روی پروژهٔ موجود" })).toBeVisible();
  await capture(page, "19-project-bootstrap.png");

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/portfolio");
  await expect(page.getByRole("heading", { name: "مرکز فرمان سبد پروژه‌ها" })).toBeVisible();
  await capture(page, "20-mobile-portfolio.png");

  await page.goto(projectPath);
  await expect(page.getByRole("heading", { name: "مرکز فرمان پروژه" })).toBeVisible();
  await capture(page, "21-mobile-project.png");
});

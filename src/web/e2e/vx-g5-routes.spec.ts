import { expect, test } from "@playwright/test";
import { projectId, projectPath } from "./support";
import { auditSurface, evidence, routeReadyBudget, viewports } from "./vx-g5-support";

test("independent route, module, RTL, keyboard, accessibility and responsive matrix", async ({ page }, testInfo) => {
  test.setTimeout(600_000);
  await page.emulateMedia({ reducedMotion: "reduce" });
  const report = evidence(testInfo, "routes");
  const routes = [
    ["projects", "/", "پروژه‌های در دسترس"],
    ["portfolio", "/portfolio", "مرکز فرمان سبد پروژه‌ها"],
    ["project", projectPath, "مرکز فرمان پروژه"],
    ["chat-off", `${projectPath}/collaboration`, "گفت‌وگو در این پروژه در دسترس نیست"],
    ["reports-off", `${projectPath}/reports`, "گزارش‌گیری در این پروژه در دسترس نیست"],
    ["portfolio-reports-off", "/portfolio/reports", "گزارش‌های سبد در دسترس نیستند"],
    ["profile", "/profile", "مشخصات کاری من"],
    ["users", "/admin/users", "کاربران، دعوت‌ها و عضویت پروژه"],
    ["login-admin", "/admin/login-experience", "مدیریت ظاهر صفحه ورود"],
    ["bootstrap", "/project-bootstraps", "ساخت از روی پروژهٔ موجود"],
  ] as const;
  const modules = ["today", "progress", "technical-office", "actions", "finance", "commercial",
    "supply", "quality-safety", "governance", "advisory", "setup"];
  for (const [name, route, heading] of routes) for (const viewport of viewports) {
    await page.setViewportSize(viewport);
    const started = Date.now();
    await page.goto(route, { waitUntil: "domcontentloaded" });
    await expect(page.getByRole("heading", { name: heading, exact: true })).toBeVisible();
    if (name === "project") {
      await expect(page.locator(".project-print-shell")).toHaveAttribute("data-command-read-state", "current");
      await expect.poll(() => page.locator('[data-testid="fact-location"] option').count()).toBeGreaterThan(1);
    }
    if (name === "users") await expect(page.getByRole("heading", { name: "دعوت‌های اخیر", exact: true })).toBeVisible();
    if (name === "bootstrap") await expect(page.locator(`option[value="${projectId}"]`)).toBeAttached();
    const ready = Date.now() - started;
    report.record(`${name}-${viewport.width}:routeReadyMilliseconds`, ready);
    expect(ready).toBeLessThanOrEqual(routeReadyBudget);
    await auditSurface(page, report, `${name}-${viewport.width}`);
    await page.evaluate(() => window.scrollTo({ top: 0, behavior: "instant" }));
    await report.capture(page, `${name}-${viewport.width}.png`);
    if (name === "project") for (const sectionId of modules) {
      const section = page.locator(`#${sectionId}`);
      await expect(section).toBeVisible();
      await section.evaluate(element => window.scrollTo({ top: Math.max(0,
        window.scrollY + element.getBoundingClientRect().top - 145), behavior: "instant" }));
      expect(await page.evaluate(() => document.documentElement.scrollWidth - innerWidth)).toBeLessThanOrEqual(1);
      if (viewport.width === 320 || viewport.width === 1440) await report.capture(page, `project-${sectionId}-${viewport.width}.png`);
    }
  }
});

import { expect, test } from "@playwright/test";
import { projectId, userId } from "./support";

const path = `/projects/${projectId}/collaboration`;

test("project Chat respects the disabled default without exposing messages", async ({ page }) => {
  await page.goto(path);
  await expect(page.getByRole("heading", { name: "گفت‌وگوی گروهی پروژه" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "گفت‌وگو در این پروژه در دسترس نیست" })).toBeVisible();
  await expect(page.getByRole("link", { name: "مرکز فرمان پروژه" })).toHaveAttribute("href", `/projects/${projectId}`);
  await expect(page.locator(".collaboration-message")).toHaveCount(0);
});

test("revoked project membership never reveals a conversation", async ({ page }) => {
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration`, (route) => route.fulfill({ status: 403 }));
  await page.goto(path);
  await expect(page.getByRole("heading", { name: "دسترسی به گفت‌وگو ندارید" })).toBeVisible();
  await expect(page.locator(".collaboration-message")).toHaveCount(0);
});

test("project Chat renders only the scoped latest messages, including tombstones", async ({ page }) => {
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration`, (route) => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify({ projectId, lastSequence: 2 }),
  }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/messages\\?after=0$`, "u"), (route) => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify({ nextSequence: 2, messages: [
      { id: "10000000-0000-4000-8000-000000000011", projectId, sequence: 1,
        authorUserId: userId, body: "پیام پروژه", createdAt: "2026-09-28T00:00:00Z" },
      { id: "10000000-0000-4000-8000-000000000012", projectId, sequence: 2,
        authorUserId: userId, body: "متن حذف‌شده", createdAt: "2026-09-28T00:01:00Z",
        deletedAt: "2026-09-28T00:02:00Z" },
    ] }),
  }));
  await page.goto(path);
  await expect(page.locator(".collaboration-message")).toHaveCount(2);
  await expect(page.getByText("پیام پروژه")).toBeVisible();
  await expect(page.getByText("متن حذف‌شده")).toHaveCount(0);
  await expect(page.getByText("این پیام دیگر برای نمایش در دسترس نیست.")).toBeVisible();
  await expect(page.getByRole("button", { name: "تازه‌سازی" })).toBeEnabled();
});

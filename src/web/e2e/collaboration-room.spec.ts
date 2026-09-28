import { expect, test } from "@playwright/test";
import { projectId, userId } from "./support";

const path = `/projects/${projectId}/collaboration`;

test.beforeEach(async ({ page }) => {
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration/unread`, (route) => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify({ lastReadSequence: 0, unreadCount: 0 }),
  }));
});

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
  let lastSequence = 2;
  let sentMessage: string | null = null;
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration`, (route) => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify({ projectId, lastSequence }),
  }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/events\\?`, "u"),
    (route) => route.fulfill({ status: 503 }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/messages\\?after=0$`, "u"), (route) => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify({ nextSequence: lastSequence, messages: [
      { id: "10000000-0000-4000-8000-000000000011", projectId, sequence: 1,
        authorUserId: userId, body: "پیام پروژه", createdAt: "2026-09-28T00:00:00Z" },
      { id: "10000000-0000-4000-8000-000000000012", projectId, sequence: 2,
        authorUserId: userId, body: "متن حذف‌شده", createdAt: "2026-09-28T00:01:00Z",
        deletedAt: "2026-09-28T00:02:00Z" },
      ...(sentMessage ? [{ id: "10000000-0000-4000-8000-000000000013", projectId, sequence: 3,
        authorUserId: userId, body: sentMessage, createdAt: "2026-09-28T00:03:00Z" }] : []),
    ] }),
  }));
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration/messages`, (route) => {
    const payload = route.request().postDataJSON() as { clientMessageId: string; body: string };
    expect(route.request().headers()["idempotency-key"]).toBe(payload.clientMessageId);
    sentMessage = payload.body;
    lastSequence = 3;
    return route.fulfill({ status: 201, contentType: "application/json",
      body: JSON.stringify({ clientMessageId: payload.clientMessageId }) });
  });
  await page.goto(path);
  await expect(page.locator(".collaboration-message")).toHaveCount(2);
  await expect(page.getByText("پیام پروژه")).toBeVisible();
  await expect(page.getByText("متن حذف‌شده")).toHaveCount(0);
  await expect(page.getByText("این پیام دیگر برای نمایش در دسترس نیست.")).toBeVisible();
  await expect(page.getByRole("button", { name: "تازه‌سازی" })).toBeEnabled();
  await page.getByLabel("پیام به گروه همین پروژه").fill("پیام جدید گروه");
  await page.getByRole("button", { name: "ارسال به گروه پروژه" }).click();
  await expect(page.getByText("پیام جدید گروه")).toBeVisible();
  await expect(page.getByText("پیام در گفت‌وگوی پروژه ثبت شد.")).toBeVisible();
});

test("offline project message resumes with one stable idempotency identity", async ({ page, context }) => {
  const attempts: { key: string | undefined; id: string }[] = [];
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration`, (route) => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify({ projectId, lastSequence: 0 }),
  }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/events\\?`, "u"),
    (route) => route.fulfill({ status: 503 }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/messages\\?after=0$`, "u"),
    (route) => route.fulfill({ status: 200, contentType: "application/json",
      body: JSON.stringify({ nextSequence: 0, messages: [] }) }));
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration/messages`, (route) => {
    const payload = route.request().postDataJSON() as { clientMessageId: string };
    attempts.push({ key: route.request().headers()["idempotency-key"], id: payload.clientMessageId });
    return route.fulfill({ status: 201, contentType: "application/json",
      body: JSON.stringify({ clientMessageId: payload.clientMessageId }) });
  });
  await page.goto(path);
  await expect(page.getByText("هنوز پیامی در این پروژه ثبت نشده است.")).toBeVisible();
  await context.setOffline(true);
  await page.getByLabel("پیام به گروه همین پروژه").fill("پیام در قطع ارتباط");
  await page.getByRole("button", { name: "ارسال به گروه پروژه" }).click();
  await expect(page.getByText(/پیام در صف ارسال/u)).toBeVisible();
  expect(attempts).toHaveLength(0);
  await context.setOffline(false);
  await expect(page.getByText("پیام‌های صف به گفت‌وگوی پروژه رسیدند.")).toBeVisible();
  expect(attempts).toHaveLength(1);
  expect(attempts[0].key).toBe(attempts[0].id);
});

test("a live project event refreshes the same room without reloading the page", async ({ page }) => {
  let lastSequence = 1;
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration`, (route) => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify({ projectId, lastSequence }),
  }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/messages\\?after=0$`, "u"),
    (route) => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
      nextSequence: lastSequence, messages: [
        { id: "10000000-0000-4000-8000-000000000011", projectId, sequence: 1,
          authorUserId: userId, body: "پیام آغازین", createdAt: "2026-09-28T00:00:00Z" },
        ...(lastSequence === 2 ? [{ id: "10000000-0000-4000-8000-000000000012", projectId,
          sequence: 2, authorUserId: userId, body: "پیام زنده پروژه",
          createdAt: "2026-09-28T00:01:00Z" }] : []),
      ],
    }) }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/events\\?`, "u"),
    (route) => {
      if (lastSequence === 2) return route.fulfill({ status: 503 });
      lastSequence = 2;
      return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
        nextSequence: 2, events: [{ messageId: "10000000-0000-4000-8000-000000000012",
          projectId, sequence: 2, createdAt: "2026-09-28T00:01:00Z" }],
      }) });
    });
  await page.goto(path);
  await expect(page.getByText("پیام زنده پروژه")).toBeVisible();
  await expect(page.locator(".collaboration-message")).toHaveCount(2);
});

test("live membership revocation removes previously loaded messages", async ({ page }) => {
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration`, (route) => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify({ projectId, lastSequence: 1 }),
  }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/messages\\?after=0$`, "u"),
    (route) => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
      nextSequence: 1, messages: [{ id: "10000000-0000-4000-8000-000000000011",
        projectId, sequence: 1, authorUserId: userId, body: "محتوای محرمانه پروژه",
        createdAt: "2026-09-28T00:00:00Z" }],
    }) }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/events\\?`, "u"),
    (route) => route.fulfill({ status: 403 }));
  await page.goto(path);
  await expect(page.getByRole("heading", { name: "دسترسی به گفت‌وگو ندارید" })).toBeVisible();
  await expect(page.getByText("محتوای محرمانه پروژه")).toHaveCount(0);
  await expect(page.getByLabel("پیام به گروه همین پروژه")).toHaveCount(0);
});

test("project Chat searches, advances read cursor, and replies within the same room", async ({ page }) => {
  const firstMessageId = "10000000-0000-4000-8000-000000000011";
  let replyTo: string | null = null;
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration`, (route) => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify({ projectId, lastSequence: 1 }),
  }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/events\\?`, "u"),
    (route) => route.fulfill({ status: 503 }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/messages\\?after=0$`, "u"),
    (route) => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
      nextSequence: 1, messages: [{ id: firstMessageId, projectId, sequence: 1,
        authorUserId: userId, body: "موضوع پیگیری فنی", createdAt: "2026-09-28T00:00:00Z" }],
    }) }));
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration/unread`, (route) => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify({ lastReadSequence: 0, unreadCount: 1 }),
  }));
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration/read-cursor`, (route) => {
    expect(route.request().method()).toBe("PUT");
    expect(route.request().postDataJSON()).toEqual({ lastReadSequence: 1 });
    return route.fulfill({ status: 200, contentType: "application/json",
      body: JSON.stringify({ lastReadSequence: 1 }) });
  });
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/messages/search\\?`, "u"),
    (route) => route.fulfill({ status: 200, contentType: "application/json",
      body: JSON.stringify([{ id: firstMessageId, projectId, sequence: 1, body: "موضوع پیگیری فنی" }]) }));
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration/messages`, (route) => {
    replyTo = (route.request().postDataJSON() as { replyToMessageId: string }).replyToMessageId;
    const clientMessageId = (route.request().postDataJSON() as { clientMessageId: string }).clientMessageId;
    return route.fulfill({ status: 201, contentType: "application/json",
      body: JSON.stringify({ clientMessageId }) });
  });
  await page.goto(path);
  await expect(page.getByText("۱ پیام خوانده‌نشده")).toBeVisible();
  await page.getByRole("button", { name: "تا اینجا خواندم" }).click();
  await expect(page.getByText("۰ پیام خوانده‌نشده")).toBeVisible();
  await page.getByLabel("جست‌وجو در پیام‌های همین پروژه").fill("پیگیری فنی");
  await page.getByRole("button", { name: "جست‌وجو", exact: true }).click();
  await expect(page.getByRole("region", { name: "نتیجه‌های جست‌وجوی پروژه" })).toContainText("موضوع پیگیری فنی");
  await page.locator(".collaboration-message").getByRole("button", { name: "پاسخ به پیام" }).click();
  await expect(page.getByText("در پاسخ به: موضوع پیگیری فنی")).toBeVisible();
  await page.getByLabel("پیام به گروه همین پروژه").fill("پاسخ محدود به پروژه");
  await page.getByRole("button", { name: "ارسال به گروه پروژه" }).click();
  await expect(page.getByText("پیام در گفت‌وگوی پروژه ثبت شد.")).toBeVisible();
  expect(replyTo).toBe(firstMessageId);
});

test("project reactions persist across reload, allow read-only viewing and clear on revoked access", async ({ page }) => {
  const messageId = "10000000-0000-4000-8000-000000000011";
  let reacted = false;
  let canReact = true;
  let revoked = false;
  const mutations: string[] = [];
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration`, (route) => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify({ projectId, lastSequence: 1 }),
  }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/events\\?`, "u"),
    (route) => route.fulfill({ status: 503 }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/messages\\?after=0$`, "u"),
    (route) => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
      nextSequence: 1, messages: [{ id: messageId, projectId, sequence: 1, authorUserId: userId,
        body: "پیام برای واکنش", createdAt: "2026-09-28T00:00:00Z" }],
    }) }));
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration/messages/${messageId}/reactions`,
    (route) => route.fulfill(revoked ? { status: 403 } : { status: 200,
      contentType: "application/json", body: JSON.stringify({ messageId, canReact,
        reactions: reacted ? [{ emoji: "👍", count: 1, reactedByMe: true }] : [] }) }));
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration/messages/${messageId}/reactions/*`,
    (route) => {
      mutations.push(route.request().method());
      reacted = route.request().method() === "PUT";
      return route.fulfill(reacted ? { status: 200, contentType: "application/json",
        body: JSON.stringify({ messageId, emoji: "👍", reacted: true }) } : { status: 204 });
    });

  await page.goto(path);
  await page.getByRole("button", { name: "واکنش‌ها" }).click();
  const thumb = page.getByRole("button", { name: "👍، ۰ واکنش" });
  await expect(thumb).toHaveAttribute("aria-pressed", "false");
  await thumb.click();
  await expect(page.getByRole("button", { name: "👍، ۱ واکنش" })).toHaveAttribute("aria-pressed", "true");
  await page.reload();
  await page.getByRole("button", { name: "واکنش‌ها" }).click();
  await expect(page.getByRole("button", { name: "👍، ۱ واکنش" })).toHaveAttribute("aria-pressed", "true");
  await page.getByRole("button", { name: "👍، ۱ واکنش" }).click();
  await expect(page.getByRole("button", { name: "👍، ۰ واکنش" })).toHaveAttribute("aria-pressed", "false");
  expect(mutations).toEqual(["PUT", "DELETE"]);

  canReact = false;
  await page.reload();
  await page.getByRole("button", { name: "واکنش‌ها" }).click();
  await expect(page.getByRole("button", { name: "👍، ۰ واکنش" })).toBeDisabled();
  await expect(page.getByText("نمایش واکنش‌ها مجاز است؛ ثبت واکنش به مجوز ارسال نیاز دارد.")).toBeVisible();
  revoked = true;
  await page.reload();
  await page.getByRole("button", { name: "واکنش‌ها" }).click();
  await expect(page.getByRole("heading", { name: "دسترسی به گفت‌وگو ندارید" })).toBeVisible();
  await expect(page.getByText("پیام برای واکنش")).toHaveCount(0);
});

test("only a project moderator can pin and unpin, while readers see the pinned state", async ({ page }) => {
  const messageId = "10000000-0000-4000-8000-000000000011";
  let canModerate = true;
  let pinnedAt: string | null = null;
  let revoked = false;
  const methods: string[] = [];
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration`, (route) => route.fulfill({
    status: 200, contentType: "application/json",
    body: JSON.stringify({ projectId, lastSequence: 1, canModerate }),
  }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/events\\?`, "u"),
    (route) => route.fulfill({ status: 503 }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/messages\\?after=0$`, "u"),
    (route) => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
      nextSequence: 1, messages: [{ id: messageId, projectId, sequence: 1, authorUserId: userId,
        body: "پیام سنجاق پروژه", pinnedAt, createdAt: "2026-09-28T00:00:00Z" }],
    }) }));
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration/messages/${messageId}/pin`,
    (route) => {
      methods.push(route.request().method());
      if (revoked) return route.fulfill({ status: 403 });
      pinnedAt = route.request().method() === "PUT" ? "2026-09-28T00:05:00Z" : null;
      return route.fulfill({ status: 200, contentType: "application/json",
        body: JSON.stringify({ id: messageId, projectId, pinnedAt }) });
    });

  await page.goto(path);
  await page.getByRole("button", { name: "سنجاق پیام" }).click();
  await expect(page.getByRole("button", { name: "برداشتن سنجاق" })).toHaveAttribute("aria-pressed", "true");
  await expect(page.getByText("سنجاق‌شده")).toBeVisible();
  await page.reload();
  await expect(page.getByRole("button", { name: "برداشتن سنجاق" })).toBeVisible();
  await page.getByRole("button", { name: "برداشتن سنجاق" }).click();
  await expect(page.getByRole("button", { name: "سنجاق پیام" })).toHaveAttribute("aria-pressed", "false");
  await expect(page.getByText("سنجاق‌شده")).toHaveCount(0);
  expect(methods).toEqual(["PUT", "DELETE"]);

  pinnedAt = "2026-09-28T00:05:00Z";
  canModerate = false;
  await page.reload();
  await expect(page.getByText("سنجاق‌شده")).toBeVisible();
  await expect(page.getByRole("button", { name: "برداشتن سنجاق" })).toHaveCount(0);
  canModerate = true;
  revoked = true;
  await page.reload();
  await page.getByRole("button", { name: "برداشتن سنجاق" }).click();
  await expect(page.getByRole("heading", { name: "دسترسی به گفت‌وگو ندارید" })).toBeVisible();
  await expect(page.getByText("پیام سنجاق پروژه")).toHaveCount(0);
});

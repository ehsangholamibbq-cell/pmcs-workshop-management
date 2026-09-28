import { expect, test } from "@playwright/test";
import { createHash } from "node:crypto";
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

test("a project reader downloads only a verified Released message attachment and loses it on 403", async ({ page }) => {
  const messageId = "10000000-0000-4000-8000-000000000011";
  const documentId = "10000000-0000-4000-8000-000000000101";
  const bytes = Buffer.from("PMCS project attachment", "utf8");
  const attachmentPath = `/api/v1/projects/${projectId}/collaboration/messages/${messageId}/attachments`;
  const contentPath = `${attachmentPath}/${documentId}/content`;
  let revoked = false;
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration`, (route) => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify({ projectId, lastSequence: 2 }),
  }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/events\\?`, "u"),
    (route) => route.fulfill({ status: 503 }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/messages\\?after=0$`, "u"),
    (route) => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
      nextSequence: 2, messages: [
        { id: messageId, projectId, sequence: 1, authorUserId: userId,
          body: "پیام با پیوست", createdAt: "2026-09-28T00:00:00Z" },
        { id: "10000000-0000-4000-8000-000000000012", projectId, sequence: 2,
          authorUserId: userId, body: "حذف‌شده", createdAt: "2026-09-28T00:01:00Z",
          deletedAt: "2026-09-28T00:02:00Z" },
      ],
    }) }));
  await page.route(`**/api/pmcs${attachmentPath}`, (route) => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify([{
      messageId, documentId, originalFileName: "project.pdf", contentType: "application/pdf",
      sizeBytes: bytes.length, sha256: createHash("sha256").update(bytes).digest("hex"),
      classification: "Internal", retentionPolicy: "Standard", legalHold: false,
      releasedAt: "2026-09-28T00:00:00Z", versionNumber: 1, contentUrl: contentPath,
    }]),
  }));
  await page.route(`**/api/pmcs${contentPath}`, (route) => route.fulfill(revoked
    ? { status: 403 } : { status: 200, contentType: "application/pdf", body: bytes }));

  await page.goto(path);
  await expect(page.getByRole("button", { name: "پیوست‌ها" })).toHaveCount(1);
  await page.getByRole("button", { name: "پیوست‌ها" }).click();
  await expect(page.getByText("project.pdf", { exact: false })).toBeVisible();
  const download = page.waitForEvent("download");
  await page.getByRole("button", { name: "دریافت پیوست" }).click();
  expect((await download).suggestedFilename()).toBe("project.pdf");
  await expect(page.getByText("پیوست پس از تأیید صحت دریافت شد.")).toBeVisible();
  revoked = true;
  await page.getByRole("button", { name: "دریافت پیوست" }).click();
  await expect(page.getByRole("heading", { name: "دسترسی به گفت‌وگو ندارید" })).toBeVisible();
  await expect(page.getByText("پیام با پیوست")).toHaveCount(0);
});

test("only the message author queues a Chat document and sees quarantine status across reload", async ({ page }) => {
  const messageId = "10000000-0000-4000-8000-000000000011";
  const bytes = Buffer.from("%PDF-1.7\nPMCS project Chat upload\n%%EOF\n", "utf8");
  let canUpload = true;
  let revoked = false;
  let released = false;
  let attached = false;
  let uploadedId = "";
  let uploadedSha = "";
  const attachmentPath = `/api/v1/projects/${projectId}/collaboration/messages/${messageId}/attachments`;
  const metadata = () => ({
    messageId, documentId: uploadedId, originalFileName: "scope.pdf", contentType: "application/pdf",
    sizeBytes: bytes.length, sha256: uploadedSha, classification: "Internal",
    retentionPolicy: "Standard", legalHold: false, releasedAt: "2026-09-28T00:02:00Z",
    versionNumber: 1, contentUrl: `${attachmentPath}/${uploadedId}/content`,
  });
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration`, (route) => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify({ projectId, lastSequence: 1, canUpload }),
  }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/events\\?`, "u"),
    (route) => route.fulfill({ status: 503 }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/messages\\?after=0$`, "u"),
    (route) => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
      nextSequence: 1, messages: [{ id: messageId, projectId, sequence: 1, authorUserId: userId,
        body: "پیام نویسنده", createdAt: "2026-09-28T00:00:00Z" }],
    }) }));
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration/messages/${messageId}/attachments`,
    (route) => route.fulfill({ status: 200, contentType: "application/json",
      body: JSON.stringify(attached ? [metadata()] : []) }));
  await page.route(new RegExp(`/api/pmcs${attachmentPath}/[^/]+$`, "u"), (route) => {
    expect(route.request().method()).toBe("PUT");
    expect(released).toBe(true);
    attached = true;
    return route.fulfill({ status: 200, contentType: "application/json",
      body: JSON.stringify(metadata()) });
  });
  await page.route(`**/api/pmcs/api/v1/upload-sessions`, (route) => {
    const payload = route.request().postDataJSON() as {
      clientGeneratedId: string; projectId: string; ownerType: string; ownerId: string; sha256: string;
    };
    expect(payload.projectId).toBe(projectId);
    expect(payload.ownerType).toBe("ProjectChat");
    expect(payload.ownerId).toBe(messageId);
    expect(route.request().headers()["idempotency-key"]).toBe(`${payload.clientGeneratedId}:session`);
    uploadedId = payload.clientGeneratedId;
    uploadedSha = payload.sha256;
    return route.fulfill({ status: 201, contentType: "application/json", body: JSON.stringify({
      document: { id: uploadedId, status: "PendingUpload" }, uploadMethod: "PUT",
      uploadUrl: `/api/v1/documents/${uploadedId}/content`, expiresAt: "2026-09-29T00:00:00Z",
    }) });
  });
  await page.route(`**/api/pmcs/api/v1/documents/*/content`, (route) => {
    expect(route.request().method()).toBe("PUT");
    expect(route.request().headers()["idempotency-key"]).toBe(`${uploadedId}:content`);
    return route.fulfill({ status: 200, contentType: "application/json",
      body: JSON.stringify({ status: "Quarantined" }) });
  });
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/messages/${messageId}/uploads/`, "u"),
    (route) => route.fulfill(revoked ? { status: 403 } : { status: 200,
      contentType: "application/json", body: JSON.stringify({
        id: uploadedId, messageId, originalFileName: "scope.pdf", contentType: "application/pdf",
        sizeBytes: bytes.length, sha256: uploadedSha, status: released ? "Released" : "Quarantined",
        versionNumber: 1, releasedAt: released ? "2026-09-28T00:02:00Z" : null,
      }) }));

  await page.goto(path);
  await page.getByRole("button", { name: "پیوست‌ها" }).click();
  await page.getByLabel("افزودن فایل به پیام خود").setInputFiles({
    name: "scope.pdf", mimeType: "application/pdf", buffer: bytes,
  });
  await expect(page.getByText("در انتظار بررسی و آزادسازی", { exact: false })).toBeVisible();
  await expect(page.getByRole("button", { name: "دریافت پیوست" })).toHaveCount(0);
  await page.reload();
  await page.getByRole("button", { name: "پیوست‌ها" }).click();
  await expect(page.getByText("در انتظار بررسی و آزادسازی", { exact: false })).toBeVisible();
  released = true;
  await page.getByRole("button", { name: "بررسی وضعیت آپلود" }).click();
  await expect(page.getByRole("button", { name: "اتصال پیوست به پیام" })).toBeVisible();
  await page.getByRole("button", { name: "اتصال پیوست به پیام" }).click();
  await expect(page.getByText("پیوست آزادشده به همین پیام متصل شد.")).toBeVisible();
  await expect(page.getByRole("button", { name: "دریافت پیوست" })).toBeVisible();
  await page.reload();
  await page.getByRole("button", { name: "پیوست‌ها" }).click();
  await expect(page.getByText("متصل به پیام", { exact: false })).toBeVisible();
  await expect(page.getByRole("button", { name: "اتصال پیوست به پیام" })).toHaveCount(0);
  canUpload = false;
  await page.reload();
  await page.getByRole("button", { name: "پیوست‌ها" }).click();
  await expect(page.getByLabel("افزودن فایل به پیام خود")).toHaveCount(0);
  canUpload = true;
  revoked = true;
  await page.reload();
  await page.getByRole("button", { name: "پیوست‌ها" }).click();
  await expect(page.getByRole("heading", { name: "دسترسی به گفت‌وگو ندارید" })).toBeVisible();
  await expect(page.getByText("پیام نویسنده")).toHaveCount(0);
});

test("own-message edit keeps the draft on revision conflict and confirms a fresh revision", async ({ page }) => {
  const messageId = "10000000-0000-4000-8000-000000000011";
  let canEditOwn = true;
  let revoked = false;
  let currentBody = "نسخه اولیه پیام";
  let revision = 1;
  let editedAt: string | null = null;
  const keys: string[] = [];
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration`, (route) => route.fulfill({
    status: 200, contentType: "application/json",
    body: JSON.stringify({ projectId, lastSequence: 1, canEditOwn }),
  }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/events\\?`, "u"),
    (route) => route.fulfill({ status: 503 }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/messages\\?after=0$`, "u"),
    (route) => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
      nextSequence: 1, messages: [{ id: messageId, projectId, sequence: 1, authorUserId: userId,
        body: currentBody, revision, editedAt, createdAt: "2026-09-28T00:00:00Z" }],
    }) }));
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration/messages/${messageId}`,
    (route) => {
      expect(route.request().method()).toBe("PATCH");
      const payload = route.request().postDataJSON() as { baseRevision: number; body: string };
      keys.push(route.request().headers()["idempotency-key"]);
      if (revoked) return route.fulfill({ status: 403 });
      if (keys.length === 1) {
        expect(payload.baseRevision).toBe(1);
        currentBody = "ویرایش همزمان دیگر";
        revision = 2;
        return route.fulfill({ status: 409, contentType: "application/json",
          body: JSON.stringify({ currentRevision: 2 }) });
      }
      expect(payload.baseRevision).toBe(2);
      expect(payload.body).toBe("پیش‌نویس من");
      currentBody = payload.body;
      revision = 3;
      editedAt = "2026-09-28T00:05:00Z";
      return route.fulfill({ status: 200, contentType: "application/json",
        body: JSON.stringify({ id: messageId, projectId, authorUserId: userId,
          sequence: 1, createdAt: "2026-09-28T00:00:00Z", body: currentBody,
          revision, editedAt, deletedAt: null, redactedAt: null }) });
    });

  await page.goto(path);
  await page.getByRole("button", { name: "ویرایش پیام" }).click();
  await page.getByLabel("ویرایش پیام خود").fill("پیش‌نویس من");
  await page.getByRole("button", { name: "ثبت ویرایش" }).click();
  await expect(page.getByText("نسخهٔ فعلی: ویرایش همزمان دیگر")).toBeVisible();
  await expect(page.getByLabel("ویرایش پیام خود")).toHaveValue("پیش‌نویس من");
  await page.getByRole("button", { name: "ویرایش دوباره بر پایهٔ نسخهٔ تازه" }).click();
  await page.getByRole("button", { name: "ثبت ویرایش" }).click();
  await expect(page.getByText("ویرایش پیام ثبت شد.")).toBeVisible();
  expect(keys).toHaveLength(2);
  expect(keys[0]).not.toBe(keys[1]);
  await page.reload();
  await expect(page.getByText("پیش‌نویس من")).toBeVisible();
  canEditOwn = false;
  await page.reload();
  await expect(page.getByRole("button", { name: "ویرایش پیام" })).toHaveCount(0);
  canEditOwn = true;
  revoked = true;
  await page.reload();
  await page.getByRole("button", { name: "ویرایش پیام" }).click();
  await page.getByLabel("ویرایش پیام خود").fill("تلاش جدید");
  await page.getByRole("button", { name: "ثبت ویرایش" }).click();
  await expect(page.getByRole("heading", { name: "دسترسی به گفت‌وگو ندارید" })).toBeVisible();
  await expect(page.getByText("پیش‌نویس من")).toHaveCount(0);
});

test("own-message display deletion respects Legal Hold, revision and revoked access", async ({ page }) => {
  const messageId = "10000000-0000-4000-8000-000000000011";
  let legalHold = false;
  let revoked = false;
  let revision = 1;
  let deletedAt: string | null = null;
  let body = "پیام برای حذف";
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration`, (route) => route.fulfill({
    status: 200, contentType: "application/json",
    body: JSON.stringify({ projectId, lastSequence: 1, canEditOwn: true }),
  }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/events\\?`, "u"),
    (route) => route.fulfill({ status: 503 }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/messages\\?after=0$`, "u"),
    (route) => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
      nextSequence: 1, messages: [{ id: messageId, projectId, sequence: 1, authorUserId: userId,
        body, revision, legalHold, deletedAt, createdAt: "2026-09-28T00:00:00Z" }],
    }) }));
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration/messages/${messageId}/delete`,
    (route) => {
      expect(route.request().method()).toBe("POST");
      expect((route.request().postDataJSON() as { baseRevision: number }).baseRevision).toBe(revision);
      if (revoked) return route.fulfill({ status: 403 });
      if (revision === 1) {
        legalHold = true;
        return route.fulfill({ status: 409, contentType: "application/json",
          body: JSON.stringify({ code: "collaboration.message.legal_hold" }) });
      }
      revision += 1;
      deletedAt = "2026-09-28T00:05:00Z";
      body = "پیام حذف شده است";
      return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
        id: messageId, projectId, authorUserId: userId, sequence: 1,
        createdAt: "2026-09-28T00:00:00Z", body, revision, legalHold, deletedAt, redactedAt: null,
      }) });
    });

  await page.goto(path);
  await page.getByRole("button", { name: "حذف نمایشی پیام" }).click();
  await expect(page.getByText("حذف فقط نمایش پیام را برمی‌دارد", { exact: false })).toBeVisible();
  await page.getByRole("button", { name: "تأیید حذف نمایشی" }).click();
  await expect(page.getByText("این پیام تحت Legal Hold است و حذف نمایشی مجاز نیست.")).toBeVisible();
  await expect(page.getByText("تحت نگهداری قانونی")).toBeVisible();
  await expect(page.getByRole("button", { name: "حذف نمایشی پیام" })).toHaveCount(0);
  legalHold = false;
  revision = 2;
  await page.reload();
  await page.getByRole("button", { name: "حذف نمایشی پیام" }).click();
  await page.getByRole("button", { name: "تأیید حذف نمایشی" }).click();
  await expect(page.getByText("پیام از نمایش گروه برداشته شد", { exact: false })).toBeVisible();
  await expect(page.getByText("این پیام دیگر برای نمایش در دسترس نیست.")).toBeVisible();
  await page.reload();
  await expect(page.getByRole("button", { name: "حذف نمایشی پیام" })).toHaveCount(0);
  deletedAt = null;
  body = "پیام بعدی";
  revision = 4;
  await page.reload();
  await page.getByRole("button", { name: "حذف نمایشی پیام" }).click();
  revoked = true;
  await page.getByRole("button", { name: "تأیید حذف نمایشی" }).click();
  await expect(page.getByRole("heading", { name: "دسترسی به گفت‌وگو ندارید" })).toBeVisible();
  await expect(page.getByText("پیام بعدی")).toHaveCount(0);
});

test("moderator records a reason for redaction and Legal Hold with revision recovery", async ({ page }) => {
  const messageId = "10000000-0000-4000-8000-000000000011";
  let canModerate = false;
  let revoked = false;
  let revision = 1;
  let legalHold = false;
  let redactedAt: string | null = null;
  let body = "پیام نیازمند بررسی";
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration`, (route) => route.fulfill({
    status: 200, contentType: "application/json",
    body: JSON.stringify({ projectId, lastSequence: 1, canModerate }),
  }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/events\\?`, "u"),
    (route) => route.fulfill({ status: 503 }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/messages\\?after=0$`, "u"),
    (route) => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
      nextSequence: 1, messages: [{ id: messageId, projectId, sequence: 1, authorUserId: userId,
        body, revision, legalHold, redactedAt, deletedAt: null, pinnedAt: null,
        createdAt: "2026-09-28T00:00:00Z" }],
    }) }));
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration/messages/${messageId}/redact`,
    (route) => {
      expect(route.request().method()).toBe("POST");
      const data = route.request().postDataJSON() as { baseRevision: number; reason: string };
      expect(data.baseRevision).toBe(revision);
      expect(data.reason).toBe("دلیل مستند تعدیل");
      if (revision === 1) {
        revision = 2;
        body = "پیام همزمان تغییر کرد";
        return route.fulfill({ status: 409, contentType: "application/json",
          body: JSON.stringify({ code: "collaboration.message.revision.conflict", currentRevision: 2 }) });
      }
      revision = 3;
      redactedAt = "2026-09-28T00:05:00Z";
      body = "پیام توسط ناظر پنهان شده است";
      return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
        id: messageId, projectId, authorUserId: userId, sequence: 1, body, revision,
        legalHold, redactedAt, deletedAt: null, pinnedAt: null, createdAt: "2026-09-28T00:00:00Z",
      }) });
    });
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration/messages/${messageId}/legal-hold`,
    (route) => {
      expect(route.request().method()).toBe("PUT");
      const data = route.request().postDataJSON() as {
        baseRevision: number; enabled: boolean; reason: string;
      };
      expect(data.baseRevision).toBe(revision);
      expect(data.reason).toBe("نگهداری بررسی");
      if (revoked) return route.fulfill({ status: 403 });
      legalHold = data.enabled;
      revision++;
      return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
        id: messageId, projectId, authorUserId: userId, sequence: 1, body, revision,
        legalHold, redactedAt, deletedAt: null, pinnedAt: null, createdAt: "2026-09-28T00:00:00Z",
      }) });
    });

  await page.goto(path);
  await expect(page.getByRole("button", { name: "پنهان‌سازی با دلیل" })).toHaveCount(0);
  canModerate = true;
  await page.reload();
  await page.getByRole("button", { name: "پنهان‌سازی با دلیل" }).click();
  await page.getByLabel("دلیل پنهان‌سازی پیام").fill("دلیل مستند تعدیل");
  await page.getByRole("button", { name: "تأیید پنهان‌سازی" }).click();
  await expect(page.getByText("نسخهٔ فعلی: پیام همزمان تغییر کرد")).toBeVisible();
  await page.getByRole("button", { name: "تعدیل بر پایهٔ نسخهٔ تازه" }).click();
  await page.getByRole("button", { name: "تأیید پنهان‌سازی" }).click();
  await expect(page.getByText("پیام با دلیل ثبت‌شده پنهان شد", { exact: false })).toBeVisible();
  await expect(page.getByText("این پیام دیگر برای نمایش در دسترس نیست.")).toBeVisible();
  await page.getByRole("button", { name: "اعمال نگهداری قانونی" }).click();
  await page.getByLabel("دلیل اعمال نگهداری قانونی").fill("نگهداری بررسی");
  await page.getByRole("button", { name: "تأیید نگهداری قانونی" }).click();
  await expect(page.getByText("تحت نگهداری قانونی")).toBeVisible();
  await page.reload();
  await expect(page.getByRole("button", { name: "برداشتن نگهداری قانونی" })).toBeVisible();
  await page.getByRole("button", { name: "برداشتن نگهداری قانونی" }).click();
  await page.getByLabel("دلیل برداشتن نگهداری قانونی").fill("نگهداری بررسی");
  revoked = true;
  await page.getByRole("button", { name: "تأیید برداشتن نگهداری" }).click();
  await expect(page.getByRole("heading", { name: "دسترسی به گفت‌وگو ندارید" })).toBeVisible();
  await expect(page.getByText("پیام نیازمند بررسی")).toHaveCount(0);
});

test("private message history opens only for author or moderator and clears on revocation", async ({ page }) => {
  const messageId = "10000000-0000-4000-8000-000000000011";
  const otherUserId = "10000000-0000-4000-8000-000000000098";
  let authorUserId = otherUserId;
  let canModerate = false;
  let revoked = false;
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration`, (route) => route.fulfill({
    status: 200, contentType: "application/json",
    body: JSON.stringify({ projectId, lastSequence: 1, canModerate }),
  }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/events\\?`, "u"),
    (route) => route.fulfill({ status: 503 }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/messages\\?after=0$`, "u"),
    (route) => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
      nextSequence: 1, messages: [{ id: messageId, projectId, sequence: 1, authorUserId,
        body: "متن فعلی", revision: 3, createdAt: "2026-09-28T00:00:00Z" }],
    }) }));
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration/messages/${messageId}/history`,
    (route) => revoked ? route.fulfill({ status: 403 }) : route.fulfill({
      status: 200, contentType: "application/json", body: JSON.stringify({
        messageId, currentRevision: 3,
        revisions: [{ fromRevision: 1, body: "متن پیشین خصوصی", action: "Edited",
          actorUserId: otherUserId, occurredAt: "2026-09-28T00:01:00Z" }],
        moderation: canModerate ? [{ messageRevision: 3, action: "HoldApplied",
          reason: "دلیل خصوصی تعدیل", actorUserId: otherUserId,
          occurredAt: "2026-09-28T00:02:00Z" }] : [],
      }),
    }));

  await page.goto(path);
  await expect(page.getByRole("button", { name: "تاریخچهٔ محدود پیام" })).toHaveCount(0);
  authorUserId = userId;
  await page.reload();
  await page.getByRole("button", { name: "تاریخچهٔ محدود پیام" }).click();
  await expect(page.getByText("متن پیشین خصوصی")).toBeVisible();
  await expect(page.getByText("دلیل خصوصی تعدیل")).toHaveCount(0);
  authorUserId = otherUserId;
  canModerate = true;
  await page.reload();
  await expect(page.getByText("متن پیشین خصوصی")).toHaveCount(0);
  await page.getByRole("button", { name: "تاریخچهٔ محدود پیام" }).click();
  await expect(page.getByText("دلیل: دلیل خصوصی تعدیل")).toBeVisible();
  await page.reload();
  revoked = true;
  await page.getByRole("button", { name: "تاریخچهٔ محدود پیام" }).click();
  await expect(page.getByRole("heading", { name: "دسترسی به گفت‌وگو ندارید" })).toBeVisible();
  await expect(page.getByText("متن فعلی")).toHaveCount(0);
});

test("formal conversion lineage stays behind the room grant and closes on revocation", async ({ page }) => {
  const messageId = "10000000-0000-4000-8000-000000000011";
  let canConvert = false;
  let revoked = false;
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration`, (route) => route.fulfill({
    status: 200, contentType: "application/json",
    body: JSON.stringify({ projectId, lastSequence: 1, canConvert }),
  }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/events\\?`, "u"),
    (route) => route.fulfill({ status: 503 }));
  await page.route(new RegExp(`/api/pmcs/api/v1/projects/${projectId}/collaboration/messages\\?after=0$`, "u"),
    (route) => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({
      nextSequence: 1, messages: [{ id: messageId, projectId, sequence: 1, authorUserId: userId,
        body: "پیام تبدیل‌شده", revision: 2, createdAt: "2026-09-28T00:00:00Z" }],
    }) }));
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration/messages/${messageId}/conversions`,
    (route) => revoked ? route.fulfill({ status: 403 }) : route.fulfill({
      status: 200, contentType: "application/json", body: JSON.stringify([{
        id: "10000000-0000-4000-8000-000000000021", messageId, messageRevision: 2,
        destinationType: "Action", destinationId: "10000000-0000-4000-8000-000000000031",
        destinationReference: "ACT-001", documents: [],
        confirmedBy: userId, confirmedAt: "2026-09-28T00:01:00Z",
      }]),
    }));

  await page.goto(path);
  await expect(page.getByRole("button", { name: "تبدیل‌های رسمی پیام" })).toHaveCount(0);
  canConvert = true;
  await page.reload();
  await page.getByRole("button", { name: "تبدیل‌های رسمی پیام" }).click();
  await expect(page.getByText("اقدام: ACT-001")).toBeVisible();
  await page.reload();
  await expect(page.getByText("اقدام: ACT-001")).toHaveCount(0);
  revoked = true;
  await page.getByRole("button", { name: "تبدیل‌های رسمی پیام" }).click();
  await expect(page.getByRole("heading", { name: "دسترسی به گفت‌وگو ندارید" })).toBeVisible();
  await expect(page.getByText("پیام تبدیل‌شده")).toHaveCount(0);
});

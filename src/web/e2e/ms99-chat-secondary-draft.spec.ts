import { expect, test } from "@playwright/test";
import { projectId, userId } from "./support";
import { chatEvidence } from "./ms99-chat-support";

test("secondary snapshots never survive a failed refresh and file selection survives without implicit upload", async ({ page, context }) => {
  await page.setViewportSize({ width: 320, height: 900 });
  const capture = chatEvidence(page, "secondary");
  const messageId = "99000000-0000-4000-8000-000000000011";
  const documentId = "99000000-0000-4000-8000-000000000012";
  let version = 1;
  let errors = false;
  let waiting = false;
  let reads = 0;
  let uploadSessions = 0;
  let release: () => void = () => {};
  const gate = new Promise<void>(resolve => { release = resolve; });
  const base = `/api/pmcs/api/v1/projects/${projectId}/collaboration`;
  const message = `${base}/messages/${messageId}`;
  await page.route(`**${base}`, route => route.fulfill({ json: { projectId, lastSequence: 1,
    canUpload: true, canConvert: true } }));
  await page.route(new RegExp(`${base}/messages\\?after=0$`, "u"), route => route.fulfill({ json: {
    nextSequence: 1, messages: [{ id: messageId, projectId, sequence: 1, revision: 1,
      authorUserId: userId, body: "پیام دارای پیش‌نویس فایل", createdAt: "2026-09-30T12:00:00Z" }],
  } }));
  await page.route(new RegExp(`${base}/events\\?`, "u"), route => route.fulfill({ status: 503 }));
  await page.route(`**${base}/unread`, route => route.fulfill({ json: { lastReadSequence: 0, unreadCount: 0 } }));
  await page.route("**/api/pmcs/api/v1/documents/upload-sessions", route => {
    uploadSessions++; return route.fulfill({ status: 503 });
  });
  for (const type of ["reactions", "attachments", "conversions"]) await page.route(`**${message}/${type}`, async route => {
    reads++;
    if (waiting) await gate;
    if (errors) return route.fulfill({ status: 503 });
    if (type === "reactions") return route.fulfill({ json: { messageId, canReact: false,
      reactions: [{ emoji: version === 1 ? "👍" : "✅", count: version, reactedByMe: false }] } });
    if (type === "attachments") return route.fulfill({ json: [{ messageId, documentId,
      originalFileName: `نسخه-${version}.pdf`, contentType: "application/pdf", sizeBytes: 10,
      sha256: "a".repeat(64), classification: "Internal", retentionPolicy: "Standard", legalHold: false,
      releasedAt: "2026-09-30T12:00:00Z", versionNumber: version,
      contentUrl: `/api/v1/projects/${projectId}/collaboration/messages/${messageId}/attachments/${documentId}/content` }] });
    return route.fulfill({ json: [{ id: "99000000-0000-4000-8000-000000000013", messageId,
      messageRevision: 1, destinationType: "Action", destinationId: "99000000-0000-4000-8000-000000000014",
      destinationReference: `ACTION-${version}`, documents: [], confirmedBy: userId,
      confirmedAt: "2026-09-30T12:00:00Z" }] });
  });

  await page.goto(`/projects/${projectId}/collaboration`);
  await page.getByRole("button", { name: "واکنش‌ها", exact: true }).click();
  await page.getByRole("button", { name: "پیوست‌ها", exact: true }).click();
  await page.getByRole("button", { name: "تبدیل‌های رسمی پیام", exact: true }).click();
  await expect(page.getByText("ACTION-1", { exact: false })).toBeVisible();
  const selection = page.getByLabel("افزودن فایل به پیام خود");
  await selection.setInputFiles({ name: "نامعتبر.exe", mimeType: "application/octet-stream", buffer: Buffer.from("invalid") });
  await page.getByRole("button", { name: "نگهداری فایل در صف دستگاه" }).click();
  await expect(page.getByText("نامعتبر.exe", { exact: true })).toBeVisible();
  expect(uploadSessions).toBe(0);
  await selection.setInputFiles({ name: "انتخاب-حفظ‌شده.pdf", mimeType: "application/pdf", buffer: Buffer.from("%PDF-1.7\n%%EOF") });
  await capture("chat-320-secondary-current.png", page.getByText("انتخاب-حفظ‌شده.pdf", { exact: true }));
  const previousReads = reads;
  waiting = true;
  await page.getByRole("button", { name: "تازه‌سازی", exact: true }).click();
  await expect.poll(() => reads).toBeGreaterThanOrEqual(previousReads + 3);
  await expect(page.getByText("ACTION-1", { exact: false })).toHaveCount(0);
  await expect(page.getByText("نسخه-1.pdf", { exact: false })).toHaveCount(0);
  await expect(page.getByText("انتخاب-حفظ‌شده.pdf", { exact: true })).toBeVisible();
  await capture("chat-320-secondary-refresh.png", page.getByText("انتخاب-حفظ‌شده.pdf", { exact: true }));
  errors = true; waiting = false; release();
  await expect(page.getByText("دریافت پیوست‌های پیام کامل نشد", { exact: false })).toBeVisible();
  await expect(page.getByText("انتخاب-حفظ‌شده.pdf", { exact: true })).toBeVisible();
  await expect(page.getByText("ACTION-1", { exact: false })).toHaveCount(0);
  await capture("chat-320-secondary-error-draft-retained.png", page.getByText("انتخاب-حفظ‌شده.pdf", { exact: true }));
  errors = false; version = 2;
  await page.getByRole("button", { name: "تازه‌سازی", exact: true }).click();
  await expect(page.getByText("ACTION-2", { exact: false })).toBeVisible();
  await expect(page.getByText("نسخه-2.pdf", { exact: false })).toBeVisible();
  await expect(page.getByRole("button", { name: /✅، ۲ واکنش/u })).toBeVisible();
  await expect(page.getByText("انتخاب-حفظ‌شده.pdf", { exact: true })).toBeVisible();
  await capture("chat-320-secondary-recovered.png", page.getByText("انتخاب-حفظ‌شده.pdf", { exact: true }));
  await context.setOffline(true);
  await expect(page.getByText("ACTION-2", { exact: false })).toHaveCount(0);
  await expect(page.getByText("نسخه-2.pdf", { exact: false })).toHaveCount(0);
  await expect(page.getByRole("button", { name: /✅، ۲ واکنش/u })).toHaveCount(0);
  await expect(selection).toBeEnabled();
  await expect(page.getByText("انتخاب-حفظ‌شده.pdf", { exact: true })).toBeVisible();
  await capture("chat-320-secondary-offline-selection.png", page.getByText("انتخاب-حفظ‌شده.pdf", { exact: true }));
  expect(uploadSessions).toBe(0);
});

import { expect, test } from "@playwright/test";
import { projectId, userId } from "./support";
import { chatEvidence } from "./ms99-chat-support";

test("all six conversions keep drafts but reject commands without a current room, retaining retry identity", async ({ page, context }) => {
  test.setTimeout(240_000);
  await page.setViewportSize({ width: 320, height: 900 });
  const capture = chatEvidence(page, "conversions");
  const messageId = "99000000-0000-4000-8000-000000000001";
  const documentId = "99000000-0000-4000-8000-000000000002";
  const reportId = "99000000-0000-4000-8000-000000000003";
  const alternateReportId = "99000000-0000-4000-8000-000000000005";
  const locationId = "99000000-0000-4000-8000-000000000004";
  let phase: "current" | "waiting" | "error" | "revoked" = "current";
  let reads = 0;
  let revision = 1;
  let release: () => void = () => {};
  const gate = new Promise<void>(resolve => { release = resolve; });
  const posted: Array<{ body: { destinationType: string; destinationId: string }; key: string }> = [];
  const base = `/api/pmcs/api/v1/projects/${projectId}`;
  const message = `${base}/collaboration/messages/${messageId}`;
  await page.route(`**${base}/collaboration`, async route => {
    reads++;
    if (phase === "waiting") await gate;
    if (phase === "error") return route.fulfill({ status: 503 });
    if (phase === "revoked") return route.fulfill({ status: 401 });
    return route.fulfill({ json: { projectId, lastSequence: 1, canConvert: true,
      canConvertAction: true, canConvertIssue: true, canConvertRfi: true,
      canConvertDailyFact: true, canConvertEvidence: true, canConvertTechnicalDocument: true } });
  });
  await page.route(new RegExp(`${base}/collaboration/messages\\?after=0$`, "u"),
    route => route.fulfill({ json: { nextSequence: 1, messages: [{ id: messageId, projectId,
      sequence: 1, revision, authorUserId: userId, body: "مشاهدهٔ مشترک کارگاه",
      createdAt: "2026-09-30T12:00:00Z" }] } }));
  await page.route(new RegExp(`${base}/collaboration/events\\?`, "u"), route => route.fulfill({ status: 503 }));
  await page.route(`**${base}/collaboration/unread`, route => route.fulfill({ json: { lastReadSequence: 0, unreadCount: 0 } }));
  await page.route(`**${message}/attachments`, route => route.fulfill({ json: [{ messageId, documentId,
    originalFileName: "مدرک-کارگاه.pdf", contentType: "application/pdf", sizeBytes: 10,
    sha256: "a".repeat(64), versionNumber: 1, classification: "Internal", retentionPolicy: "Standard",
    legalHold: false, releasedAt: "2026-09-30T12:00:00Z",
    contentUrl: `/api/v1/projects/${projectId}/collaboration/messages/${messageId}/attachments/${documentId}/content` }] }));
  await page.route(`**${message}/conversions`, route => {
    if (route.request().method() === "GET") return route.fulfill({ json: [] });
    posted.push({ body: route.request().postDataJSON(), key: route.request().headers()["idempotency-key"] });
    return route.abort("failed");
  });
  const report = { id: reportId, projectId, reportDate: "2099-01-03", locationName: "کارگاه", status: "Draft", revision: 2, facts: [] };
  const alternateReport = { ...report, id: alternateReportId, reportDate: "2099-01-04" };
  await page.route(`**${base}/daily-reports`, route => route.fulfill({ json: [report, alternateReport] }));
  await page.route(`**${base}/daily-reports/${reportId}`, route => route.fulfill({ json: report }));
  await page.route(`**${base}/daily-reports/${alternateReportId}`, route => route.fulfill({ json: alternateReport }));
  await page.route(`**${base}/locations`, route => route.fulfill({ json: [{ id: locationId,
    projectId, code: "SITE", name: "کارگاه", status: "Active" }] }));

  await page.goto(`/projects/${projectId}/collaboration`);
  await expect(page.getByText("مشاهدهٔ مشترک کارگاه", { exact: true })).toBeVisible();
  const specs = [
    ["اقدام", "ساخت اقدام رسمی از پیام", "تبدیل پیام به اقدام رسمی", "عنوان اقدام"],
    ["مسئله", "ساخت مسئلهٔ رسمی از پیام", "تبدیل پیام به مسئلهٔ رسمی", "عنوان مسئله"],
    ["RFI", "ساخت RFI رسمی از پیام", "تبدیل پیام به RFI رسمی", "عنوان RFI"],
    ["واقعیت", "ساخت واقعیت روزانه از پیام", "تبدیل پیام به واقعیت روزانهٔ رسمی", "شرح واقعیت"],
    ["مدرک", "ساخت مدرک رسمی از فایل پیام", "تبدیل فایل پیام به مدرک رسمی", ""],
    ["سند", "ساخت سند فنی رسمی از فایل پیام", "تبدیل فایل پیام به سند فنی رسمی", "عنوان سند"],
  ] as const;
  const forms = [];
  for (const [label, button, region, field] of specs) {
    await page.getByRole("button", { name: button, exact: true }).click();
    const form = page.locator(`section[aria-label="${region}"]`);
    await expect(form.locator("form")).toBeVisible();
    if (label === "RFI") {
      await form.getByLabel("مخاطب پاسخ").fill("دفتر فنی");
      await form.getByLabel("رشتهٔ فنی").fill("سازه");
    }
    if (label === "سند") await form.getByLabel("رشتهٔ فنی").fill("سازه");
    const confirmation = form.getByRole("checkbox", { name: /تأیید می‌کنم/u });
    await confirmation.check();
    if (field) {
      const input = label === "واقعیت" ? form.locator("textarea").first() : form.getByRole("textbox", { name: field, exact: true });
      await input.fill(`پیش‌نویس حفظ‌شدهٔ ${label}`);
      await expect(confirmation).not.toBeChecked();
      await confirmation.check();
    } else {
      await form.getByRole("combobox", { name: "گزارش روزانه", exact: true }).selectOption(alternateReportId);
      await expect(confirmation).not.toBeChecked();
      await expect(form.getByRole("combobox", { name: "گزارش روزانه", exact: true })).toHaveValue(alternateReportId);
      await confirmation.check();
    }
    forms.push(form);
    await capture(`chat-320-conversion-${forms.length}-reapproved.png`, form);
  }

  await context.setOffline(true);
  await expect(forms[0].locator('button[type="submit"]')).toBeDisabled();
  for (const form of forms) await form.locator("form").dispatchEvent("submit");
  expect(posted).toHaveLength(0);
  await capture("chat-320-offline-command-gate.png", page.getByText("اتصال قطع است", { exact: false }));

  phase = "waiting";
  await context.setOffline(false);
  await expect.poll(() => reads).toBe(2);
  await expect(forms[0]).toBeHidden();
  for (const form of forms) {
    await expect(form.locator('button[type="submit"]')).toBeDisabled();
    await form.locator("form").dispatchEvent("submit");
  }
  expect(posted).toHaveLength(0);
  await capture("chat-320-refresh-command-gate.png", page.getByText("در حال دریافت گفت‌وگوی پروژه…", { exact: true }));
  phase = "error"; release();
  await expect(page.getByRole("heading", { name: "دریافت گفت‌وگو کامل نشد" })).toBeVisible();
  await expect(forms[0]).toBeHidden();
  await expect(forms[0].getByLabel("عنوان اقدام")).toHaveValue("پیش‌نویس حفظ‌شدهٔ اقدام");
  await capture("chat-320-read-error-draft-retained.png", page.getByRole("heading", { name: "دریافت گفت‌وگو کامل نشد" }));
  phase = "current";
  await page.getByRole("button", { name: "تلاش دوباره", exact: true }).click();
  for (const form of forms) await expect(form.locator("form")).toBeVisible();
  await expect(forms[0].getByLabel("عنوان اقدام")).toHaveValue("پیش‌نویس حفظ‌شدهٔ اقدام");
  await capture("chat-320-recovered-drafts.png", forms[0]);
  for (const form of forms) {
    await form.locator('button[type="submit"]').click();
    await expect(form.getByText("نتیجهٔ ارسال قطعی نیست", { exact: false })).toBeVisible();
    await form.locator('button[type="submit"]').click();
  }
  await expect.poll(() => posted.length).toBe(12);
  for (let index = 0; index < posted.length; index += 2) {
    expect(posted[index + 1]).toEqual(posted[index]);
    expect(posted[index].key).toMatch(/^[0-9a-f-]{36}$/u);
  }
  expect(new Set(posted.map(p => p.body.destinationType))).toEqual(new Set(["Action", "Issue", "RFI", "DailyFact", "Evidence", "TechnicalDocument"]));
  revision = 2;
  await page.getByRole("button", { name: "تازه‌سازی", exact: true }).click();
  for (const form of forms) {
    await expect(form.getByText("نسخهٔ پیام تغییر کرده است", { exact: false })).toBeVisible();
    await expect(form.locator('button[type="submit"]')).toBeDisabled();
    await form.locator("form").dispatchEvent("submit");
    await form.getByRole("button", { name: /بر پایهٔ نسخهٔ تازه/u }).click();
    await expect(form.getByRole("checkbox", { name: /تأیید می‌کنم/u })).not.toBeChecked();
  }
  expect(posted).toHaveLength(12);
  await expect(forms[0].getByLabel("عنوان اقدام")).toHaveValue("پیش‌نویس حفظ‌شدهٔ اقدام");
  await capture("chat-320-new-revision-reapproval.png", forms[0]);
  phase = "revoked";
  await page.getByRole("button", { name: "تازه‌سازی", exact: true }).click();
  await expect(page.getByRole("heading", { name: "دسترسی به گفت‌وگو ندارید" })).toBeVisible();
  for (const form of forms) await expect(form).toHaveCount(0);
  await capture("chat-320-401-purged.png", page.getByRole("heading", { name: "دسترسی به گفت‌وگو ندارید" }));
});

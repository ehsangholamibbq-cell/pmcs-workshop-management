import assert from "node:assert/strict";
import test from "node:test";
import {
  apiProblemMessage,
  currencyLabel,
  ensureApiSuccess,
  formatAmountFa,
  toUserMessage,
} from "../lib/localization.ts";

test("English API details are not exposed to the user", () => {
  const message = apiProblemMessage({
    code: "commercial.contract.revision.conflict",
    detail: "The contract has changed on the server.",
  }, 409);

  assert.equal(message, "اطلاعات هم‌زمان تغییر کرده است؛ صفحه را تازه کنید و دوباره تلاش کنید.");
  assert.doesNotMatch(message, /[A-Za-z]/);
});

test("Persian API detail is preserved", () => {
  assert.equal(apiProblemMessage({ detail: "شرح فارسی معتبر" }, 422), "شرح فارسی معتبر");
});

test("HTTP permission failures receive a Persian message", async () => {
  const response = new Response("Forbidden", { status: 403 });
  await assert.rejects(() => ensureApiSuccess(response), /اجازه انجام این عملیات/);
});

test("rate limit code receives a Persian message", () => {
  assert.equal(
    apiProblemMessage({ code: "rate_limit.exceeded", title: "Too many requests." }, 429),
    "تعداد درخواست‌ها زیاد است؛ کمی بعد دوباره تلاش کنید.",
  );
});

test("invitation cleanup race receives a Persian message", () => {
  assert.equal(
    apiProblemMessage({ code: "invitation.cleanup.pending", title: "Cleanup is pending." }, 409),
    "پاک‌سازی حساب ورود قبلی هنوز در حال انجام است؛ پس از تکمیل دوباره تلاش کنید.",
  );
});

test("browser network errors fall back to a Persian message", () => {
  const fallback = "ارتباط با سرور برقرار نشد.";
  assert.equal(toUserMessage(new TypeError("Failed to fetch"), fallback), fallback);
});

test("amounts use Persian digits and a localized currency name", () => {
  assert.equal(currencyLabel("IRR"), "ریال");
  assert.match(formatAmountFa(1250000, "IRR"), /۱٬۲۵۰٬۰۰۰ ریال/);
});

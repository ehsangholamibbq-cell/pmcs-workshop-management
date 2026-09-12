"use client";

import { useEffect } from "react";

export default function ErrorPage({ error, reset }: { readonly error: Error; readonly reset: () => void }) {
  useEffect(() => {
    console.error("خطای پیش‌بینی‌نشده در رابط کاربری", error);
  }, [error]);

  return (
    <main className="system-page">
      <section className="system-message">
        <span>خطای موقت</span>
        <h1>نمایش این بخش ممکن نشد</h1>
        <p>اطلاعات ثبت‌شده شما حذف نشده است. دوباره تلاش کنید و در صورت تکرار، موضوع را به پشتیبانی اطلاع دهید.</p>
        <button type="button" onClick={reset}>تلاش دوباره</button>
      </section>
    </main>
  );
}

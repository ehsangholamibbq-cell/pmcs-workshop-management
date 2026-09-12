"use client";

import { useEffect } from "react";

export default function GlobalError({ error, reset }: { readonly error: Error; readonly reset: () => void }) {
  useEffect(() => {
    console.error("خطای پیش‌بینی‌نشده در پوسته برنامه", error);
  }, [error]);

  return (
    <html lang="fa" dir="rtl">
      <body>
        <main className="system-page">
          <section className="system-message">
            <span>خطای موقت</span>
            <h1>بارگذاری برنامه ممکن نشد</h1>
            <p>داده‌های ذخیره‌شده روی دستگاه محفوظ‌اند. برای بارگذاری دوباره برنامه تلاش کنید.</p>
            <button type="button" onClick={reset}>تلاش دوباره</button>
          </section>
        </main>
      </body>
    </html>
  );
}

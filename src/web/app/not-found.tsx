import Link from "next/link";

export default function NotFound() {
  return (
    <main className="system-page">
      <section className="system-message">
        <span>صفحه پیدا نشد</span>
        <h1>نشانی واردشده در دسترس نیست</h1>
        <p>ممکن است صفحه جابه‌جا شده باشد یا شما به آن دسترسی نداشته باشید.</p>
        <Link href="/">بازگشت به مرکز فرمان پروژه</Link>
      </section>
    </main>
  );
}

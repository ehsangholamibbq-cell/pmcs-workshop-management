"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { BrandMark } from "@/components/brand-mark";
import { PmcsSessionBoundary, SessionBadge, usePmcsSession } from "@/components/pmcs-session";
import { loadProjectConversation, type ProjectConversationView } from "@/lib/collaboration-room";
import { formatPersianDateTime } from "@/lib/persian-date";

export function ProjectCollaboration({ projectId }: { readonly projectId: string }) {
  return <PmcsSessionBoundary><ConversationContent projectId={projectId} /></PmcsSessionBoundary>;
}

function ConversationContent({ projectId }: { readonly projectId: string }) {
  const session = usePmcsSession();
  const [view, setView] = useState<ProjectConversationView | null>(null);
  const [failure, setFailure] = useState("");
  const [refresh, setRefresh] = useState(0);

  useEffect(() => {
    let active = true;
    void loadProjectConversation("/api/pmcs", projectId).then((result) => {
      if (!active) return;
      setView(result);
      setFailure("");
    }).catch(() => {
      if (!active) return;
      setFailure("دریافت گفت‌وگو انجام نشد؛ اتصال را بررسی و دوباره تلاش کنید.");
    });
    return () => { active = false; };
  }, [projectId, refresh]);

  return (
    <main className="app-shell collaboration-shell">
      <aside className="sidebar" aria-label="ناوبری اصلی">
        <BrandMark />
        <nav>
          <Link className="nav-item" href={`/projects/${projectId}`}>مرکز فرمان پروژه</Link>
          <span className="nav-item active" aria-current="page">گفت‌وگوی پروژه</span>
          <Link className="nav-item" href="/portfolio">سبد پروژه‌ها</Link>
        </nav>
        <SessionBadge />
      </aside>
      <section className="workspace collaboration-workspace" aria-labelledby="conversation-title">
        <header className="topbar">
          <div>
            <p className="eyebrow">همکاری در محدودهٔ همین پروژه</p>
            <h1 id="conversation-title">گفت‌وگوی گروهی پروژه</h1>
            <p className="muted">پیام‌ها زمینهٔ همکاری هستند؛ ثبت رسمی فقط با تأیید و مجوز مستقل انجام می‌شود.</p>
          </div>
          <button className="secondary-button" type="button" disabled={!view || view.kind !== "ready"}
            onClick={() => setRefresh((value) => value + 1)}>تازه‌سازی</button>
        </header>
        {failure ? (
          <section className="collaboration-state" role="alert">
            <h2>دریافت گفت‌وگو کامل نشد</h2><p>{failure}</p>
            <button type="button" onClick={() => setRefresh((value) => value + 1)}>تلاش دوباره</button>
          </section>
        ) : !view ? (
          <p className="collaboration-state" role="status">در حال دریافت گفت‌وگوی پروژه…</p>
        ) : view.kind === "unavailable" ? (
          <section className="collaboration-state" role="status">
            <h2>گفت‌وگو در این پروژه در دسترس نیست</h2>
            <p>پس از فعال‌سازی مجاز، پیام‌های همین پروژه در این بخش نمایش داده می‌شوند.</p>
          </section>
        ) : view.kind === "forbidden" ? (
          <section className="collaboration-state" role="alert">
            <h2>دسترسی به گفت‌وگو ندارید</h2><p>عضویت یا مجوز پروژه را با مدیر بررسی کنید.</p>
          </section>
        ) : (
          <section className="collaboration-room" aria-label="پیام‌های اخیر پروژه">
            <p className="collaboration-boundary">آخرین پیام‌های همین پروژه</p>
            {view.messages.length === 0 ? <p className="collaboration-state">هنوز پیامی در این پروژه ثبت نشده است.</p> : (
              <ol className="collaboration-messages">
                {view.messages.map((message) => (
                  <li className="collaboration-message" key={message.id}>
                    <div className="collaboration-message-meta">
                      <strong>{message.authorUserId.toLowerCase() === session.userId.toLowerCase() ? "شما" : "عضو پروژه"}</strong>
                      <time dateTime={message.createdAt}>{formatPersianDateTime(message.createdAt)}</time>
                    </div>
                    <p>{message.deletedAt || message.redactedAt ? "این پیام دیگر برای نمایش در دسترس نیست." : message.body}</p>
                    {message.editedAt && !message.deletedAt && !message.redactedAt && <small>ویرایش‌شده</small>}
                    {message.pinnedAt && <small>سنجاق‌شده</small>}
                  </li>
                ))}
              </ol>
            )}
          </section>
        )}
      </section>
    </main>
  );
}

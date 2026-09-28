"use client";

import Link from "next/link";
import { type FormEvent, useEffect, useState } from "react";
import { BrandMark } from "@/components/brand-mark";
import { PmcsSessionBoundary, SessionBadge, usePmcsSession } from "@/components/pmcs-session";
import { loadProjectConversation, type ProjectConversationView } from "@/lib/collaboration-room";
import { CollaborationAccessError, watchCollaborationEvents } from "@/lib/collaboration-events";
import {
  enqueueCollaborationMessage, listQueuedCollaborationMessages, syncCollaborationMessages,
} from "@/lib/collaboration-offline";
import { formatPersianDateTime } from "@/lib/persian-date";

export function ProjectCollaboration({ projectId }: { readonly projectId: string }) {
  return <PmcsSessionBoundary><ConversationContent projectId={projectId} /></PmcsSessionBoundary>;
}

function ConversationContent({ projectId }: { readonly projectId: string }) {
  const session = usePmcsSession();
  const [view, setView] = useState<ProjectConversationView | null>(null);
  const [failure, setFailure] = useState("");
  const [refresh, setRefresh] = useState(0);
  const [draft, setDraft] = useState("");
  const [pending, setPending] = useState(0);
  const [sending, setSending] = useState(false);
  const [sendStatus, setSendStatus] = useState("");

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

  useEffect(() => {
    if (view?.kind !== "ready") return undefined;
    const controller = new AbortController();
    void watchCollaborationEvents("/api/pmcs", projectId, view.lastSequence,
      () => setRefresh((value) => value + 1), controller.signal).catch((error: unknown) => {
      if (controller.signal.aborted) return;
      if (error instanceof CollaborationAccessError) {
        setView(error.status === 403 ? { kind: "forbidden" } : { kind: "unavailable" });
        setDraft("");
      }
    });
    return () => controller.abort();
  }, [projectId, view]);

  useEffect(() => {
    if (view?.kind !== "ready") return undefined;
    let active = true;
    const recover = async () => {
      try {
        if (navigator.onLine) {
          const result = await syncCollaborationMessages("/api/pmcs", projectId);
          if (active && result.sent > 0) {
            setRefresh((value) => value + 1);
            setSendStatus("پیام‌های صف به گفت‌وگوی پروژه رسیدند.");
          }
        }
        const queue = await listQueuedCollaborationMessages(projectId);
        if (active) setPending(queue.length);
      } catch {
        if (active) setSendStatus("بررسی صف پیام‌ها کامل نشد؛ دوباره تلاش کنید.");
      }
    };
    void recover();
    window.addEventListener("online", recover);
    return () => { active = false; window.removeEventListener("online", recover); };
  }, [projectId, view?.kind]);

  async function submitMessage(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (view?.kind !== "ready" || sending) return;
    setSending(true);
    setSendStatus("");
    try {
      await enqueueCollaborationMessage({
        tenantId: session.tenantId, userId: session.userId, projectId, body: draft,
      });
      setDraft("");
      const result = navigator.onLine
        ? await syncCollaborationMessages("/api/pmcs", projectId)
        : null;
      const queue = await listQueuedCollaborationMessages(projectId);
      setPending(queue.length);
      if (result?.sent) setRefresh((value) => value + 1);
      setSendStatus(queue.length ? "پیام در صف امن دستگاه باقی ماند؛ پس از اتصال دوباره تلاش می‌شود."
        : "پیام در گفت‌وگوی پروژه ثبت شد.");
    } catch {
      setSendStatus("ثبت پیام کامل نشد؛ متن و نشست خود را بررسی کنید.");
    } finally {
      setSending(false);
    }
  }

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
            <form className="collaboration-composer" onSubmit={(event) => void submitMessage(event)}>
              <label htmlFor="collaboration-message-draft">پیام به گروه همین پروژه</label>
              <textarea id="collaboration-message-draft" value={draft} maxLength={4000} rows={3}
                onChange={(event) => setDraft(event.target.value)} placeholder="پیام کاری خود را بنویسید…" />
              <div className="collaboration-composer-actions">
                <span aria-live="polite">{pending ? `${pending.toLocaleString("fa-IR")} پیام در صف ارسال` : sendStatus}</span>
                <button type="submit" disabled={sending || !draft.trim()}>
                  {sending ? "در حال ثبت…" : "ارسال به گروه پروژه"}
                </button>
              </div>
            </form>
          </section>
        )}
      </section>
    </main>
  );
}

"use client";

import Link from "next/link";
import { type ChangeEvent, type FormEvent, useCallback, useEffect, useRef, useState } from "react";
import { BrandMark } from "@/components/brand-mark";
import { SidebarNavigation } from "@/components/sidebar-navigation";
import { PersianDateInput } from "@/components/persian-date-input";
import { ProjectRfiConversion } from "@/components/project-rfi-conversion";
import { ProjectDailyFactConversion } from "@/components/project-daily-fact-conversion";
import { ProjectEvidenceConversion } from "@/components/project-evidence-conversion";
import { ProjectTechnicalDocumentConversion } from "@/components/project-technical-document-conversion";
import { PmcsSessionBoundary, SessionBadge, usePmcsSession } from "@/components/pmcs-session";
import {
  loadProjectConversation, type ProjectConversationMessage, type ProjectConversationView,
} from "@/lib/collaboration-room";
import { CollaborationAccessError, watchCollaborationEvents } from "@/lib/collaboration-events";
import {
  CollaborationActionAlreadyExists, CollaborationActionValidationError,
  CollaborationIssueAlreadyExists, CollaborationIssueValidationError,
  convertProjectMessageToAction, convertProjectMessageToIssue, loadProjectMessageConversions,
  type ProjectActionConversionDetails, type ProjectIssueConversionDetails,
  type ProjectMessageConversionLineage,
} from "@/lib/collaboration-conversions";
import {
  attachProjectChatDocument, downloadProjectMessageAttachment, loadProjectMessageAttachments,
  loadProjectChatUploadState, type ProjectChatUploadState, type ProjectMessageAttachment,
} from "@/lib/collaboration-attachments";
import {
  enqueueDocumentUpload, listProjectChatDocumentUploads, recoverInterruptedDocumentUploads,
  syncPendingDocumentUploads, type QueuedDocumentUpload,
} from "@/lib/document-upload-queue";
import {
  loadProjectConversationUnread, loadProjectMessageReactions, markProjectConversationRead,
  searchProjectConversation, setProjectMessagePin, setProjectMessageReaction,
  editOwnProjectMessage, CollaborationRevisionConflict,
  deleteOwnProjectMessage, CollaborationLegalHoldError,
  redactProjectMessage, setProjectMessageLegalHold,
  loadProjectMessageHistory, type ProjectMessageHistory,
  type ProjectConversationUnread, type ProjectMessageReactionsView, type ProjectReactionEmoji,
} from "@/lib/collaboration-interactions";
import {
  enqueueCollaborationMessage, listQueuedCollaborationMessages, syncCollaborationMessages,
} from "@/lib/collaboration-offline";
import { formatPersianDateTime, futureProjectDate } from "@/lib/persian-date";

export function ProjectCollaboration({ projectId }: { readonly projectId: string }) {
  return <PmcsSessionBoundary><ConversationContent key={projectId} projectId={projectId} /></PmcsSessionBoundary>;
}

function ConversationContent({ projectId }: { readonly projectId: string }) {
  const session = usePmcsSession();
  const [view, setView] = useState<ProjectConversationView | null>(null);
  const [failure, setFailure] = useState("");
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [refresh, setRefresh] = useState(0);
  const [draft, setDraft] = useState("");
  const [pending, setPending] = useState(0);
  const [sending, setSending] = useState(false);
  const [sendStatus, setSendStatus] = useState("");
  const [replyTo, setReplyTo] = useState<ProjectConversationMessage | null>(null);
  const [searchQuery, setSearchQuery] = useState("");
  const [searchResults, setSearchResults] = useState<readonly ProjectConversationMessage[] | null>(null);
  const [searchStatus, setSearchStatus] = useState("");
  const [searchError, setSearchError] = useState(false);
  const [searchBusy, setSearchBusy] = useState(false);
  const searchRequest = useRef(0);
  const [unread, setUnread] = useState<ProjectConversationUnread | null>(null);
  const [pinningMessageId, setPinningMessageId] = useState<string | null>(null);
  const [pinStatus, setPinStatus] = useState("");
  const [editing, setEditing] = useState<{
    readonly base: ProjectConversationMessage; readonly body: string;
    readonly key: string; readonly conflict: boolean;
  } | null>(null);
  const [editBusy, setEditBusy] = useState(false);
  const [editStatus, setEditStatus] = useState("");
  const [deleting, setDeleting] = useState<{
    readonly base: ProjectConversationMessage; readonly key: string; readonly conflict: boolean;
  } | null>(null);
  const [deleteBusy, setDeleteBusy] = useState(false);
  const [deleteStatus, setDeleteStatus] = useState("");
  const [moderating, setModerating] = useState<{
    readonly base: ProjectConversationMessage; readonly action: "redact" | "hold";
    readonly enabled: boolean; readonly reason: string; readonly key: string;
    readonly conflict: boolean;
  } | null>(null);
  const [moderationBusy, setModerationBusy] = useState(false);
  const [moderationStatus, setModerationStatus] = useState("");
  const [history, setHistory] = useState<ProjectMessageHistory | null>(null);
  const [historyBusyId, setHistoryBusyId] = useState<string | null>(null);
  const [historyStatus, setHistoryStatus] = useState("");
  const historyController = useRef<AbortController | null>(null);
  const accessRevoked = useRef(false);

  const requestRefresh = useCallback((clearView = false) => {
    searchRequest.current += 1;
    setIsRefreshing(true);
    if (clearView) setView(null);
    setFailure("");
    setSearchResults(null);
    setSearchStatus("");
    setSearchError(false);
    setSearchBusy(false);
    setUnread(null);
    setRefresh((value) => value + 1);
  }, []);

  const closeRestrictedConversation = useCallback((status: number) => {
    accessRevoked.current = true;
    historyController.current?.abort();
    historyController.current = null;
    searchRequest.current += 1;
    setView(status === 403 ? { kind: "forbidden" } : { kind: "unavailable" });
    setIsRefreshing(false);
    setFailure("");
    setDraft("");
    setReplyTo(null);
    setSearchResults(null);
    setSearchStatus("");
    setSearchError(false);
    setSearchBusy(false);
    setUnread(null);
    setPinStatus("");
    setEditing(null);
    setEditStatus("");
    setDeleting(null);
    setDeleteStatus("");
    setModerating(null);
    setModerationStatus("");
    setHistory(null);
    setHistoryBusyId(null);
    setHistoryStatus("");
  }, []);

  useEffect(() => {
    let active = true;
    accessRevoked.current = false;
    historyController.current?.abort();
    historyController.current = null;
    void loadProjectConversation("/api/pmcs", projectId).then((result) => {
      if (!active || accessRevoked.current) return;
      setSearchResults(null);
      setSearchStatus("");
      setHistory(null);
      setHistoryBusyId(null);
      setHistoryStatus("");
      if (result.kind !== "ready") {
        setDraft("");
        setReplyTo(null);
        setSearchResults(null);
        setEditing(null);
        setEditStatus("");
        setDeleting(null);
        setDeleteStatus("");
        setModerating(null);
        setModerationStatus("");
      }
      setView(result);
      setIsRefreshing(false);
      setFailure("");
    }).catch(() => {
      if (!active || accessRevoked.current) return;
      setIsRefreshing(false);
      setFailure("دریافت گفت‌وگو انجام نشد؛ اتصال را بررسی و دوباره تلاش کنید.");
    });
    return () => { active = false; };
  }, [projectId, refresh]);

  useEffect(() => {
    if (view?.kind !== "ready") return undefined;
    const controller = new AbortController();
    void watchCollaborationEvents("/api/pmcs", projectId, view.lastSequence,
      () => requestRefresh(), controller.signal).catch((error: unknown) => {
      if (controller.signal.aborted) return;
      if (error instanceof CollaborationAccessError) {
        closeRestrictedConversation(error.status);
      }
    });
    return () => controller.abort();
  }, [projectId, view, closeRestrictedConversation, requestRefresh]);

  useEffect(() => {
    if (view?.kind !== "ready") return undefined;
    let active = true;
    const recover = async () => {
      try {
        if (navigator.onLine) {
          const result = await syncCollaborationMessages("/api/pmcs", projectId);
          if (active && result.sent > 0) {
            requestRefresh();
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
  }, [projectId, view?.kind, requestRefresh]);

  useEffect(() => {
    if (view?.kind !== "ready") return undefined;
    let active = true;
    void loadProjectConversationUnread("/api/pmcs", projectId).then((result) => {
      if (active) setUnread(result);
    }).catch((error: unknown) => {
      if (active && error instanceof CollaborationAccessError) {
        closeRestrictedConversation(error.status);
      }
    });
    return () => { active = false; };
  }, [projectId, view?.kind, refresh, closeRestrictedConversation]);

  async function submitSearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (view?.kind !== "ready" || searchBusy) return;
    const request = ++searchRequest.current;
    setSearchResults(null);
    setSearchStatus("");
    setSearchError(false);
    setSearchBusy(true);
    try {
      const results = await searchProjectConversation("/api/pmcs", projectId, searchQuery);
      if (request !== searchRequest.current) return;
      setSearchResults(results);
      setSearchStatus(results.length ? "نتیجه‌های همین پروژه" : "پیامی با این عبارت در پروژه پیدا نشد.");
    } catch (error) {
      if (request !== searchRequest.current) return;
      if (error instanceof CollaborationAccessError) {
        closeRestrictedConversation(error.status);
      } else {
        setSearchStatus(error instanceof Error ? error.message : "جست‌وجو کامل نشد.");
        setSearchError(true);
      }
    } finally {
      if (request === searchRequest.current) setSearchBusy(false);
    }
  }

  async function markRead() {
    if (view?.kind !== "ready") return;
    try {
      const cursor = await markProjectConversationRead("/api/pmcs", projectId, view.lastSequence);
      setUnread({ lastReadSequence: cursor, unreadCount: 0 });
    } catch (error) {
      if (error instanceof CollaborationAccessError) {
        closeRestrictedConversation(error.status);
      } else {
        setSendStatus("ثبت نشانگر خواندن انجام نشد؛ دوباره تلاش کنید.");
      }
    }
  }

  async function submitMessage(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (view?.kind !== "ready" || sending) return;
    setSending(true);
    setSendStatus("");
    try {
      await enqueueCollaborationMessage({
        tenantId: session.tenantId, userId: session.userId, projectId, body: draft,
        replyToMessageId: replyTo?.id ?? null,
      });
      setDraft("");
      setReplyTo(null);
      const result = navigator.onLine
        ? await syncCollaborationMessages("/api/pmcs", projectId)
        : null;
      const queue = await listQueuedCollaborationMessages(projectId);
      setPending(queue.length);
      if (result?.sent) requestRefresh();
      setSendStatus(queue.length ? "پیام در صف امن دستگاه باقی ماند؛ پس از اتصال دوباره تلاش می‌شود."
        : "پیام در گفت‌وگوی پروژه ثبت شد.");
    } catch {
      setSendStatus("ثبت پیام کامل نشد؛ متن و نشست خود را بررسی کنید.");
    } finally {
      setSending(false);
    }
  }

  async function togglePin(message: ProjectConversationMessage) {
    if (view?.kind !== "ready" || !view.canModerate || pinningMessageId) return;
    setPinningMessageId(message.id);
    setPinStatus("");
    const pin = !message.pinnedAt;
    try {
      const pinnedAt = await setProjectMessagePin("/api/pmcs", projectId, message.id, pin);
      setView((current) => current?.kind === "ready" ? {
        ...current,
        messages: current.messages.map((item) => item.id === message.id ? { ...item, pinnedAt } : item),
      } : current);
      setPinStatus(pin ? "پیام برای اعضای پروژه سنجاق شد." : "سنجاق پیام برداشته شد.");
      requestRefresh();
    } catch (error) {
      if (error instanceof CollaborationAccessError) closeRestrictedConversation(error.status);
      else setPinStatus("تغییر سنجاق پیام انجام نشد؛ دوباره تلاش کنید.");
    } finally {
      setPinningMessageId(null);
    }
  }

  async function submitEdit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (view?.kind !== "ready" || !view.canEditOwn || !editing || editing.conflict || editBusy) return;
    setEditBusy(true);
    setEditStatus("");
    try {
      const result = await editOwnProjectMessage("/api/pmcs", projectId,
        editing.base, editing.body, editing.key);
      setView((current) => current?.kind === "ready" ? {
        ...current, messages: current.messages.map((item) => item.id === result.id ? result : item),
      } : current);
      setEditing(null);
      setEditStatus("ویرایش پیام ثبت شد.");
      requestRefresh();
    } catch (failure) {
      if (failure instanceof CollaborationAccessError) closeRestrictedConversation(failure.status);
      else if (failure instanceof CollaborationRevisionConflict) {
        setEditing((current) => current ? { ...current, conflict: true } : null);
        setEditStatus("پیام همزمان تغییر کرده است؛ نسخهٔ تازه را ببینید، سپس دربارهٔ پیش‌نویس تصمیم بگیرید.");
        requestRefresh();
      } else setEditStatus(failure instanceof Error ? failure.message : "ویرایش پیام کامل نشد.");
    } finally { setEditBusy(false); }
  }

  async function submitDelete(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (view?.kind !== "ready" || !view.canEditOwn || !deleting || deleting.conflict || deleteBusy) return;
    setDeleteBusy(true);
    setDeleteStatus("");
    try {
      const result = await deleteOwnProjectMessage("/api/pmcs", projectId,
        deleting.base, deleting.key);
      setView((current) => current?.kind === "ready" ? {
        ...current, messages: current.messages.map((item) => item.id === result.id ? result : item),
      } : current);
      setDeleting(null);
      setDeleteStatus("پیام از نمایش گروه برداشته شد؛ سوابق و نگهداری سازمانی حفظ می‌شوند.");
      setReplyTo((current) => current?.id === result.id ? null : current);
      setSearchResults(null);
      requestRefresh();
    } catch (failure) {
      if (failure instanceof CollaborationAccessError) closeRestrictedConversation(failure.status);
      else if (failure instanceof CollaborationRevisionConflict) {
        setDeleting((current) => current ? { ...current, conflict: true } : null);
        setDeleteStatus("نسخهٔ پیام تغییر کرده است؛ پیام تازه را بخوانید و حذف را دوباره تأیید کنید.");
        requestRefresh();
      } else if (failure instanceof CollaborationLegalHoldError) {
        setDeleting(null);
        setDeleteStatus(failure.message);
        requestRefresh();
      } else setDeleteStatus(failure instanceof Error ? failure.message : "حذف پیام کامل نشد.");
    } finally { setDeleteBusy(false); }
  }

  async function submitModeration(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (view?.kind !== "ready" || !view.canModerate || !moderating ||
        moderating.conflict || moderationBusy) return;
    setModerationBusy(true);
    setModerationStatus("");
    try {
      const result = moderating.action === "redact"
        ? await redactProjectMessage("/api/pmcs", projectId, moderating.base,
          moderating.reason, moderating.key)
        : await setProjectMessageLegalHold("/api/pmcs", projectId, moderating.base,
          moderating.enabled, moderating.reason, moderating.key);
      setView((current) => current?.kind === "ready" ? {
        ...current, messages: current.messages.map((item) => item.id === result.id ? result : item),
      } : current);
      setModerating(null);
      setModerationStatus(moderating.action === "redact" ? "پیام با دلیل ثبت‌شده پنهان شد؛ سوابق حفظ می‌شوند."
        : moderating.enabled ? "نگهداری قانونی پیام ثبت شد." : "نگهداری قانونی پیام برداشته شد.");
      if (moderating.action === "redact") {
        setReplyTo((current) => current?.id === result.id ? null : current);
        setSearchResults(null);
      }
      requestRefresh();
    } catch (error) {
      if (error instanceof CollaborationAccessError) closeRestrictedConversation(error.status);
      else if (error instanceof CollaborationRevisionConflict) {
        setModerating((current) => current ? { ...current, conflict: true } : null);
        setModerationStatus("نسخهٔ پیام تغییر کرده است؛ پیام تازه را بخوانید و تعدیل را دوباره تأیید کنید.");
        requestRefresh();
      } else setModerationStatus(error instanceof Error ? error.message : "تعدیل پیام کامل نشد.");
    } finally { setModerationBusy(false); }
  }

  async function openHistory(message: ProjectConversationMessage) {
    if (view?.kind !== "ready" ||
        !(view.canModerate || message.authorUserId.toLowerCase() === session.userId.toLowerCase())) return;
    historyController.current?.abort();
    if (history?.messageId === message.id) {
      historyController.current = null;
      setHistory(null);
      setHistoryBusyId(null);
      setHistoryStatus("");
      return;
    }
    const controller = new AbortController();
    historyController.current = controller;
    setHistory(null);
    setHistoryStatus("");
    setHistoryBusyId(message.id);
    try {
      const result = await loadProjectMessageHistory("/api/pmcs", projectId, message.id, controller.signal);
      if (controller.signal.aborted) return;
      setHistory(result);
    } catch (error) {
      if (controller.signal.aborted) return;
      if (error instanceof CollaborationAccessError) closeRestrictedConversation(error.status);
      else setHistoryStatus(error instanceof Error ? error.message : "دریافت تاریخچهٔ پیام کامل نشد.");
    } finally {
      if (!controller.signal.aborted) setHistoryBusyId(null);
    }
  }

  return (
    <main className="app-shell collaboration-shell">
      <aside className="sidebar disclosure-sidebar" aria-label="ناوبری اصلی">
        <BrandMark />
        <SidebarNavigation label="بخش‌های گفت‌وگوی پروژه">
          <Link className="nav-item" href={`/projects/${projectId}`}>مرکز فرمان پروژه</Link>
          <span className="nav-item active" aria-current="page">گفت‌وگوی پروژه</span>
          <Link className="nav-item" href={`/projects/${projectId}/reports`}>مرکز گزارش‌ها</Link>
          <Link className="nav-item" href="/portfolio">سبد پروژه‌ها</Link>
        </SidebarNavigation>
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
            onClick={() => requestRefresh(true)}>تازه‌سازی</button>
        </header>
        {failure ? (
          <section className="collaboration-state" role="alert">
            <h2>دریافت گفت‌وگو کامل نشد</h2><p>{failure}</p>
            <button type="button" onClick={() => requestRefresh()}>تلاش دوباره</button>
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
        ) : (<>
          {isRefreshing && <p className="collaboration-state" role="status">در حال دریافت گفت‌وگوی پروژه…</p>}
          <section className="collaboration-room" aria-label="پیام‌های اخیر پروژه" hidden={isRefreshing}>
            <div className="collaboration-room-heading">
              <p className="collaboration-boundary">آخرین پیام‌های همین پروژه</p>
              <div className="collaboration-read-state">
                <span aria-live="polite">{unread ? `${unread.unreadCount.toLocaleString("fa-IR")} پیام خوانده‌نشده` : ""}</span>
                <button className="secondary-button" type="button" onClick={() => void markRead()}
                  disabled={!unread || unread.unreadCount === 0}>تا اینجا خواندم</button>
              </div>
            </div>
            {pinStatus && <p role="status">{pinStatus}</p>}
            {editStatus && <p role={editing?.conflict ? "alert" : "status"}>{editStatus}</p>}
            {deleteStatus && <p role={deleting?.conflict ? "alert" : "status"}>{deleteStatus}</p>}
            {moderationStatus && <p role={moderating?.conflict ? "alert" : "status"}>{moderationStatus}</p>}
            {historyStatus && <p role="alert">{historyStatus}</p>}
            <form className="collaboration-search" onSubmit={(event) => void submitSearch(event)} role="search">
              <label htmlFor="collaboration-search-query">جست‌وجو در پیام‌های همین پروژه</label>
              <div><input id="collaboration-search-query" value={searchQuery} maxLength={120}
                onChange={(event) => {
                  searchRequest.current += 1;
                  setSearchQuery(event.target.value);
                  setSearchResults(null);
                  setSearchStatus("");
                  setSearchError(false);
                  setSearchBusy(false);
                }} />
                <button className="secondary-button" type="submit" disabled={searchBusy || searchQuery.trim().length < 2}>
                  {searchBusy ? "در حال جست‌وجو…" : "جست‌وجو"}</button></div>
            </form>
            {searchResults && <section className="collaboration-search-results" aria-label="نتیجه‌های جست‌وجوی پروژه">
              <div className="collaboration-search-title"><strong>{searchStatus}</strong>
                <button className="secondary-button" type="button" onClick={() => {
                  setSearchResults(null); setSearchStatus("");
                }}>بستن نتایج</button></div>
              <ul>{searchResults.map((message) => <li key={message.id}>{message.body}</li>)}</ul>
            </section>}
            {!searchResults && searchStatus && <p role={searchError ? "alert" : "status"}>{searchStatus}</p>}
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
                    {message.legalHold && <small>تحت نگهداری قانونی</small>}
                    {editing?.base.id === message.id && <form className="collaboration-edit"
                      onSubmit={(event) => void submitEdit(event)}>
                      <label htmlFor={`edit-${message.id}`}>ویرایش پیام خود</label>
                      <textarea id={`edit-${message.id}`} rows={3} maxLength={4000}
                        value={editing.body} onChange={(event) => setEditing((current) => current
                          ? { ...current, body: event.target.value } : null)} />
                      {editing.conflict && <p>نسخهٔ فعلی: {message.body}</p>}
                      <div className="collaboration-message-actions">
                        {editing.conflict && message.revision > editing.base.revision &&
                          <button className="secondary-button" type="button" onClick={() => {
                            setEditing({ ...editing, base: message, key: crypto.randomUUID(), conflict: false });
                            setEditStatus("");
                          }}>ویرایش دوباره بر پایهٔ نسخهٔ تازه</button>}
                        <button type="submit" disabled={editBusy || editing.conflict ||
                          !editing.body.trim() || editing.body.trim() === editing.base.body}>
                          {editBusy ? "در حال ثبت…" : "ثبت ویرایش"}
                        </button>
                        <button className="secondary-button" type="button"
                          onClick={() => { setEditing(null); setEditStatus(""); }}>لغو ویرایش</button>
                      </div>
                    </form>}
                    {deleting?.base.id === message.id && !message.deletedAt && !message.redactedAt &&
                      <form className="collaboration-edit" onSubmit={(event) => void submitDelete(event)}>
                        <p>حذف فقط نمایش پیام را برمی‌دارد؛ ردپاهای ممیزی، پیوست و سوابق طبق سیاست نگهداری باقی می‌مانند.</p>
                        {deleting.conflict && <p>نسخهٔ فعلی: {message.body}</p>}
                        <div className="collaboration-message-actions">
                          {deleting.conflict && message.revision > deleting.base.revision && !message.legalHold &&
                            <button className="secondary-button" type="button" onClick={() => {
                              setDeleting({ base: message, key: crypto.randomUUID(), conflict: false });
                              setDeleteStatus("");
                            }}>حذف بر پایهٔ نسخهٔ تازه</button>}
                          <button type="submit" disabled={deleteBusy || deleting.conflict || message.legalHold}>
                            {deleteBusy ? "در حال ثبت…" : "تأیید حذف نمایشی"}
                          </button>
                          <button className="secondary-button" type="button" onClick={() => {
                            setDeleting(null); setDeleteStatus("");
                          }}>انصراف از حذف</button>
                        </div>
                      </form>}
                    {moderating?.base.id === message.id &&
                      <form className="collaboration-edit" onSubmit={(event) => void submitModeration(event)}>
                        <label htmlFor={`moderation-${message.id}`}>
                          {moderating.action === "redact" ? "دلیل پنهان‌سازی پیام" :
                            moderating.enabled ? "دلیل اعمال نگهداری قانونی" : "دلیل برداشتن نگهداری قانونی"}
                        </label>
                        <textarea id={`moderation-${message.id}`} rows={2} maxLength={500}
                          value={moderating.reason} onChange={(event) => setModerating((current) => current
                            ? { ...current, reason: event.target.value } : null)} />
                        <p>دلیل در سابقهٔ محدود تعدیل ثبت می‌شود؛ متن و ردپاهای ممیزی حفظ می‌شوند.</p>
                        {moderating.conflict && <p>نسخهٔ فعلی: {message.deletedAt || message.redactedAt
                          ? "پیام دیگر قابل نمایش نیست." : message.body}</p>}
                        <div className="collaboration-message-actions">
                          {moderating.conflict && message.revision > moderating.base.revision &&
                            (moderating.action === "redact" ? !message.deletedAt && !message.redactedAt :
                              message.legalHold !== moderating.enabled) &&
                            <button className="secondary-button" type="button" onClick={() => {
                              setModerating({ ...moderating, base: message, key: crypto.randomUUID(), conflict: false });
                              setModerationStatus("");
                            }}>تعدیل بر پایهٔ نسخهٔ تازه</button>}
                          <button type="submit" disabled={moderationBusy || moderating.conflict ||
                            !moderating.reason.trim() || (moderating.action === "redact" &&
                              Boolean(message.deletedAt || message.redactedAt)) ||
                            (moderating.action === "hold" && message.legalHold === moderating.enabled)}>
                            {moderationBusy ? "در حال ثبت…" : moderating.action === "redact"
                              ? "تأیید پنهان‌سازی" : moderating.enabled
                                ? "تأیید نگهداری قانونی" : "تأیید برداشتن نگهداری"}
                          </button>
                          <button className="secondary-button" type="button" onClick={() => {
                            setModerating(null); setModerationStatus("");
                          }}>انصراف از تعدیل</button>
                        </div>
                      </form>}
                    {!message.deletedAt && !message.redactedAt &&
                      <ProjectMessageReactions key={`${message.id}-${refresh}`} projectId={projectId} messageId={message.id}
                        refreshToken={refresh} onAccessLoss={closeRestrictedConversation} />}
                    {!message.deletedAt && !message.redactedAt &&
                      <ProjectMessageAttachments key={`${message.id}-${refresh}`} projectId={projectId} messageId={message.id}
                        canUpload={view.canUpload && message.authorUserId.toLowerCase() === session.userId.toLowerCase()}
                        refreshToken={refresh} onAccessLoss={closeRestrictedConversation} />}
                    {!message.deletedAt && !message.redactedAt && <div className="collaboration-message-actions">
                      <button className="secondary-button" type="button" onClick={() => setReplyTo(message)}>
                        پاسخ به پیام
                      </button>
                      {view.canEditOwn && message.revision > 0 &&
                        message.authorUserId.toLowerCase() === session.userId.toLowerCase() &&
                        <button className="secondary-button" type="button" disabled={editBusy}
                          onClick={() => { setEditing({ base: message, body: message.body,
                            key: crypto.randomUUID(), conflict: false }); setEditStatus(""); }}>
                          ویرایش پیام
                        </button>}
                      {view.canEditOwn && message.revision > 0 && !message.legalHold &&
                        message.authorUserId.toLowerCase() === session.userId.toLowerCase() &&
                        <button className="secondary-button" type="button" disabled={deleteBusy}
                          onClick={() => { setDeleting({ base: message, key: crypto.randomUUID(), conflict: false });
                            setDeleteStatus(""); }}>حذف نمایشی پیام</button>}
                      {view.canModerate && <button className="secondary-button collaboration-pin" type="button"
                        aria-pressed={Boolean(message.pinnedAt)} disabled={pinningMessageId !== null}
                        onClick={() => void togglePin(message)}>
                        {pinningMessageId === message.id ? "در حال ثبت…" : message.pinnedAt ? "برداشتن سنجاق" : "سنجاق پیام"}
                      </button>}
                      {view.canModerate && message.revision > 0 &&
                        <button className="secondary-button" type="button" disabled={moderationBusy}
                          onClick={() => { setModerating({ base: message, action: "redact", enabled: false,
                            reason: "", key: crypto.randomUUID(), conflict: false }); setModerationStatus(""); }}>
                          پنهان‌سازی با دلیل
                        </button>}
                    </div>}
                    {view.canModerate && message.revision > 0 &&
                      <div className="collaboration-message-actions">
                        <button className="secondary-button" type="button" disabled={moderationBusy}
                          onClick={() => { setModerating({ base: message, action: "hold",
                            enabled: !message.legalHold, reason: "", key: crypto.randomUUID(),
                            conflict: false }); setModerationStatus(""); }}>
                          {message.legalHold ? "برداشتن نگهداری قانونی" : "اعمال نگهداری قانونی"}
                        </button>
                      </div>}
                    {(view.canModerate || message.authorUserId.toLowerCase() === session.userId.toLowerCase()) &&
                      <div className="collaboration-message-actions">
                        <button className="secondary-button" type="button" disabled={historyBusyId !== null}
                          aria-expanded={history?.messageId === message.id}
                          onClick={() => void openHistory(message)}>
                          {historyBusyId === message.id ? "در حال دریافت تاریخچه…" :
                            history?.messageId === message.id ? "بستن تاریخچه" : "تاریخچهٔ محدود پیام"}
                        </button>
                      </div>}
                    {history?.messageId === message.id &&
                      <section className="collaboration-edit" aria-label="تاریخچهٔ محدود پیام">
                        <h3>سوابق نسخه‌های پیام</h3>
                        {history.revisions.length === 0 ? <p>ویرایش یا حذف پیشین ثبت نشده است.</p> :
                          <ol>{history.revisions.map((item) => <li key={`${item.fromRevision}-${item.action}`}>
                            <strong>{item.action === "Edited" ? "ویرایش" : item.action === "Deleted"
                              ? "حذف نمایشی" : "پنهان‌سازی"}</strong>
                            <span> · نسخهٔ {item.fromRevision.toLocaleString("fa-IR")} · </span>
                            <time dateTime={item.occurredAt}>{formatPersianDateTime(item.occurredAt)}</time>
                            <p>متن پیشین: {item.body}</p>
                          </li>)}</ol>}
                        {view.canModerate && <>
                          <h3>تصمیم‌های تعدیل</h3>
                          {history.moderation.length === 0 ? <p>تصمیم تعدیلی ثبت نشده است.</p> :
                            <ol>{history.moderation.map((item, index) => <li key={`${item.messageRevision}-${index}`}>
                              <strong>{item.action === "Redacted" ? "پنهان‌سازی" :
                                item.action === "HoldApplied" ? "اعمال نگهداری قانونی" :
                                  "برداشتن نگهداری قانونی"}</strong>
                              <span> · نسخهٔ {item.messageRevision.toLocaleString("fa-IR")} · </span>
                              <time dateTime={item.occurredAt}>{formatPersianDateTime(item.occurredAt)}</time>
                              <p>دلیل: {item.reason}</p>
                            </li>)}</ol>}
                        </>}
                      </section>}
                    {view.canConvert && <ProjectMessageConversions key={`${message.id}-${refresh}`} projectId={projectId}
                      messageId={message.id} refreshToken={refresh}
                      onAccessLoss={closeRestrictedConversation} />}
                    {view.canConvertAction && !message.deletedAt && !message.redactedAt &&
                      <ProjectActionConversion projectId={projectId} message={message}
                        actorUserId={session.userId} onAccessLoss={closeRestrictedConversation}
                        onChanged={() => requestRefresh()} />}
                    {view.canConvertIssue && !message.deletedAt && !message.redactedAt &&
                      <ProjectIssueConversion projectId={projectId} message={message}
                        actorUserId={session.userId} onAccessLoss={closeRestrictedConversation}
                        onChanged={() => requestRefresh()} />}
                    {view.canConvertRfi && !message.deletedAt && !message.redactedAt &&
                      <ProjectRfiConversion projectId={projectId} message={message}
                        actorUserId={session.userId} onAccessLoss={closeRestrictedConversation}
                        onChanged={() => requestRefresh()} />}
                    {view.canConvertDailyFact && !message.deletedAt && !message.redactedAt &&
                      <ProjectDailyFactConversion projectId={projectId} message={message}
                        tenantId={session.tenantId} actorUserId={session.userId}
                        onAccessLoss={closeRestrictedConversation}
                        onChanged={() => requestRefresh()} />}
                    {view.canConvertEvidence && !message.deletedAt && !message.redactedAt &&
                      <ProjectEvidenceConversion projectId={projectId} message={message}
                        tenantId={session.tenantId} actorUserId={session.userId}
                        onAccessLoss={closeRestrictedConversation}
                        onChanged={() => requestRefresh()} />}
                    {view.canConvertTechnicalDocument && !message.deletedAt && !message.redactedAt &&
                      <ProjectTechnicalDocumentConversion projectId={projectId} message={message}
                        actorUserId={session.userId} onAccessLoss={closeRestrictedConversation}
                        onChanged={() => requestRefresh()} />}
                  </li>
                ))}
              </ol>
            )}
            <form className="collaboration-composer" onSubmit={(event) => void submitMessage(event)}>
              {replyTo && <div className="collaboration-reply-preview">
                <span>در پاسخ به: {replyTo.body}</span>
                <button className="secondary-button" type="button" onClick={() => setReplyTo(null)}>لغو پاسخ</button>
              </div>}
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
          </>
        )}
      </section>
    </main>
  );
}

const conversionLabels: Record<ProjectMessageConversionLineage["destinationType"], string> = {
  Action: "اقدام", Issue: "مسئله", RFI: "درخواست اطلاعات", DailyFact: "واقعیت روزانه",
  Evidence: "مدرک", TechnicalDocument: "سند فنی",
};

function ProjectActionConversion({ projectId, message, actorUserId, onAccessLoss, onChanged }: {
  readonly projectId: string; readonly message: ProjectConversationMessage;
  readonly actorUserId: string; readonly onAccessLoss: (status: number) => void;
  readonly onChanged: () => void;
}) {
  const [phase, setPhase] = useState<"closed" | "checking" | "editing" | "sending" |
    "uncertain" | "conflict" | "done" | "exists">("closed");
  const [base, setBase] = useState(message);
  const [identity, setIdentity] = useState<{ destinationId: string; key: string } | null>(null);
  const [files, setFiles] = useState<readonly ProjectMessageAttachment[]>([]);
  const [selectedIds, setSelectedIds] = useState<readonly string[]>([]);
  const [details, setDetails] = useState<ProjectActionConversionDetails>({
    assigneeUserId: actorUserId, dueDate: futureProjectDate(3), priority: "Medium",
    title: message.body.slice(0, 240), description: "",
  });
  const [confirmed, setConfirmed] = useState(false);
  const [status, setStatus] = useState("");

  async function open() {
    if (phase !== "closed") return;
    setPhase("checking");
    setStatus("");
    try {
      const [entries, attached] = await Promise.all([
        loadProjectMessageConversions("/api/pmcs", projectId, message.id),
        loadProjectMessageAttachments("/api/pmcs", projectId, message.id),
      ]);
      if (entries.some((item) => item.destinationType === "Action")) {
        setPhase("exists");
        setStatus("برای این پیام اقدام رسمی قبلاً ثبت شده است؛ تبار تبدیل را ببینید.");
      } else {
        setBase(message);
        setFiles(attached);
        setSelectedIds([]);
        setIdentity({ destinationId: crypto.randomUUID(), key: crypto.randomUUID() });
        setConfirmed(false);
        setPhase("editing");
      }
    } catch (error) {
      if (error instanceof CollaborationAccessError) onAccessLoss(error.status);
      else { setPhase("closed"); setStatus("بررسی تبار پیش از تبدیل کامل نشد؛ دوباره تلاش کنید."); }
    }
  }

  async function reconcile() {
    try {
      const entries = await loadProjectMessageConversions("/api/pmcs", projectId, message.id);
      if (entries.some((item) => item.destinationType === "Action")) {
        setPhase("done");
        setStatus("اقدام رسمی در تبار پیام ثبت شده است.");
        onChanged();
      } else setStatus("هنوز اقدام ثبت‌شده‌ای دیده نمی‌شود؛ تلاش مجدد فقط با همان شناسه انجام شود.");
    } catch (error) {
      if (error instanceof CollaborationAccessError) onAccessLoss(error.status);
      else setStatus("بازبینی نتیجه کامل نشد؛ هویت درخواست حفظ شده است.");
    }
  }

  async function rebase() {
    if (phase !== "conflict" || message.revision <= base.revision ||
        message.deletedAt || message.redactedAt) return;
    setPhase("checking"); setStatus("");
    try {
      const [lineage, attached] = await Promise.all([
        loadProjectMessageConversions("/api/pmcs", projectId, message.id),
        loadProjectMessageAttachments("/api/pmcs", projectId, message.id),
      ]);
      if (lineage.some((item) => item.destinationType === "Action")) {
        setPhase("exists"); setStatus("برای این پیام اقدام رسمی قبلاً ثبت شده است؛ تبار تبدیل را ببینید.");
        return;
      }
      const retained = files.filter((file) => selectedIds.includes(file.documentId) &&
        attached.some((current) => current.documentId.toLowerCase() === file.documentId.toLowerCase() &&
          current.sha256.toLowerCase() === file.sha256.toLowerCase() &&
          current.versionNumber === file.versionNumber &&
          current.originalFileName === file.originalFileName &&
          current.contentType === file.contentType && current.sizeBytes === file.sizeBytes));
      setFiles(attached);
      setSelectedIds(retained.map((file) => file.documentId));
      setBase(message);
      setIdentity({ destinationId: crypto.randomUUID(), key: crypto.randomUUID() });
      setConfirmed(false);
      setStatus(retained.length < selectedIds.length
        ? "برخی فایل‌های انتخابی دیگر در فهرست آزادشده نیستند؛ انتخاب را دوباره بررسی کنید." : "");
      setPhase("editing");
    } catch (error) {
      if (error instanceof CollaborationAccessError) onAccessLoss(error.status);
      else { setPhase("conflict"); setStatus("بازخوانی فایل‌ها و تبار کامل نشد؛ دوباره تلاش کنید."); }
    }
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!identity || !confirmed || (phase !== "editing" && phase !== "uncertain")) return;
    if (!navigator.onLine) { setStatus("تبدیل رسمی فقط هنگام اتصال به سرور انجام می‌شود."); return; }
    setPhase("sending");
    setStatus("در حال ثبت اقدام رسمی…");
    try {
      const result = await convertProjectMessageToAction("/api/pmcs", projectId, base,
        actorUserId, details, identity.destinationId, identity.key,
        files.filter((item) => selectedIds.includes(item.documentId)));
      setPhase("done");
      setStatus(`اقدام رسمی با ارجاع ${result.destinationReference} ثبت شد؛ تبار پیام را تازه‌سازی کنید.`);
      onChanged();
    } catch (error) {
      if (error instanceof CollaborationAccessError) onAccessLoss(error.status);
      else if (error instanceof CollaborationRevisionConflict) {
        setPhase("conflict");
        setStatus("نسخهٔ پیام تغییر کرده است؛ نسخهٔ تازه را بخوانید و تبدیل را دوباره تأیید کنید.");
        onChanged();
      } else if (error instanceof CollaborationActionAlreadyExists) {
        setPhase("exists"); setStatus(error.message); onChanged();
      } else if (error instanceof CollaborationActionValidationError) {
        setPhase("editing");
        setIdentity({ destinationId: crypto.randomUUID(), key: crypto.randomUUID() });
        setConfirmed(false);
        setStatus(error.message);
      } else {
        setPhase("uncertain");
        setStatus("نتیجهٔ ارسال قطعی نیست؛ ابتدا تبار را بازبینی یا با همان شناسه تلاش مجدد کنید.");
      }
    }
  }

  return <section className="collaboration-edit" aria-label="تبدیل پیام به اقدام رسمی">
    {phase === "closed" && <button className="secondary-button" type="button"
      onClick={() => void open()}>ساخت اقدام رسمی از پیام</button>}
    {phase === "checking" && <p role="status">در حال بررسی تبار پیام…</p>}
    {status && <p role={phase === "conflict" ? "alert" : "status"}>{status}</p>}
    {phase === "conflict" && <div>
      <p>نسخهٔ فعلی پیام: {message.body}</p>
      {message.revision > base.revision && !message.deletedAt && !message.redactedAt &&
        <button className="secondary-button" type="button" onClick={() => void rebase()}>
          تبدیل بر پایهٔ نسخهٔ تازه</button>}
    </div>}
    {(phase === "editing" || phase === "sending" || phase === "uncertain") &&
      <form onSubmit={(event) => void submit(event)}>
        <p>این اقدام یک رکورد رسمی جداگانه است؛ پیام به‌تنهایی وضعیت پروژه را تغییر نمی‌دهد.</p>
        <label>عنوان اقدام<input value={details.title} maxLength={240}
          disabled={phase !== "editing"} onChange={(event) => setDetails((current) =>
            ({ ...current, title: event.target.value }))} required /></label>
        <label>شرح تکمیلی<textarea value={details.description} maxLength={2000} rows={2}
          disabled={phase !== "editing"} onChange={(event) => setDetails((current) =>
            ({ ...current, description: event.target.value }))} /></label>
        <label>اولویت<select value={details.priority} disabled={phase !== "editing"}
          onChange={(event) => setDetails((current) => ({ ...current,
            priority: event.target.value as ProjectActionConversionDetails["priority"] }))}>
          <option value="Low">کم</option><option value="Medium">متوسط</option>
          <option value="High">زیاد</option><option value="Critical">بحرانی</option>
        </select></label>
        <label>مهلت شمسی<PersianDateInput value={details.dueDate} disabled={phase !== "editing"}
          onChange={(date) => setDetails((current) => ({ ...current, dueDate: date }))}
          required ariaLabel="مهلت شمسی اقدام رسمی" /></label>
        <fieldset disabled={phase !== "editing"}>
          <legend>فایل‌های آزادشدهٔ همین پیام برای تبار اقدام، اختیاری (حداکثر ۱۰ فایل)</legend>
          {files.length === 0 && <p>این پیام فایل آزادشده‌ای ندارد.</p>}
          {files.map((file) => <label key={file.documentId}>
            <input type="checkbox" checked={selectedIds.includes(file.documentId)}
              disabled={!selectedIds.includes(file.documentId) && selectedIds.length >= 10}
              onChange={(event) => {
                setSelectedIds((current) => event.target.checked
                  ? [...current, file.documentId] : current.filter((id) => id !== file.documentId));
                setConfirmed(false);
              }} />
            فایل {file.originalFileName} · نسخهٔ {file.versionNumber}
          </label>)}
        </fieldset>
        {selectedIds.length > 0 && <p>هش و نسخهٔ {selectedIds.length.toLocaleString("fa-IR")} فایل انتخاب‌شده در تبار اقدام ثبت می‌شود.</p>}
        <p>مسئول این برش: خود شما. انتخاب مسئول دیگر در مرحلهٔ جدا بررسی می‌شود.</p>
        <label><input type="checkbox" checked={confirmed} disabled={phase !== "editing"}
          onChange={(event) => setConfirmed(event.target.checked)} />
          ایجاد رکورد رسمی با این عنوان، اولویت و مهلت را تأیید می‌کنم.</label>
        <div className="collaboration-message-actions">
          <button type="submit" disabled={phase === "sending" || !confirmed ||
            !details.title.trim() || !details.dueDate}>
            {phase === "uncertain" ? "تلاش مجدد با همان شناسه" : phase === "sending"
              ? "در حال ثبت…" : "تأیید و ساخت اقدام رسمی"}
          </button>
          {phase === "uncertain" && <button className="secondary-button" type="button"
            onClick={() => void reconcile()}>بازبینی نتیجه</button>}
          {phase === "editing" && <button className="secondary-button" type="button"
            onClick={() => { setPhase("closed"); setStatus(""); setIdentity(null); }}>
            انصراف</button>}
        </div>
      </form>}
  </section>;
}

function ProjectIssueConversion({ projectId, message, actorUserId, onAccessLoss, onChanged }: {
  readonly projectId: string; readonly message: ProjectConversationMessage;
  readonly actorUserId: string; readonly onAccessLoss: (status: number) => void;
  readonly onChanged: () => void;
}) {
  const [phase, setPhase] = useState<"closed" | "checking" | "editing" | "sending" |
    "uncertain" | "conflict" | "done" | "exists">("closed");
  const [base, setBase] = useState(message);
  const [identity, setIdentity] = useState<{ destinationId: string; key: string } | null>(null);
  const [files, setFiles] = useState<readonly ProjectMessageAttachment[]>([]);
  const [selectedIds, setSelectedIds] = useState<readonly string[]>([]);
  const [details, setDetails] = useState<ProjectIssueConversionDetails>({
    ownerUserId: actorUserId, targetResolutionDate: futureProjectDate(3),
    title: message.body.slice(0, 240), observedFact: message.body,
    category: "هماهنگی", severity: "Medium", urgency: "Soon",
  });
  const [confirmed, setConfirmed] = useState(false);
  const [status, setStatus] = useState("");

  async function open() {
    if (phase !== "closed") return;
    setPhase("checking"); setStatus("");
    try {
      const [entries, attached] = await Promise.all([
        loadProjectMessageConversions("/api/pmcs", projectId, message.id),
        loadProjectMessageAttachments("/api/pmcs", projectId, message.id),
      ]);
      if (entries.some((item) => item.destinationType === "Issue")) {
        setPhase("exists");
        setStatus("برای این پیام مسئلهٔ رسمی قبلاً ثبت شده است؛ تبار تبدیل را ببینید.");
      } else {
        setBase(message);
        setFiles(attached); setSelectedIds([]);
        setIdentity({ destinationId: crypto.randomUUID(), key: crypto.randomUUID() });
        setConfirmed(false); setPhase("editing");
      }
    } catch (error) {
      if (error instanceof CollaborationAccessError) onAccessLoss(error.status);
      else { setPhase("closed"); setStatus("بررسی تبار پیش از تبدیل کامل نشد؛ دوباره تلاش کنید."); }
    }
  }

  async function reconcile() {
    try {
      const entries = await loadProjectMessageConversions("/api/pmcs", projectId, message.id);
      if (entries.some((item) => item.destinationType === "Issue")) {
        setPhase("done"); setStatus("مسئلهٔ رسمی در تبار پیام ثبت شده است."); onChanged();
      } else setStatus("هنوز مسئلهٔ ثبت‌شده‌ای دیده نمی‌شود؛ تلاش مجدد فقط با همان شناسه انجام شود.");
    } catch (error) {
      if (error instanceof CollaborationAccessError) onAccessLoss(error.status);
      else setStatus("بازبینی نتیجه کامل نشد؛ هویت درخواست حفظ شده است.");
    }
  }

  async function rebase() {
    if (phase !== "conflict" || message.revision <= base.revision ||
        message.deletedAt || message.redactedAt) return;
    setPhase("checking"); setStatus("");
    try {
      const [entries, attached] = await Promise.all([
        loadProjectMessageConversions("/api/pmcs", projectId, message.id),
        loadProjectMessageAttachments("/api/pmcs", projectId, message.id),
      ]);
      if (entries.some((item) => item.destinationType === "Issue")) {
        setPhase("exists"); setStatus("برای این پیام مسئلهٔ رسمی قبلاً ثبت شده است؛ تبار تبدیل را ببینید.");
        return;
      }
      const retained = files.filter((file) => selectedIds.includes(file.documentId) &&
        attached.some((current) => current.documentId.toLowerCase() === file.documentId.toLowerCase() &&
          current.sha256.toLowerCase() === file.sha256.toLowerCase() &&
          current.versionNumber === file.versionNumber &&
          current.originalFileName === file.originalFileName &&
          current.contentType === file.contentType && current.sizeBytes === file.sizeBytes));
      setFiles(attached); setSelectedIds(retained.map((file) => file.documentId));
      setBase(message);
      setIdentity({ destinationId: crypto.randomUUID(), key: crypto.randomUUID() });
      setConfirmed(false);
      setStatus(retained.length < selectedIds.length
        ? "برخی فایل‌های انتخابی دیگر در فهرست آزادشده نیستند؛ انتخاب را دوباره بررسی کنید." : "");
      setPhase("editing");
    } catch (error) {
      if (error instanceof CollaborationAccessError) onAccessLoss(error.status);
      else { setPhase("conflict"); setStatus("بازخوانی فایل‌ها و تبار کامل نشد؛ دوباره تلاش کنید."); }
    }
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!identity || !confirmed || (phase !== "editing" && phase !== "uncertain")) return;
    if (!navigator.onLine) { setStatus("تبدیل رسمی فقط هنگام اتصال به سرور انجام می‌شود."); return; }
    setPhase("sending"); setStatus("در حال ثبت مسئلهٔ رسمی…");
    try {
      const result = await convertProjectMessageToIssue("/api/pmcs", projectId, base,
        actorUserId, details, identity.destinationId, identity.key,
        files.filter((item) => selectedIds.includes(item.documentId)));
      setPhase("done");
      setStatus(`مسئلهٔ رسمی با ارجاع ${result.destinationReference} ثبت شد؛ تبار پیام را تازه‌سازی کنید.`);
      onChanged();
    } catch (error) {
      if (error instanceof CollaborationAccessError) onAccessLoss(error.status);
      else if (error instanceof CollaborationRevisionConflict) {
        setPhase("conflict");
        setStatus("نسخهٔ پیام تغییر کرده است؛ نسخهٔ تازه را بخوانید و تبدیل مسئله را دوباره تأیید کنید.");
        onChanged();
      } else if (error instanceof CollaborationIssueAlreadyExists) {
        setPhase("exists"); setStatus(error.message); onChanged();
      } else if (error instanceof CollaborationIssueValidationError) {
        setPhase("editing");
        setIdentity({ destinationId: crypto.randomUUID(), key: crypto.randomUUID() });
        setConfirmed(false); setStatus(error.message);
      } else {
        setPhase("uncertain");
        setStatus("نتیجهٔ ارسال قطعی نیست؛ ابتدا تبار را بازبینی یا با همان شناسه تلاش مجدد کنید.");
      }
    }
  }

  return <section className="collaboration-edit" aria-label="تبدیل پیام به مسئلهٔ رسمی">
    {phase === "closed" && <button className="secondary-button" type="button"
      onClick={() => void open()}>ساخت مسئلهٔ رسمی از پیام</button>}
    {phase === "checking" && <p role="status">در حال بررسی تبار پیام…</p>}
    {status && <p role={phase === "conflict" ? "alert" : "status"}>{status}</p>}
    {phase === "conflict" && <div>
      <p>نسخهٔ فعلی پیام: {message.body}</p>
      {message.revision > base.revision && !message.deletedAt && !message.redactedAt &&
        <button className="secondary-button" type="button" onClick={() => void rebase()}>
          تبدیل مسئله بر پایهٔ نسخهٔ تازه</button>}
    </div>}
    {(phase === "editing" || phase === "sending" || phase === "uncertain") &&
      <form onSubmit={(event) => void submit(event)}>
        <p>این مسئله یک رکورد رسمی جداگانه و عمومی همین پروژه است؛ مسئول آن خود شما هستید.</p>
        <label>عنوان مسئله<input value={details.title} maxLength={240}
          disabled={phase !== "editing"} required onChange={(event) => setDetails((current) =>
            ({ ...current, title: event.target.value }))} /></label>
        <label>واقعیت مشاهده‌شده<textarea value={details.observedFact} maxLength={5000}
          disabled={phase !== "editing"} required rows={3} onChange={(event) => setDetails((current) =>
            ({ ...current, observedFact: event.target.value }))} /></label>
        <label>دستهٔ مسئله<input value={details.category} maxLength={120}
          disabled={phase !== "editing"} required onChange={(event) => setDetails((current) =>
            ({ ...current, category: event.target.value }))} /></label>
        <label>شدت مسئله<select value={details.severity} disabled={phase !== "editing"}
          onChange={(event) => setDetails((current) => ({ ...current,
            severity: event.target.value as ProjectIssueConversionDetails["severity"] }))}>
          <option value="Low">کم</option><option value="Medium">متوسط</option>
          <option value="High">زیاد</option><option value="Critical">بحرانی</option>
        </select></label>
        <label>فوریت مسئله<select value={details.urgency} disabled={phase !== "editing"}
          onChange={(event) => setDetails((current) => ({ ...current,
            urgency: event.target.value as ProjectIssueConversionDetails["urgency"] }))}>
          <option value="Routine">عادی</option><option value="Soon">به‌زودی</option>
          <option value="Immediate">فوری</option>
        </select></label>
        <label>مهلت رفع شمسی<PersianDateInput value={details.targetResolutionDate}
          disabled={phase !== "editing"} required ariaLabel="مهلت رفع شمسی مسئلهٔ رسمی"
          onChange={(date) => setDetails((current) => ({ ...current, targetResolutionDate: date }))} /></label>
        <fieldset disabled={phase !== "editing"}>
          <legend>فایل‌های آزادشدهٔ همین پیام برای شواهد مسئله، اختیاری (حداکثر ۱۰ فایل)</legend>
          {files.length === 0 && <p>این پیام فایل آزادشده‌ای ندارد.</p>}
          {files.map((file) => <label key={file.documentId}>
            <input type="checkbox" checked={selectedIds.includes(file.documentId)}
              disabled={!selectedIds.includes(file.documentId) && selectedIds.length >= 10}
              onChange={(event) => {
                setSelectedIds((current) => event.target.checked
                  ? [...current, file.documentId] : current.filter((id) => id !== file.documentId));
                setConfirmed(false);
              }} />
            فایل {file.originalFileName} · نسخهٔ {file.versionNumber}
          </label>)}
        </fieldset>
        {selectedIds.length > 0 && <p>هش و نسخهٔ {selectedIds.length.toLocaleString("fa-IR")} فایل انتخاب‌شده در شواهد مسئله ثبت می‌شود.</p>}
        <label><input type="checkbox" checked={confirmed} disabled={phase !== "editing"}
          onChange={(event) => setConfirmed(event.target.checked)} />
          ایجاد مسئلهٔ عمومی رسمی با این عنوان، شدت، فوریت و مهلت را تأیید می‌کنم.</label>
        <div className="collaboration-message-actions">
          <button type="submit" disabled={phase === "sending" || !confirmed ||
            !details.title.trim() || !details.observedFact.trim() || !details.category.trim() ||
            !details.targetResolutionDate}>
            {phase === "uncertain" ? "تلاش مجدد مسئله با همان شناسه" : phase === "sending"
              ? "در حال ثبت…" : "تأیید و ساخت مسئلهٔ رسمی"}
          </button>
          {phase === "uncertain" && <button className="secondary-button" type="button"
            onClick={() => void reconcile()}>بازبینی نتیجهٔ مسئله</button>}
          {phase === "editing" && <button className="secondary-button" type="button"
            onClick={() => { setPhase("closed"); setStatus(""); setIdentity(null); }}>انصراف از مسئله</button>}
        </div>
      </form>}
  </section>;
}

function ProjectMessageConversions({ projectId, messageId, refreshToken, onAccessLoss }: {
  readonly projectId: string; readonly messageId: string; readonly refreshToken: number;
  readonly onAccessLoss: (status: number) => void;
}) {
  const [expanded, setExpanded] = useState(false);
  const [entries, setEntries] = useState<readonly ProjectMessageConversionLineage[] | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!expanded) return undefined;
    const controller = new AbortController();
    void loadProjectMessageConversions("/api/pmcs", projectId, messageId, controller.signal)
      .then((result) => { if (!controller.signal.aborted) { setEntries(result); setError(""); } })
      .catch((failure: unknown) => {
        if (controller.signal.aborted) return;
        setEntries(null);
        if (failure instanceof CollaborationAccessError) onAccessLoss(failure.status);
        else setError(failure instanceof Error ? failure.message : "دریافت تبار تبدیل کامل نشد.");
      });
    return () => controller.abort();
  }, [expanded, projectId, messageId, refreshToken, onAccessLoss]);

  return <section className="collaboration-edit" aria-label="تبار تبدیل‌های رسمی پیام">
    <button className="secondary-button" type="button" aria-expanded={expanded}
      onClick={() => { setExpanded((value) => !value); setEntries(null); setError(""); }}>
      {expanded ? "بستن تبدیل‌های رسمی" : "تبدیل‌های رسمی پیام"}
    </button>
    {expanded && <div>
      {error && <p role="alert">{error}</p>}
      {!error && entries === null && <p role="status">در حال دریافت تبار تبدیل…</p>}
      {entries?.length === 0 && <p>هنوز رکورد رسمی از این پیام ساخته نشده است.</p>}
      {entries && entries.length > 0 && <ol>{entries.map((item) => <li key={item.id}>
        <strong>{conversionLabels[item.destinationType]}: {item.destinationReference}</strong>
        <span> · نسخهٔ پیام {item.messageRevision.toLocaleString("fa-IR")} · </span>
        <time dateTime={item.confirmedAt}>{formatPersianDateTime(item.confirmedAt)}</time>
        <p>{item.documents.length.toLocaleString("fa-IR")} سند پیوسته در تبار رسمی</p>
      </li>)}</ol>}
    </div>}
  </section>;
}

function ProjectMessageReactions({ projectId, messageId, refreshToken, onAccessLoss }: {
  readonly projectId: string;
  readonly messageId: string;
  readonly refreshToken: number;
  readonly onAccessLoss: (status: number) => void;
}) {
  const [expanded, setExpanded] = useState(false);
  const [view, setView] = useState<ProjectMessageReactionsView | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!expanded) return undefined;
    const controller = new AbortController();
    void loadProjectMessageReactions("/api/pmcs", projectId, messageId, controller.signal)
      .then((result) => { if (!controller.signal.aborted) { setView(result); setError(""); } })
      .catch((failure: unknown) => {
        if (controller.signal.aborted) return;
        setView(null);
        if (failure instanceof CollaborationAccessError) onAccessLoss(failure.status);
        else setError("دریافت واکنش‌های پیام کامل نشد؛ دوباره تلاش کنید.");
      });
    return () => controller.abort();
  }, [expanded, projectId, messageId, refreshToken, onAccessLoss]);

  async function toggle(emoji: ProjectReactionEmoji, reactedByMe: boolean) {
    if (!view?.canReact || busy) return;
    setBusy(true);
    setError("");
    try {
      await setProjectMessageReaction("/api/pmcs", projectId, messageId, emoji, !reactedByMe);
      setView(await loadProjectMessageReactions("/api/pmcs", projectId, messageId));
    } catch (failure) {
      setView(null);
      if (failure instanceof CollaborationAccessError) onAccessLoss(failure.status);
      else setError("ثبت واکنش کامل نشد؛ دوباره تلاش کنید.");
    } finally {
      setBusy(false);
    }
  }

  return <div className="collaboration-reactions">
    <button className="secondary-button" type="button" aria-expanded={expanded}
      aria-controls={`reactions-${messageId}`}
      onClick={() => { setExpanded((value) => !value); setView(null); setError(""); }}>
      واکنش‌ها
    </button>
    {expanded && <div id={`reactions-${messageId}`} className="collaboration-reaction-panel"
      aria-label="واکنش‌های همین پیام">
      {!view && !error && <span role="status">در حال دریافت واکنش‌ها…</span>}
      {view && <>
        {view.reactions.map((reaction) => <button key={reaction.emoji} type="button"
          className="collaboration-reaction-button" aria-pressed={reaction.reactedByMe}
          aria-label={`${reaction.emoji}، ${reaction.count.toLocaleString("fa-IR")} واکنش`}
          disabled={!view.canReact || busy}
          onClick={() => void toggle(reaction.emoji, reaction.reactedByMe)}>
          <span aria-hidden="true">{reaction.emoji}</span> {reaction.count.toLocaleString("fa-IR")}
        </button>)}
        {!view.canReact && <span>نمایش واکنش‌ها مجاز است؛ ثبت واکنش به مجوز ارسال نیاز دارد.</span>}
      </>}
      {error && <span role="alert">{error}</span>}
    </div>}
  </div>;
}

function ProjectMessageAttachments({ projectId, messageId, canUpload, refreshToken, onAccessLoss }: {
  readonly projectId: string;
  readonly messageId: string;
  readonly canUpload: boolean;
  readonly refreshToken: number;
  readonly onAccessLoss: (status: number) => void;
}) {
  const [expanded, setExpanded] = useState(false);
  const [attachments, setAttachments] = useState<readonly ProjectMessageAttachment[] | null>(null);
  const [busyId, setBusyId] = useState("");
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  useEffect(() => {
    if (!expanded) return undefined;
    const controller = new AbortController();
    void loadProjectMessageAttachments("/api/pmcs", projectId, messageId, controller.signal)
      .then((result) => { if (!controller.signal.aborted) { setAttachments(result); setError(""); } })
      .catch((failure: unknown) => {
        if (controller.signal.aborted) return;
        setAttachments(null);
        if (failure instanceof CollaborationAccessError) onAccessLoss(failure.status);
        else setError("دریافت پیوست‌های پیام کامل نشد؛ دوباره تلاش کنید.");
      });
    return () => controller.abort();
  }, [expanded, projectId, messageId, refreshToken, onAccessLoss]);

  async function download(attachment: ProjectMessageAttachment) {
    if (busyId) return;
    setBusyId(attachment.documentId);
    setError("");
    setNotice("");
    try {
      const blob = await downloadProjectMessageAttachment("/api/pmcs", projectId, messageId, attachment);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = attachment.originalFileName;
      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      window.setTimeout(() => URL.revokeObjectURL(url), 30_000);
      setNotice("پیوست پس از تأیید صحت دریافت شد.");
    } catch (failure) {
      if (failure instanceof CollaborationAccessError) onAccessLoss(failure.status);
      else setError(failure instanceof Error ? failure.message : "دریافت پیوست کامل نشد.");
    } finally {
      setBusyId("");
    }
  }

  return <div className="collaboration-attachments">
    <button className="secondary-button" type="button" aria-expanded={expanded}
      aria-controls={`attachments-${messageId}`}
      onClick={() => { setExpanded((value) => !value); setAttachments(null); setError(""); setNotice(""); }}>
      پیوست‌ها
    </button>
    {expanded && <div id={`attachments-${messageId}`} className="collaboration-attachment-panel"
      aria-label="پیوست‌های همین پیام">
      {!attachments && !error && <span role="status">در حال دریافت پیوست‌ها…</span>}
      {attachments?.length === 0 && <span>پیوست تأییدشده‌ای برای این پیام ثبت نشده است.</span>}
      {attachments && attachments.length > 0 && <ul>
        {attachments.map((attachment) => <li key={attachment.documentId}>
          <span>{attachment.originalFileName} · {attachment.sizeBytes.toLocaleString("fa-IR")} بایت</span>
          <button type="button" className="secondary-button" disabled={Boolean(busyId)}
            onClick={() => void download(attachment)}>
            {busyId === attachment.documentId ? "در حال دریافت…" : "دریافت پیوست"}
          </button>
        </li>)}
      </ul>}
      {notice && <span role="status">{notice}</span>}
      {error && <span role="alert">{error}</span>}
      {canUpload && attachments && !error && <ProjectMessageUpload projectId={projectId} messageId={messageId}
        attachedDocumentIds={attachments?.map((attachment) => attachment.documentId) ?? []}
        onAccessLoss={onAccessLoss} onAttached={async () => {
          setAttachments(null);
          setAttachments(await loadProjectMessageAttachments("/api/pmcs", projectId, messageId));
          setNotice("پیوست آزادشده به همین پیام متصل شد.");
        }} />}
    </div>}
  </div>;
}

function ProjectMessageUpload({ projectId, messageId, attachedDocumentIds, onAccessLoss, onAttached }: {
  readonly projectId: string;
  readonly messageId: string;
  readonly attachedDocumentIds: readonly string[];
  readonly onAccessLoss: (status: number) => void;
  readonly onAttached: () => Promise<void>;
}) {
  const session = usePmcsSession();
  const [uploads, setUploads] = useState<readonly QueuedDocumentUpload[]>([]);
  const [states, setStates] = useState<Record<string, ProjectChatUploadState>>({});
  const [busy, setBusy] = useState(false);
  const [attachingId, setAttachingId] = useState("");
  const [notice, setNotice] = useState("");

  const refreshUploads = useCallback(async (retry: boolean) => {
    try {
      if (retry && navigator.onLine) {
        await recoverInterruptedDocumentUploads(projectId);
        await syncPendingDocumentUploads("/api/pmcs", projectId,
          { ownerType: "ProjectChat", ownerId: messageId });
      }
      const items = await listProjectChatDocumentUploads(projectId, messageId);
      setUploads(items);
      const accepted = items.filter((item) => item.status === "quarantined" || item.status === "released");
      const server = await Promise.all(accepted.map((item) =>
        loadProjectChatUploadState("/api/pmcs", projectId, messageId, item)));
      setStates(Object.fromEntries(server.map((state) => [state.id, state])));
      setNotice(items.some((item) => item.status === "queued")
        ? "فایل در صف امن دستگاه است؛ پس از اتصال دوباره ارسال می‌شود." : "");
    } catch (failure) {
      setStates({});
      if (failure instanceof CollaborationAccessError) onAccessLoss(failure.status);
      else setNotice("بررسی وضعیت آپلود کامل نشد؛ دوباره تلاش کنید.");
    }
  }, [projectId, messageId, onAccessLoss]);

  useEffect(() => {
    let active = true;
    void Promise.resolve().then(() => { if (active) return refreshUploads(navigator.onLine); });
    const online = () => { void refreshUploads(true); };
    window.addEventListener("online", online);
    return () => { active = false; window.removeEventListener("online", online); };
  }, [refreshUploads]);

  async function selectFile(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file || busy) return;
    setBusy(true);
    setNotice("در حال ثبت فایل در صف امن و کنترل امنیتی…");
    try {
      await enqueueDocumentUpload({
        tenantId: session.tenantId, userId: session.userId, projectId,
        ownerType: "ProjectChat", ownerId: messageId, file,
        classification: "Internal", retentionPolicy: "Standard",
      });
      await refreshUploads(navigator.onLine);
    } catch (failure) {
      setNotice(failure instanceof Error ? failure.message : "ثبت فایل کامل نشد.");
    } finally { setBusy(false); }
  }

  function uploadLabel(item: QueuedDocumentUpload): string {
    const state = states[item.assetId];
    if (attachedDocumentIds.some((id) => id.toLowerCase() === item.assetId.toLowerCase()))
      return "متصل به پیام";
    if (state?.status === "Released") return "آزادشده و آمادهٔ اتصال";
    if (state?.status === "Quarantined") return "در انتظار بررسی و آزادسازی";
    if (item.status === "rejected" || state?.status === "Rejected") return "ردشده در کنترل امنیتی";
    if (item.status === "queued") return "در صف ارسال";
    return "در حال بررسی وضعیت";
  }

  async function attach(item: QueuedDocumentUpload) {
    if (states[item.assetId]?.status !== "Released" || busy || attachingId) return;
    setAttachingId(item.assetId);
    setNotice("");
    try {
      await attachProjectChatDocument("/api/pmcs", projectId, messageId, item);
      await onAttached();
    } catch (failure) {
      if (failure instanceof CollaborationAccessError) onAccessLoss(failure.status);
      else setNotice(failure instanceof Error ? failure.message : "اتصال پیوست کامل نشد.");
    } finally { setAttachingId(""); }
  }

  return <section className="collaboration-upload" aria-label="آپلود فایل برای همین پیام">
    <label htmlFor={`chat-upload-${messageId}`}>افزودن فایل به پیام خود</label>
    <input id={`chat-upload-${messageId}`} type="file" disabled={busy}
      accept=".jpg,.jpeg,.png,.webp,.heic,.heif,.pdf,.txt,.log,.csv,.docx,.xlsx,.pptx"
      onChange={(event) => void selectFile(event)} />
    <p>فایل ابتدا اسکن و قرنطینه می‌شود؛ پس از آزادسازی می‌توان آن را به پیام متصل کرد.</p>
    <button type="button" className="secondary-button" disabled={busy}
      onClick={() => void refreshUploads(navigator.onLine)}>بررسی وضعیت آپلود</button>
    {notice && <span role="status">{notice}</span>}
    {uploads.length > 0 && <ul>{uploads.map((item) => <li key={item.assetId}>
      <span>{item.originalFileName} · {uploadLabel(item)}</span>
      {states[item.assetId]?.status === "Released" &&
        !attachedDocumentIds.some((id) => id.toLowerCase() === item.assetId.toLowerCase()) &&
        <button className="secondary-button" type="button" disabled={busy || Boolean(attachingId)}
          onClick={() => void attach(item)}>
          {attachingId === item.assetId ? "در حال اتصال…" : "اتصال پیوست به پیام"}
        </button>}
    </li>)}</ul>}
  </section>;
}

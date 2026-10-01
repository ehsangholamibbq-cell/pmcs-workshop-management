"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { transitionAction, type ManagementActionStatus } from "@/lib/actions";
import { reviewDailyReport } from "@/lib/daily-reports";
import { scopedStorageKey } from "@/lib/field-database";
import { ApiRequestError, toUserMessage } from "@/lib/localization";
import { formatPersianDate, formatPersianDateTime } from "@/lib/persian-date";
import {
  changeNotificationReceipt,
  getMyWork,
  listNotifications,
  type InAppNotificationModel,
  type MyWorkItemModel,
  type MyWorkModel,
} from "@/lib/work-management";

interface MyWorkCenterProps {
  readonly apiBaseUrl: string;
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly isOnline: boolean;
  readonly refreshToken: number;
  readonly onChanged: () => void;
}

export function MyWorkCenter(props: MyWorkCenterProps) {
  const [work, setWork] = useState<MyWorkModel | null>(null);
  const [notifications, setNotifications] = useState<readonly InAppNotificationModel[] | null>(null);
  const [comments, setComments] = useState<Record<string, string>>({});
  const [busyId, setBusyId] = useState<string | null>(null);
  const [message, setMessage] = useState("در حال دریافت کارتابل یکپارچه…");
  const [messageKind, setMessageKind] = useState<"status" | "error">("status");
  const [readState, setReadState] = useState<"loading" | "current" | "cached" | "unavailable" | "forbidden">("loading");
  const actorIdentity = useMemo(
    () => ({ tenantId: props.tenantId, userId: props.userId }),
    [props.tenantId, props.userId],
  );
  const cacheKey = useMemo(() => scopedStorageKey(`pmcs-my-work:${props.projectId}`), [props.projectId]);
  const notificationCacheKey = useMemo(
    () => scopedStorageKey(`pmcs-notifications:${props.projectId}`),
    [props.projectId],
  );

  const load = useCallback(async () => {
    if (!props.isOnline) {
      const cachedWork = readCache<MyWorkModel>(cacheKey);
      const cachedNotifications = readCache<readonly InAppNotificationModel[]>(notificationCacheKey);
      setWork(cachedWork);
      setNotifications(cachedNotifications);
      setReadState(cachedWork || cachedNotifications ? "cached" : "unavailable");
      setMessage(cachedWork || cachedNotifications
        ? "نسخهٔ ذخیره‌شده روی دستگاه نمایش داده می‌شود؛ ممکن است قدیمی باشد و اقدام رسمی نیازمند اتصال است."
        : "در حالت آفلاین نسخهٔ ذخیره‌شده‌ای از کارتابل و اعلان‌ها در دسترس نیست.");
      setMessageKind("status");
      return;
    }

    setReadState("loading");
    setWork(null);
    setNotifications(null);
    setMessage("در حال دریافت تازهٔ کارتابل و اعلان‌ها…");
    setMessageKind("status");
    try {
      const [nextWork, nextNotifications] = await Promise.all([
        getMyWork(props.apiBaseUrl, actorIdentity, props.projectId),
        listNotifications(props.apiBaseUrl, actorIdentity, props.projectId),
      ]);
      setWork(nextWork);
      setNotifications(nextNotifications);
      localStorage.setItem(cacheKey, JSON.stringify(nextWork));
      localStorage.setItem(notificationCacheKey, JSON.stringify(nextNotifications));
      setReadState("current");
      setMessage(nextWork.items.length === 0 ? "کار فعالی به شما تخصیص داده نشده است." : "کارتابل بر اساس مجوز فعلی شما محاسبه شد.");
    } catch (error) {
      if (error instanceof ApiRequestError && [401, 403, 404].includes(error.status)) {
        localStorage.removeItem(cacheKey);
        localStorage.removeItem(notificationCacheKey);
        setWork(null);
        setNotifications(null);
        setReadState("forbidden");
        setMessage("دسترسی به کارتابل یا اعلان‌های این پروژه تأیید نشد؛ دادهٔ ذخیره‌شده نمایش داده نمی‌شود.");
      } else {
        const cachedWork = readCache<MyWorkModel>(cacheKey);
        const cachedNotifications = readCache<readonly InAppNotificationModel[]>(notificationCacheKey);
        setWork(cachedWork);
        setNotifications(cachedNotifications);
        setReadState(cachedWork || cachedNotifications ? "cached" : "unavailable");
        setMessage(cachedWork || cachedNotifications
          ? "دریافت تازه کامل نشد؛ فقط نسخهٔ ذخیره‌شده نمایش داده می‌شود و ممکن است قدیمی باشد."
          : toUserMessage(error, "دریافت کارتابل و اعلان‌ها کامل نشد."));
      }
      setMessageKind("error");
    }
  }, [
    cacheKey,
    notificationCacheKey,
    actorIdentity,
    props.apiBaseUrl,
    props.isOnline,
    props.projectId,
  ]);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => void load(), 0);
    return () => window.clearTimeout(timeoutId);
  }, [load, props.refreshToken]);

  async function transition(item: MyWorkItemModel, targetStatus: ManagementActionStatus) {
    setBusyId(item.id);
    try {
      await transitionAction(
        props.apiBaseUrl,
        actorIdentity,
        props.projectId,
        item.targetId,
        item.revision,
        targetStatus,
      );
      await load();
      props.onChanged();
    } catch (error) {
      setMessage(toUserMessage(error, "تغییر وضعیت اقدام انجام نشد."));
      setMessageKind("error");
    } finally {
      setBusyId(null);
    }
  }

  async function review(item: MyWorkItemModel, action: "approve" | "return") {
    const comment = comments[item.id] ?? "";
    if (action === "return" && !comment.trim()) {
      setMessage("برای عودت گزارش، دلیل اصلاح را ثبت کنید.");
      setMessageKind("error");
      return;
    }

    setBusyId(item.id);
    try {
      await reviewDailyReport(
        props.apiBaseUrl,
        actorIdentity,
        props.projectId,
        item.targetId,
        action,
        item.revision,
        comment,
      );
      setComments((current) => ({ ...current, [item.id]: "" }));
      await load();
      props.onChanged();
    } catch (error) {
      setMessage(toUserMessage(error, "تصمیم گزارش ثبت نشد."));
      setMessageKind("error");
    } finally {
      setBusyId(null);
    }
  }

  async function receipt(notification: InAppNotificationModel, action: "read" | "acknowledge") {
    setBusyId(notification.id);
    try {
      await changeNotificationReceipt(
        props.apiBaseUrl,
        actorIdentity,
        props.projectId,
        notification.id,
        notification.revision,
        action,
      );
      await load();
    } catch (error) {
      setMessage(toUserMessage(error, "ثبت رسید اعلان انجام نشد."));
      setMessageKind("error");
    } finally {
      setBusyId(null);
    }
  }

  const items = work?.items ?? [];
  const visibleNotifications = notifications ?? [];
  const unread = visibleNotifications.filter((item) => item.readAt === null).length;
  const canAct = props.isOnline && readState === "current";
  return (
    <section className="my-work-center" aria-label="کارهای من و اعلان‌ها" data-testid="my-work-center" data-read-state={readState}>
      <div className={`my-work-feedback ${messageKind}`} role={messageKind === "error" ? "alert" : "status"}>
        <p>{message}</p>
        {props.isOnline && (readState === "cached" || readState === "unavailable" || readState === "forbidden") &&
          <button className="secondary-button" type="button" onClick={() => void load()}>تلاش دوباره برای دریافت کارتابل</button>}
      </div>
      <article className="operational-card my-work-card" data-testid="my-work-panel">
        <div className="card-heading">
          <div><p className="eyebrow">کارتابل یکپارچه</p><h2>کارهای من</h2></div>
          <span className={items.some((item) => item.isOverdue) ? "count-badge warning" : "count-badge"}>
            {work ? items.length.toLocaleString("fa-IR") : "—"}
          </span>
        </div>
        <div className="my-work-list" data-testid="my-work-list">
          {items.map((item) => (
            <div className={`my-work-item${item.isOverdue ? " overdue" : ""}`} data-testid="my-work-item" data-entity-id={item.targetId} key={item.id}>
              <div className="work-summary">
                <span className={`priority-badge priority-${item.priority.toLowerCase()}`}>{priorityLabel(item.priority)}</span>
                <strong>{item.title}</strong>
                <small>{workDate(item)} · {statusLabel(item.status)}{item.isOverdue ? " · عقب‌افتاده" : ""}</small>
                {item.description && <p>{item.description}</p>}
              </div>
              {item.kind === "ManagementAction" && (
                <div className="review-actions">
                  {item.status !== "InProgress" && <button className="secondary-button" data-testid="my-work-action-start" type="button" disabled={!canAct || busyId === item.id} onClick={() => void transition(item, "InProgress")}>شروع</button>}
                  <button data-testid="my-work-action-done" type="button" disabled={!canAct || busyId === item.id} onClick={() => void transition(item, "Done")}>انجام شد</button>
                </div>
              )}
              {item.kind === "DailyReportReview" && (
                <div className="work-review-controls">
                  <textarea rows={2} aria-label={`نظر ${item.title}`} placeholder="نظر اختیاری؛ برای عودت، دلیل الزامی است" value={comments[item.id] ?? ""} onChange={(event) => setComments((current) => ({ ...current, [item.id]: event.target.value }))} />
                  <div className="review-actions">
                    <button data-testid="daily-report-review-approve" type="button" disabled={!canAct || busyId === item.id} onClick={() => void review(item, "approve")}>تأیید</button>
                    <button className="secondary-button" data-testid="daily-report-review-return" type="button" disabled={!canAct || busyId === item.id} onClick={() => void review(item, "return")}>عودت برای اصلاح</button>
                  </div>
                </div>
              )}
              {item.kind === "DailyReportCorrection" && <a className="inline-link" href="#daily-report-history">بازکردن نسخه اصلاحی</a>}
            </div>
          ))}
          {work && items.length === 0 && <p className="muted">{readState === "cached" ? "در نسخهٔ ذخیره‌شده موردی برای اقدام ثبت نشده است." : "در حال حاضر موردی برای اقدام شما وجود ندارد."}</p>}
          {!work && readState !== "loading" && <p className="muted">دادهٔ تأییدشدهٔ کارتابل در دسترس نیست.</p>}
        </div>
      </article>

      <article className="operational-card notification-card" data-testid="notification-panel">
        <div className="card-heading">
          <div><p className="eyebrow">اعلان داخل سامانه</p><h2>اعلان‌های من</h2></div>
          <span className={unread > 0 ? "count-badge warning" : "count-badge"}>{notifications ? unread.toLocaleString("fa-IR") : "—"}</span>
        </div>
        <div className="notification-list" data-testid="notification-list">
          {visibleNotifications.slice(0, 20).map((notification) => (
            <div className={`notification-item${notification.readAt ? " read" : ""}`} data-testid="notification-item" data-entity-id={notification.id} key={notification.id}>
              <div><strong>{notification.title}</strong><small>{formatPersianDateTime(notification.occurredAt)}</small><p>{notification.body}</p></div>
              <div className="review-actions">
                {!notification.readAt && <button className="secondary-button" data-testid="notification-read" type="button" disabled={!canAct || busyId === notification.id} onClick={() => void receipt(notification, "read")}>خواندم</button>}
                {!notification.acknowledgedAt && <button data-testid="notification-acknowledge" type="button" disabled={!canAct || busyId === notification.id} onClick={() => void receipt(notification, "acknowledge")}>تأیید دریافت</button>}
              </div>
            </div>
          ))}
          {notifications && notifications.length === 0 && <p className="muted">{readState === "cached" ? "در نسخهٔ ذخیره‌شده اعلان فعالی ثبت نشده است." : "اعلان فعالی وجود ندارد."}</p>}
          {!notifications && readState !== "loading" && <p className="muted">دادهٔ تأییدشدهٔ اعلان‌ها در دسترس نیست.</p>}
        </div>
      </article>
    </section>
  );
}

function priorityLabel(value: MyWorkItemModel["priority"]): string {
  return ({ Low: "کم", Medium: "متوسط", High: "زیاد", Critical: "بحرانی" })[value];
}

function statusLabel(value: string): string {
  return ({
    Open: "باز", InProgress: "در حال انجام", Blocked: "متوقف",
    WaitingForReview: "در انتظار بازبینی", CorrectionRequired: "نیازمند اصلاح",
    DraftCorrection: "پیش‌نویس اصلاحی",
  } as Record<string, string>)[value] ?? value;
}

function workDate(item: MyWorkItemModel): string {
  const value = item.dueDate ?? item.referenceDate;
  return value ? `${item.kind === "ManagementAction" ? "مهلت" : "تاریخ گزارش"} ${formatPersianDate(value)}` : "بدون تاریخ";
}

function readCache<T>(key: string): T | null {
  try {
    const raw = localStorage.getItem(key);
    return raw ? JSON.parse(raw) as T : null;
  } catch {
    return null;
  }
}

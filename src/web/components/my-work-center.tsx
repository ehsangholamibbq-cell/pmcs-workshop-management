"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { transitionAction, type ManagementActionStatus } from "@/lib/actions";
import { reviewDailyReport } from "@/lib/daily-reports";
import { scopedStorageKey } from "@/lib/field-database";
import { toUserMessage } from "@/lib/localization";
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
  const [notifications, setNotifications] = useState<readonly InAppNotificationModel[]>([]);
  const [comments, setComments] = useState<Record<string, string>>({});
  const [busyId, setBusyId] = useState<string | null>(null);
  const [message, setMessage] = useState("در حال دریافت کارتابل یکپارچه…");
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
      setWork(readCache<MyWorkModel>(cacheKey));
      setNotifications(readCache<readonly InAppNotificationModel[]>(notificationCacheKey) ?? []);
      setMessage("آخرین نسخه ذخیره‌شده روی دستگاه نمایش داده می‌شود؛ رسید رسمی نیازمند اتصال است.");
      return;
    }

    try {
      const [nextWork, nextNotifications] = await Promise.all([
        getMyWork(props.apiBaseUrl, actorIdentity, props.projectId),
        listNotifications(props.apiBaseUrl, actorIdentity, props.projectId),
      ]);
      setWork(nextWork);
      setNotifications(nextNotifications);
      localStorage.setItem(cacheKey, JSON.stringify(nextWork));
      localStorage.setItem(notificationCacheKey, JSON.stringify(nextNotifications));
      setMessage(nextWork.items.length === 0 ? "کار فعالی به شما تخصیص داده نشده است." : "کارتابل بر اساس مجوز فعلی شما محاسبه شد.");
    } catch (error) {
      setWork(readCache<MyWorkModel>(cacheKey));
      setNotifications(readCache<readonly InAppNotificationModel[]>(notificationCacheKey) ?? []);
      setMessage(toUserMessage(error, "کارتابل یکپارچه دریافت نشد."));
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
    } finally {
      setBusyId(null);
    }
  }

  async function review(item: MyWorkItemModel, action: "approve" | "return") {
    const comment = comments[item.id] ?? "";
    if (action === "return" && !comment.trim()) {
      setMessage("برای عودت گزارش، دلیل اصلاح را ثبت کنید.");
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
    } finally {
      setBusyId(null);
    }
  }

  const items = work?.items ?? [];
  const unread = notifications.filter((item) => item.readAt === null).length;
  return (
    <section className="my-work-center" aria-label="کارهای من و اعلان‌ها">
      <article className="operational-card my-work-card">
        <div className="card-heading">
          <div><p className="eyebrow">کارتابل یکپارچه</p><h2>کارهای من</h2></div>
          <span className={items.some((item) => item.isOverdue) ? "count-badge warning" : "count-badge"}>
            {items.length.toLocaleString("fa-IR")}
          </span>
        </div>
        <p className="microcopy" aria-live="polite">{message}</p>
        <div className="my-work-list">
          {items.map((item) => (
            <div className={`my-work-item${item.isOverdue ? " overdue" : ""}`} key={item.id}>
              <div className="work-summary">
                <span className={`priority-badge priority-${item.priority.toLowerCase()}`}>{priorityLabel(item.priority)}</span>
                <strong>{item.title}</strong>
                <small>{workDate(item)} · {statusLabel(item.status)}{item.isOverdue ? " · عقب‌افتاده" : ""}</small>
                {item.description && <p>{item.description}</p>}
              </div>
              {item.kind === "ManagementAction" && (
                <div className="review-actions">
                  {item.status !== "InProgress" && <button className="secondary-button" type="button" disabled={!props.isOnline || busyId === item.id} onClick={() => void transition(item, "InProgress")}>شروع</button>}
                  <button type="button" disabled={!props.isOnline || busyId === item.id} onClick={() => void transition(item, "Done")}>انجام شد</button>
                </div>
              )}
              {item.kind === "DailyReportReview" && (
                <div className="work-review-controls">
                  <textarea rows={2} aria-label={`نظر ${item.title}`} placeholder="نظر اختیاری؛ برای عودت، دلیل الزامی است" value={comments[item.id] ?? ""} onChange={(event) => setComments((current) => ({ ...current, [item.id]: event.target.value }))} />
                  <div className="review-actions">
                    <button type="button" disabled={!props.isOnline || busyId === item.id} onClick={() => void review(item, "approve")}>تأیید</button>
                    <button className="secondary-button" type="button" disabled={!props.isOnline || busyId === item.id} onClick={() => void review(item, "return")}>عودت برای اصلاح</button>
                  </div>
                </div>
              )}
              {item.kind === "DailyReportCorrection" && <a className="inline-link" href="#daily-report-history">بازکردن نسخه اصلاحی</a>}
            </div>
          ))}
          {items.length === 0 && <p className="muted">در حال حاضر موردی برای اقدام شما وجود ندارد.</p>}
        </div>
      </article>

      <article className="operational-card notification-card">
        <div className="card-heading">
          <div><p className="eyebrow">اعلان داخل سامانه</p><h2>اعلان‌های من</h2></div>
          <span className={unread > 0 ? "count-badge warning" : "count-badge"}>{unread.toLocaleString("fa-IR")}</span>
        </div>
        <div className="notification-list">
          {notifications.slice(0, 20).map((notification) => (
            <div className={`notification-item${notification.readAt ? " read" : ""}`} key={notification.id}>
              <div><strong>{notification.title}</strong><small>{formatPersianDateTime(notification.occurredAt)}</small><p>{notification.body}</p></div>
              <div className="review-actions">
                {!notification.readAt && <button className="secondary-button" type="button" disabled={!props.isOnline || busyId === notification.id} onClick={() => void receipt(notification, "read")}>خواندم</button>}
                {!notification.acknowledgedAt && <button type="button" disabled={!props.isOnline || busyId === notification.id} onClick={() => void receipt(notification, "acknowledge")}>تأیید دریافت</button>}
              </div>
            </div>
          ))}
          {notifications.length === 0 && <p className="muted">اعلان فعالی وجود ندارد.</p>}
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

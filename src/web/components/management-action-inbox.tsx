"use client";

import { useCallback, useEffect, useState } from "react";
import {
  listActions,
  transitionAction,
  type ManagementActionModel,
  type ManagementActionStatus,
} from "@/lib/actions";
import { toUserMessage } from "@/lib/localization";
import { formatPersianDate } from "@/lib/persian-date";

interface ManagementActionInboxProps {
  readonly apiBaseUrl: string;
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly isOnline: boolean;
  readonly refreshToken: number;
  readonly onChanged: () => void;
}

export function ManagementActionInbox(props: ManagementActionInboxProps) {
  const [actions, setActions] = useState<readonly ManagementActionModel[]>([]);
  const [message, setMessage] = useState("در حال دریافت اقدامات رسمی…");
  const [busyId, setBusyId] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!props.isOnline) {
      setMessage("فهرست اقدامات رسمی فقط هنگام اتصال به سرور تازه می‌شود.");
      return;
    }

    try {
      const result = await listActions(
        props.apiBaseUrl,
        { tenantId: props.tenantId, userId: props.userId },
        props.projectId,
      );
      setActions(result);
      setMessage(result.length === 0 ? "هنوز اقدام رسمی ساخته نشده است." : "اقدامات رسمی پروژه");
    } catch (error) {
      setMessage(toUserMessage(error, "فهرست اقدامات دریافت نشد."));
    }
  }, [props.apiBaseUrl, props.isOnline, props.projectId, props.tenantId, props.userId]);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => void load(), 0);
    return () => window.clearTimeout(timeoutId);
  }, [load, props.refreshToken]);

  async function transition(action: ManagementActionModel, targetStatus: ManagementActionStatus) {
    setBusyId(action.id);
    try {
      await transitionAction(
        props.apiBaseUrl,
        { tenantId: props.tenantId, userId: props.userId },
        props.projectId,
        action.id,
        action.revision,
        targetStatus,
      );
      await load();
      props.onChanged();
    } catch (error) {
      setMessage(toUserMessage(error, "تغییر وضعیت اقدام ناموفق بود."));
    } finally {
      setBusyId(null);
    }
  }

  const openActions = actions.filter((action) => action.status !== "Done" && action.status !== "Cancelled");
  return (
    <article className="operational-card">
      <div className="card-heading">
        <div>
          <p className="eyebrow">کنترل اقدامات</p>
          <h2>اقدامات باز</h2>
        </div>
        <span className="count-badge">{openActions.length.toLocaleString("fa-IR")}</span>
      </div>
      <p className="microcopy">{message}</p>
      <div className="action-list">
        {openActions.slice(0, 8).map((action) => (
          <div className="action-item" key={action.id}>
            <div>
              <span className={`priority-badge priority-${action.priority.toLowerCase()}`}>{priorityLabel(action.priority)}</span>
              <strong>{action.title}</strong>
              <small>{action.assigneeDisplayName} · مهلت {formatDate(action.dueDate)} · {statusLabel(action.status)}</small>
            </div>
            <div className="review-actions">
              {action.status !== "InProgress" && (
                <button
                  className="secondary-button"
                  type="button"
                  disabled={busyId === action.id || !props.isOnline}
                  onClick={() => void transition(action, "InProgress")}
                >
                  شروع
                </button>
              )}
              <button
                type="button"
                disabled={busyId === action.id || !props.isOnline}
                onClick={() => void transition(action, "Done")}
              >
                انجام شد
              </button>
            </div>
          </div>
        ))}
      </div>
    </article>
  );
}

function priorityLabel(priority: ManagementActionModel["priority"]): string {
  return ({ Low: "کم", Medium: "متوسط", High: "زیاد", Critical: "بحرانی" })[priority];
}

function statusLabel(status: ManagementActionStatus): string {
  return ({ Open: "باز", InProgress: "در حال انجام", Blocked: "متوقف", Done: "انجام‌شده", Cancelled: "لغوشده" })[status];
}

function formatDate(value: string): string {
  return formatPersianDate(value);
}

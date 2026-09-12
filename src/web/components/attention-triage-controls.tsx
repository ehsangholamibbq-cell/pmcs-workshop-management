"use client";

import { useMemo, useState } from "react";
import { PersianDateInput } from "@/components/persian-date-input";
import { createActionFromAttention, dismissAttention, type ActionPriority } from "@/lib/actions";
import type { ProjectAttentionItem } from "@/lib/command-center";
import { toUserMessage } from "@/lib/localization";
import { futureProjectDate } from "@/lib/persian-date";

interface AttentionTriageControlsProps {
  readonly apiBaseUrl: string;
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly item: ProjectAttentionItem;
  readonly isOnline: boolean;
  readonly onChanged: () => void;
}

export function AttentionTriageControls(props: AttentionTriageControlsProps) {
  const [mode, setMode] = useState<"closed" | "action" | "dismiss">("closed");
  const [priority, setPriority] = useState<ActionPriority | "">(
    props.item.priority === "Unassessed" ? "" : props.item.priority,
  );
  const [dueDate, setDueDate] = useState(() => futureProjectDate(3));
  const [reason, setReason] = useState("");
  const [message, setMessage] = useState("");
  const [isBusy, setIsBusy] = useState(false);
  const handledLabel = useMemo(() => ({
    ConvertedToAction: "به اقدام تبدیل شد",
    Dismissed: "با دلیل بسته شد",
    NeedsTriage: "نیازمند تعیین تکلیف",
  })[props.item.disposition], [props.item.disposition]);

  if (props.item.disposition !== "NeedsTriage") {
    return (
      <div className="triage-result">
        <span>{handledLabel}</span>
        {props.item.dispositionReason && <small>{props.item.dispositionReason}</small>}
      </div>
    );
  }

  if (!props.isOnline) {
    return <small className="triage-note">تعیین تکلیف مدیریتی هنگام اتصال به سرور انجام می‌شود.</small>;
  }

  async function createAction() {
    if (!priority) {
      setMessage("اولویت اقدام را مشخص کنید؛ مقدار ارزیابی‌نشده به‌صورت خودکار تبدیل نمی‌شود.");
      return;
    }

    setIsBusy(true);
    setMessage("در حال ایجاد اقدام رسمی…");
    try {
      await createActionFromAttention(
        props.apiBaseUrl,
        { tenantId: props.tenantId, userId: props.userId },
        props.projectId,
        props.item.sourceFactId,
        {
          assigneeUserId: props.userId,
          dueDate,
          priority,
          title: props.item.description,
        },
      );
      props.onChanged();
    } catch (error) {
      setMessage(toUserMessage(error, "ایجاد اقدام ناموفق بود."));
    } finally {
      setIsBusy(false);
    }
  }

  async function dismiss() {
    if (!reason.trim()) {
      setMessage("ثبت دلیل برای بستن مورد الزامی است.");
      return;
    }

    setIsBusy(true);
    setMessage("در حال ثبت تصمیم…");
    try {
      await dismissAttention(
        props.apiBaseUrl,
        { tenantId: props.tenantId, userId: props.userId },
        props.projectId,
        props.item.sourceFactId,
        reason.trim(),
      );
      props.onChanged();
    } catch (error) {
      setMessage(toUserMessage(error, "ثبت تصمیم ناموفق بود."));
    } finally {
      setIsBusy(false);
    }
  }

  return (
    <div className="triage-controls">
      {mode === "closed" && (
        <div className="review-actions">
          <button type="button" onClick={() => setMode("action")}>تبدیل به اقدام</button>
          <button className="secondary-button" type="button" onClick={() => setMode("dismiss")}>بستن با دلیل</button>
        </div>
      )}
      {mode === "action" && (
        <div className="triage-form">
          <label>
            اولویت
            <select value={priority} onChange={(event) => setPriority(event.target.value as ActionPriority | "")}>
              <option value="">انتخاب کنید</option>
              <option value="Low">کم</option>
              <option value="Medium">متوسط</option>
              <option value="High">زیاد</option>
              <option value="Critical">بحرانی</option>
            </select>
          </label>
          <label>
            مهلت
            <PersianDateInput value={dueDate} onChange={setDueDate} required ariaLabel="مهلت شمسی اقدام" />
          </label>
          <div className="review-actions">
            <button type="button" disabled={isBusy} onClick={() => void createAction()}>ثبت اقدام برای خودم</button>
            <button className="secondary-button" type="button" onClick={() => setMode("closed")}>انصراف</button>
          </div>
        </div>
      )}
      {mode === "dismiss" && (
        <div className="triage-form">
          <label>
            دلیل بستن
            <textarea rows={2} value={reason} onChange={(event) => setReason(event.target.value)} />
          </label>
          <div className="review-actions">
            <button type="button" disabled={isBusy} onClick={() => void dismiss()}>ثبت تصمیم</button>
            <button className="secondary-button" type="button" onClick={() => setMode("closed")}>انصراف</button>
          </div>
        </div>
      )}
      {message && <output aria-live="polite">{message}</output>}
    </div>
  );
}

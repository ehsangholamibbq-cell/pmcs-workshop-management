"use client";

import { type FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import {
  createMeasurementItem,
  deactivateMeasurementItem,
  getProgressLedger,
  listMeasurementItems,
  type MeasurementItemModel,
  type ProgressLedgerModel,
} from "@/lib/planning";
import { toUserMessage } from "@/lib/localization";
import { formatPersianDate } from "@/lib/persian-date";
import { PlanningBasisControl } from "@/components/planning-basis-control";

interface PlanningProgressPanelProps {
  readonly apiBaseUrl: string;
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly isOnline: boolean;
  readonly refreshToken: number;
  readonly onItemsChanged: (items: readonly MeasurementItemModel[]) => void;
  readonly onChanged?: () => void;
}

export function PlanningProgressPanel(props: PlanningProgressPanelProps) {
  const {
    apiBaseUrl,
    tenantId,
    userId,
    projectId,
    isOnline,
    refreshToken,
    onItemsChanged,
    onChanged,
  } = props;
  const [items, setItems] = useState<readonly MeasurementItemModel[]>([]);
  const [ledger, setLedger] = useState<ProgressLedgerModel | null>(null);
  const [code, setCode] = useState("");
  const [title, setTitle] = useState("");
  const [unit, setUnit] = useState("");
  const [target, setTarget] = useState("");
  const [notes, setNotes] = useState("");
  const [message, setMessage] = useState("در حال دریافت دفتر پیشرفت…");
  const [busy, setBusy] = useState(false);
  const identity = useMemo(
    () => ({ tenantId, userId }),
    [tenantId, userId],
  );

  const load = useCallback(async () => {
    if (!isOnline) {
      setMessage("دفتر پیشرفت رسمی هنگام اتصال به سرور به‌روز می‌شود؛ ثبت واقعیت آفلاین همچنان فعال است.");
      return;
    }

    try {
      const [nextItems, nextLedger] = await Promise.all([
        listMeasurementItems(apiBaseUrl, identity, projectId),
        getProgressLedger(apiBaseUrl, identity, projectId),
      ]);
      setItems(nextItems);
      setLedger(nextLedger);
      onItemsChanged(nextItems.filter((item) => item.status === "Active"));
      setMessage(progressMessage(nextLedger));
    } catch (error) {
      setMessage(toUserMessage(error, "دفتر پیشرفت دریافت نشد یا دسترسی این کاربر محدود است."));
    }
  }, [apiBaseUrl, identity, isOnline, onItemsChanged, projectId]);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => void load(), 0);
    return () => window.clearTimeout(timeoutId);
  }, [load, refreshToken]);

  async function createItem(event: FormEvent) {
    event.preventDefault();
    const targetQuantity = target.trim() ? Number(target) : null;
    if (!code.trim() || !title.trim() || !unit.trim() ||
        (targetQuantity !== null && (!Number.isFinite(targetQuantity) || targetQuantity <= 0))) {
      setMessage("کد، عنوان و واحد الزامی‌اند؛ مقدار هدف در صورت ثبت باید عددی مثبت باشد.");
      return;
    }

    setBusy(true);
    setMessage("در حال ثبت قلم اندازه‌گیری…");
    try {
      await createMeasurementItem(apiBaseUrl, identity, projectId, {
        code,
        title,
        unit,
        targetQuantity,
        notes,
      });
      setCode("");
      setTitle("");
      setUnit("");
      setTarget("");
      setNotes("");
      await load();
      onChanged?.();
    } catch (error) {
      setMessage(toUserMessage(error, "ثبت قلم اندازه‌گیری ناموفق بود."));
    } finally {
      setBusy(false);
    }
  }

  async function deactivate(itemId: string) {
    const item = items.find((candidate) => candidate.id === itemId);
    if (!item) {
      setMessage("قلم اندازه‌گیری در فهرست فعلی پیدا نشد؛ صفحه را تازه کنید.");
      return;
    }

    setBusy(true);
    try {
      await deactivateMeasurementItem(apiBaseUrl, identity, projectId, item);
      await load();
      onChanged?.();
    } catch (error) {
      setMessage(toUserMessage(error, "غیرفعال‌کردن قلم اندازه‌گیری ناموفق بود."));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="section-block planning-progress" id="progress">
      <div className="section-title">
        <div>
          <p className="eyebrow">برنامه‌ریزی و پیشرفت</p>
          <h2>دفتر مستقل اندازه‌گیری پیشرفت</h2>
        </div>
        <span className="section-note">موقت و تأییدشده جدا هستند</span>
      </div>

      <div className="progress-rule-grid">
        <article>
          <span>حالت برنامه‌ریزی</span>
          <strong>{planningModeLabel(ledger?.planningMode)}</strong>
          <small>{ledger?.planningMode === "None"
            ? "نبود برنامه معتبر است؛ انحراف زمان‌بندی ساخته نمی‌شود."
            : "محاسبه زمان‌بندی فقط با مبنای رسمی همان حالت انجام می‌شود."}</small>
        </article>
        <article>
          <span>پیشرفت کل رسمی</span>
          <strong>{formatPercent(ledger?.officialOverallPhysicalPercent)}</strong>
          <small>فقط از وزن‌های مصوب و پیشرفت‌های تأییدشده ساخته می‌شود.</small>
        </article>
        <article>
          <span>پیشرفت برنامه‌ای</span>
          <strong>{formatPercent(ledger?.plannedOverallPhysicalPercent)}</strong>
          <small>فقط با خط مبنای زمان‌بندی مصوب محاسبه می‌شود.</small>
        </article>
        <article>
          <span>انحراف برنامه</span>
          <strong>{formatPercent(ledger?.scheduleVariancePercent)}</strong>
          <small>عدد صفر جایگزین «مبنای برنامه وجود ندارد» نمی‌شود.</small>
        </article>
      </div>

      <p className="calculation-note" aria-live="polite">{message}</p>

      <form className="measurement-form" onSubmit={createItem}>
        <label><span>کد قلم</span><input value={code} onChange={(event) => setCode(event.target.value)} placeholder="مثال: بتن-۰۱" /></label>
        <label><span>عنوان قلم</span><input value={title} onChange={(event) => setTitle(event.target.value)} placeholder="مثال: بتن‌ریزی سقف" /></label>
        <label><span>واحد</span><input value={unit} onChange={(event) => setUnit(event.target.value)} placeholder="مترمکعب" /></label>
        <label><span>مقدار هدف اختیاری</span><input inputMode="decimal" value={target} onChange={(event) => setTarget(event.target.value)} placeholder="اختیاری" /></label>
        <label className="wide"><span>یادداشت اختیاری</span><input value={notes} onChange={(event) => setNotes(event.target.value)} /></label>
        <button type="submit" disabled={busy || !isOnline}>افزودن قلم</button>
      </form>

      {ledger && ledger.items.length > 0 ? (
        <div className="progress-table-wrap">
          <table className="progress-table">
            <thead><tr><th>قلم اندازه‌گیری</th><th>هدف</th><th>مقدار تأییدشده</th><th>مقدار موقت</th><th>درصد قلم</th><th>وضعیت</th></tr></thead>
            <tbody>
              {ledger.items.map((item) => (
                <tr key={item.measurementItemId}>
                  <td><strong>{item.title}</strong><small>{item.code}</small></td>
                  <td>{formatQuantity(item.targetQuantity, item.unit)}</td>
                  <td>{formatQuantity(item.approvedQuantity, item.unit)}</td>
                  <td>{formatQuantity(item.provisionalQuantity, item.unit)}</td>
                  <td>{formatPercent(item.approvedCompletionPercent)}</td>
                  <td>{item.isActive
                    ? <button type="button" disabled={busy} onClick={() => void deactivate(item.measurementItemId)}>غیرفعال‌کردن</button>
                    : "غیرفعال"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <p className="empty-state">هنوز قلم اندازه‌گیری تعریف نشده است؛ واقعیت اجرایی بدون WBS همچنان قابل ثبت است.</p>
      )}

      {ledger && (ledger.unlinkedApprovedFactCount > 0 || ledger.unlinkedProvisionalFactCount > 0) && (
        <p className="progress-unlinked">
          {ledger.unlinkedApprovedFactCount.toLocaleString("fa-IR")} واقعیت تأییدشده و {ledger.unlinkedProvisionalFactCount.toLocaleString("fa-IR")} واقعیت موقت به قلم اندازه‌گیری متصل نیستند؛ این داده‌ها حذف یا به‌زور تخصیص داده نشده‌اند.
        </p>
      )}

      {ledger && ledger.milestones.length > 0 && (
        <div className="progress-table-wrap">
          <table className="progress-table">
            <thead><tr><th>نقطه عطف</th><th>تاریخ برنامه</th><th>وزن</th><th>پیشرفت تأییدشده</th><th>وضعیت موعد</th></tr></thead>
            <tbody>
              {ledger.milestones.map((item) => (
                <tr key={item.baselineEntryId}>
                  <td><strong>{item.title}</strong><small>{item.code}</small></td>
                  <td>{formatDate(item.plannedDate)}</td>
                  <td>{formatPercent(item.weightPercent)}</td>
                  <td>{formatPercent(item.approvedProgressPercent)}</td>
                  <td>{milestoneStateLabel(item.scheduleState)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <PlanningBasisControl
        apiBaseUrl={apiBaseUrl}
        tenantId={tenantId}
        userId={userId}
        projectId={projectId}
        isOnline={isOnline}
        refreshToken={refreshToken}
        measurementItems={items}
        ledger={ledger}
        onChanged={onChanged}
      />
    </section>
  );
}

function planningModeLabel(mode?: ProgressLedgerModel["planningMode"]): string {
  return ({
    None: "بدون برنامه زمان‌بندی",
    SimpleWorkList: "فهرست ساده کارها",
    Milestones: "نقاط عطف",
    WbsBaseline: "خط مبنای ساختار شکست کار (WBS)",
    ExternalSchedule: "برنامه زمان‌بندی بیرونی",
  } as const)[mode ?? "None"];
}

function formatQuantity(value: number | null | undefined, unit: string): string {
  return value === null || value === undefined
    ? "تعریف نشده"
    : `${value.toLocaleString("fa-IR", { maximumFractionDigits: 3 })} ${unit}`;
}

function formatPercent(value: number | null | undefined): string {
  return value === null || value === undefined ? "قابل محاسبه نیست" : `${value.toLocaleString("fa-IR")}٪`;
}

function formatDate(value: string): string {
  return formatPersianDate(value);
}

function milestoneStateLabel(value: ProgressLedgerModel["milestones"][number]["scheduleState"]): string {
  return ({ Upcoming: "آینده", Due: "موعد امروز", Late: "عقب‌افتاده", Completed: "تکمیل‌شده" })[value];
}

function progressMessage(ledger: ProgressLedgerModel): string {
  if (ledger.approvedFactCount === 0 && ledger.provisionalFactCount === 0) {
    return "هنوز واقعیت پیشرفتِ ارسال‌شده یا تأییدشده‌ای ثبت نشده است.";
  }
  if (ledger.officialProgressBasisState === "IncompleteActualData") {
    return `${ledger.missingActualEntryCount.toLocaleString("fa-IR")} ردیف وزن‌دار هنوز داده رسمی کافی ندارد؛ درصد کل محاسبه نشده است.`;
  }
  if (ledger.officialProgressBasisState === "ModeMismatch") {
    return "مبنای مصوب قبلی با حالت فعلی برنامه‌ریزی سازگار نیست و در محاسبات استفاده نمی‌شود.";
  }
  return `${ledger.approvedFactCount.toLocaleString("fa-IR")} واقعیت تأییدشده و ${ledger.provisionalFactCount.toLocaleString("fa-IR")} واقعیت موقت جداگانه نمایش داده می‌شوند.`;
}

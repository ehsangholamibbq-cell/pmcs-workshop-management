"use client";

import { type FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import {
  addDailyReportFact,
  getDailyReport,
  listDailyReports,
  removeDailyReportFact,
  reviseDailyReportDetails,
  startDailyReportCorrection,
  submitDailyReport,
  type DailyReportSummary,
} from "@/lib/daily-reports";
import {
  buildDailyFactPayload,
  emptyFactDraft,
  factFields,
  factKinds,
  FactValidationError,
  type DailyFactDraft,
} from "@/lib/field-facts";
import { toUserMessage } from "@/lib/localization";
import { formatPersianDate } from "@/lib/persian-date";
import type { MeasurementItemModel } from "@/lib/planning";
import type { ProjectLocationModel } from "@/lib/projects";

interface DailyReportHistoryProps {
  readonly apiBaseUrl: string;
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly isOnline: boolean;
  readonly refreshToken: number;
  readonly locations: readonly ProjectLocationModel[];
  readonly measurementItems: readonly MeasurementItemModel[];
  readonly onChanged: () => void;
}

export function DailyReportHistory(props: DailyReportHistoryProps) {
  const [reports, setReports] = useState<readonly DailyReportSummary[]>([]);
  const [selected, setSelected] = useState<DailyReportSummary | null>(null);
  const [reasons, setReasons] = useState<Record<string, string>>({});
  const [locationName, setLocationName] = useState("");
  const [narrative, setNarrative] = useState("");
  const [draft, setDraft] = useState<DailyFactDraft>(emptyFactDraft);
  const [busy, setBusy] = useState<string | null>(null);
  const [message, setMessage] = useState("در حال دریافت تاریخچه نسخه‌ها…");
  const actorIdentity = useMemo(
    () => ({ tenantId: props.tenantId, userId: props.userId }),
    [props.tenantId, props.userId],
  );
  const fields = useMemo(() => factFields(draft.kind), [draft.kind]);
  const activeLocations = useMemo(
    () => props.locations.filter((location) => location.status === "Active"),
    [props.locations],
  );
  const selectedId = selected?.id ?? null;

  const load = useCallback(async () => {
    if (!props.isOnline) {
      setMessage("تاریخچه رسمی و ایجاد نسخه اصلاحی هنگام اتصال به سرور در دسترس است.");
      return;
    }
    try {
      const result = await listDailyReports(props.apiBaseUrl, actorIdentity, props.projectId);
      setReports(result);
      setMessage(result.length === 0 ? "گزارش رسمی ثبت نشده است." : "نسخه‌ها بدون حذف سابقه نمایش داده می‌شوند.");
      if (selectedId) {
        const current = await getDailyReport(props.apiBaseUrl, actorIdentity, props.projectId, selectedId);
        if (current) selectEditor(current);
      }
    } catch (error) {
      setMessage(toUserMessage(error, "تاریخچه گزارش‌ها دریافت نشد."));
    }
  }, [actorIdentity, props.apiBaseUrl, props.isOnline, props.projectId, selectedId]);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => void load(), 0);
    return () => window.clearTimeout(timeoutId);
  }, [load, props.refreshToken]);

  async function open(reportId: string) {
    setBusy(`open:${reportId}`);
    try {
      const report = await getDailyReport(props.apiBaseUrl, actorIdentity, props.projectId, reportId);
      if (report) selectEditor(report);
    } catch (error) {
      setMessage(toUserMessage(error, "جزئیات نسخه دریافت نشد."));
    } finally {
      setBusy(null);
    }
  }

  async function startCorrection(report: DailyReportSummary) {
    const reason = reasons[report.id]?.trim() ?? "";
    if (!reason) {
      setMessage("دلیل ایجاد نسخه اصلاحی الزامی است.");
      return;
    }
    setBusy(`correction:${report.id}`);
    try {
      const correction = await startDailyReportCorrection(
        props.apiBaseUrl,
        actorIdentity,
        props.projectId,
        report.id,
        report.revision,
        reason,
      );
      setReasons((current) => ({ ...current, [report.id]: "" }));
      selectEditor(correction);
      setMessage("نسخه اصلاحی با کپی قابل‌ردیابی واقعیت‌ها ساخته شد؛ نسخه رسمی قبلی هنوز معتبر است.");
      await load();
      props.onChanged();
    } catch (error) {
      setMessage(toUserMessage(error, "نسخه اصلاحی ایجاد نشد."));
    } finally {
      setBusy(null);
    }
  }

  async function saveDetails(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selected) return;
    await updateSelected("details", () => reviseDailyReportDetails(
      props.apiBaseUrl,
      actorIdentity,
      props.projectId,
      selected.id,
      selected.revision,
      locationName,
      narrative,
    ), "مشخصات نسخه اصلاحی ذخیره شد.");
  }

  async function addFact(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selected) return;
    try {
      if (!draft.locationId) throw new FactValidationError("محل پروژه برای واقعیت اصلاحی الزامی است.");
      const payload = buildDailyFactPayload(draft, crypto.randomUUID(), selected.reportDate);
      await updateSelected("add-fact", () => addDailyReportFact(
        props.apiBaseUrl,
        actorIdentity,
        props.projectId,
        selected.id,
        selected.revision,
        payload,
      ), "واقعیت اصلاحی افزوده شد.");
      setDraft((current) => ({
        ...emptyFactDraft,
        kind: current.kind,
        locationId: current.locationId,
        locationName: current.locationName,
      }));
    } catch (error) {
      setMessage(error instanceof FactValidationError ? error.message : toUserMessage(error, "واقعیت افزوده نشد."));
    }
  }

  async function removeFact(factId: string) {
    if (!selected) return;
    await updateSelected(`remove:${factId}`, () => removeDailyReportFact(
      props.apiBaseUrl,
      actorIdentity,
      props.projectId,
      selected.id,
      factId,
      selected.revision,
    ), "واقعیت از نسخه اصلاحی حذف شد؛ نسخه قبلی دست‌نخورده باقی ماند.");
  }

  async function submitCorrection() {
    if (!selected) return;
    await updateSelected("submit", () => submitDailyReport(
      props.apiBaseUrl,
      actorIdentity,
      props.projectId,
      selected.id,
      selected.revision,
    ), "نسخه اصلاحی برای بازبینی ارسال شد.");
  }

  async function updateSelected(
    key: string,
    operation: () => Promise<DailyReportSummary>,
    success: string,
  ) {
    setBusy(key);
    try {
      const updated = await operation();
      selectEditor(updated);
      setMessage(success);
      await load();
      props.onChanged();
    } catch (error) {
      setMessage(toUserMessage(error, "عملیات نسخه اصلاحی انجام نشد."));
    } finally {
      setBusy(null);
    }
  }

  const selectedEditable = selected && (selected.status === "Draft" || selected.status === "Returned");
  return (
    <section className="operational-card daily-report-history" data-testid="daily-report-history" id="daily-report-history">
      <div className="card-heading">
        <div><p className="eyebrow">ردیابی اصلاحات</p><h2>نسخه‌های گزارش روزانه</h2></div>
        <span className="count-badge">{reports.length.toLocaleString("fa-IR")}</span>
      </div>
      <p className="microcopy" aria-live="polite">{message}</p>
      <div className="report-version-list">
        {reports.slice(0, 20).map((report) => {
          const activeCorrection = reports.some((candidate) => candidate.supersedesReportId === report.id && ["Draft", "Submitted", "Returned"].includes(candidate.status));
          return (
            <div className="report-version-item" data-testid="daily-report-version" data-entity-id={report.id} key={report.id}>
              <div>
                <strong>{formatPersianDate(report.reportDate, "full")}</strong>
                <small>نسخه {report.versionNumber.toLocaleString("fa-IR")} · {statusLabel(report.status)} · {report.factCount.toLocaleString("fa-IR")} واقعیت</small>
                {report.correctionReason && <p>دلیل اصلاح: {report.correctionReason}</p>}
              </div>
              <div className="review-actions">
                <button className="secondary-button" data-testid="daily-report-version-open" type="button" disabled={!props.isOnline || busy !== null} onClick={() => void open(report.id)}>جزئیات</button>
              </div>
              {report.status === "Approved" && !report.supersededByReportId && !activeCorrection && (
                <div className="correction-start">
                  <textarea data-testid="daily-report-correction-reason" rows={2} aria-label={`دلیل اصلاح گزارش ${formatPersianDate(report.reportDate)}`} placeholder="دلیل مستند ایجاد نسخه اصلاحی" value={reasons[report.id] ?? ""} onChange={(event) => setReasons((current) => ({ ...current, [report.id]: event.target.value }))} />
                  <button data-testid="daily-report-correction-start" type="button" disabled={!props.isOnline || busy !== null} onClick={() => void startCorrection(report)}>ایجاد نسخه اصلاحی</button>
                </div>
              )}
            </div>
          );
        })}
      </div>

      {selected && (
        <div className="correction-editor" data-testid="daily-report-correction-editor" data-entity-id={selected.id}>
          <div className="section-title"><div><p className="eyebrow">نسخه انتخاب‌شده</p><h3>گزارش {formatPersianDate(selected.reportDate)} · نسخه {selected.versionNumber.toLocaleString("fa-IR")}</h3></div><span className="section-note">{statusLabel(selected.status)}</span></div>
          {selectedEditable && (
            <form className="correction-details" onSubmit={saveDetails}>
              <label>شرح محل کلی<input value={locationName} onChange={(event) => setLocationName(event.target.value)} /></label>
              <label>شرح تکمیلی<textarea rows={2} value={narrative} onChange={(event) => setNarrative(event.target.value)} /></label>
              <button data-testid="daily-report-correction-save" type="submit" disabled={!props.isOnline || busy !== null}>ذخیره مشخصات</button>
            </form>
          )}
          <div className="correction-facts">
            {(selected.facts ?? []).map((fact) => (
              <div className="correction-fact" data-testid="daily-report-correction-fact" data-entity-id={fact.id} key={fact.id}>
                <div><strong>{factKindLabel(fact.kind)} · {fact.description}</strong><small>{fact.locationName ?? "بدون محل"}{fact.copiedFromFactId ? " · کپی ردیابی‌شده از نسخه قبل" : " · ثبت جدید"}</small></div>
                {selectedEditable && <button className="secondary-button" data-testid="daily-report-correction-fact-remove" type="button" disabled={!props.isOnline || busy !== null} onClick={() => void removeFact(fact.id)}>حذف از نسخه اصلاحی</button>}
              </div>
            ))}
          </div>
          {selectedEditable && (
            <>
              <form className="capture-form correction-fact-form" onSubmit={addFact}>
                <div className="field"><label>نوع واقعیت<select value={draft.kind} onChange={(event) => setDraft((current) => ({ ...current, kind: event.target.value as DailyFactDraft["kind"] }))}>{factKinds.map((kind) => <option key={kind.value} value={kind.value}>{kind.label}</option>)}</select></label></div>
                <div className="field"><label>محل پروژه<select required value={draft.locationId ?? ""} onChange={(event) => selectLocation(event.target.value)}><option value="">انتخاب محل</option>{activeLocations.map((location) => <option key={location.id} value={location.id}>{location.code} · {location.name}</option>)}</select></label></div>
                {fields.category && <div className="field"><label>دسته یا عنوان<input value={draft.category} onChange={(event) => updateDraft("category", event.target.value)} /></label></div>}
                {draft.kind === "WorkProgress" && props.measurementItems.length > 0 && <div className="field"><label>قلم اندازه‌گیری<select value={draft.measurementItemId} onChange={(event) => selectMeasurement(event.target.value)}><option value="">بدون قلم</option>{props.measurementItems.map((item) => <option key={item.id} value={item.id}>{item.code} · {item.title}</option>)}</select></label></div>}
                {fields.quantity && <><div className="field compact"><label>مقدار<input inputMode="decimal" value={draft.quantity} onChange={(event) => updateDraft("quantity", event.target.value)} /></label></div><div className="field compact"><label>واحد<input value={draft.unit} onChange={(event) => updateDraft("unit", event.target.value)} /></label></div></>}
                {fields.resources && <><div className="field compact"><label>تعداد<input inputMode="numeric" value={draft.resourceCount} onChange={(event) => updateDraft("resourceCount", event.target.value)} /></label></div><div className="field compact"><label>ساعت<input inputMode="decimal" value={draft.hours} onChange={(event) => updateDraft("hours", event.target.value)} /></label></div></>}
                {fields.impact && <div className="field"><label>شدت اثر<select value={draft.impactLevel} onChange={(event) => updateDraft("impactLevel", event.target.value as DailyFactDraft["impactLevel"])}><option value="">ارزیابی نشده</option><option value="Low">کم</option><option value="Medium">متوسط</option><option value="High">زیاد</option><option value="Critical">بحرانی</option></select></label></div>}
                {fields.reference && <div className="field"><label>مرجع<input value={draft.referenceCode} onChange={(event) => updateDraft("referenceCode", event.target.value)} /></label></div>}
                <div className="field full-width"><label>شرح واقعیت<textarea required rows={2} value={draft.description} onChange={(event) => updateDraft("description", event.target.value)} /></label></div>
                <div className="form-actions full-width"><button data-testid="daily-report-correction-fact-add" type="submit" disabled={!props.isOnline || busy !== null}>افزودن واقعیت اصلاحی</button><span className="draft-note">نسخه قبلی تغییر نمی‌کند</span></div>
              </form>
              <div className="correction-submit"><button data-testid="daily-report-correction-submit" type="button" disabled={!props.isOnline || busy !== null || (selected.facts?.length ?? 0) === 0} onClick={() => void submitCorrection()}>ارسال نسخه اصلاحی برای تأیید</button></div>
            </>
          )}
        </div>
      )}
    </section>
  );

  function selectEditor(report: DailyReportSummary) {
    setSelected(report);
    setLocationName(report.locationName ?? "");
    setNarrative(report.narrative ?? "");
  }

  function updateDraft<K extends keyof DailyFactDraft>(key: K, value: DailyFactDraft[K]) {
    setDraft((current) => ({ ...current, [key]: value }));
  }

  function selectLocation(locationId: string) {
    const location = activeLocations.find((item) => item.id === locationId);
    setDraft((current) => ({ ...current, locationId, locationName: location?.name ?? "" }));
  }

  function selectMeasurement(measurementItemId: string) {
    const item = props.measurementItems.find((candidate) => candidate.id === measurementItemId);
    setDraft((current) => ({ ...current, measurementItemId, category: item?.title ?? current.category, unit: item?.unit ?? current.unit }));
  }
}

function statusLabel(status: DailyReportSummary["status"]): string {
  return ({ Draft: "پیش‌نویس", Submitted: "در انتظار بازبینی", Returned: "عودت‌شده", Approved: "مصوب", Rejected: "ردشده", Superseded: "جایگزین‌شده" })[status];
}

function factKindLabel(kind: NonNullable<DailyReportSummary["facts"]>[number]["kind"]): string {
  return factKinds.find((item) => item.value === kind)?.label ?? kind;
}

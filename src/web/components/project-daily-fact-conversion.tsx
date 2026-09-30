"use client";

import { type FormEvent, useState } from "react";
import { useCollaborationCommandGate } from "@/components/collaboration-command-gate";
import { CollaborationAccessError } from "@/lib/collaboration-events";
import {
  CollaborationDailyFactAlreadyExists, CollaborationDailyFactTargetConflict,
  CollaborationDailyFactValidationError, convertProjectMessageToDailyFact,
  loadProjectMessageConversions,
} from "@/lib/collaboration-conversions";
import { CollaborationRevisionConflict } from "@/lib/collaboration-interactions";
import type { ProjectConversationMessage } from "@/lib/collaboration-room";
import { listDailyReports, type DailyReportSummary } from "@/lib/daily-reports";
import {
  buildDailyFactPayload, emptyFactDraft, factFields, factKinds, FactValidationError,
  type DailyFactDraft, type DailyFactKind, type DailyImpactLevel,
} from "@/lib/field-facts";
import { ApiRequestError } from "@/lib/localization";
import { formatPersianDate } from "@/lib/persian-date";
import { listProjectLocations, type ProjectLocationModel } from "@/lib/projects";

type Phase = "closed" | "checking" | "editing" | "sending" | "uncertain" |
  "message-conflict" | "report-conflict" | "done" | "exists";

export function ProjectDailyFactConversion({ projectId, message, tenantId, actorUserId,
  onAccessLoss, onChanged }: {
  readonly projectId: string; readonly message: ProjectConversationMessage;
  readonly tenantId: string; readonly actorUserId: string;
  readonly onAccessLoss: (status: number) => void; readonly onChanged: () => void;
}) {
  const { canCommand, isCurrent } = useCollaborationCommandGate();
  const [phase, setPhase] = useState<Phase>("closed");
  const [base, setBase] = useState(message);
  const [reports, setReports] = useState<readonly DailyReportSummary[]>([]);
  const [locations, setLocations] = useState<readonly ProjectLocationModel[]>([]);
  const [reportId, setReportId] = useState("");
  const [draft, setDraft] = useState<DailyFactDraft>({ ...emptyFactDraft,
    kind: "Note", description: message.body.slice(0, 1000) });
  const [identity, setIdentity] = useState<{ destinationId: string; key: string } | null>(null);
  const [confirmed, setConfirmed] = useState(false);
  const [status, setStatus] = useState("");
  const stale = (phase === "editing" || phase === "uncertain") && message.revision > base.revision;
  const fields = factFields(draft.kind);
  const report = reports.find((item) => item.id === reportId && item.status === "Draft");
  const location = locations.find((item) => item.id === draft.locationId && item.status === "Active");

  function fail(error: unknown, fallback: string) {
    if (error instanceof CollaborationAccessError) onAccessLoss(error.status);
    else if (error instanceof ApiRequestError && [401, 403].includes(error.status))
      onAccessLoss(error.status);
    else setStatus(fallback);
  }

  async function loadTargets() {
    if (!isCurrent()) throw new Error("خواندن جاری پروژه لازم است.");
    const identity = { tenantId, userId: actorUserId };
    const [allReports, allLocations] = await Promise.all([
      listDailyReports("/api/pmcs", identity, projectId),
      listProjectLocations("/api/pmcs", identity, projectId),
    ]);
    if (!Array.isArray(allReports) || !Array.isArray(allLocations) ||
        allReports.some((item) => item.projectId?.toLowerCase() !== projectId.toLowerCase() ||
          !Number.isSafeInteger(item.revision) || item.revision < 1 ||
          typeof item.reportDate !== "string") ||
        allLocations.some((item) => item.projectId?.toLowerCase() !== projectId.toLowerCase() ||
          typeof item.name !== "string")) {
      throw new Error("محدودهٔ گزارش و محل پروژه معتبر نیست.");
    }
    const drafts = allReports.filter((item) => item.status === "Draft");
    const active = allLocations.filter((item) => item.status === "Active");
    setReports(drafts);
    setLocations(active);
    return { drafts, active };
  }

  async function open() {
    if (!isCurrent() || phase !== "closed") return;
    setPhase("checking"); setStatus("");
    try {
      const entries = await loadProjectMessageConversions("/api/pmcs", projectId, message.id);
      if (entries.some((item) => item.destinationType === "DailyFact")) {
        setPhase("exists"); setStatus("برای این پیام واقعیت روزانهٔ رسمی قبلاً ثبت شده است؛ تبار را ببینید.");
        return;
      }
      const { drafts, active } = await loadTargets();
      if (drafts.length === 0 || active.length === 0) {
        setPhase("closed");
        setStatus("برای تبدیل، یک گزارش روزانهٔ پیش‌نویس و یک محل فعال در همین پروژه لازم است.");
        return;
      }
      setBase(message);
      setReportId(drafts[0].id);
      setDraft((current) => ({ ...current, locationId: active[0].id,
        locationName: active[0].name }));
      setIdentity({ destinationId: crypto.randomUUID(), key: crypto.randomUUID() });
      setConfirmed(false); setPhase("editing");
    } catch (error) {
      setPhase("closed"); fail(error, "بررسی تبار، گزارش یا محل پروژه کامل نشد؛ دوباره تلاش کنید.");
    }
  }

  async function refreshReport() {
    if (!isCurrent()) return;
    setStatus("در حال بازخوانی نسخهٔ گزارش…");
    try {
      const { drafts, active } = await loadTargets();
      const current = drafts.find((item) => item.id === reportId);
      if (!current || active.every((item) => item.id !== draft.locationId)) {
        setStatus("گزارش دیگر پیش‌نویس یا محل انتخاب‌شده دیگر فعال نیست؛ یک مقصد معتبر انتخاب کنید.");
        setPhase("closed"); return;
      }
      setIdentity({ destinationId: crypto.randomUUID(), key: crypto.randomUUID() });
      setConfirmed(false); setStatus(""); setPhase("editing");
    } catch (error) { fail(error, "بازخوانی گزارش انجام نشد؛ دوباره تلاش کنید."); }
  }

  async function reconcile() {
    if (!isCurrent()) return;
    try {
      const entries = await loadProjectMessageConversions("/api/pmcs", projectId, message.id);
      if (entries.some((item) => item.destinationType === "DailyFact")) {
        setPhase("done"); setStatus("واقعیت روزانهٔ رسمی در تبار پیام ثبت شده است."); onChanged();
      } else setStatus("هنوز واقعیت ثبت‌شده‌ای دیده نمی‌شود؛ تلاش مجدد فقط با همان شناسه انجام شود.");
    } catch (error) { fail(error, "بازبینی نتیجه کامل نشد؛ هویت درخواست حفظ شده است."); }
  }

  async function rebaseMessage() {
    if (!isCurrent() || message.revision <= base.revision || message.deletedAt || message.redactedAt) return;
    try {
      const entries = await loadProjectMessageConversions("/api/pmcs", projectId, message.id);
      if (entries.some(item => item.destinationType === "DailyFact")) {
        setPhase("exists"); setStatus("واقعیت روزانهٔ رسمی قبلاً در تبار پیام ثبت شده است."); return;
      }
      setBase(message); setIdentity({ destinationId: crypto.randomUUID(), key: crypto.randomUUID() });
      setConfirmed(false); setStatus(""); setPhase("editing");
    } catch (error) { fail(error, "بازخوانی تبار انجام نشد؛ پیش‌نویس حفظ شده است."); }
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!isCurrent() || stale || !identity || !confirmed || !report || !location ||
        (phase !== "editing" && phase !== "uncertain")) return;
    if (!navigator.onLine) { setStatus("تبدیل رسمی فقط هنگام اتصال به سرور انجام می‌شود."); return; }
    let fact;
    try {
      fact = buildDailyFactPayload(draft, identity.destinationId, report.reportDate);
      if (fact.measurementItemId) throw new FactValidationError("تبدیل پیام به قلم اندازه‌گیری در این مرحله پشتیبانی نمی‌شود.");
    } catch (error) {
      setStatus(error instanceof FactValidationError ? error.message : "مشخصات واقعیت معتبر نیست."); return;
    }
    setPhase("sending"); setStatus("در حال ثبت واقعیت در گزارش روزانهٔ پیش‌نویس…");
    try {
      const result = await convertProjectMessageToDailyFact("/api/pmcs", projectId, base,
        actorUserId, { reportId: report.id, baseReportRevision: report.revision,
          kind: fact.kind, description: fact.description, locationId: location.id,
          category: fact.category, quantity: fact.quantity, unit: fact.unit,
          resourceCount: fact.resourceCount, hours: fact.hours, impactLevel: fact.impactLevel },
        identity.destinationId, identity.key);
      setPhase("done"); setStatus(`واقعیت روزانهٔ رسمی با ارجاع ${result.destinationReference} ثبت شد.`);
      onChanged();
    } catch (error) {
      if (error instanceof CollaborationAccessError) onAccessLoss(error.status);
      else if (error instanceof CollaborationRevisionConflict) {
        setPhase("message-conflict");
        setStatus("نسخهٔ پیام تغییر کرده است؛ متن تازه را بررسی و تبدیل را دوباره تأیید کنید.");
        onChanged();
      } else if (error instanceof CollaborationDailyFactTargetConflict) {
        setPhase("report-conflict"); setStatus(error.message);
      } else if (error instanceof CollaborationDailyFactAlreadyExists) {
        setPhase("exists"); setStatus(error.message); onChanged();
      } else if (error instanceof CollaborationDailyFactValidationError) {
        setPhase("editing"); setIdentity({ destinationId: crypto.randomUUID(), key: crypto.randomUUID() });
        setConfirmed(false); setStatus(error.message);
      } else {
        setPhase("uncertain");
        setStatus("نتیجهٔ ارسال قطعی نیست؛ ابتدا تبار را بازبینی یا با همان شناسه تلاش مجدد کنید.");
      }
    }
  }

  const editable = phase === "editing";
  return <section className="collaboration-edit" aria-label="تبدیل پیام به واقعیت روزانهٔ رسمی">
    {phase === "closed" && <button className="secondary-button" type="button" disabled={!canCommand}
      onClick={() => void open()}>ساخت واقعیت روزانه از پیام</button>}
    {phase === "checking" && <p role="status">در حال بررسی گزارش و تبار پیام…</p>}
    {status && <p role={phase === "message-conflict" || phase === "report-conflict" ? "alert" : "status"}>{status}</p>}
    {(phase === "message-conflict" || stale) && <div>
      {stale && <p role="alert">نسخهٔ پیام تغییر کرده است؛ پیش‌نویس را بر پایهٔ نسخهٔ تازه بررسی و دوباره تأیید کنید.</p>}
      <p>نسخهٔ فعلی پیام: {message.body}</p>
      {message.revision > base.revision && !message.deletedAt && !message.redactedAt &&
        <button className="secondary-button" type="button" disabled={!canCommand}
          onClick={() => void rebaseMessage()}>تبدیل واقعیت بر پایهٔ نسخهٔ تازه</button>}
    </div>}
    {phase === "report-conflict" && <button className="secondary-button" type="button" disabled={!canCommand}
      onClick={() => void refreshReport()}>بازخوانی گزارش و تأیید دوباره</button>}
    {(phase === "editing" || phase === "sending" || phase === "uncertain") &&
      <form onSubmit={(event) => void submit(event)}>
        <p>این فرمان یک واقعیت رسمی را فقط به گزارش پیش‌نویس انتخاب‌شده می‌افزاید؛ تأیید گزارش مرحلهٔ جداگانه است.</p>
        <label>گزارش روزانهٔ پیش‌نویس<select required value={reportId} disabled={!editable}
          onChange={(event) => { setReportId(event.target.value); setConfirmed(false); }}>
          {reports.map((item) => <option key={item.id} value={item.id}>
            {formatPersianDate(item.reportDate)} · {item.locationName || "بدون عنوان محل"} · نسخهٔ {item.revision}
          </option>)}
        </select></label>
        <label>محل فعال پروژه<select required value={draft.locationId ?? ""} disabled={!editable}
          onChange={(event) => {
            const selected = locations.find((item) => item.id === event.target.value);
            setDraft((current) => ({ ...current, locationId: event.target.value,
              locationName: selected?.name ?? "" })); setConfirmed(false);
          }}>
          {locations.map((item) => <option key={item.id} value={item.id}>{item.code} · {item.name}</option>)}
        </select></label>
        <label>نوع واقعیت<select value={draft.kind} disabled={!editable} onChange={(event) => {
          setDraft((current) => ({ ...current, kind: event.target.value as DailyFactKind }));
          setConfirmed(false);
        }}>{factKinds.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}</select></label>
        <label>شرح واقعیت<textarea required maxLength={1000} rows={3} value={draft.description}
          disabled={!editable} onChange={(event) => { setDraft((current) =>
            ({ ...current, description: event.target.value })); setConfirmed(false); }} /></label>
        {fields.category && <label>رسته، فعالیت یا موضوع<input maxLength={120} value={draft.category}
          disabled={!editable} onChange={(event) => { setDraft((current) =>
            ({ ...current, category: event.target.value })); setConfirmed(false); }} /></label>}
        {fields.quantity && <><label>مقدار واقعی<input inputMode="decimal" value={draft.quantity}
          disabled={!editable} onChange={(event) => { setDraft((current) =>
            ({ ...current, quantity: event.target.value })); setConfirmed(false); }} /></label>
          <label>واحد<input maxLength={40} value={draft.unit} disabled={!editable}
            onChange={(event) => { setDraft((current) =>
              ({ ...current, unit: event.target.value })); setConfirmed(false); }} /></label></>}
        {fields.resources && <><label>تعداد<input inputMode="numeric" value={draft.resourceCount}
          disabled={!editable} onChange={(event) => { setDraft((current) =>
            ({ ...current, resourceCount: event.target.value })); setConfirmed(false); }} /></label>
          <label>ساعت<input inputMode="decimal" value={draft.hours} disabled={!editable}
            onChange={(event) => { setDraft((current) =>
              ({ ...current, hours: event.target.value })); setConfirmed(false); }} /></label></>}
        {fields.impact && <label>شدت اثر<select value={draft.impactLevel} disabled={!editable}
          onChange={(event) => { setDraft((current) => ({ ...current,
            impactLevel: event.target.value as DailyImpactLevel | "" })); setConfirmed(false); }}>
          <option value="">بدون ارزیابی</option><option value="Low">کم</option>
          <option value="Medium">متوسط</option><option value="High">زیاد</option>
          <option value="Critical">بحرانی</option>
        </select></label>}
        <p>نسخهٔ پیام: {base.revision} · نسخهٔ گزارش: {report?.revision ?? "—"}؛ منبع در تبار ثبت می‌شود و پیوست خودکار منتقل نمی‌شود.</p>
        <label><input type="checkbox" checked={confirmed} disabled={!canCommand || !editable}
          onChange={(event) => setConfirmed(event.target.checked)} />
          افزودن این واقعیت به گزارش روزانهٔ پیش‌نویس را تأیید می‌کنم.</label>
        <div className="collaboration-message-actions">
          <button type="submit" disabled={!canCommand || stale || phase === "sending" || !confirmed || !report || !location || !draft.description.trim()}>
            {phase === "uncertain" ? "تلاش مجدد با همان شناسه" : phase === "sending"
              ? "در حال ثبت…" : "تأیید و ساخت واقعیت رسمی"}
          </button>
          {phase === "uncertain" && <button className="secondary-button" type="button" disabled={!canCommand}
            onClick={() => void reconcile()}>بازبینی نتیجهٔ واقعیت</button>}
          {editable && <button className="secondary-button" type="button" onClick={() => {
            setPhase("closed"); setStatus(""); setIdentity(null);
          }}>انصراف از واقعیت</button>}
        </div>
      </form>}
  </section>;
}

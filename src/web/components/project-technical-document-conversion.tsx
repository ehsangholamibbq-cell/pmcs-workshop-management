"use client";

import { type FormEvent, useState } from "react";
import { useCollaborationCommandGate } from "@/components/collaboration-command-gate";
import { CollaborationAccessError } from "@/lib/collaboration-events";
import { loadProjectMessageAttachments, type ProjectMessageAttachment } from "@/lib/collaboration-attachments";
import {
  CollaborationTechnicalDocumentSourceAlreadyExists, CollaborationTechnicalDocumentValidationError,
  convertProjectMessageToTechnicalDocument, loadProjectMessageConversions,
  type ProjectTechnicalDocumentConversionDetails,
} from "@/lib/collaboration-conversions";
import { CollaborationRevisionConflict } from "@/lib/collaboration-interactions";
import type { ProjectConversationMessage } from "@/lib/collaboration-room";
import type { TechnicalDocumentType } from "@/lib/technical-office";

const documentTypes: readonly { value: TechnicalDocumentType; label: string }[] = [
  { value: "Drawing", label: "نقشه" }, { value: "Specification", label: "مشخصات فنی" },
  { value: "MethodStatement", label: "روش اجرا" }, { value: "MaterialSubmittal", label: "مدرک مصالح" },
  { value: "ShopDrawing", label: "نقشهٔ کارگاهی" }, { value: "CalculationOrReport", label: "محاسبه یا گزارش" },
  { value: "MeetingMinute", label: "صورت‌جلسه" }, { value: "Correspondence", label: "مکاتبه" },
  { value: "Instruction", label: "دستور" }, { value: "MeasurementSheet", label: "برگهٔ اندازه‌گیری" },
  { value: "HandoverOrTestRecord", label: "تحویل یا آزمایش" }, { value: "Other", label: "سایر" },
];

type Phase = "closed" | "checking" | "editing" | "sending" | "uncertain" |
  "conflict" | "done" | "exists";

export function ProjectTechnicalDocumentConversion({ projectId, message, actorUserId,
  onAccessLoss, onChanged }: {
  readonly projectId: string; readonly message: ProjectConversationMessage;
  readonly actorUserId: string; readonly onAccessLoss: (status: number) => void;
  readonly onChanged: () => void;
}) {
  const { canCommand, isCurrent } = useCollaborationCommandGate();
  const [phase, setPhase] = useState<Phase>("closed");
  const [base, setBase] = useState(message);
  const [files, setFiles] = useState<readonly ProjectMessageAttachment[]>([]);
  const [documentId, setDocumentId] = useState("");
  const [details, setDetails] = useState<ProjectTechnicalDocumentConversionDetails>({
    title: message.body.slice(0, 240), type: "Other", discipline: "", originator: "", revisionCode: "A0",
  });
  const [identity, setIdentity] = useState<{ destinationId: string; key: string } | null>(null);
  const [confirmed, setConfirmed] = useState(false);
  const [status, setStatus] = useState("");
  const stale = (phase === "editing" || phase === "uncertain") && message.revision > base.revision;
  const attachment = files.find((item) => item.documentId === documentId);

  async function open() {
    if (!isCurrent() || phase !== "closed") return;
    setPhase("checking"); setStatus("");
    try {
      const [lineage, attached] = await Promise.all([
        loadProjectMessageConversions("/api/pmcs", projectId, message.id),
        loadProjectMessageAttachments("/api/pmcs", projectId, message.id),
      ]);
      const used = new Set(lineage.filter((item) => item.destinationType === "TechnicalDocument")
        .flatMap((item) => item.documents.map((document) => document.id.toLowerCase())));
      const available = attached.filter((item) => !used.has(item.documentId.toLowerCase()));
      if (available.length === 0) {
        setPhase("closed");
        setStatus("برای تبدیل، یک فایل آزادشده تبدیل‌نشده در همین پیام لازم است."); return;
      }
      setFiles(available); setDocumentId(available[0].documentId); setBase(message);
      setIdentity({ destinationId: crypto.randomUUID(), key: crypto.randomUUID() });
      setConfirmed(false); setPhase("editing");
    } catch (error) {
      setPhase("closed");
      if (error instanceof CollaborationAccessError) onAccessLoss(error.status);
      else setStatus("بررسی تبار و فایل‌های پیام کامل نشد؛ دوباره تلاش کنید.");
    }
  }

  async function reconcile() {
    if (!isCurrent()) return;
    try {
      const lineage = await loadProjectMessageConversions("/api/pmcs", projectId, message.id);
      if (lineage.some((item) => item.destinationType === "TechnicalDocument" &&
          item.documents.some((document) => document.id.toLowerCase() === documentId.toLowerCase()))) {
        setPhase("done"); setStatus("سند فنی رسمی این فایل در تبار پیام ثبت شده است."); onChanged();
      } else setStatus("هنوز سند ثبت‌شده‌ای برای این فایل دیده نمی‌شود؛ تلاش مجدد فقط با همان شناسه انجام شود.");
    } catch (error) {
      if (error instanceof CollaborationAccessError) onAccessLoss(error.status);
      else setStatus("بازبینی نتیجه کامل نشد؛ هویت درخواست حفظ شده است.");
    }
  }

  async function rebaseMessage() {
    if (!isCurrent() || message.revision <= base.revision || message.deletedAt || message.redactedAt) return;
    try {
      const [lineage, currentFiles] = await Promise.all([
        loadProjectMessageConversions("/api/pmcs", projectId, message.id),
        loadProjectMessageAttachments("/api/pmcs", projectId, message.id),
      ]);
      if (lineage.some(item => item.destinationType === "TechnicalDocument" &&
          item.documents.some(document => document.id.toLowerCase() === documentId.toLowerCase()))) {
        setPhase("exists"); setStatus("رکورد رسمی این فایل قبلاً در تبار پیام ثبت شده است."); return;
      }
      const retained = currentFiles.find(item => attachment && item.documentId === attachment.documentId &&
        item.sha256 === attachment.sha256 && item.versionNumber === attachment.versionNumber &&
        item.originalFileName === attachment.originalFileName && item.contentType === attachment.contentType &&
        item.sizeBytes === attachment.sizeBytes);
      setFiles(currentFiles); setDocumentId(retained?.documentId ?? "");
      setBase(message); setIdentity({ destinationId: crypto.randomUUID(), key: crypto.randomUUID() });
      setConfirmed(false); setPhase("editing");
      setStatus(retained ? "" : "فایل انتخاب‌شده تغییر کرده یا در دسترس نیست؛ منبع جاری را دوباره انتخاب کنید.");
    } catch (error) { if (error instanceof CollaborationAccessError) onAccessLoss(error.status);
      else setStatus("بازخوانی فایل و تبار کامل نشد؛ پیش‌نویس حفظ شده است."); }
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!isCurrent() || stale) return;
    if (!identity || !confirmed || !attachment || (phase !== "editing" && phase !== "uncertain")) return;
    if (!navigator.onLine) { setStatus("تبدیل رسمی فقط هنگام اتصال به سرور انجام می‌شود."); return; }
    setPhase("sending"); setStatus("در حال ثبت سند فنی و نسخهٔ پیش‌نویس…");
    try {
      const result = await convertProjectMessageToTechnicalDocument("/api/pmcs", projectId,
        base, actorUserId, attachment, details, identity.destinationId, identity.key);
      setPhase("done"); setStatus(`سند فنی رسمی با ارجاع ${result.destinationReference} ثبت شد.`); onChanged();
    } catch (error) {
      if (error instanceof CollaborationAccessError) onAccessLoss(error.status);
      else if (error instanceof CollaborationRevisionConflict) {
        setPhase("conflict"); setStatus("نسخهٔ پیام تغییر کرده است؛ فایل و مشخصات سند را دوباره بررسی و تأیید کنید.");
        onChanged();
      } else if (error instanceof CollaborationTechnicalDocumentSourceAlreadyExists) {
        setPhase("exists"); setStatus(error.message); onChanged();
      } else if (error instanceof CollaborationTechnicalDocumentValidationError) {
        setPhase("editing"); setIdentity({ destinationId: crypto.randomUUID(), key: crypto.randomUUID() });
        setConfirmed(false); setStatus(error.message);
      } else {
        setPhase("uncertain");
        setStatus("نتیجهٔ ارسال قطعی نیست؛ ابتدا تبار را بازبینی یا با همان شناسه تلاش مجدد کنید.");
      }
    }
  }

  const editable = phase === "editing";
  return <section className="collaboration-edit" aria-label="تبدیل فایل پیام به سند فنی رسمی">
    {phase === "closed" && <button className="secondary-button" type="button" disabled={!canCommand}
      onClick={() => void open()}>ساخت سند فنی رسمی از فایل پیام</button>}
    {phase === "checking" && <p role="status">در حال بررسی فایل آزادشده و تبار پیام…</p>}
    {status && <p role={phase === "conflict" ? "alert" : "status"}>{status}</p>}
    {(phase === "conflict" || stale) && <div>
      {stale && <p role="alert">نسخهٔ پیام تغییر کرده است؛ پیش‌نویس را بر پایهٔ نسخهٔ تازه بررسی و دوباره تأیید کنید.</p>}
      <p>نسخهٔ فعلی پیام: {message.body}</p>
      {message.revision > base.revision && !message.deletedAt && !message.redactedAt &&
        <button className="secondary-button" type="button" disabled={!canCommand}
          onClick={() => void rebaseMessage()}>تبدیل سند فنی بر پایهٔ نسخهٔ تازه</button>}
    </div>}
    {(phase === "editing" || phase === "sending" || phase === "uncertain") &&
      <form onSubmit={(event) => void submit(event)}>
        <p>سند فنی رسمی با یک نسخهٔ اولیهٔ کاری و تبار یک فایل آزادشده ساخته می‌شود؛ صدور سند مرحلهٔ جداگانه است.</p>
        <label>فایل آزادشده پیام<select required value={documentId} disabled={!editable}
          onChange={(event) => { setDocumentId(event.target.value); setConfirmed(false); }}>
          <option value="">انتخاب فایل جاری</option>
          {files.map((item) => <option key={item.documentId} value={item.documentId}>
            {item.originalFileName} · نسخهٔ {item.versionNumber}
          </option>)}
        </select></label>
        {attachment && <p>هش منبع: <code dir="ltr">{attachment.sha256}</code></p>}
        <label>عنوان سند<input required maxLength={240} value={details.title} disabled={!editable}
          onChange={(event) => { setDetails((current) => ({ ...current, title: event.target.value }));
            setConfirmed(false); }} /></label>
        <label>نوع سند<select value={details.type} disabled={!editable} onChange={(event) => {
          setDetails((current) => ({ ...current, type: event.target.value as TechnicalDocumentType }));
          setConfirmed(false);
        }}>{documentTypes.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}</select></label>
        <label>رشتهٔ فنی<input required maxLength={120} value={details.discipline} disabled={!editable}
          onChange={(event) => { setDetails((current) => ({ ...current, discipline: event.target.value }));
            setConfirmed(false); }} /></label>
        <label>تهیه‌کننده، اختیاری<input maxLength={240} value={details.originator} disabled={!editable}
          onChange={(event) => { setDetails((current) => ({ ...current, originator: event.target.value }));
            setConfirmed(false); }} /></label>
        <label>کد نسخهٔ اولیه<input required maxLength={80} value={details.revisionCode} disabled={!editable}
          onChange={(event) => { setDetails((current) => ({ ...current, revisionCode: event.target.value }));
            setConfirmed(false); }} /></label>
        <p>نسخهٔ پیام: {base.revision}؛ هش و نسخهٔ فایل در تبار سند ثبت می‌شوند.</p>
        <label><input type="checkbox" checked={confirmed} disabled={!canCommand || !editable}
          onChange={(event) => setConfirmed(event.target.checked)} />
          ساخت سند فنی رسمی و نسخهٔ اولیه از همین فایل را تأیید می‌کنم.</label>
        <div className="collaboration-message-actions">
          <button type="submit" disabled={!canCommand || stale || phase === "sending" || !confirmed || !attachment ||
            !details.title.trim() || !details.discipline.trim() || !details.revisionCode.trim()}>
            {phase === "uncertain" ? "تلاش مجدد با همان شناسه" : phase === "sending"
              ? "در حال ثبت…" : "تأیید و ساخت سند فنی رسمی"}
          </button>
          {phase === "uncertain" && <button className="secondary-button" type="button" disabled={!canCommand}
            onClick={() => void reconcile()}>بازبینی نتیجهٔ سند فنی</button>}
          {editable && <button className="secondary-button" type="button" onClick={() => {
            setPhase("closed"); setStatus(""); setIdentity(null);
          }}>انصراف از سند فنی</button>}
        </div>
      </form>}
  </section>;
}

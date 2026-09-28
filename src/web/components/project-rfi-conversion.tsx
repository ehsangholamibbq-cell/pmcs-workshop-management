"use client";

import { type FormEvent, useState } from "react";
import { PersianDateInput } from "@/components/persian-date-input";
import { CollaborationAccessError } from "@/lib/collaboration-events";
import { CollaborationRevisionConflict } from "@/lib/collaboration-interactions";
import {
  CollaborationRfiAlreadyExists, CollaborationRfiValidationError,
  convertProjectMessageToRfi, loadProjectMessageConversions,
  type ProjectRfiConversionDetails, type RfiPotentialImpact,
} from "@/lib/collaboration-conversions";
import type { ProjectConversationMessage } from "@/lib/collaboration-room";

const impactChoices: readonly { value: RfiPotentialImpact; label: string }[] = [
  { value: "Time", label: "زمان" }, { value: "Cost", label: "هزینه" },
  { value: "Quality", label: "کیفیت" }, { value: "Scope", label: "دامنه" },
  { value: "Safety", label: "ایمنی" },
];

export function ProjectRfiConversion({ projectId, message, actorUserId, onAccessLoss, onChanged }: {
  readonly projectId: string; readonly message: ProjectConversationMessage;
  readonly actorUserId: string; readonly onAccessLoss: (status: number) => void;
  readonly onChanged: () => void;
}) {
  const [phase, setPhase] = useState<"closed" | "checking" | "editing" | "sending" |
    "uncertain" | "conflict" | "done" | "exists">("closed");
  const [base, setBase] = useState(message);
  const [identity, setIdentity] = useState<{ destinationId: string; key: string } | null>(null);
  const [details, setDetails] = useState<ProjectRfiConversionDetails>({
    title: message.body.slice(0, 240), question: message.body,
    requestedFrom: "", discipline: "", requiredByDate: "",
    potentialImpacts: [], isBlocking: false, proposedSolution: "",
  });
  const [confirmed, setConfirmed] = useState(false);
  const [status, setStatus] = useState("");

  async function open() {
    if (phase !== "closed") return;
    setPhase("checking"); setStatus("");
    try {
      const entries = await loadProjectMessageConversions("/api/pmcs", projectId, message.id);
      if (entries.some((item) => item.destinationType === "RFI")) {
        setPhase("exists");
        setStatus("برای این پیام RFI رسمی قبلاً ثبت شده است؛ تبار تبدیل را ببینید.");
      } else {
        setBase(message);
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
      if (entries.some((item) => item.destinationType === "RFI")) {
        setPhase("done"); setStatus("پیش‌نویس RFI رسمی در تبار پیام ثبت شده است."); onChanged();
      } else setStatus("هنوز RFI ثبت‌شده‌ای دیده نمی‌شود؛ تلاش مجدد فقط با همان شناسه انجام شود.");
    } catch (error) {
      if (error instanceof CollaborationAccessError) onAccessLoss(error.status);
      else setStatus("بازبینی نتیجه کامل نشد؛ هویت درخواست حفظ شده است.");
    }
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!identity || !confirmed || (phase !== "editing" && phase !== "uncertain")) return;
    if (!navigator.onLine) { setStatus("تبدیل رسمی فقط هنگام اتصال به سرور انجام می‌شود."); return; }
    setPhase("sending"); setStatus("در حال ثبت پیش‌نویس RFI رسمی…");
    try {
      const result = await convertProjectMessageToRfi("/api/pmcs", projectId, base,
        actorUserId, details, identity.destinationId, identity.key);
      setPhase("done");
      setStatus(`پیش‌نویس RFI رسمی با ارجاع ${result.destinationReference} ثبت شد؛ تبار پیام را تازه‌سازی کنید.`);
      onChanged();
    } catch (error) {
      if (error instanceof CollaborationAccessError) onAccessLoss(error.status);
      else if (error instanceof CollaborationRevisionConflict) {
        setPhase("conflict");
        setStatus("نسخهٔ پیام تغییر کرده است؛ پرسش و نسخهٔ تازه را بررسی و تبدیل RFI را دوباره تأیید کنید.");
        onChanged();
      } else if (error instanceof CollaborationRfiAlreadyExists) {
        setPhase("exists"); setStatus(error.message); onChanged();
      } else if (error instanceof CollaborationRfiValidationError) {
        setPhase("editing");
        setIdentity({ destinationId: crypto.randomUUID(), key: crypto.randomUUID() });
        setConfirmed(false); setStatus(error.message);
      } else {
        setPhase("uncertain");
        setStatus("نتیجهٔ ارسال قطعی نیست؛ ابتدا تبار را بازبینی یا با همان شناسه تلاش مجدد کنید.");
      }
    }
  }

  function toggleImpact(impact: RfiPotentialImpact, enabled: boolean) {
    setDetails((current) => ({ ...current, potentialImpacts: enabled
      ? [...current.potentialImpacts, impact]
      : current.potentialImpacts.filter((item) => item !== impact) }));
  }

  return <section className="collaboration-edit" aria-label="تبدیل پیام به RFI رسمی">
    {phase === "closed" && <button className="secondary-button" type="button"
      onClick={() => void open()}>ساخت RFI رسمی از پیام</button>}
    {phase === "checking" && <p role="status">در حال بررسی تبار پیام…</p>}
    {status && <p role={phase === "conflict" ? "alert" : "status"}>{status}</p>}
    {phase === "conflict" && <div>
      <p>نسخهٔ فعلی پیام: {message.body}</p>
      {message.revision > base.revision && !message.deletedAt && !message.redactedAt &&
        <button className="secondary-button" type="button" onClick={() => {
          setBase(message);
          setIdentity({ destinationId: crypto.randomUUID(), key: crypto.randomUUID() });
          setConfirmed(false); setStatus(""); setPhase("editing");
        }}>تبدیل RFI بر پایهٔ نسخهٔ تازه</button>}
    </div>}
    {(phase === "editing" || phase === "sending" || phase === "uncertain") &&
      <form onSubmit={(event) => void submit(event)}>
        <p>این فرمان فقط پیش‌نویس رسمی RFI می‌سازد؛ بررسی داخلی و صدور آن مراحل جداگانه‌اند.</p>
        <label>عنوان RFI<input value={details.title} maxLength={240} required
          disabled={phase !== "editing"} onChange={(event) => setDetails((current) =>
            ({ ...current, title: event.target.value }))} /></label>
        <label>سؤال فنی<textarea value={details.question} maxLength={6000} rows={3} required
          disabled={phase !== "editing"} onChange={(event) => setDetails((current) =>
            ({ ...current, question: event.target.value }))} /></label>
        <label>مخاطب پاسخ<input value={details.requestedFrom} maxLength={240} required
          disabled={phase !== "editing"} onChange={(event) => setDetails((current) =>
            ({ ...current, requestedFrom: event.target.value }))} /></label>
        <label>رشتهٔ فنی<input value={details.discipline} maxLength={120} required
          disabled={phase !== "editing"} onChange={(event) => setDetails((current) =>
            ({ ...current, discipline: event.target.value }))} /></label>
        <label>تاریخ موردنیاز شمسی، اختیاری<PersianDateInput value={details.requiredByDate}
          disabled={phase !== "editing"} ariaLabel="تاریخ شمسی موردنیاز RFI"
          onChange={(date) => setDetails((current) => ({ ...current, requiredByDate: date }))} /></label>
        <fieldset><legend>اثر احتمالی RFI</legend>
          {impactChoices.map((choice) => <label key={choice.value}>
            <input type="checkbox" checked={details.potentialImpacts.includes(choice.value)}
              disabled={phase !== "editing"} onChange={(event) => toggleImpact(choice.value, event.target.checked)} />
            {choice.label}</label>)}
          <small>اگر اثری انتخاب نشود، «بدون اثر اعلام‌شده» ثبت می‌شود.</small>
        </fieldset>
        <label><input type="checkbox" checked={details.isBlocking} disabled={phase !== "editing"}
          onChange={(event) => setDetails((current) => ({ ...current, isBlocking: event.target.checked }))} />
          مانع اجرای کار است</label>
        <label>راه‌حل پیشنهادی، اختیاری<textarea value={details.proposedSolution} maxLength={4000}
          disabled={phase !== "editing"} rows={2} onChange={(event) => setDetails((current) =>
            ({ ...current, proposedSolution: event.target.value }))} /></label>
        <label><input type="checkbox" checked={confirmed} disabled={phase !== "editing"}
          onChange={(event) => setConfirmed(event.target.checked)} />
          ایجاد پیش‌نویس RFI رسمی با این پرسش، مخاطب و اثر احتمالی را تأیید می‌کنم.</label>
        <div className="collaboration-message-actions">
          <button type="submit" disabled={phase === "sending" || !confirmed || !details.title.trim() ||
            !details.question.trim() || !details.requestedFrom.trim() || !details.discipline.trim()}>
            {phase === "uncertain" ? "تلاش مجدد RFI با همان شناسه" : phase === "sending"
              ? "در حال ثبت…" : "تأیید و ساخت پیش‌نویس RFI"}
          </button>
          {phase === "uncertain" && <button className="secondary-button" type="button"
            onClick={() => void reconcile()}>بازبینی نتیجهٔ RFI</button>}
          {phase === "editing" && <button className="secondary-button" type="button"
            onClick={() => { setPhase("closed"); setStatus(""); setIdentity(null); }}>انصراف از RFI</button>}
        </div>
      </form>}
  </section>;
}

"use client";

import { type FormEvent, useRef, useState } from "react";
import { PmcsFileInput } from "@/components/pmcs-file-input";
import { enqueueAttachment } from "@/lib/attachment-store";
import { findDailyReportId } from "@/lib/operation-store";
import { toUserMessage } from "@/lib/localization";
import { todayIsoInProjectTimeZone } from "@/lib/persian-date";

interface EvidenceCaptureProps {
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly lastFactId: string | null;
  readonly onQueued: () => Promise<void>;
  readonly projectReadState: "loading" | "current" | "cached" | "error" | "forbidden";
  readonly isProjectAccessRevoked: () => boolean;
}

export function EvidenceCapture(props: EvidenceCaptureProps) {
  const [message, setMessage] = useState("عکس و سند پی‌دی‌اف در صف جداگانه حافظه محلی مرورگر نگهداری می‌شوند.");
  const [isBusy, setIsBusy] = useState(false);
  const busy = useRef(false);
  const [file, setFile] = useState<File | null>(null);
  const [messageKind, setMessageKind] = useState<"info" | "success" | "error">("info");
  const accessRevoked = props.projectReadState === "forbidden";

  function ensureProjectAccess() {
    if (props.isProjectAccessRevoked()) {
      throw new Error("دسترسی به پروژه تأیید نشد؛ مدرک تازه روی این دستگاه صف نمی‌شود.");
    }
  }

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!file || busy.current) return;
    if (accessRevoked || props.isProjectAccessRevoked()) {
      setMessageKind("error");
      setMessage("دسترسی به پروژه تأیید نشد؛ مدرک تازه روی این دستگاه صف نمی‌شود.");
      return;
    }
    busy.current = true;
    setIsBusy(true);
    setMessage("در حال محاسبه اثر انگشت فایل و ذخیره روی دستگاه…");
    setMessageKind("info");
    try {
      const reportDate = todayIsoInProjectTimeZone();
      const dailyReportId = findDailyReportId(props.projectId, reportDate);
      if (!dailyReportId) {
        throw new Error("ابتدا حداقل یک واقعیت برای گزارش امروز ثبت کنید.");
      }
      await enqueueAttachment({
        tenantId: props.tenantId,
        userId: props.userId,
        projectId: props.projectId,
        dailyReportId,
        dailyFactId: props.lastFactId,
        file,
        ensureAllowed: ensureProjectAccess,
      });
      setFile(null);
      setMessageKind("success");
      setMessage(props.lastFactId
        ? "مدرک به آخرین واقعیت متصل و روی این دستگاه ذخیره شد؛ تا پذیرش سرور رسمی نیست."
        : "مدرک به گزارش امروز متصل و روی این دستگاه ذخیره شد؛ تا پذیرش سرور رسمی نیست.");
      await props.onQueued().catch(() => {
        setMessage("مدرک روی این دستگاه ذخیره شد؛ نمایش شمارندهٔ صف تازه نشد. تا پذیرش سرور رسمی نیست.");
      });
    } catch (error) {
      setMessageKind("error");
      setMessage(toUserMessage(error, "ذخیره محلی مدرک ناموفق بود."));
    } finally {
      busy.current = false;
      setIsBusy(false);
    }
  }

  return (
    <form className="evidence-capture" data-testid="evidence-capture"
      data-project-read-state={props.projectReadState} aria-busy={isBusy} onSubmit={save}>
      <div className="evidence-capture-status">
        <strong>عکس یا مدرک واقعی</strong>
        <p className={`evidence-feedback evidence-feedback--${accessRevoked ? "error" : messageKind}`}
          role={accessRevoked || messageKind === "error" ? "alert" : "status"}>{accessRevoked
            ? "دسترسی به پروژه تأیید نشد؛ مدرک تازه روی این دستگاه صف نمی‌شود."
            : message}</p>
        {!accessRevoked && props.projectReadState !== "current" &&
          <small className="field-help" role="status">این مدرک فقط پیش‌نویس محلی است؛ پذیرش سرور و مجوز جاری هنوز تأیید نشده‌اند.</small>}
      </div>
      <PmcsFileInput label="عکس یا سند پی‌دی‌اف" capture="environment"
        accept="image/jpeg,image/png,image/webp,image/heic,image/heif,application/pdf"
        file={file} disabled={isBusy || accessRevoked} onFileChange={(selected) => {
          setFile(selected);
          setMessageKind("info");
          setMessage(selected ? "فایل انتخاب شد؛ برای نگهداری در صف محلی، ذخیره را بزنید."
            : "فایلی انتخاب نشده؛ عکس یا سند را برای ذخیرهٔ محلی انتخاب کنید.");
        }} />
      <button type="submit" disabled={!file || isBusy || accessRevoked}>
        {isBusy ? "در حال ذخیره…" : accessRevoked ? "ذخیره مدرک غیرفعال است" : "ذخیره مدرک روی این دستگاه"}
      </button>
    </form>
  );
}

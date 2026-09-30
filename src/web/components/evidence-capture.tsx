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
}

export function EvidenceCapture(props: EvidenceCaptureProps) {
  const [message, setMessage] = useState("عکس و سند پی‌دی‌اف در صف جداگانه حافظه محلی مرورگر نگهداری می‌شوند.");
  const [isBusy, setIsBusy] = useState(false);
  const busy = useRef(false);
  const [file, setFile] = useState<File | null>(null);
  const [messageKind, setMessageKind] = useState<"info" | "success" | "error">("info");

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!file || busy.current) return;
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
    <form className="evidence-capture" data-testid="evidence-capture" aria-busy={isBusy} onSubmit={save}>
      <div className="evidence-capture-status">
        <strong>عکس یا مدرک واقعی</strong>
        <p className={`evidence-feedback evidence-feedback--${messageKind}`}
          role={messageKind === "error" ? "alert" : "status"}>{message}</p>
      </div>
      <PmcsFileInput label="عکس یا سند پی‌دی‌اف" capture="environment"
        accept="image/jpeg,image/png,image/webp,image/heic,image/heif,application/pdf"
        file={file} disabled={isBusy} onFileChange={(selected) => {
          setFile(selected);
          setMessageKind("info");
          setMessage(selected ? "فایل انتخاب شد؛ برای نگهداری در صف محلی، ذخیره را بزنید."
            : "فایلی انتخاب نشده؛ عکس یا سند را برای ذخیرهٔ محلی انتخاب کنید.");
        }} />
      <button type="submit" disabled={!file || isBusy}>
        {isBusy ? "در حال ذخیره…" : "ذخیره مدرک روی این دستگاه"}
      </button>
    </form>
  );
}

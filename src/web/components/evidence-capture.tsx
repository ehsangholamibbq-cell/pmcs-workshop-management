"use client";

import { type ChangeEvent, useState } from "react";
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

  async function selectFile(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) {
      return;
    }

    setIsBusy(true);
    setMessage("در حال محاسبه اثر انگشت فایل و ذخیره روی دستگاه…");
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
      setMessage(props.lastFactId
        ? "مدرک به آخرین واقعیت متصل و در صف مستقل آپلود ذخیره شد."
        : "مدرک به گزارش امروز متصل و در صف مستقل آپلود ذخیره شد.");
      await props.onQueued();
    } catch (error) {
      setMessage(toUserMessage(error, "ذخیره محلی مدرک ناموفق بود."));
    } finally {
      setIsBusy(false);
    }
  }

  return (
    <div className="evidence-capture">
      <div>
        <strong>عکس یا مدرک واقعی</strong>
        <span>{message}</span>
      </div>
      <label className="file-button" aria-disabled={isBusy}>
        {isBusy ? "در حال ذخیره…" : "افزودن عکس یا سند پی‌دی‌اف"}
        <input
          type="file"
          accept="image/jpeg,image/png,image/webp,image/heic,image/heif,application/pdf"
          capture="environment"
          disabled={isBusy}
          onChange={(event) => void selectFile(event)}
        />
      </label>
    </div>
  );
}

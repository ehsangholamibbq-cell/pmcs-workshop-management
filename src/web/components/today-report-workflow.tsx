"use client";

import { useCallback, useEffect, useState } from "react";
import {
  getDailyReport,
  submitDailyReport,
  type DailyReportSummary,
} from "@/lib/daily-reports";
import { findDailyReportId } from "@/lib/operation-store";
import { toUserMessage } from "@/lib/localization";
import { todayIsoInProjectTimeZone } from "@/lib/persian-date";

interface TodayReportWorkflowProps {
  readonly apiBaseUrl: string;
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly pendingCount: number;
  readonly isOnline: boolean;
  readonly refreshToken: number;
  readonly onChanged: () => void;
}

export function TodayReportWorkflow(props: TodayReportWorkflowProps) {
  const [report, setReport] = useState<DailyReportSummary | null>(null);
  const [message, setMessage] = useState("ابتدا یک واقعیت ثبت و با سرور همگام‌سازی کنید.");
  const [isBusy, setIsBusy] = useState(false);

  const load = useCallback(async () => {
    if (!props.isOnline) {
      setMessage("ارسال رسمی هنگام اتصال به سرور انجام می‌شود؛ پیش‌نویس‌های آفلاین محفوظ‌اند.");
      return;
    }
    const reportId = findDailyReportId(props.projectId, todayIsoInProjectTimeZone());
    if (!reportId) {
      setReport(null);
      setMessage("هنوز گزارش روزانه‌ای روی این دستگاه ساخته نشده است.");
      return;
    }

    try {
      const loaded = await getDailyReport(
        props.apiBaseUrl,
        { tenantId: props.tenantId, userId: props.userId },
        props.projectId,
        reportId,
      );
      setReport(loaded);
      setMessage(loaded ? statusDescription(loaded) : "پیش‌نویس هنوز توسط سرور پذیرفته نشده است.");
    } catch (error) {
      setMessage(toUserMessage(error, "وضعیت رسمی گزارش از سرور دریافت نشد."));
    }
  }, [props.apiBaseUrl, props.isOnline, props.projectId, props.tenantId, props.userId]);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      void load();
    }, 0);
    return () => window.clearTimeout(timeoutId);
  }, [load, props.refreshToken]);

  async function submit() {
    if (!report) {
      return;
    }
    setIsBusy(true);
    setMessage("در حال ارسال گزارش برای بازبینی…");
    try {
      const updated = await submitDailyReport(
        props.apiBaseUrl,
        { tenantId: props.tenantId, userId: props.userId },
        props.projectId,
        report.id,
        report.revision,
      );
      setReport(updated);
      setMessage(statusDescription(updated));
      props.onChanged();
    } catch (error) {
      setMessage(toUserMessage(error, "ارسال گزارش ناموفق بود."));
    } finally {
      setIsBusy(false);
    }
  }

  const canSubmit = report && (report.status === "Draft" || report.status === "Returned");

  return (
    <div className="workflow-strip">
      <div>
        <strong>وضعیت گزارش امروز</strong>
        <span>{message}</span>
        {report?.status === "Returned" && report.reviewComment && (
          <small>دلیل عودت: {report.reviewComment}</small>
        )}
      </div>
      {canSubmit && (
        <button
          type="button"
          disabled={isBusy || !props.isOnline || props.pendingCount > 0}
          onClick={() => void submit()}
        >
          {isBusy ? "در حال ارسال…" : "ارسال برای تأیید"}
        </button>
      )}
    </div>
  );
}

function statusDescription(report: DailyReportSummary): string {
  const status = {
    Draft: "پیش‌نویس رسمی سرور",
    Submitted: "در انتظار بازبینی",
    Returned: "برای اصلاح عودت شده",
    Approved: "تأییدشده و آماده ورود به وضعیت پروژه",
    Rejected: "ردشده",
    Superseded: "با نسخه جدید جایگزین‌شده",
  }[report.status];
  return `${status} · ${report.factCount.toLocaleString("fa-IR")} واقعیت`;
}

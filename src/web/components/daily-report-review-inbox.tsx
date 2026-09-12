"use client";

import { useCallback, useEffect, useState } from "react";
import {
  listReviewInbox,
  reviewDailyReport,
  type DailyReportSummary,
} from "@/lib/daily-reports";
import { toUserMessage } from "@/lib/localization";
import { formatPersianDate } from "@/lib/persian-date";

interface DailyReportReviewInboxProps {
  readonly apiBaseUrl: string;
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly isOnline: boolean;
  readonly refreshToken: number;
  readonly onChanged?: () => void;
}

export function DailyReportReviewInbox(props: DailyReportReviewInboxProps) {
  const [reports, setReports] = useState<readonly DailyReportSummary[]>([]);
  const [comments, setComments] = useState<Record<string, string>>({});
  const [message, setMessage] = useState("در حال دریافت کارتابل…");
  const [busyReportId, setBusyReportId] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!props.isOnline) {
      setMessage("کارتابل رسمی هنگام اتصال به سرور در دسترس است.");
      return;
    }
    try {
      const items = await listReviewInbox(
        props.apiBaseUrl,
        { tenantId: props.tenantId, userId: props.userId },
        props.projectId,
      );
      setReports(items);
      setMessage(items.length === 0 ? "گزارشی در انتظار بازبینی نیست." : "");
    } catch (error) {
      setMessage(toUserMessage(error, "کارتابل از سرور دریافت نشد."));
    }
  }, [props.apiBaseUrl, props.isOnline, props.projectId, props.tenantId, props.userId]);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      void load();
    }, 0);
    return () => window.clearTimeout(timeoutId);
  }, [load, props.refreshToken]);

  async function review(report: DailyReportSummary, action: "approve" | "return") {
    const comment = comments[report.id] ?? "";
    if (action === "return" && !comment.trim()) {
      setMessage("برای عودت گزارش، دلیل اصلاح را بنویسید.");
      return;
    }

    setBusyReportId(report.id);
    setMessage(action === "approve" ? "در حال تأیید…" : "در حال عودت برای اصلاح…");
    try {
      await reviewDailyReport(
        props.apiBaseUrl,
        { tenantId: props.tenantId, userId: props.userId },
        props.projectId,
        report.id,
        action,
        report.revision,
        comment,
      );
      setComments((current) => ({ ...current, [report.id]: "" }));
      await load();
      props.onChanged?.();
    } catch (error) {
      setMessage(toUserMessage(error, "عملیات بازبینی ناموفق بود."));
    } finally {
      setBusyReportId(null);
    }
  }

  return (
    <article className="operational-card">
      <div className="card-heading">
        <div>
          <p className="eyebrow">کارتابل بازبینی</p>
          <h2>گزارش‌های در انتظار تصمیم</h2>
        </div>
        <span className={reports.length > 0 ? "count-badge warning" : "count-badge"}>{reports.length}</span>
      </div>

      {reports.length > 0 && (
        <div className="review-list">
          {reports.map((report) => (
            <div className="review-item" key={report.id}>
              <div className="review-summary">
                <strong>{formatReportDate(report.reportDate)}</strong>
                <span>{report.factCount.toLocaleString("fa-IR")} واقعیت · نسخه {report.revision.toLocaleString("fa-IR")}</span>
              </div>
              <textarea
                rows={2}
                aria-label={`نظر بازبینی گزارش ${formatReportDate(report.reportDate)}`}
                placeholder="نظر اختیاری برای تأیید؛ دلیل الزامی برای عودت"
                value={comments[report.id] ?? ""}
                onChange={(event) => setComments((current) => ({
                  ...current,
                  [report.id]: event.target.value,
                }))}
              />
              <div className="review-actions">
                <button
                  type="button"
                  disabled={!props.isOnline || busyReportId === report.id}
                  onClick={() => void review(report, "approve")}
                >تأیید</button>
                <button
                  className="secondary-button"
                  type="button"
                  disabled={!props.isOnline || busyReportId === report.id}
                  onClick={() => void review(report, "return")}
                >عودت برای اصلاح</button>
              </div>
            </div>
          ))}
        </div>
      )}
      {message && <p className="muted" aria-live="polite">{message}</p>}
    </article>
  );
}

function formatReportDate(value: string): string {
  return formatPersianDate(value, "full");
}

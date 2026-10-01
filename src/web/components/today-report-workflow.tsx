"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import {
  getDailyReport,
  submitDailyReport,
  type DailyReportSummary,
} from "@/lib/daily-reports";
import { findDailyReportId } from "@/lib/operation-store";
import { ApiRequestError, toUserMessage } from "@/lib/localization";
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
  const readScope = `${props.apiBaseUrl}:${props.tenantId}:${props.userId}:${props.projectId}`;
  const [report, setReport] = useState<DailyReportSummary | null>(null);
  const [message, setMessage] = useState("ابتدا یک واقعیت ثبت و با سرور همگام‌سازی کنید.");
  const [readState, setReadState] = useState<"loading" | "current" | "empty" | "offline" | "unavailable" | "forbidden">("loading");
  const [visibleScope, setVisibleScope] = useState<string | null>(null);
  const effectiveReadState = visibleScope === readScope ? readState : "loading";
  const readSequence = useRef(0);
  const currentReadSequence = useRef(0);
  const [isBusy, setIsBusy] = useState(false);

  const load = useCallback(async () => {
    const requestId = ++readSequence.current;
    currentReadSequence.current = 0;
    setReport(null);
    setVisibleScope(null);
    if (!props.isOnline) {
      setVisibleScope(readScope);
      setReadState("offline");
      setMessage("ارسال رسمی هنگام اتصال به سرور انجام می‌شود؛ پیش‌نویس‌های آفلاین محفوظ‌اند.");
      return;
    }
    setReadState("loading");
    setMessage("در حال دریافت وضعیت رسمی گزارش امروز…");
    const reportId = findDailyReportId(props.projectId, todayIsoInProjectTimeZone());
    if (!reportId) {
      setVisibleScope(readScope);
      setReadState("empty");
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
      if (requestId !== readSequence.current) return;
      setReport(loaded);
      currentReadSequence.current = loaded ? requestId : 0;
      setVisibleScope(readScope);
      setReadState(loaded ? "current" : "unavailable");
      setMessage(loaded ? statusDescription(loaded) : "شناسهٔ محلی گزارش امروز هنوز با پاسخ رسمی سرور تأیید نشد؛ دوباره تلاش کنید.");
    } catch (error) {
      if (requestId !== readSequence.current) return;
      const denied = error instanceof ApiRequestError && [401, 403, 404].includes(error.status);
      setVisibleScope(readScope);
      setReadState(denied ? "forbidden" : "unavailable");
      setMessage(denied ? "دسترسی به وضعیت گزارش امروز تأیید نشد؛ دادهٔ قبلی نمایش داده نمی‌شود."
        : toUserMessage(error, "وضعیت رسمی گزارش از سرور دریافت نشد."));
    }
  }, [props.apiBaseUrl, props.isOnline, props.projectId, props.tenantId, props.userId, readScope]);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      void load();
    }, 0);
    return () => window.clearTimeout(timeoutId);
  }, [load, props.refreshToken]);

  async function submit() {
    if (!report || !props.isOnline || effectiveReadState !== "current" || isBusy || props.pendingCount > 0 ||
      currentReadSequence.current !== readSequence.current) {
      return;
    }
    const commandSequence = readSequence.current;
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
      if (commandSequence !== readSequence.current) return;
      setReport(updated);
      setMessage(statusDescription(updated));
      props.onChanged();
    } catch (error) {
      if (commandSequence !== readSequence.current) return;
      if (error instanceof ApiRequestError && [401, 403, 404].includes(error.status)) {
        readSequence.current += 1;
        currentReadSequence.current = 0;
        setReport(null);
        setVisibleScope(readScope);
        setReadState("forbidden");
        setMessage("دسترسی به وضعیت گزارش امروز تأیید نشد؛ دادهٔ قبلی نمایش داده نمی‌شود.");
      } else setMessage(toUserMessage(error, "ارسال گزارش ناموفق بود."));
    } finally {
      setIsBusy(false);
    }
  }

  const canSubmit = effectiveReadState === "current" && report && (report.status === "Draft" || report.status === "Returned");

  return (
    <div className="workflow-strip" data-testid="today-report-workflow" data-read-state={effectiveReadState}>
      <div>
        <strong>وضعیت گزارش امروز</strong>
        <span role={effectiveReadState === "unavailable" || effectiveReadState === "forbidden" ? "alert" : "status"}>{effectiveReadState === "loading" && visibleScope !== readScope ? "در حال دریافت وضعیت رسمی گزارش امروز…" : message}</span>
        {effectiveReadState === "current" && report?.status === "Returned" && report.reviewComment && (
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
      {props.isOnline && (effectiveReadState === "unavailable" || effectiveReadState === "forbidden") &&
        <button type="button" className="secondary-button" onClick={() => void load()}>تلاش دوباره برای دریافت وضعیت گزارش امروز</button>}
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

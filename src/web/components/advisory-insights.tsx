"use client";

import { useCallback, useEffect, useState } from "react";
import {
  getAdvisoryInsights,
  requestAdvisoryInsight,
  reviewAdvisoryInsight,
  type AdvisoryConfidenceBand,
  type AdvisoryInsightModel,
  type AdvisoryInsightType,
  type AdvisoryReviewStatus,
} from "@/lib/intelligence";
import { apiProblemMessage, toUserMessage } from "@/lib/localization";
import { formatPersianDate, formatPersianDateTime } from "@/lib/persian-date";

interface AdvisoryInsightsProps {
  readonly apiBaseUrl: string;
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly isOnline: boolean;
  readonly refreshToken: number;
}

export function AdvisoryInsights(props: AdvisoryInsightsProps) {
  const [insights, setInsights] = useState<readonly AdvisoryInsightModel[]>([]);
  const [providerConfigured, setProviderConfigured] = useState<boolean | null>(null);
  const [canGenerate, setCanGenerate] = useState(false);
  const [canReview, setCanReview] = useState(false);
  const [hasActiveRequest, setHasActiveRequest] = useState(false);
  const [message, setMessage] = useState("در حال دریافت آخرین تحلیل مشورتی…");
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    if (!props.isOnline) {
      setMessage("تولید و دریافت تحلیل مشورتی فقط هنگام اتصال به سرور انجام می‌شود.");
      return;
    }

    try {
      const result = await getAdvisoryInsights(
        props.apiBaseUrl,
        { tenantId: props.tenantId, userId: props.userId },
        props.projectId,
      );
      setInsights(result.insights);
      setProviderConfigured(result.providerConfigured);
      setCanGenerate(result.canGenerate);
      setCanReview(result.canReview);
      setHasActiveRequest(result.activeRequests.length > 0);
      if (!result.providerConfigured) {
        setMessage("اتصال سرویس هوش مصنوعی هنوز توسط مدیر سامانه پیکربندی نشده است.");
      } else if (result.activeRequests.length > 0) {
        setMessage("درخواست تحلیل در صف امن پردازش است؛ این صفحه خودکار به‌روز می‌شود.");
      } else if (result.recentRequests[0]?.status === "Failed") {
        setMessage(apiProblemMessage({ code: result.recentRequests[0].lastErrorCode ?? "ai.processing.failed" }));
      } else if (result.insights.length === 0) {
        setMessage("هنوز تحلیل مشورتی تولید نشده است.");
      } else {
        setMessage("خروجی زیر مشورتی است و وضعیت رسمی پروژه را تغییر نمی‌دهد.");
      }
    } catch (error) {
      setMessage(toUserMessage(error, "تحلیل‌های مشورتی دریافت نشدند."));
    }
  }, [props.apiBaseUrl, props.isOnline, props.projectId, props.tenantId, props.userId]);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => void load(), 0);
    return () => window.clearTimeout(timeoutId);
  }, [load, props.refreshToken]);

  useEffect(() => {
    if (!hasActiveRequest || !props.isOnline) return;
    const intervalId = window.setInterval(() => void load(), 3000);
    return () => window.clearInterval(intervalId);
  }, [hasActiveRequest, load, props.isOnline]);

  async function generate() {
    if (!props.isOnline) {
      setMessage("برای تولید تحلیل جدید به اتصال سرور نیاز است.");
      return;
    }

    setBusy(true);
    try {
      await requestAdvisoryInsight(
        props.apiBaseUrl,
        { tenantId: props.tenantId, userId: props.userId },
        props.projectId,
      );
      setHasActiveRequest(true);
      setMessage("درخواست ثبت شد؛ فقط داده‌های رسمی و مجاز بررسی می‌شوند.");
      await load();
    } catch (error) {
      setMessage(toUserMessage(error, "ثبت درخواست تحلیل ناموفق بود."));
    } finally {
      setBusy(false);
    }
  }

  async function review(insight: AdvisoryInsightModel, decision: "accept" | "dismiss") {
    setBusy(true);
    try {
      await reviewAdvisoryInsight(
        props.apiBaseUrl,
        { tenantId: props.tenantId, userId: props.userId },
        props.projectId,
        insight,
        decision,
      );
      setMessage(decision === "accept"
        ? "تحلیل به‌عنوان خروجی بازبینی‌شده پذیرفته شد؛ وضعیت رسمی همچنان بدون تغییر است."
        : "تحلیل کنار گذاشته شد و در سابقه ممیزی باقی ماند.");
      await load();
    } catch (error) {
      setMessage(toUserMessage(error, "ثبت نتیجه بازبینی ناموفق بود."));
    } finally {
      setBusy(false);
    }
  }

  const current = insights[0];
  return (
    <section className="advisory-section" id="advisory" aria-label="تحلیل مشورتی هوش مصنوعی">
      <div className="advisory-heading">
        <div>
          <p className="eyebrow">هوشمندی پروژه</p>
          <h2>تحلیل مشورتی مستند</h2>
          <p className="muted">بر پایه آخرین تصویر رسمی و فقط در محدوده دسترسی درخواست‌کننده</p>
        </div>
        <button
          type="button"
          className="secondary-button"
          disabled={!props.isOnline || !canGenerate || !providerConfigured || hasActiveRequest || busy}
          onClick={() => void generate()}
        >
          {hasActiveRequest ? "در صف پردازش…" : "تولید تحلیل جدید"}
        </button>
      </div>
      <p className="calculation-note" aria-live="polite">{message}</p>

      {current ? (
        <article className="advisory-card">
          <div className="advisory-meta">
            <span className={`review-badge review-${current.reviewStatus.toLowerCase()}`}>
              {current.isStale ? "منقضی یا مربوط به تصویر قدیمی" : reviewStatusLabel(current.reviewStatus)}
            </span>
            <span>{insightTypeLabel(current.output.insightType)}</span>
            <span>اطمینان {confidenceLabel(current.output.confidenceBand)}</span>
            <span>{formatDateTime(current.generatedAt)}</span>
          </div>
          <h3>{current.output.statement}</h3>
          <div className="advisory-grid">
            <div>
              <strong>اثر احتمالی</strong>
              <p>{current.output.potentialImpact}</p>
            </div>
            <div>
              <strong>مسئول پیشنهادی</strong>
              <p>{current.output.suggestedOwnerRole}</p>
            </div>
          </div>
          {current.output.suggestedActions.length > 0 && (
            <div className="advisory-list">
              <strong>اقدامات پیشنهادی</strong>
              {current.output.suggestedActions.map((action) => (
                <div key={`${action.title}:${action.rationale}`}>
                  <b>{action.title}</b>
                  <span>{action.rationale}</span>
                </div>
              ))}
            </div>
          )}
          <details className="advisory-evidence">
            <summary>شواهد، فرض‌ها و شکاف‌های داده</summary>
            <strong>واقعیت‌های استفاده‌شده</strong>
            <ul>{current.output.factsUsed.map((fact) => <li key={fact}>{fact}</li>)}</ul>
            <strong>ارجاعات قابل ردیابی</strong>
            <ul>{current.output.evidenceReferences.map((reference) => (
              <li key={reference}>{evidenceLabel(reference)}</li>
            ))}</ul>
            {current.output.assumptions.length > 0 && (
              <>
                <strong>فرض‌ها</strong>
                <ul>{current.output.assumptions.map((item) => <li key={item}>{item}</li>)}</ul>
              </>
            )}
            {current.output.dataGaps.length > 0 && (
              <>
                <strong>شکاف‌های داده</strong>
                <ul>{current.output.dataGaps.map((item) => <li key={item}>{item}</li>)}</ul>
              </>
            )}
          </details>
          {canReview && current.reviewStatus === "NeedsReview" && !current.isStale && (
            <div className="review-actions">
              <button type="button" disabled={busy || !props.isOnline} onClick={() => void review(current, "accept")}>
                پذیرش پس از بررسی
              </button>
              <button className="secondary-button" type="button" disabled={busy || !props.isOnline} onClick={() => void review(current, "dismiss")}>
                کنار گذاشتن
              </button>
            </div>
          )}
          <p className="advisory-disclaimer">
            پذیرش این متن به معنی تأیید پرداخت، تغییر برنامه، بستن ریسک یا ایجاد اقدام رسمی نیست.
          </p>
        </article>
      ) : (
        <div className="advisory-empty">
          <strong>تحلیل آماده‌ای وجود ندارد</strong>
          <span>ابتدا باید یک تصویر رسمی و به‌روز از وضعیت پروژه ساخته شده باشد.</span>
        </div>
      )}
    </section>
  );
}

function reviewStatusLabel(status: AdvisoryReviewStatus): string {
  return ({ NeedsReview: "پیشنهادی · نیازمند بازبینی", Accepted: "بازبینی و پذیرفته‌شده", Dismissed: "کنار گذاشته‌شده" })[status];
}

function insightTypeLabel(type: AdvisoryInsightType): string {
  return ({
    ExecutiveSummary: "خلاصه مدیریتی",
    EmergingRisk: "ریسک نوظهور",
    LikelyCause: "فرضیه علت محتمل",
    MissingDataWarning: "هشدار کمبود داده",
  })[type];
}

function confidenceLabel(value: AdvisoryConfidenceBand): string {
  return ({ Low: "کم", Medium: "متوسط", High: "زیاد" })[value];
}

function evidenceLabel(reference: string): string {
  const [kind, ...parts] = reference.split(":");
  const identifier = parts.join(":");
  const label = ({
    "project-state": "تصویر رسمی وضعیت",
    "daily-fact": "واقعیت تأییدشده روزانه",
    "financial-state": "تصویر وضعیت مالی",
    "commercial-state": "تصویر وضعیت قرارداد و خرید",
    "management-action": "اقدام مدیریتی",
  } as Readonly<Record<string, string>>)[kind] ?? "رکورد پروژه";
  const calculatedDate = /^calculated:(\d{4}-\d{2}-\d{2})$/.exec(identifier)?.[1];
  if (calculatedDate) return `${label} · محاسبه ${formatPersianDate(calculatedDate)}`;
  return `${label} · ${shortIdentifier(identifier)}`;
}

function shortIdentifier(value: string): string {
  if (value.length <= 12) return value;
  return `${value.slice(0, 8)}…${value.slice(-4)}`;
}

function formatDateTime(value: string): string {
  return formatPersianDateTime(value);
}

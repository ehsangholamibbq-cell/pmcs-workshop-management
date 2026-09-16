"use client";

import { useCallback, useEffect, useState } from "react";
import {
  listOperationIssues,
  resolveConflictOperation,
  type OperationIssue,
} from "@/lib/operation-store";
import { listAttachmentIssues, type AttachmentIssue } from "@/lib/attachment-store";
import { getOrCreateDeviceId } from "@/lib/field-database";
import {
  listSyncDevices,
  readLocalSyncDiagnostics,
  revokeSyncDevice,
  type LocalSyncDiagnostics,
  type SyncDeviceModel,
} from "@/lib/sync-client";
import { toUserMessage } from "@/lib/localization";
import { formatPersianDateTime } from "@/lib/persian-date";
import { readSyncRecoveryState, type LocalSyncRecoveryState } from "@/lib/sync-recovery";

interface SyncIssuesPanelProps {
  readonly apiBaseUrl: string;
  readonly projectId: string;
  readonly refreshToken: number;
}

export function SyncIssuesPanel({ apiBaseUrl, projectId, refreshToken }: SyncIssuesPanelProps) {
  const [issues, setIssues] = useState<readonly OperationIssue[]>([]);
  const [attachmentIssues, setAttachmentIssues] = useState<readonly AttachmentIssue[]>([]);
  const [diagnostics, setDiagnostics] = useState<LocalSyncDiagnostics | null>(null);
  const [recovery, setRecovery] = useState<LocalSyncRecoveryState | null>(null);
  const [devices, setDevices] = useState<readonly SyncDeviceModel[]>([]);
  const [loadingFailed, setLoadingFailed] = useState(false);
  const [busy, setBusy] = useState("");
  const [message, setMessage] = useState("");
  const [currentDeviceId, setCurrentDeviceId] = useState("");
  const [localRefresh, setLocalRefresh] = useState(0);

  const load = useCallback(async () => {
    const [items, attachments, localDiagnostics, recoveryState] = await Promise.all([
      listOperationIssues(projectId),
      listAttachmentIssues(projectId),
      readLocalSyncDiagnostics(projectId),
      readSyncRecoveryState(projectId),
    ]);
    setIssues(items);
    setAttachmentIssues(attachments);
    setDiagnostics(localDiagnostics);
    setRecovery(recoveryState);
    setCurrentDeviceId(getOrCreateDeviceId());

    if (typeof navigator !== "undefined" && navigator.onLine) {
      setDevices(await listSyncDevices(apiBaseUrl));
    }
    setLoadingFailed(false);
  }, [apiBaseUrl, projectId]);

  useEffect(() => {
    let active = true;
    const timeoutId = window.setTimeout(() => {
      void load().catch(() => {
        if (active) setLoadingFailed(true);
      });
    }, 0);
    return () => {
      active = false;
      window.clearTimeout(timeoutId);
    };
  }, [load, localRefresh, refreshToken]);

  async function resolve(issue: OperationIssue, resolution: "keep-server" | "reapply") {
    setBusy(issue.operationId);
    setMessage("");
    try {
      await resolveConflictOperation(apiBaseUrl, projectId, issue.operationId, resolution);
      setMessage(resolution === "keep-server"
        ? "نسخه رسمی سرور پذیرفته و قصد محلی بایگانی شد."
        : "قصد محلی با شناسه جدید و نسخه فعلی سرور دوباره در صف قرار گرفت.");
      setLocalRefresh((value) => value + 1);
    } catch (error) {
      setMessage(toUserMessage(error, "تعیین تکلیف تعارض انجام نشد."));
    } finally {
      setBusy("");
    }
  }

  async function revoke(device: SyncDeviceModel) {
    setBusy(device.registrationId);
    setMessage("");
    try {
      await revokeSyncDevice(apiBaseUrl, device, "لغو دسترسی دستگاه توسط صاحب حساب");
      setMessage("نشست‌ها و مجوزهای آفلاین دستگاه لغو شدند.");
      setLocalRefresh((value) => value + 1);
    } catch (error) {
      setMessage(toUserMessage(error, "لغو دسترسی دستگاه انجام نشد."));
    } finally {
      setBusy("");
    }
  }

  const issueCount = issues.length + attachmentIssues.length;

  return (
    <article className="operational-card" data-testid="sync-recovery-center" data-project-id={projectId}>
      <div className="card-heading">
        <div>
          <p className="eyebrow">مرکز تعارض و همگام‌سازی</p>
          <h2>کنترل وضعیت همین دستگاه</h2>
        </div>
        <span className={issueCount > 0 ? "count-badge warning" : "count-badge"}>
          {issueCount.toLocaleString("fa-IR")}
        </span>
      </div>

      {diagnostics && (
        <div className="metric-row">
          <div>
            <strong>{diagnostics.lastPushAt ? formatDeviceTime(diagnostics.lastPushAt) : "—"}</strong>
            <span>آخرین ارسال موفق</span>
          </div>
          <div>
            <strong>{diagnostics.lastPullAt ? formatDeviceTime(diagnostics.lastPullAt) : "—"}</strong>
            <span>آخرین دریافت و ثبت نقطه کنترل</span>
          </div>
          <div>
            <strong>{diagnostics.leaseExpiresAt ? formatDeviceTime(diagnostics.leaseExpiresAt) : "—"}</strong>
            <span>اعتبار مجوز آفلاین</span>
          </div>
        </div>
      )}

      {recovery && (
        <div className="sync-recovery-summary" data-testid="sync-recovery-status" data-sync-phase={recovery.phase}>
          <strong>{recoveryPhaseLabel(recovery.phase)}</strong>
          <span>{consistencyLabel(recovery.consistency)}</span>
          <small>
            نقطه کنترل محلی {recovery.localCheckpointSequence.toLocaleString("fa-IR")} · سرور {recovery.serverCheckpointSequence.toLocaleString("fa-IR")} · نشانگر {recovery.serverWatermark.toLocaleString("fa-IR")}
          </small>
          {recovery.nextRetryAt && <small>تلاش بعدی: {formatDeviceTime(recovery.nextRetryAt)}</small>}
        </div>
      )}

      {diagnostics?.clockSkewWarning && (
        <p className="calculation-note" role="alert">
          ساعت دستگاه با سرور اختلاف قابل‌توجه دارد؛ زمان رسمی پذیرش همچنان زمان سرور است.
        </p>
      )}
      {diagnostics?.bootstrapRequired && (
        <p className="calculation-note">
          نقطه کنترل محلی با سرور یکسان نبود؛ دریافت کنترل‌شده از ابتدای داده مجاز انجام می‌شود.
        </p>
      )}

      {loadingFailed ? (
        <p className="muted">خواندن وضعیت محلی ممکن نشد.</p>
      ) : issueCount === 0 ? (
        <p className="muted">تعارض یا عملیات ردشده‌ای روی این دستگاه وجود ندارد.</p>
      ) : (
        <div className="issue-list">
          {issues.map((issue) => (
            <div className="issue-item" key={issue.operationId}>
              <div>
                <strong>{issue.status === "conflict" ? "تعارض" : "ردشده"}</strong>
                <span>{issue.message}</span>
                {issue.status === "conflict" && issue.conflictId && (
                  <div className="state-controls">
                    <button
                      className="secondary-button"
                      type="button"
                      disabled={busy === issue.operationId}
                      onClick={() => void resolve(issue, "keep-server")}
                    >
                      پذیرش نسخه رسمی سرور
                    </button>
                    {issue.serverRevision !== undefined && (
                      <button
                        className="secondary-button"
                        type="button"
                        disabled={busy === issue.operationId}
                        onClick={() => void resolve(issue, "reapply")}
                      >
                        بازاعمال روی نسخه فعلی
                      </button>
                    )}
                  </div>
                )}
              </div>
              <small>{formatDeviceTime(issue.createdAtDevice)}</small>
            </div>
          ))}
          {attachmentIssues.map((issue) => (
            <div className="issue-item" key={issue.attachmentId}>
              <div>
                <strong>مدرک ردشده · {issue.originalFileName}</strong>
                <span>{issue.message}</span>
              </div>
              <small>{formatDeviceTime(issue.createdAtDevice)}</small>
            </div>
          ))}
        </div>
      )}

      {devices.length > 0 && (
        <details>
          <summary>دستگاه‌های ثبت‌شده این حساب</summary>
          <div className="issue-list">
            {devices.map((device) => (
              <div className="issue-item" key={device.registrationId}>
                <div>
                  <strong>{device.displayName}{device.deviceId === currentDeviceId ? " · دستگاه جاری" : ""}</strong>
                  <span>
                    {device.status === "Active" ? "فعال" : "لغوشده"} · آخرین ارتباط {formatDeviceTime(device.lastSeenAt)}
                  </span>
                  {device.status === "Active" && device.deviceId !== currentDeviceId && (
                    <button
                      className="secondary-button"
                      type="button"
                      disabled={busy === device.registrationId}
                      onClick={() => void revoke(device)}
                    >
                      لغو نشست و مجوز آفلاین
                    </button>
                  )}
                </div>
              </div>
            ))}
          </div>
        </details>
      )}

      {message && <p className="calculation-note" aria-live="polite">{message}</p>}
      <p className="microcopy">
        موارد تعارض خودکار حل نمی‌شوند. لغو دستگاه در نخستین اتصال بعدی اثر می‌کند و پاک‌سازی از راه دور روی دستگاه کاملاً آفلاین تضمین‌پذیر نیست.
      </p>
    </article>
  );
}

function formatDeviceTime(value: string): string {
  return formatPersianDateTime(value, undefined, "short", "short");
}

function recoveryPhaseLabel(phase: LocalSyncRecoveryState["phase"]): string {
  const labels: Record<LocalSyncRecoveryState["phase"], string> = {
    offline: "آفلاین؛ صف محلی محفوظ است",
    recovering: "در حال بازیابی عملیات ناتمام",
    pushing: "در حال ارسال عملیات",
    uploading: "در حال انتقال مدارک",
    verifying: "در حال تطبیق دستگاه و سرور",
    succeeded: "همگام‌سازی کنترل‌شده موفق",
    attention: "نیازمند بررسی تعارض یا ردشدن",
    "retry-scheduled": "تلاش مجدد زمان‌بندی شده",
    blocked: "همگام‌سازی مسدود شده است",
  };
  return labels[phase];
}

function consistencyLabel(consistency: LocalSyncRecoveryState["consistency"]): string {
  const labels: Record<LocalSyncRecoveryState["consistency"], string> = {
    consistent: "نسخه محلی و سرور هم‌تراز هستند",
    "pending-upload": "داده ارسال‌نشده روی دستگاه باقی مانده است",
    "pending-download": "تغییر سرور باید دریافت شود",
    attention: "تعیین تکلیف انسانی لازم است",
    diverged: "نقطه کنترل محلی و سرور ناسازگار است",
    unavailable: "تطبیق نهایی هنوز در دسترس نیست",
  };
  return labels[consistency];
}

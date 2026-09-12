"use client";

import { useCallback, useEffect, useState } from "react";
import {
  configureProjectCalendar,
  getProject,
  type ProjectCalendarMode,
  type ProjectModel,
  type Weekday,
} from "@/lib/projects";
import { toUserMessage } from "@/lib/localization";

interface ProjectCalendarSettingsProps {
  readonly apiBaseUrl: string;
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly isOnline: boolean;
  readonly refreshToken: number;
  readonly onChanged?: () => void;
}

const dayOptions: readonly { key: Weekday; label: string }[] = [
  { key: "Saturday", label: "شنبه" },
  { key: "Sunday", label: "یکشنبه" },
  { key: "Monday", label: "دوشنبه" },
  { key: "Tuesday", label: "سه‌شنبه" },
  { key: "Wednesday", label: "چهارشنبه" },
  { key: "Thursday", label: "پنجشنبه" },
  { key: "Friday", label: "جمعه" },
];

export function ProjectCalendarSettings(props: ProjectCalendarSettingsProps) {
  const [project, setProject] = useState<ProjectModel | null>(null);
  const [mode, setMode] = useState<ProjectCalendarMode>("NotConfigured");
  const [workingDays, setWorkingDays] = useState<readonly Weekday[]>([]);
  const [message, setMessage] = useState("در حال دریافت تنظیم تقویم…");
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    if (!props.isOnline) {
      setMessage("تغییر تقویم پروژه فقط هنگام اتصال به سرور انجام می‌شود.");
      return;
    }

    try {
      const current = await getProject(
        props.apiBaseUrl,
        { tenantId: props.tenantId, userId: props.userId },
        props.projectId,
      );
      setProject(current);
      setMode(current.calendarMode);
      setWorkingDays(current.workingDays);
      setMessage(current.calendarMode === "NotConfigured"
        ? "تقویم تنظیم نشده؛ پوشش داده با مبنای شفاف هفت روز تقویمی محاسبه می‌شود."
        : "پوشش داده فقط بر اساس روزهای کاری انتخاب‌شده محاسبه می‌شود.");
    } catch (error) {
      setMessage(toUserMessage(error, "تنظیم تقویم از سرور دریافت نشد."));
    }
  }, [props.apiBaseUrl, props.isOnline, props.projectId, props.tenantId, props.userId]);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => void load(), 0);
    return () => window.clearTimeout(timeoutId);
  }, [load, props.refreshToken]);

  function toggle(day: Weekday) {
    setWorkingDays((current) => current.includes(day)
      ? current.filter((item) => item !== day)
      : [...current, day]);
  }

  async function save() {
    if (!project || !props.isOnline) return;
    if (mode === "WorkingWeek" && workingDays.length === 0) {
      setMessage("برای تقویم کاری حداقل یک روز را انتخاب کنید.");
      return;
    }

    setBusy(true);
    setMessage("در حال ذخیره و نسخه‌بندی تقویم…");
    try {
      const updated = await configureProjectCalendar(
        props.apiBaseUrl,
        { tenantId: props.tenantId, userId: props.userId },
        props.projectId,
        project.revision,
        mode,
        workingDays,
      );
      setProject(updated);
      setWorkingDays(updated.workingDays);
      setMessage(mode === "NotConfigured"
        ? "تقویم اختیاری حذف شد؛ موتور به مبنای هفت روز تقویمی بازگشت."
        : "تقویم کاری ذخیره شد؛ تصویر وضعیت عملیاتی برای اعمال مبنای جدید باید نوسازی شود.");
      props.onChanged?.();
    } catch (error) {
      setMessage(toUserMessage(error, "ذخیره تقویم ناموفق بود."));
    } finally {
      setBusy(false);
    }
  }

  return (
    <article className="calendar-settings">
      <div className="card-heading">
        <div>
          <p className="eyebrow">تقویم اختیاری پروژه</p>
          <h3>مبنای روزهای مورد انتظار گزارش</h3>
        </div>
        <select
          aria-label="نوع تقویم پروژه"
          value={mode}
          onChange={(event) => setMode(event.target.value as ProjectCalendarMode)}
        >
          <option value="NotConfigured">بدون تقویم پروژه</option>
          <option value="WorkingWeek">هفته کاری مشخص</option>
        </select>
      </div>
      {mode === "WorkingWeek" && (
        <div className="weekday-grid">
          {dayOptions.map((day) => (
            <label key={day.key}>
              <input
                type="checkbox"
                checked={workingDays.includes(day.key)}
                onChange={() => toggle(day.key)}
              />
              {day.label}
            </label>
          ))}
        </div>
      )}
      <div className="form-actions">
        <button type="button" disabled={!project || !props.isOnline || busy} onClick={() => void save()}>
          {busy ? "در حال ذخیره…" : "ذخیره تقویم"}
        </button>
        <output aria-live="polite">{message}</output>
      </div>
    </article>
  );
}

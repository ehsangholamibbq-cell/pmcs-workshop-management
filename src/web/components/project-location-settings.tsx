"use client";

import { type FormEvent, useState } from "react";
import {
  createProjectLocation,
  retireProjectLocation,
  type ProjectLocationModel,
} from "@/lib/projects";
import { toUserMessage } from "@/lib/localization";

interface ProjectLocationSettingsProps {
  readonly apiBaseUrl: string;
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly isOnline: boolean;
  readonly locations: readonly ProjectLocationModel[];
  readonly onChanged: () => void;
}

export function ProjectLocationSettings(props: ProjectLocationSettingsProps) {
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [parentLocationId, setParentLocationId] = useState("");
  const [busyId, setBusyId] = useState<string | null>(null);
  const [message, setMessage] = useState("محل‌ها مرجع مشترک ثبت واقعیت و گزارش‌گیری هستند.");
  const activeLocations = props.locations.filter((location) => location.status === "Active");

  async function create(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const effectiveParentId = parentLocationId || activeLocations[0]?.id;
    if (!effectiveParentId) {
      setMessage("محل ریشه پروژه در دسترس نیست؛ ابتدا فهرست را تازه‌سازی کنید.");
      return;
    }
    setBusyId("create");
    setMessage("در حال ثبت محل پروژه…");
    try {
      await createProjectLocation(
        props.apiBaseUrl,
        { tenantId: props.tenantId, userId: props.userId },
        props.projectId,
        { code, name, parentLocationId: effectiveParentId },
      );
      setCode("");
      setName("");
      setParentLocationId("");
      setMessage("محل ثبت شد و در ورودی‌های عملیاتی قابل انتخاب است.");
      props.onChanged();
    } catch (error) {
      setMessage(toUserMessage(error, "ثبت محل پروژه انجام نشد."));
    } finally {
      setBusyId(null);
    }
  }

  async function retire(location: ProjectLocationModel) {
    setBusyId(location.id);
    setMessage(`در حال غیرفعال‌کردن محل ${location.name}…`);
    try {
      await retireProjectLocation(
        props.apiBaseUrl,
        { tenantId: props.tenantId, userId: props.userId },
        props.projectId,
        location,
      );
      setMessage("محل غیرفعال شد؛ سوابق قبلی با شناسه همان محل حفظ می‌شوند.");
      props.onChanged();
    } catch (error) {
      setMessage(toUserMessage(error, "غیرفعال‌کردن محل انجام نشد."));
    } finally {
      setBusyId(null);
    }
  }

  return (
    <section className="project-location-settings" aria-labelledby="project-location-title">
      <div>
        <h3 id="project-location-title">ساختار مکان پروژه</h3>
        <p className="muted">هر محل می‌تواند زیرمجموعهٔ محل دیگر باشد؛ محل غیرفعال از ثبت جدید حذف می‌شود ولی تاریخچه پاک نمی‌شود.</p>
      </div>

      <div className="project-location-list">
        {props.locations.map((location) => (
          <article key={location.id} className={location.status === "Retired" ? "retired" : ""}>
            <div>
              <strong>{location.code} · {location.name}</strong>
              <small>{location.parentLocationId ? `زیرمجموعه ${parentLabel(props.locations, location.parentLocationId)}` : "سطح اصلی پروژه"}</small>
            </div>
            <span>{location.status === "Active" ? "فعال" : "غیرفعال"}</span>
            {location.status === "Active" && location.code !== "ROOT" && (
              <button
                type="button"
                className="secondary-button"
                disabled={!props.isOnline || busyId !== null}
                onClick={() => void retire(location)}
              >
                {busyId === location.id ? "در حال ثبت…" : "غیرفعال‌کردن"}
              </button>
            )}
          </article>
        ))}
      </div>

      <form className="project-location-form" onSubmit={create}>
        <label>
          کد محل
          <input required minLength={1} maxLength={40} dir="ltr" value={code} onChange={(event) => setCode(event.target.value)} placeholder="مثال: طبقه-۰۳" />
        </label>
        <label>
          نام محل
          <input required maxLength={200} value={name} onChange={(event) => setName(event.target.value)} placeholder="طبقه سوم" />
        </label>
        <label>
          محل بالادست
          <select required value={parentLocationId || activeLocations[0]?.id || ""} onChange={(event) => setParentLocationId(event.target.value)}>
            {activeLocations.length === 0 && <option value="">محل ریشه در دسترس نیست</option>}
            {activeLocations.map((location) => (
              <option key={location.id} value={location.id}>{location.code} · {location.name}</option>
            ))}
          </select>
        </label>
        <button type="submit" disabled={!props.isOnline || busyId !== null || activeLocations.length === 0}>
          {busyId === "create" ? "در حال ثبت…" : "افزودن محل"}
        </button>
      </form>
      <p className="calculation-note" aria-live="polite">{message}</p>
    </section>
  );
}

function parentLabel(locations: readonly ProjectLocationModel[], parentId: string): string {
  const parent = locations.find((location) => location.id === parentId);
  return parent ? `${parent.code} · ${parent.name}` : "ثبت‌شده";
}

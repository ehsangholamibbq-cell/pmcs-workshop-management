"use client";

import { type FormEvent, useEffect, useState } from "react";
import {
  createProjectLocation,
  retireProjectLocation,
  type ProjectLocationModel,
} from "@/lib/projects";
import { ApiRequestError, toUserMessage } from "@/lib/localization";

interface ProjectLocationSettingsProps {
  readonly apiBaseUrl: string;
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly isOnline: boolean;
  readonly locations: readonly ProjectLocationModel[];
  readonly readState: "loading" | "current" | "cached" | "unavailable" | "forbidden";
  readonly readVersion: number;
  readonly isCurrentRead: (version: number) => boolean;
  readonly readMessage: string;
  readonly onRetry: () => void;
  readonly onAccessRevoked: () => void;
  readonly onChanged: () => void;
}

export function ProjectLocationSettings(props: ProjectLocationSettingsProps) {
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [parentLocationId, setParentLocationId] = useState("");
  const [busyId, setBusyId] = useState<string | null>(null);
  const [message, setMessage] = useState("محل‌ها مرجع مشترک ثبت واقعیت و گزارش‌گیری هستند.");
  const [messageKind, setMessageKind] = useState<"info" | "success" | "error">("info");
  const activeLocations = props.locations.filter((location) => location.status === "Active");
  const canCommand = props.isOnline && props.readState === "current";

  useEffect(() => {
    setMessage("محل‌ها مرجع مشترک ثبت واقعیت و گزارش‌گیری هستند.");
    setMessageKind("info");
  }, [props.readVersion]);

  async function create(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canCommand || busyId !== null) return;
    const effectiveParentId = parentLocationId || activeLocations[0]?.id;
    if (!effectiveParentId) {
      setMessage("محل ریشه پروژه در دسترس نیست؛ ابتدا فهرست را تازه‌سازی کنید.");
      setMessageKind("error");
      return;
    }
    setBusyId("create");
    const commandVersion = props.readVersion;
    setMessage("در حال ثبت محل پروژه…");
    setMessageKind("info");
    try {
      await createProjectLocation(
        props.apiBaseUrl,
        { tenantId: props.tenantId, userId: props.userId },
        props.projectId,
        { code, name, parentLocationId: effectiveParentId },
      );
      if (!props.isCurrentRead(commandVersion)) return;
      setCode("");
      setName("");
      setParentLocationId("");
      setMessage("محل ثبت شد و در ورودی‌های عملیاتی قابل انتخاب است.");
      setMessageKind("success");
      props.onChanged();
    } catch (error) {
      if (!props.isCurrentRead(commandVersion)) return;
      setMessageKind("error");
      if (error instanceof ApiRequestError && [401, 403, 404].includes(error.status)) {
        props.onAccessRevoked();
        setMessage("دسترسی به مکان‌های پروژه تأیید نشد؛ ورودی شما برای تلاش بعدی محفوظ است.");
      } else setMessage(toUserMessage(error, "ثبت محل پروژه انجام نشد."));
    } finally {
      setBusyId(null);
    }
  }

  async function retire(location: ProjectLocationModel) {
    if (!canCommand || busyId !== null) return;
    setBusyId(location.id);
    const commandVersion = props.readVersion;
    setMessage(`در حال غیرفعال‌کردن محل ${location.name}…`);
    setMessageKind("info");
    try {
      await retireProjectLocation(
        props.apiBaseUrl,
        { tenantId: props.tenantId, userId: props.userId },
        props.projectId,
        location,
      );
      if (!props.isCurrentRead(commandVersion)) return;
      setMessage("محل غیرفعال شد؛ سوابق قبلی با شناسه همان محل حفظ می‌شوند.");
      setMessageKind("success");
      props.onChanged();
    } catch (error) {
      if (!props.isCurrentRead(commandVersion)) return;
      setMessageKind("error");
      if (error instanceof ApiRequestError && [401, 403, 404].includes(error.status)) {
        props.onAccessRevoked();
        setMessage("دسترسی به مکان‌های پروژه تأیید نشد؛ فهرست قبلی کنار گذاشته شد.");
      } else setMessage(toUserMessage(error, "غیرفعال‌کردن محل انجام نشد."));
    } finally {
      setBusyId(null);
    }
  }

  return (
    <section className="project-location-settings" aria-labelledby="project-location-title" data-testid="project-location-settings" data-read-state={props.readState}>
      <div>
        <h3 id="project-location-title">ساختار مکان پروژه</h3>
        <p className="muted">هر محل می‌تواند زیرمجموعهٔ محل دیگر باشد؛ محل غیرفعال از ثبت جدید حذف می‌شود ولی تاریخچه پاک نمی‌شود.</p>
      </div>

      <p className="location-read-message" role={props.readState === "forbidden" || props.readState === "unavailable" ? "alert" : "status"}>{props.readMessage}</p>
      {props.isOnline && ["cached", "unavailable", "forbidden"].includes(props.readState) &&
        <button type="button" className="secondary-button" onClick={props.onRetry}>تلاش دوباره برای دریافت مکان‌ها</button>}

      <div className="project-location-list">
        {props.locations.map((location) => (
          <article key={location.id} className={location.status === "Retired" ? "retired" : ""}>
            <div>
              <strong>{location.code} · {location.name}</strong>
              <small>{location.parentLocationId ? `زیرمجموعه ${parentLabel(props.locations, location.parentLocationId)}` : "سطح اصلی پروژه"}</small>
            </div>
            <span>{props.readState === "cached" ? "نسخهٔ محلی · " : ""}{location.status === "Active" ? "فعال" : "غیرفعال"}</span>
            {canCommand && location.status === "Active" && location.code !== "ROOT" && (
              <button
                type="button"
                className="secondary-button"
                disabled={!canCommand || busyId !== null}
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
        <button type="submit" disabled={!canCommand || busyId !== null || activeLocations.length === 0}>
          {busyId === "create" ? "در حال ثبت…" : "افزودن محل"}
        </button>
      </form>
      {props.readState === "current" &&
        <p className="calculation-note location-command-message" data-kind={messageKind}
          role={messageKind === "error" ? "alert" : "status"}>{message}</p>}
    </section>
  );
}

function parentLabel(locations: readonly ProjectLocationModel[], parentId: string): string {
  const parent = locations.find((location) => location.id === parentId);
  return parent ? `${parent.code} · ${parent.name}` : "ثبت‌شده";
}

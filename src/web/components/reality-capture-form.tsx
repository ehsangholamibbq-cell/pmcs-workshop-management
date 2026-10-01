"use client";

import { type FormEvent, useMemo, useState } from "react";
import {
  buildDailyFactPayload,
  emptyFactDraft,
  factFields,
  factKinds,
  FactValidationError,
  type DailyFactDraft,
} from "@/lib/field-facts";
import { enqueueOperation, getOrCreateDailyReportId } from "@/lib/operation-store";
import type { MeasurementItemModel } from "@/lib/planning";
import type { ProjectLocationModel } from "@/lib/projects";
import { todayIsoInProjectTimeZone } from "@/lib/persian-date";

interface RealityCaptureFormProps {
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly statusMessage: string;
  readonly onStatus: (message: string) => void;
  readonly onQueued: (factId: string) => Promise<void>;
  readonly measurementItems?: readonly MeasurementItemModel[];
  readonly locations?: readonly ProjectLocationModel[];
  readonly locationReadState: "loading" | "current" | "cached" | "unavailable" | "forbidden";
}

export function RealityCaptureForm({
  tenantId,
  userId,
  projectId,
  statusMessage,
  onStatus,
  onQueued,
  measurementItems = [],
  locations = [],
  locationReadState,
}: RealityCaptureFormProps) {
  const [draft, setDraft] = useState<DailyFactDraft>(emptyFactDraft);
  const fields = useMemo(() => factFields(draft.kind), [draft.kind]);
  const activeLocations = useMemo(
    () => locations.filter((location) => location.status === "Active"),
    [locations],
  );
  const selectedKind = factKinds.find((item) => item.value === draft.kind);
  const selectedLocation = activeLocations.find((location) => location.id === draft.locationId);
  const staleLocation = Boolean(draft.locationId && !selectedLocation);
  const staleMeasurementItem = Boolean(draft.measurementItemId &&
    !measurementItems.some((item) => item.id === draft.measurementItemId));

  function update<K extends keyof DailyFactDraft>(key: K, value: DailyFactDraft[K]) {
    setDraft((current) => ({ ...current, [key]: value }));
  }

  function selectMeasurementItem(itemId: string) {
    const item = measurementItems.find((candidate) => candidate.id === itemId);
    setDraft((current) => ({
      ...current,
      measurementItemId: itemId,
      category: item?.title ?? current.category,
      unit: item?.unit ?? current.unit,
    }));
  }

  function selectLocation(locationId: string) {
    const location = activeLocations.find((candidate) => candidate.id === locationId);
    setDraft((current) => ({
      ...current,
      locationId,
      locationName: location?.name ?? "",
    }));
  }

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    try {
      onStatus("در حال ذخیره روی این دستگاه…");
      if (locationReadState !== "current" && locationReadState !== "cached") {
        throw new FactValidationError("فهرست محل‌های مجاز در دسترس نیست؛ پیش‌نویس روی این دستگاه حفظ شد.");
      }
      if (!draft.locationId) {
        throw new FactValidationError("انتخاب محل پروژه برای ثبت واقعیت الزامی است");
      }
      if (!selectedLocation) {
        throw new FactValidationError("محل انتخاب‌شده در فهرست فعال فعلی نیست؛ محل را دوباره انتخاب کنید.");
      }
      if (draft.kind === "WorkProgress" && draft.measurementItemId &&
        !measurementItems.some((item) => item.id === draft.measurementItemId)) {
        throw new FactValidationError("قلم اندازه‌گیری انتخاب‌شده دیگر در فهرست فعلی نیست؛ دوباره انتخاب کنید.");
      }

      const reportDate = todayIsoInProjectTimeZone();
      const factId = crypto.randomUUID();
      const payload = buildDailyFactPayload({ ...draft, locationName: selectedLocation.name }, factId, reportDate);
      await enqueueOperation({
        tenantId,
        userId,
        projectId,
        entityType: "DailyReport",
        entityId: getOrCreateDailyReportId(projectId, reportDate),
        commandType: "CaptureDailyReportFact",
        payload,
      });
      setDraft((current) => ({
        ...emptyFactDraft,
        kind: current.kind,
        locationId: selectedLocation.id,
        locationName: selectedLocation.name,
      }));
      await onQueued(factId);
    } catch (error) {
      onStatus(error instanceof FactValidationError
        ? error.message
        : "ذخیره محلی ناموفق بود؛ فضای مرورگر را بررسی کنید");
    }
  }

  return (
    <form className="capture-form" data-testid="reality-capture-form" data-location-read-state={locationReadState} onSubmit={save} noValidate>
      <div className="field full-width">
        <label htmlFor="fact-kind">نوع واقعیت</label>
        <select
          id="fact-kind"
          value={draft.kind}
          onChange={(event) => update("kind", event.target.value as DailyFactDraft["kind"])}
        >
          {factKinds.map((kind) => <option key={kind.value} value={kind.value}>{kind.label}</option>)}
        </select>
        <small>{selectedKind?.hint}</small>
      </div>

      {fields.category && (
        <div className="field">
          <label htmlFor="fact-category">{categoryLabel(draft.kind)}</label>
          <input
            id="fact-category"
            value={draft.category}
            onChange={(event) => update("category", event.target.value)}
            placeholder={categoryPlaceholder(draft.kind)}
          />
        </div>
      )}

      {draft.kind === "WorkProgress" && (measurementItems.length > 0 || Boolean(draft.measurementItemId)) && (
        <div className="field">
          <label htmlFor="fact-measurement-item">قلم اندازه‌گیری اختیاری</label>
          <select
            id="fact-measurement-item"
            value={draft.measurementItemId}
            onChange={(event) => selectMeasurementItem(event.target.value)}
          >
            <option value="">ثبت مستقل بدون قلم</option>
            {staleMeasurementItem && <option value={draft.measurementItemId} disabled>قلم قبلی در فهرست فعلی نیست</option>}
            {measurementItems.map((item) => (
              <option key={item.id} value={item.id}>{item.code} · {item.title} · {item.unit}</option>
            ))}
          </select>
          <small>این اتصال مستقل از ساختار شکست کار (WBS) است.</small>
          {staleMeasurementItem && <small className="field-help" role="alert">قلم قبلی در فهرست فعلی نیست؛ پیش از ثبت دوباره انتخاب کنید یا اتصال را حذف کنید.</small>}
        </div>
      )}

      <div className="field">
        <label htmlFor="fact-location">محل پروژه</label>
        <select
          id="fact-location"
          required
          value={draft.locationId ?? ""}
          onChange={(event) => selectLocation(event.target.value)}
        >
          <option value="">{activeLocations.length > 0 ? "انتخاب محل" : "فهرست محل در دسترس نیست"}</option>
          {staleLocation && <option value={draft.locationId} disabled>محل قبلی دیگر فعال یا در دسترس نیست؛ دوباره انتخاب کنید</option>}
          {activeLocations.map((location) => (
            <option key={location.id} value={location.id}>{location.code} · {location.name}</option>
          ))}
        </select>
        {staleLocation && <small className="field-help" role="alert">محل قبلی در فهرست فعال فعلی نیست؛ پیش‌نویس حفظ شده و انتخاب تازه لازم است.</small>}
        {locationReadState === "cached" && <small className="field-help" role="status">فهرست محل‌ها نسخهٔ ذخیره‌شدهٔ این دستگاه است؛ ثبت، پیش‌نویس محلی و تا پذیرش سرور رسمی نیست.</small>}
        {(locationReadState === "loading" || locationReadState === "forbidden" || locationReadState === "unavailable") &&
          <small className="field-help" role="status">فهرست محل‌های مجاز هنوز قابل اتکا نیست؛ متن فرم حفظ می‌شود و ثبت پس از دریافت فهرست ممکن است.</small>}
        {activeLocations.length === 0 && (
          <small className="field-help">برای ثبت آفلاین، ابتدا باید فهرست محل‌های مجاز در حالت آنلاین دریافت شود.</small>
        )}
      </div>

      {fields.quantity && (
        <>
          <div className="field compact">
            <label htmlFor="fact-quantity">مقدار واقعی</label>
            <input
              id="fact-quantity"
              inputMode="decimal"
              value={draft.quantity}
              onChange={(event) => update("quantity", event.target.value)}
              placeholder="0"
            />
          </div>
          <div className="field compact">
            <label htmlFor="fact-unit">واحد</label>
            <input
              id="fact-unit"
              value={draft.unit}
              onChange={(event) => update("unit", event.target.value)}
              placeholder="مترمکعب، تن، عدد"
            />
          </div>
        </>
      )}

      {fields.resources && (
        <>
          <div className="field compact">
            <label htmlFor="fact-count">تعداد</label>
            <input
              id="fact-count"
              inputMode="numeric"
              value={draft.resourceCount}
              onChange={(event) => update("resourceCount", event.target.value)}
              placeholder="0"
            />
          </div>
          <div className="field compact">
            <label htmlFor="fact-hours">نفر/دستگاه‌ساعت</label>
            <input
              id="fact-hours"
              inputMode="decimal"
              value={draft.hours}
              onChange={(event) => update("hours", event.target.value)}
              placeholder="0"
            />
          </div>
        </>
      )}

      {fields.impact && (
        <div className="field">
          <label htmlFor="fact-impact">شدت اثر مشاهده‌شده</label>
          <select
            id="fact-impact"
            value={draft.impactLevel}
            onChange={(event) => update("impactLevel", event.target.value as DailyFactDraft["impactLevel"])}
          >
            <option value="">ارزیابی نشده</option>
            <option value="Low">کم</option>
            <option value="Medium">متوسط</option>
            <option value="High">زیاد</option>
            <option value="Critical">بحرانی</option>
          </select>
        </div>
      )}

      {fields.reference && (
        <div className="field">
          <label htmlFor="fact-reference">مرجع اختیاری</label>
          <input
            id="fact-reference"
            value={draft.referenceCode}
            onChange={(event) => update("referenceCode", event.target.value)}
            placeholder="ساختار شکست کار (WBS)، درخواست اطلاعات (RFI) یا کد داخلی — اختیاری"
          />
        </div>
      )}

      <div className="field full-width">
        <label htmlFor="fact-description">شرح آنچه واقعاً مشاهده شد</label>
        <textarea
          id="fact-description"
          value={draft.description}
          onChange={(event) => update("description", event.target.value)}
          placeholder="مثال: اجرای برق طبقه سوم به‌علت نبود نقشه تأییدشده متوقف شد."
          rows={3}
        />
      </div>

      <div className="form-actions full-width">
        <button type="submit">ذخیره پیش‌نویس آفلاین</button>
        <span className="draft-note">تا پذیرش سرور رسمی نیست</span>
      </div>
      <output className="full-width" aria-live="polite">{statusMessage}</output>
    </form>
  );
}

function categoryLabel(kind: DailyFactDraft["kind"]): string {
  return ({
    WorkProgress: "نام فعالیت اجرایی",
    Labor: "رسته نیروی انسانی",
    Equipment: "نوع ماشین‌آلات",
    Material: "نام مصالح",
    Issue: "دسته مشکل",
    Stoppage: "دسته توقف",
  } as Partial<Record<DailyFactDraft["kind"], string>>)[kind] ?? "دسته";
}

function categoryPlaceholder(kind: DailyFactDraft["kind"]): string {
  return ({
    WorkProgress: "مثال: بتن‌ریزی سقف",
    Labor: "مثال: آرماتوربند",
    Equipment: "مثال: جرثقیل برجی",
    Material: "مثال: سیمان تیپ ۲",
    Issue: "مثال: نبود نقشه تأییدشده",
    Stoppage: "مثال: توقف به‌علت مجوز",
  } as Partial<Record<DailyFactDraft["kind"], string>>)[kind] ?? "";
}

"use client";

import { useEffect, useId, useMemo, useRef, useState } from "react";
import {
  formatPersianDateInput,
  isoDateToPersian,
  parsePersianDateInput,
  persianCalendarMonth,
  persianMonthTitle,
  shiftPersianMonth,
  todayIsoInProjectTimeZone,
  toPersianDigits,
} from "@/lib/persian-date";

const weekdayNames = ["ش", "ی", "د", "س", "چ", "پ", "ج"] as const;

interface PersianDateInputProps {
  readonly value: string;
  readonly onChange: (isoDate: string) => void;
  readonly id?: string;
  readonly name?: string;
  readonly disabled?: boolean;
  readonly required?: boolean;
  readonly ariaLabel?: string;
}

export function PersianDateInput({
  value,
  onChange,
  id,
  name,
  disabled = false,
  required = false,
  ariaLabel = "تاریخ شمسی",
}: PersianDateInputProps) {
  const generatedId = useId();
  const inputId = id ?? generatedId;
  const rootRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const today = useMemo(() => todayIsoInProjectTimeZone(), []);
  const initialDate = isoDateToPersian(value || today);
  const [displayValue, setDisplayValue] = useState(() => value ? formatPersianDateInput(value) : "");
  const [visibleMonth, setVisibleMonth] = useState({ year: initialDate.year, month: initialDate.month });
  const [open, setOpen] = useState(false);
  const [invalid, setInvalid] = useState(false);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      setDisplayValue(value ? formatPersianDateInput(value) : "");
      inputRef.current?.setCustomValidity("");
      setInvalid(false);
      if (value) {
        const next = isoDateToPersian(value);
        setVisibleMonth({ year: next.year, month: next.month });
      }
    }, 0);
    return () => window.clearTimeout(timeoutId);
  }, [value]);

  useEffect(() => {
    if (!open) return undefined;
    const closeOnOutsidePointer = (event: PointerEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false);
    };
    document.addEventListener("pointerdown", closeOnOutsidePointer);
    return () => document.removeEventListener("pointerdown", closeOnOutsidePointer);
  }, [open]);

  const days = useMemo(
    () => persianCalendarMonth(visibleMonth.year, visibleMonth.month, today),
    [today, visibleMonth.month, visibleMonth.year],
  );

  function acceptText(nextDisplay: string, showError: boolean): boolean {
    const normalized = nextDisplay.trim();
    if (!normalized) {
      onChange("");
      inputRef.current?.setCustomValidity(required ? "واردکردن تاریخ شمسی الزامی است." : "");
      setInvalid(showError && required);
      return !required;
    }
    const isoDate = parsePersianDateInput(normalized);
    if (!isoDate) {
      inputRef.current?.setCustomValidity("تاریخ شمسی معتبر وارد کنید؛ نمونه: ۱۴۰۵/۰۱/۰۱");
      setInvalid(showError);
      return false;
    }
    inputRef.current?.setCustomValidity("");
    onChange(isoDate);
    setDisplayValue(formatPersianDateInput(isoDate));
    setInvalid(false);
    const next = isoDateToPersian(isoDate);
    setVisibleMonth({ year: next.year, month: next.month });
    return true;
  }

  function moveMonth(offset: number) {
    const next = shiftPersianMonth(visibleMonth.year, visibleMonth.month, offset);
    setVisibleMonth({ year: next.year, month: next.month });
  }

  function selectDate(isoDate: string) {
    inputRef.current?.setCustomValidity("");
    onChange(isoDate);
    setDisplayValue(formatPersianDateInput(isoDate));
    setInvalid(false);
    setOpen(false);
    const next = isoDateToPersian(isoDate);
    setVisibleMonth({ year: next.year, month: next.month });
  }

  return (
    <div className="persian-date-input" ref={rootRef}>
      <div className="persian-date-input-control">
        <input
          ref={inputRef}
          id={inputId}
          name={name}
          type="text"
          inputMode="numeric"
          autoComplete="off"
          dir="ltr"
          value={displayValue}
          disabled={disabled}
          required={required}
          aria-label={ariaLabel}
          aria-invalid={invalid}
          aria-describedby={invalid ? `${inputId}-error` : undefined}
          placeholder="۱۴۰۵/۰۱/۰۱"
          onChange={(event) => {
            const next = event.target.value;
            setDisplayValue(next);
            setInvalid(false);
            acceptText(next, false);
          }}
          onBlur={() => acceptText(displayValue, true)}
          onKeyDown={(event) => {
            if (event.key === "Enter" && !acceptText(displayValue, true)) {
              event.preventDefault();
              inputRef.current?.reportValidity();
            }
            if (event.key === "Escape") setOpen(false);
          }}
        />
        <button
          type="button"
          className="persian-date-trigger"
          disabled={disabled}
          aria-label="باز کردن تقویم شمسی"
          aria-expanded={open}
          aria-controls={`${inputId}-calendar`}
          onPointerDown={(event) => event.preventDefault()}
          onClick={() => setOpen((current) => !current)}
        >
          <span aria-hidden="true">▦</span>
        </button>
      </div>
      {invalid && <small className="persian-date-error" id={`${inputId}-error`}>تاریخ شمسی معتبر وارد کنید؛ نمونه: ۱۴۰۵/۰۱/۰۱</small>}
      {open && (
        <div className="persian-date-popover" id={`${inputId}-calendar`} role="dialog" aria-label="انتخاب تاریخ شمسی">
          <div className="persian-date-header">
            <button type="button" className="secondary-button" aria-label="ماه بعد" onClick={() => moveMonth(1)}>‹</button>
            <strong>{persianMonthTitle(visibleMonth.year, visibleMonth.month)}</strong>
            <button type="button" className="secondary-button" aria-label="ماه قبل" onClick={() => moveMonth(-1)}>›</button>
          </div>
          <div className="persian-date-weekdays" aria-hidden="true">
            {weekdayNames.map((weekday) => <span key={weekday}>{weekday}</span>)}
          </div>
          <div className="persian-date-grid" role="grid">
            {days.map((day) => (
              <button
                type="button"
                role="gridcell"
                key={day.isoDate}
                className={`${day.inCurrentMonth ? "" : "is-adjacent"} ${day.isToday ? "is-today" : ""}`.trim()}
                aria-selected={day.isoDate === value}
                aria-current={day.isToday ? "date" : undefined}
                onClick={() => selectDate(day.isoDate)}
              >
                {toPersianDigits(day.day)}
              </button>
            ))}
          </div>
          <div className="persian-date-footer">
            <button type="button" className="secondary-button" onClick={() => selectDate(today)}>امروز</button>
            {!required && <button type="button" className="secondary-button" onClick={() => { inputRef.current?.setCustomValidity(""); onChange(""); setDisplayValue(""); setInvalid(false); setOpen(false); }}>پاک‌کردن</button>}
          </div>
        </div>
      )}
    </div>
  );
}

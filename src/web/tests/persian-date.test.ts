import assert from "node:assert/strict";
import test from "node:test";
import {
  addDaysToIsoDate,
  formatPersianDate,
  formatPersianDateInput,
  formatPersianDateTime,
  isPersianLeapYear,
  parsePersianDateInput,
  persianCalendarMonth,
  persianDateToIso,
  persianMonthLength,
  todayIsoInProjectTimeZone,
} from "../lib/persian-date.ts";

test("Nowruz conversion is exact in both directions", () => {
  assert.equal(formatPersianDateInput("2024-03-20"), "۱۴۰۳/۰۱/۰۱");
  assert.equal(persianDateToIso(1403, 1, 1), "2024-03-20");
});

test("Persian leap Esfand is accepted and non-leap Esfand is rejected", () => {
  assert.equal(isPersianLeapYear(1403), true);
  assert.equal(persianMonthLength(1403, 12), 30);
  assert.equal(parsePersianDateInput("۱۴۰۳/۱۲/۳۰"), "2025-03-20");
  assert.equal(isPersianLeapYear(1404), false);
  assert.equal(persianMonthLength(1404, 12), 29);
  assert.equal(parsePersianDateInput("۱۴۰۴/۱۲/۳۰"), null);
});

test("Persian input accepts Persian, Arabic and Latin digits", () => {
  assert.equal(parsePersianDateInput("۱۴۰۵/۰۶/۲۱"), "2026-09-12");
  assert.equal(parsePersianDateInput("١٤٠٥-٠٦-٢١"), "2026-09-12");
  assert.equal(parsePersianDateInput("1405/6/21"), "2026-09-12");
});

test("visible date and date-time always resolve to the Persian calendar", () => {
  assert.match(formatPersianDate("2026-09-12"), /۲۱ شهریور ۱۴۰۵/);
  assert.match(formatPersianDateTime("2026-09-12T10:30:00Z"), /۲۱ شهریور ۱۴۰۵/);
});

test("ISO arithmetic remains stable across Nowruz", () => {
  assert.equal(addDaysToIsoDate("2025-03-20", 1), "2025-03-21");
  assert.equal(formatPersianDateInput(addDaysToIsoDate("2025-03-20", 1)), "۱۴۰۴/۰۱/۰۱");
});

test("project today uses Tehran civil date around the UTC boundary", () => {
  assert.equal(todayIsoInProjectTimeZone(new Date("2026-09-11T21:00:00Z")), "2026-09-12");
});

test("calendar month is Saturday-first and contains exactly 42 selectable days", () => {
  const days = persianCalendarMonth(1405, 6, "2026-09-12");
  assert.equal(days.length, 42);
  assert.equal(days.filter((day) => day.inCurrentMonth).length, 31);
  assert.equal(days.find((day) => day.isToday)?.day, 21);
  assert.equal(new Date(`${days[0].isoDate}T12:00:00Z`).getUTCDay(), 6);
});

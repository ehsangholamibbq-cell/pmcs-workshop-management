export const PERSIAN_CALENDAR_LOCALE = "fa-IR-u-ca-persian-nu-arabext";
export const PROJECT_TIME_ZONE = "Asia/Tehran";

export interface PersianDateParts {
  readonly year: number;
  readonly month: number;
  readonly day: number;
}

export interface PersianCalendarDay extends PersianDateParts {
  readonly isoDate: string;
  readonly inCurrentMonth: boolean;
  readonly isToday: boolean;
}

const persianMonthNames = [
  "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
  "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند",
] as const;

const jalaliBreaks = [
  -61, 9, 38, 199, 426, 686, 756, 818, 1111, 1181,
  1210, 1635, 2060, 2097, 2192, 2262, 2324, 2394, 2456, 3178,
] as const;

export function toPersianDigits(value: string | number): string {
  return String(value).replace(/\d/g, (digit) => "۰۱۲۳۴۵۶۷۸۹"[Number(digit)]);
}

export function normalizePersianDigits(value: string): string {
  return value
    .replace(/[۰-۹]/g, (digit) => String("۰۱۲۳۴۵۶۷۸۹".indexOf(digit)))
    .replace(/[٠-٩]/g, (digit) => String("٠١٢٣٤٥٦٧٨٩".indexOf(digit)));
}

export function isoDateToPersian(value: string): PersianDateParts {
  const { year, month, day } = parseIsoDate(value);
  return dayNumberToPersian(gregorianToDayNumber(year, month, day));
}

export function persianDateToIso(year: number, month: number, day: number): string {
  if (!isValidPersianDate(year, month, day)) {
    throw new RangeError("persian_date.invalid");
  }
  const gregorian = dayNumberToGregorian(persianToDayNumber(year, month, day));
  return `${padYear(gregorian.year)}-${pad2(gregorian.month)}-${pad2(gregorian.day)}`;
}

export function isValidPersianDate(year: number, month: number, day: number): boolean {
  if (!Number.isInteger(year) || !Number.isInteger(month) || !Number.isInteger(day)) return false;
  if (year < jalaliBreaks[0] || year >= jalaliBreaks[jalaliBreaks.length - 1]) return false;
  return month >= 1 && month <= 12 && day >= 1 && day <= persianMonthLength(year, month);
}

export function isPersianLeapYear(year: number): boolean {
  return jalaliCalendar(year).leap === 0;
}

export function persianMonthLength(year: number, month: number): number {
  if (month < 1 || month > 12) throw new RangeError("persian_month.invalid");
  if (month <= 6) return 31;
  if (month <= 11) return 30;
  return isPersianLeapYear(year) ? 30 : 29;
}

export function formatPersianDateInput(value: string): string {
  if (!value) return "";
  const date = isoDateToPersian(value);
  return toPersianDigits(`${date.year}/${pad2(date.month)}/${pad2(date.day)}`);
}

export function parsePersianDateInput(value: string): string | null {
  const normalized = normalizePersianDigits(value)
    .trim()
    .replace(/[.\-–—]/g, "/")
    .replace(/\s*\/\s*/g, "/");
  const match = /^(\d{3,4})\/(\d{1,2})\/(\d{1,2})$/.exec(normalized);
  if (!match) return null;
  const year = Number(match[1]);
  const month = Number(match[2]);
  const day = Number(match[3]);
  return isValidPersianDate(year, month, day) ? persianDateToIso(year, month, day) : null;
}

export function formatPersianDate(
  value: string | null | undefined,
  dateStyle: Intl.DateTimeFormatOptions["dateStyle"] = "medium",
): string {
  if (!value) return "—";
  const { year, month, day } = parseIsoDate(value);
  const instant = new Date(Date.UTC(year, month - 1, day, 12));
  return new Intl.DateTimeFormat(PERSIAN_CALENDAR_LOCALE, {
    dateStyle,
    timeZone: "UTC",
  }).format(instant);
}

export function formatPersianDateTime(
  value: string | null | undefined,
  timeZone = PROJECT_TIME_ZONE,
  dateStyle: Intl.DateTimeFormatOptions["dateStyle"] = "medium",
  timeStyle: Intl.DateTimeFormatOptions["timeStyle"] = "short",
): string {
  if (!value) return "—";
  const instant = new Date(value);
  if (Number.isNaN(instant.getTime())) throw new RangeError("date_time.invalid");
  return new Intl.DateTimeFormat(PERSIAN_CALENDAR_LOCALE, {
    dateStyle,
    timeStyle,
    timeZone,
  }).format(instant);
}

export function todayIsoInProjectTimeZone(now = new Date()): string {
  const parts = new Intl.DateTimeFormat("en-CA-u-ca-gregory-nu-latn", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    timeZone: PROJECT_TIME_ZONE,
  }).formatToParts(now);
  const read = (type: Intl.DateTimeFormatPartTypes) => parts.find((part) => part.type === type)?.value;
  const year = read("year");
  const month = read("month");
  const day = read("day");
  if (!year || !month || !day) throw new Error("project_date.unavailable");
  return `${year}-${month}-${day}`;
}

export function addDaysToIsoDate(value: string, days: number): string {
  const { year, month, day } = parseIsoDate(value);
  const instant = new Date(Date.UTC(year, month - 1, day + days, 12));
  return `${padYear(instant.getUTCFullYear())}-${pad2(instant.getUTCMonth() + 1)}-${pad2(instant.getUTCDate())}`;
}

export function futureProjectDate(days: number, now = new Date()): string {
  return addDaysToIsoDate(todayIsoInProjectTimeZone(now), days);
}

export function shiftPersianMonth(year: number, month: number, offset: number): PersianDateParts {
  const absoluteMonth = year * 12 + month - 1 + offset;
  const nextYear = floorDivide(absoluteMonth, 12);
  const nextMonth = absoluteMonth - nextYear * 12 + 1;
  if (nextYear < jalaliBreaks[0] || nextYear >= jalaliBreaks[jalaliBreaks.length - 1]) {
    throw new RangeError("persian_date.out_of_range");
  }
  return { year: nextYear, month: nextMonth, day: 1 };
}

export function persianMonthTitle(year: number, month: number): string {
  if (month < 1 || month > 12) throw new RangeError("persian_month.invalid");
  return `${persianMonthNames[month - 1]} ${toPersianDigits(year)}`;
}

export function persianCalendarMonth(
  year: number,
  month: number,
  todayIso = todayIsoInProjectTimeZone(),
): readonly PersianCalendarDay[] {
  const firstIso = persianDateToIso(year, month, 1);
  const { year: firstYear, month: firstMonth, day: firstDay } = parseIsoDate(firstIso);
  const weekday = new Date(Date.UTC(firstYear, firstMonth - 1, firstDay, 12)).getUTCDay();
  const daysBeforeSaturday = (weekday + 1) % 7;
  const gridStart = addDaysToIsoDate(firstIso, -daysBeforeSaturday);

  return Array.from({ length: 42 }, (_, index) => {
    const isoDate = addDaysToIsoDate(gridStart, index);
    const persian = isoDateToPersian(isoDate);
    return {
      ...persian,
      isoDate,
      inCurrentMonth: persian.year === year && persian.month === month,
      isToday: isoDate === todayIso,
    };
  });
}

function parseIsoDate(value: string): { readonly year: number; readonly month: number; readonly day: number } {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  if (!match) throw new RangeError("iso_date.invalid");
  const year = Number(match[1]);
  const month = Number(match[2]);
  const day = Number(match[3]);
  const instant = new Date(Date.UTC(year, month - 1, day, 12));
  if (instant.getUTCFullYear() !== year || instant.getUTCMonth() + 1 !== month || instant.getUTCDate() !== day) {
    throw new RangeError("iso_date.invalid");
  }
  return { year, month, day };
}

function jalaliCalendar(year: number, withoutLeap = false): { readonly leap: number; readonly gregorianYear: number; readonly marchDay: number } {
  const breaksLength = jalaliBreaks.length;
  const gregorianYear = year + 621;
  let leapDays = -14;
  let previousBreak: number = jalaliBreaks[0];
  let currentBreak = 0;
  let jump = 0;

  if (year < previousBreak || year >= jalaliBreaks[breaksLength - 1]) throw new RangeError("persian_date.out_of_range");

  for (let index = 1; index < breaksLength; index += 1) {
    currentBreak = jalaliBreaks[index];
    jump = currentBreak - previousBreak;
    if (year < currentBreak) break;
    leapDays += truncate(jump / 33) * 8 + truncate((jump % 33) / 4);
    previousBreak = currentBreak;
  }

  let yearsSinceBreak = year - previousBreak;
  leapDays += truncate(yearsSinceBreak / 33) * 8 + truncate(((yearsSinceBreak % 33) + 3) / 4);
  if (jump % 33 === 4 && jump - yearsSinceBreak === 4) leapDays += 1;

  const gregorianLeapDays = truncate(gregorianYear / 4)
    - truncate(((truncate(gregorianYear / 100) + 1) * 3) / 4) - 150;
  const marchDay = 20 + leapDays - gregorianLeapDays;
  if (withoutLeap) return { leap: 0, gregorianYear, marchDay };

  if (jump - yearsSinceBreak < 6) {
    yearsSinceBreak = yearsSinceBreak - jump + truncate((jump + 4) / 33) * 33;
  }
  let leap = ((yearsSinceBreak + 1) % 33 - 1) % 4;
  if (leap === -1) leap = 4;
  return { leap, gregorianYear, marchDay };
}

function persianToDayNumber(year: number, month: number, day: number): number {
  const calendar = jalaliCalendar(year, true);
  return gregorianToDayNumber(calendar.gregorianYear, 3, calendar.marchDay)
    + (month - 1) * 31 - truncate(month / 7) * (month - 7) + day - 1;
}

function dayNumberToPersian(dayNumber: number): PersianDateParts {
  const gregorian = dayNumberToGregorian(dayNumber);
  let year = gregorian.year - 621;
  const calendar = jalaliCalendar(year);
  const firstFarvardin = gregorianToDayNumber(gregorian.year, 3, calendar.marchDay);
  let offset = dayNumber - firstFarvardin;
  if (offset >= 0) {
    if (offset <= 185) return { year, month: 1 + truncate(offset / 31), day: (offset % 31) + 1 };
    offset -= 186;
  } else {
    year -= 1;
    offset += 179;
    if (calendar.leap === 1) offset += 1;
  }
  return { year, month: 7 + truncate(offset / 30), day: (offset % 30) + 1 };
}

function gregorianToDayNumber(year: number, month: number, day: number): number {
  let result = truncate(((year + truncate((month - 8) / 6) + 100100) * 1461) / 4)
    + truncate((153 * ((month + 9) % 12) + 2) / 5) + day - 34_840_408;
  result = result - truncate((truncate((year + 100100 + truncate((month - 8) / 6)) / 100) * 3) / 4) + 752;
  return result;
}

function dayNumberToGregorian(dayNumber: number): { readonly year: number; readonly month: number; readonly day: number } {
  let value = 4 * dayNumber + 139_361_631;
  value = value + truncate((truncate((4 * dayNumber + 183_187_720) / 146097) * 3) / 4) * 4 - 3908;
  const inner = truncate((value % 1461) / 4) * 5 + 308;
  const day = truncate((inner % 153) / 5) + 1;
  const month = (truncate(inner / 153) % 12) + 1;
  const year = truncate(value / 1461) - 100100 + truncate((8 - month) / 6);
  return { year, month, day };
}

function truncate(value: number): number {
  return value < 0 ? Math.ceil(value) : Math.floor(value);
}

function floorDivide(value: number, divisor: number): number {
  return Math.floor(value / divisor);
}

function pad2(value: number): string {
  return String(value).padStart(2, "0");
}

function padYear(value: number): string {
  return String(value).padStart(4, "0");
}

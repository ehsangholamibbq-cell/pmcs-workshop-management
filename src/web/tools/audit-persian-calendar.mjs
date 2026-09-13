import assert from "node:assert/strict";
import { readFileSync, readdirSync, statSync } from "node:fs";
import { extname, join, relative } from "node:path";

const roots = ["app", "components"];
const failures = [];

for (const root of roots) {
  for (const file of walk(root)) {
    const source = readFileSync(file, "utf8");
    check(file, source, /type\s*=\s*["'](?:date|datetime-local|month|week)["']/g,
      "ورودی تاریخ بومی مرورگر مجاز نیست؛ از PersianDateInput استفاده کنید");
    check(file, source, /Intl\.DateTimeFormat\s*\(/g,
      "قالب‌بندی تاریخ در رابط باید فقط از lib/persian-date انجام شود");
    check(file, source, /\.toLocaleDateString\s*\(/g,
      "قالب‌بندی محلی مستقیم تاریخ مجاز نیست");
    check(file, source, /new\s+Date\s*\(\s*\)\.toISOString\s*\(\s*\)\.slice\s*\(\s*0\s*,\s*10\s*\)/g,
      "روز جاری باید با منطقه زمانی پروژه از todayIsoInProjectTimeZone ساخته شود");
  }
}

const dateLibrary = readFileSync("lib/persian-date.ts", "utf8");
assert.match(dateLibrary, /fa-IR-u-ca-persian-nu-arabext/);
assert.match(dateLibrary, /Asia\/Tehran/);
assert.match(dateLibrary, /persianDateToIso/);
assert.match(readFileSync("components/persian-date-input.tsx", "utf8"), /parsePersianDateInput/);

if (failures.length > 0) {
  console.error("نقض قرارداد تقویم شمسی پیدا شد:");
  failures.forEach((failure) => console.error(`- ${failure}`));
  process.exitCode = 1;
} else {
  console.log("ممیزی تقویم شمسی با موفقیت انجام شد.");
}

function walk(directory) {
  return readdirSync(directory).flatMap((entry) => {
    const path = join(directory, entry);
    if (statSync(path).isDirectory()) return walk(path);
    return [".ts", ".tsx"].includes(extname(path)) ? [path] : [];
  });
}

function check(file, source, pattern, message) {
  for (const match of source.matchAll(pattern)) {
    const line = source.slice(0, match.index).split("\n").length;
    failures.push(`${relative(".", file)}:${line} — ${message}`);
  }
}

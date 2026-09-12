import type { ProjectCapability } from "@/lib/command-center";

export type CapabilityStatus =
  | "active"
  | "not-configured"
  | "not-enabled"
  | "setup-required"
  | "no-data"
  | "suspended";

export interface CapabilityView {
  readonly key: string;
  readonly label: string;
  readonly status: CapabilityStatus;
  readonly detail: string;
}

export const initialCapabilities: readonly CapabilityView[] = [
  {
    key: "operations",
    label: "عملیات کارگاه",
    status: "active",
    detail: "آماده ثبت واقعیت روزانه",
  },
  {
    key: "planning",
    label: "برنامه و ساختار شکست کار (WBS)",
    status: "not-configured",
    detail: "بدون تولید انحراف برنامه ساختگی",
  },
  {
    key: "budget",
    label: "بودجه",
    status: "setup-required",
    detail: "بودجه اولیه ثبت نشده؛ در ارزیابی عملیات وارد نمی‌شود",
  },
  {
    key: "calendar",
    label: "تقویم پروژه",
    status: "not-configured",
    detail: "در نبود تقویم، پوشش داده با مبنای هفت روز تقویمی محاسبه می‌شود",
  },
  {
    key: "finance",
    label: "کنترل مالی پایه",
    status: "no-data",
    detail: "فعال است، اما هنوز سند قطعی ثبت نشده است",
  },
  {
    key: "hse",
    label: "ایمنی، بهداشت و محیط‌زیست (HSE)",
    status: "not-enabled",
    detail: "از ارزیابی جامع و محاسبه پوشش داده حذف شده است",
  },
] as const;

export function toCapabilityView(capability: ProjectCapability): CapabilityView {
  const status = capability.configurationState === "NotConfigured"
    ? "not-configured"
    : capability.configurationState === "NotEnabled"
      ? "not-enabled"
      : capability.configurationState === "SetupRequired"
        ? "setup-required"
        : capability.configurationState === "Suspended"
          ? "suspended"
          : capability.metricState === "Available"
            ? "active"
            : "no-data";

  return {
    key: capability.key,
    label: capabilityLabelName(capability.key, capability.label),
    status,
    detail: capability.includedInAssessment
      ? "در ارزیابی عملیاتی جاری استفاده می‌شود"
      : capability.metricState === "NotApplicable"
        ? "در این پروژه فعال نیست و از ارزیابی حذف شده است"
        : "فعال است، اما هنوز شاخص رسمی این ماژول ساخته نشده است",
  };
}

export function capabilityLabel(status: CapabilityStatus): string {
  switch (status) {
    case "active":
      return "فعال";
    case "not-configured":
      return "پیکربندی نشده";
    case "not-enabled":
      return "فعال نشده";
    case "setup-required":
      return "نیازمند راه‌اندازی";
    case "no-data":
      return "بدون شاخص رسمی";
    case "suspended":
      return "تعلیق‌شده";
  }
}

function capabilityLabelName(key: string, fallback: string): string {
  return ({
    operations: "عملیات کارگاه",
    contract: "قرارداد",
    planning: "برنامه و ساختار شکست کار (WBS)",
    budget: "بودجه",
    calendar: "تقویم پروژه",
    finance: "کنترل مالی پایه",
    procurement: "تدارکات و خرید",
    quality: "کنترل کیفیت",
    hse: "ایمنی، بهداشت و محیط‌زیست (HSE)",
  } as Record<string, string>)[key] ?? (/[\u0600-\u06ff]/u.test(fallback) ? fallback : "قابلیت پروژه");
}

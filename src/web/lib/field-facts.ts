export type DailyFactKind =
  | "WorkProgress"
  | "Labor"
  | "Equipment"
  | "Material"
  | "Issue"
  | "Stoppage"
  | "SiteCondition"
  | "Note";

export type DailyImpactLevel = "Low" | "Medium" | "High" | "Critical";

export interface DailyFactDraft {
  readonly kind: DailyFactKind;
  readonly description: string;
  readonly category: string;
  readonly locationName: string;
  readonly quantity: string;
  readonly unit: string;
  readonly resourceCount: string;
  readonly hours: string;
  readonly impactLevel: DailyImpactLevel | "";
  readonly referenceCode: string;
  readonly measurementItemId: string;
}

export interface DailyFactPayload {
  readonly factId: string;
  readonly reportDate: string;
  readonly locationName: null;
  readonly kind: DailyFactKind;
  readonly description: string;
  readonly category: string | null;
  readonly factLocationName: string | null;
  readonly quantity: number | null;
  readonly unit: string | null;
  readonly resourceCount: number | null;
  readonly hours: number | null;
  readonly impactLevel: DailyImpactLevel | null;
  readonly referenceCode: string | null;
  readonly measurementItemId: string | null;
}

export const factKinds: readonly { value: DailyFactKind; label: string; hint: string }[] = [
  { value: "WorkProgress", label: "پیشرفت اجرایی", hint: "کار انجام‌شده و مقدار واقعی" },
  { value: "Labor", label: "نیروی انسانی", hint: "رسته، تعداد و نفرساعت" },
  { value: "Equipment", label: "ماشین‌آلات", hint: "نوع، تعداد و ساعات کار" },
  { value: "Material", label: "مصالح", hint: "دریافت یا مصرف واقعی" },
  { value: "Issue", label: "مشکل", hint: "مانع یا موضوع نیازمند اقدام" },
  { value: "Stoppage", label: "توقف", hint: "کار متوقف‌شده و اثر مشاهده‌شده" },
  { value: "SiteCondition", label: "شرایط کارگاه", hint: "وضعیت محیط یا دسترسی" },
  { value: "Note", label: "یادداشت واقعی", hint: "مشاهده‌ای که در دسته‌های دیگر نیست" },
] as const;

export const emptyFactDraft: DailyFactDraft = {
  kind: "WorkProgress",
  description: "",
  category: "",
  locationName: "",
  quantity: "",
  unit: "",
  resourceCount: "",
  hours: "",
  impactLevel: "",
  referenceCode: "",
  measurementItemId: "",
};

export class FactValidationError extends Error {}

export function buildDailyFactPayload(
  draft: DailyFactDraft,
  factId: string,
  reportDate: string,
): DailyFactPayload {
  const description = requiredText(draft.description, "شرح واقعیت الزامی است");
  const fields = factFields(draft.kind);
  const category = fields.category ? optionalText(draft.category) : null;
  const quantity = fields.quantity
    ? optionalNumber(draft.quantity, "مقدار باید عددی نامنفی باشد")
    : null;
  const resourceCount = fields.resources
    ? optionalInteger(draft.resourceCount, "تعداد باید عدد صحیح بزرگ‌تر از صفر باشد")
    : null;
  const hours = fields.resources
    ? optionalNumber(draft.hours, "ساعت باید عددی بین صفر و ۱۰۰٬۰۰۰ باشد", 100_000)
    : null;
  const unit = fields.quantity ? optionalText(draft.unit) : null;

  if ((draft.kind === "Labor" || draft.kind === "Equipment") && (!category || resourceCount === null)) {
    throw new FactValidationError("برای نیروی انسانی و ماشین‌آلات، رسته/نوع و تعداد الزامی است");
  }

  if (draft.kind === "Material" && (!category || quantity === null || !unit)) {
    throw new FactValidationError("برای مصالح، نام مصالح، مقدار و واحد الزامی است");
  }

  const measurementItemId = draft.kind === "WorkProgress" ? optionalText(draft.measurementItemId) : null;
  if (draft.kind === "WorkProgress" && !category && !measurementItemId) {
    throw new FactValidationError("نام فعالیت اجرایی یا قلم اندازه‌گیری الزامی است؛ کد WBS اختیاری است");
  }
  if (measurementItemId && (quantity === null || !unit)) {
    throw new FactValidationError("برای قلم اندازه‌گیری، مقدار واقعی و واحد الزامی است");
  }

  return {
    factId,
    reportDate,
    locationName: null,
    kind: draft.kind,
    description,
    category,
    factLocationName: optionalText(draft.locationName),
    quantity,
    unit,
    resourceCount,
    hours,
    impactLevel: fields.impact ? draft.impactLevel || null : null,
    referenceCode: fields.reference ? optionalText(draft.referenceCode) : null,
    measurementItemId,
  };
}

export function factFields(kind: DailyFactKind) {
  return {
    category: ["WorkProgress", "Labor", "Equipment", "Material", "Issue", "Stoppage"].includes(kind),
    quantity: ["WorkProgress", "Material"].includes(kind),
    resources: ["Labor", "Equipment"].includes(kind),
    impact: ["Issue", "Stoppage"].includes(kind),
    reference: ["WorkProgress", "Issue", "Stoppage"].includes(kind),
  };
}

function requiredText(value: string, message: string): string {
  const normalized = value.trim();
  if (!normalized) {
    throw new FactValidationError(message);
  }
  return normalized;
}

function optionalText(value: string): string | null {
  const normalized = value.trim();
  return normalized || null;
}

function optionalNumber(value: string, message: string, maximum?: number): number | null {
  if (!value.trim()) {
    return null;
  }
  const number = Number(value);
  if (!Number.isFinite(number) || number < 0 || (maximum !== undefined && number > maximum)) {
    throw new FactValidationError(message);
  }
  return number;
}

function optionalInteger(value: string, message: string): number | null {
  const number = optionalNumber(value, message);
  if (number !== null && (!Number.isInteger(number) || number === 0)) {
    throw new FactValidationError(message);
  }
  return number;
}

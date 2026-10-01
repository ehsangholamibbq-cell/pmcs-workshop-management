export type LoginCompositionVariant = "BlueprintSplit" | "MonolithFocus" | "WarmMinimal";
export type LoginSurfaceTone = "WarmStone" | "WarmIvory" | "DeepNavy";
export type LoginAccentPalette = "CorporateNavyGreen" | "NavySilver" | "GreenStone";
export type LoginMotionPolicy = "Calm" | "Balanced" | "Expressive";

export interface LoginExperienceDescriptor {
  readonly version: number;
  readonly fallbackUsed: boolean;
  readonly compositionVariant: LoginCompositionVariant;
  readonly surfaceTone: LoginSurfaceTone;
  readonly accentPalette: LoginAccentPalette;
  readonly motionPolicy: LoginMotionPolicy;
  readonly eyebrow: string;
  readonly headline: string;
  readonly supportingText: string;
  readonly logoUrl: string | null;
  readonly heroUrl: string | null;
}

const compositions = ["BlueprintSplit", "MonolithFocus", "WarmMinimal"] as const;
const surfaces = ["WarmStone", "WarmIvory", "DeepNavy"] as const;
const accents = ["CorporateNavyGreen", "NavySilver", "GreenStone"] as const;
const motions = ["Calm", "Balanced", "Expressive"] as const;

export function fallbackLoginExperience(): LoginExperienceDescriptor {
  return {
    version: 0,
    fallbackUsed: true,
    compositionVariant: "BlueprintSplit",
    surfaceTone: "WarmStone",
    accentPalette: "CorporateNavyGreen",
    motionPolicy: "Balanced",
    eyebrow: "سامانه جامع مدیریت پروژه",
    headline: "ساختن، فراتر از امروز",
    supportingText: "مرکز فرمان یکپارچه برای تصمیم‌های دقیق، قابل ردیابی و مبتنی بر واقعیت پروژه.",
    logoUrl: null,
    heroUrl: null,
  };
}

export function normalizeLoginExperience(value: unknown): LoginExperienceDescriptor {
  if (!isRecord(value)) return fallbackLoginExperience();
  const version = Number(value.version);
  const compositionVariant = allowlisted(value.compositionVariant, compositions);
  const surfaceTone = allowlisted(value.surfaceTone, surfaces);
  const accentPalette = allowlisted(value.accentPalette, accents);
  const motionPolicy = allowlisted(value.motionPolicy, motions);
  const eyebrow = plainText(value.eyebrow, 80);
  const headline = plainText(value.headline, 140);
  const supportingText = plainText(value.supportingText, 320);
  if (!Number.isSafeInteger(version) || version < 0 || !compositionVariant || !surfaceTone ||
      !accentPalette || !motionPolicy || !eyebrow || !headline || !supportingText) {
    return fallbackLoginExperience();
  }

  const fallbackUsed = value.fallbackUsed === true || version === 0;
  return {
    version,
    fallbackUsed,
    compositionVariant,
    surfaceTone,
    accentPalette,
    motionPolicy,
    eyebrow,
    headline,
    supportingText,
    logoUrl: !fallbackUsed && safeAssetMarker(value.logoUrl)
      ? `/api/login-experience/assets/logo?version=${version}`
      : null,
    heroUrl: !fallbackUsed && safeAssetMarker(value.heroUrl)
      ? `/api/login-experience/assets/hero?version=${version}`
      : null,
  };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function allowlisted<T extends string>(value: unknown, allowed: readonly T[]): T | null {
  return typeof value === "string" && allowed.includes(value as T) ? value as T : null;
}

function plainText(value: unknown, maximumLength: number): string | null {
  if (typeof value !== "string") return null;
  const normalized = value.trim();
  return normalized.length > 0 && normalized.length <= maximumLength &&
    !/[<>\u0000-\u001f\u007f]/u.test(normalized)
    ? normalized
    : null;
}

function safeAssetMarker(value: unknown): boolean {
  return typeof value === "string" &&
    /^\/api\/v1\/public\/login-experience\/assets\/(?:logo|hero)\?/u.test(value);
}

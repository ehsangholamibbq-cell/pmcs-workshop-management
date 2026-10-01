import { ensureApiSuccess } from "./localization.ts";
import type {
  LoginAccentPalette,
  LoginCompositionVariant,
  LoginMotionPolicy,
  LoginSurfaceTone,
} from "./login-experience.ts";

export type LoginExperienceStatus = "Draft" | "Published" | "Superseded";

export interface LoginExperienceVersion {
  readonly id: string;
  readonly versionNumber: number;
  readonly status: LoginExperienceStatus;
  readonly compositionVariant: LoginCompositionVariant;
  readonly surfaceTone: LoginSurfaceTone;
  readonly accentPalette: LoginAccentPalette;
  readonly motionPolicy: LoginMotionPolicy;
  readonly eyebrow: string;
  readonly headline: string;
  readonly supportingText: string;
  readonly logoDocumentId: string | null;
  readonly heroDocumentId: string | null;
  readonly logoPreviewUrl: string | null;
  readonly heroPreviewUrl: string | null;
  readonly createdAt: string;
  readonly publishedAt: string | null;
  readonly revision: number;
}

export interface CreateLoginExperienceInput {
  readonly clientGeneratedId: string;
  readonly compositionVariant: LoginCompositionVariant;
  readonly surfaceTone: LoginSurfaceTone;
  readonly accentPalette: LoginAccentPalette;
  readonly motionPolicy: LoginMotionPolicy;
  readonly eyebrow: string;
  readonly headline: string;
  readonly supportingText: string;
  readonly logoDocumentId: string | null;
  readonly heroDocumentId: string | null;
}

export async function listLoginExperiences(): Promise<readonly LoginExperienceVersion[]> {
  const response = await fetch("/api/pmcs/api/v1/identity/login-experiences", { cache: "no-store" });
  await ensureApiSuccess(response);
  return response.json() as Promise<readonly LoginExperienceVersion[]>;
}

export async function createLoginExperience(
  input: CreateLoginExperienceInput,
): Promise<LoginExperienceVersion> {
  const response = await fetch("/api/pmcs/api/v1/identity/login-experiences", {
    method: "POST",
    headers: commandHeaders(),
    body: JSON.stringify(input),
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<LoginExperienceVersion>;
}

export async function activateLoginExperience(
  version: LoginExperienceVersion,
): Promise<LoginExperienceVersion> {
  const action = version.status === "Superseded" ? "rollback" : "publish";
  const response = await fetch(
    `/api/pmcs/api/v1/identity/login-experiences/${version.versionNumber}/${action}`,
    {
      method: "POST",
      headers: commandHeaders(),
      body: JSON.stringify({ baseRevision: version.revision }),
    },
  );
  await ensureApiSuccess(response);
  return response.json() as Promise<LoginExperienceVersion>;
}

function commandHeaders(): Record<string, string> {
  return {
    "Content-Type": "application/json",
    "Idempotency-Key": crypto.randomUUID(),
  };
}

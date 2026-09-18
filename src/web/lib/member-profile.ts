import { ensureApiSuccess } from "./localization.ts";

export interface ProfileAvatarCrop {
  readonly x: number;
  readonly y: number;
  readonly width: number;
  readonly height: number;
}

export interface MemberProfileModel {
  readonly userId: string;
  readonly displayName: string;
  readonly email: string;
  readonly jobTitle: string | null;
  readonly organizationUnit: string | null;
  readonly workPhone: string | null;
  readonly avatarDocumentId: string | null;
  readonly avatarCrop: ProfileAvatarCrop | null;
  readonly avatarUrl: string | null;
  readonly userRevision: number;
  readonly profileRevision: number;
  readonly updatedAt: string;
}

export interface UpdateSelfProfileInput {
  readonly baseUserRevision: number;
  readonly baseProfileRevision: number;
  readonly displayName: string;
  readonly jobTitle: string | null;
  readonly workPhone: string | null;
  readonly avatarDocumentId: string | null;
  readonly avatarCrop: ProfileAvatarCrop | null;
}

export interface DocumentState {
  readonly id: string;
  readonly status: "PendingUpload" | "Quarantined" | "Released" | "Rejected";
  readonly revision: number;
}

export async function loadMyProfile(): Promise<MemberProfileModel> {
  const response = await fetch("/api/pmcs/api/v1/member-profile", { cache: "no-store" });
  await ensureApiSuccess(response);
  return response.json() as Promise<MemberProfileModel>;
}

export async function updateMyProfile(
  input: UpdateSelfProfileInput,
): Promise<MemberProfileModel> {
  const response = await fetch("/api/pmcs/api/v1/member-profile", {
    method: "PUT",
    headers: {
      "Content-Type": "application/json",
      "Idempotency-Key": crypto.randomUUID(),
    },
    body: JSON.stringify(input),
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<MemberProfileModel>;
}

export async function loadDocumentState(documentId: string): Promise<DocumentState> {
  const response = await fetch(`/api/pmcs/api/v1/documents/${documentId}`, { cache: "no-store" });
  await ensureApiSuccess(response);
  return response.json() as Promise<DocumentState>;
}

export async function releaseOwnProfileImage(document: DocumentState): Promise<DocumentState> {
  const response = await fetch(`/api/pmcs/api/v1/documents/${document.id}/release`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "Idempotency-Key": crypto.randomUUID(),
    },
    body: JSON.stringify({ baseRevision: document.revision }),
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<DocumentState>;
}

export function profileAssetUrl(path: string | null): string | null {
  return path?.startsWith("/api/v1/") ? `/api/pmcs${path}` : null;
}

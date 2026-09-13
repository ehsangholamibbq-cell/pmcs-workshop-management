import { ensureApiSuccess } from "./localization.ts";

export type TenantRole = "Member" | "PortfolioViewer" | "TenantAdministrator";
export type UserStatus = "Invited" | "Active" | "Suspended" | "Deactivated";
export type InvitationStatus = "Queued" | "Processing" | "RetryScheduled" | "Sent" | "Failed" | "Revoked" | "Expired";
export type MembershipStatus = "Proposed" | "Active" | "Suspended" | "Expired" | "Revoked";

export interface MembershipModel {
  readonly id: string;
  readonly projectId: string;
  readonly roleCode: string;
  readonly status: MembershipStatus;
  readonly startsAt: string;
  readonly endsAt: string | null;
  readonly revision: number;
}

export interface UserDirectoryModel {
  readonly id: string;
  readonly displayName: string;
  readonly email: string;
  readonly tenantRole: TenantRole;
  readonly status: UserStatus;
  readonly createdAt: string;
  readonly revision: number;
  readonly memberships: readonly MembershipModel[];
  readonly providerSyncPending: boolean;
}

export interface InvitationModel {
  readonly id: string;
  readonly displayName: string;
  readonly email: string;
  readonly tenantRole: TenantRole;
  readonly status: InvitationStatus;
  readonly createdAt: string;
  readonly expiresAt: string;
  readonly sentAt: string | null;
  readonly nextAttemptAt: string | null;
  readonly userId: string | null;
  readonly attempts: number;
  readonly lastErrorCode: string | null;
  readonly projects: readonly InvitationProjectInput[];
}

export interface IdentityDirectoryModel {
  readonly provisioningEnabled: boolean;
  readonly tenantRoles: readonly TenantRole[];
  readonly projectRoles: readonly string[];
  readonly users: readonly UserDirectoryModel[];
  readonly invitations: readonly InvitationModel[];
}

export interface EffectivePermissionDecisionModel {
  readonly operation: string;
  readonly allowed: boolean;
  readonly source: string;
  readonly scope: string;
  readonly condition: string;
  readonly denyReason: string | null;
  readonly expiresAt: string | null;
  readonly delegationEffect: string;
}

export interface EffectivePermissionPreviewModel {
  readonly userId: string;
  readonly projectId: string;
  readonly accountStatus: string;
  readonly tenantRole: string | null;
  readonly projectRole: string | null;
  readonly policyVersion: string;
  readonly evaluatedAt: string;
  readonly decisions: readonly EffectivePermissionDecisionModel[];
}

export interface InvitationProjectInput {
  readonly projectId: string;
  readonly roleCode: string;
}

export interface InviteUserInput {
  readonly displayName: string;
  readonly email: string;
  readonly tenantRole: TenantRole;
  readonly projects: readonly InvitationProjectInput[];
}

export async function getIdentityDirectory(apiBaseUrl: string): Promise<IdentityDirectoryModel> {
  const response = await fetch(`${normalize(apiBaseUrl)}/api/v1/identity/directory`, { cache: "no-store" });
  await ensureApiSuccess(response);
  return response.json() as Promise<IdentityDirectoryModel>;
}

export async function getEffectivePermissionPreview(
  apiBaseUrl: string,
  userId: string,
  projectId: string,
  proposedRoleCode?: string,
): Promise<EffectivePermissionPreviewModel> {
  const query = new URLSearchParams({ userId, projectId });
  if (proposedRoleCode) query.set("proposedRoleCode", proposedRoleCode);
  const response = await fetch(`${normalize(apiBaseUrl)}/api/v1/identity/permissions/preview?${query}`, {
    cache: "no-store",
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<EffectivePermissionPreviewModel>;
}

export async function inviteUser(apiBaseUrl: string, input: InviteUserInput): Promise<InvitationModel> {
  return mutate<InvitationModel>(`${normalize(apiBaseUrl)}/api/v1/identity/invitations`, "POST", input);
}

export async function resendInvitation(apiBaseUrl: string, invitationId: string): Promise<void> {
  await mutate(`${normalize(apiBaseUrl)}/api/v1/identity/invitations/${invitationId}/resend`, "POST", {});
}

export async function revokeInvitation(apiBaseUrl: string, invitationId: string): Promise<void> {
  await mutate(`${normalize(apiBaseUrl)}/api/v1/identity/invitations/${invitationId}/revoke`, "POST", {});
}

export async function changeUserStatus(apiBaseUrl: string, userId: string, status: UserStatus): Promise<void> {
  await mutate(`${normalize(apiBaseUrl)}/api/v1/identity/users/${userId}/status`, "PUT", { status });
}

export async function changeTenantRole(apiBaseUrl: string, userId: string, tenantRole: TenantRole): Promise<void> {
  await mutate(`${normalize(apiBaseUrl)}/api/v1/identity/users/${userId}/tenant-role`, "PUT", { tenantRole });
}

export async function upsertMembership(
  apiBaseUrl: string,
  userId: string,
  projectId: string,
  roleCode: string,
): Promise<void> {
  await mutate(`${normalize(apiBaseUrl)}/api/v1/identity/users/${userId}/memberships/${projectId}`, "PUT", { roleCode });
}

export async function revokeMembership(
  apiBaseUrl: string,
  userId: string,
  projectId: string,
): Promise<void> {
  await mutate(`${normalize(apiBaseUrl)}/api/v1/identity/users/${userId}/memberships/${projectId}`, "DELETE");
}

async function mutate<T = unknown>(url: string, method: string, body?: unknown): Promise<T> {
  const response = await fetch(url, {
    method,
    headers: {
      "Content-Type": "application/json",
      "Idempotency-Key": crypto.randomUUID(),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<T>;
}

function normalize(value: string): string {
  return value.replace(/\/$/, "");
}

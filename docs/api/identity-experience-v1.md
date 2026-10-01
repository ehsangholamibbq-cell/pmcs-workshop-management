# PMCS Identity Experience API V1

- Stage: `V1.1-IAM1 — Identity, Profile and Configurable Login Experience`
- Base paths: `/api/v1/member-profile`, `/api/v1/member-profiles` and `/api/v1/identity/login-experiences`
- State: implementation candidate; qualification evidence pending

## Boundary

IAM1 changes presentation and directory data only. OIDC, Keycloak, BFF session, callback URLs,
credentials, MFA and authorization settings remain outside `LoginExperienceDescriptor` and cannot be
changed through these endpoints. Every tenant user has one shared `MemberProfile`; project
membership, role and scope remain project-specific records.

## Member profile

| Method | Route | Permission | Result |
| --- | --- | --- | --- |
| GET | `/api/v1/member-profile` | `member-profile.read-self` | Authenticated member profile |
| PUT | `/api/v1/member-profile` | `member-profile.update-self` | Revision-controlled self-service fields |
| GET | `/api/v1/member-profiles/{userId}` | `member-profile.read-directory` or shared active project | Permission-scoped directory profile |
| GET | `/api/v1/member-profiles/{userId}/avatar` | Same as profile read | Private integrity-verified image bytes |
| PUT | `/api/v1/member-profiles/{userId}/directory` | `member-profile.manage-directory` | Organization-controlled fields |

Self-service can change display name, job title, work phone and avatar association. Email is
read-only; organization unit requires directory administration. Both `userRevision` and
`profileRevision` are required for updates. A stale revision returns `409`.

Profile images use Shared Documents with `ownerType=MemberProfile`, `ownerId=<userId>`, no project,
`Confidential` classification and `Standard` retention. Only allowlisted image media types up to
5 MiB are accepted. The owner may release only their own clean, policy-constrained image through
`member-profile.avatar.publish-self`; this permission does not grant general quarantine release.
Avatar responses are authenticated, tenant-isolated and use private versioned caching.

## Configurable login experience

| Method | Route | Access | Result |
| --- | --- | --- | --- |
| GET | `/api/v1/public/login-experience?tenantId={tenantId}` | Anonymous | Published allowlisted descriptor or embedded fallback |
| GET | `/api/v1/public/login-experience/assets/{logo|hero}?tenantId={tenantId}&version={n}` | Anonymous | Published, immutable, integrity-verified image |
| GET | `/api/v1/identity/login-experiences` | `login-experience.manage` | At most 50 recent versions |
| POST | `/api/v1/identity/login-experiences` | `login-experience.manage` | Create a Draft |
| POST | `/api/v1/identity/login-experiences/{version}/publish` | `login-experience.manage` | Publish Draft and supersede current version |
| POST | `/api/v1/identity/login-experiences/{version}/rollback` | `login-experience.manage` | Reactivate a superseded version |

The descriptor accepts only these server enums:

- composition: `BlueprintSplit`, `MonolithFocus`, `WarmMinimal`;
- surface: `WarmStone`, `WarmIvory`, `DeepNavy`;
- accent: `CorporateNavyGreen`, `NavySilver`, `GreenStone`;
- motion: `Calm`, `Balanced`, `Expressive`.

Text is length-limited plain text. HTML, CSS, JavaScript, iframe, arbitrary URL and authentication
configuration are not contract fields. Unknown or malformed presentation data fails closed to the
embedded usable login. `prefers-reduced-motion` disables decorative animation without disabling the
authentication action.

Logo and hero assets use Shared Documents with `ownerType=LoginExperience`, an owner id equal to the
client-generated descriptor id, `Internal` classification, `Standard` retention and a 10 MiB image
limit. Assets must be clean and released before a Draft can reference them. Public asset URLs are
version-pinned and immutable; the active descriptor itself is not cached.

## Mutation, privacy and evidence

Every PUT/POST requires `Idempotency-Key`, authenticated server-derived Tenant/Actor context and
central permission evaluation. Mutations record Audit, transactional Outbox and idempotency receipt
in the same database transaction. Public profile events contain only user/revision identifiers;
display name, email and work phone are excluded. Published-login events contain descriptor id,
version and activation kind, never credentials or authentication configuration.

Integration contracts:

- `identity.member-profile.updated.v1`;
- `identity.login-experience.published.v1`.


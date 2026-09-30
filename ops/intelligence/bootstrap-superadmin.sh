#!/usr/bin/env bash
set -euo pipefail

# One-time, explicit provisioning after the INT1 migration. psql reads PGHOST,
# PGDATABASE, PGUSER and PGPASSWORD/PGPASSFILE from the operator environment.
if [[ "${PMCS_INT1_BOOTSTRAP_CONFIRM:-}" != "BOOTSTRAP-INT1-SUPERADMIN" ]]; then
  echo "Explicit PMCS_INT1_BOOTSTRAP_CONFIRM is required." >&2
  exit 2
fi

uuid_pattern='^[[:xdigit:]]{8}-[[:xdigit:]]{4}-[[:xdigit:]]{4}-[[:xdigit:]]{4}-[[:xdigit:]]{12}$'
for variable in PMCS_INT1_TARGET_TENANT_ID PMCS_INT1_TARGET_USER_ID PMCS_INT1_OPERATOR_USER_ID; do
  if [[ ! "${!variable:-}" =~ $uuid_pattern ]]; then
    echo "A valid UUID is required for ${variable}." >&2
    exit 2
  fi
done

command -v psql >/dev/null || { echo "psql is required." >&2; exit 2; }

psql -X -q -v ON_ERROR_STOP=1 \
  -v target_tenant="${PMCS_INT1_TARGET_TENANT_ID}" \
  -v target_user="${PMCS_INT1_TARGET_USER_ID}" \
  -v operator_user="${PMCS_INT1_OPERATOR_USER_ID}" <<'SQL'
begin;
lock table intelligence.administration_grants in share row exclusive mode;
with eligible as (
    select target.tenant_id, target.id as user_id
    from identity_access.users target
    join identity_access.tenants tenant on tenant.id = target.tenant_id
    where target.tenant_id = :'target_tenant'::uuid
      and target.id = :'target_user'::uuid
      and target.status = 'Active' and tenant.status = 'Active'
      and exists (
          select 1 from identity_access.users operator_account
          where operator_account.tenant_id = target.tenant_id
            and operator_account.id = :'operator_user'::uuid
            and operator_account.status = 'Active')
      and not exists (
          select 1 from intelligence.administration_grants grant_row
          where grant_row.scope_tenant_id is null)
), inserted as (
    insert into intelligence.administration_grants (
        id, actor_tenant_id, actor_id, scope_tenant_id, permission,
        issued_by, starts_at, expires_at, revoked_at, revision)
    select gen_random_uuid(), eligible.tenant_id, eligible.user_id, null,
           permission.code, :'operator_user'::uuid, now(), now() + interval '90 days', null, 1
    from eligible
    cross join (values ('intelligence.providers.manage'),
                       ('intelligence.profiles.publish'),
                       ('intelligence.catalog.read')) as permission(code)
    returning id, actor_tenant_id, permission
), audited as (
    insert into foundation.audit_events (
        event_id, tenant_id, project_id, actor_user_id, event_type,
        resource_type, resource_id, occurred_at, data, correlation_id)
    select gen_random_uuid(), actor_tenant_id, null, :'operator_user'::uuid,
           'IntelligenceSuperAdministratorBootstrapped',
           'IntelligenceAdministrationGrant', id::text, now(),
           jsonb_build_object('permission', permission, 'targetUserId', :'target_user'::uuid),
           'int1-explicit-bootstrap'
    from inserted
    returning event_id
)
select ((select count(*) from inserted) = 3 and (select count(*) from audited) = 3)
    as bootstrap_ok \gset
\if :bootstrap_ok
commit;
\echo INT1 super-administrator grants created and audited.
\else
rollback;
\echo Target/operator ineligible or bootstrap already completed.
\quit 3
\endif
SQL

#!/usr/bin/env bash
set -euo pipefail

: "${PMCS_QA_CONNECTION_STRING:?Set isolated QA database.}"
: "${PMCS_QA_DATABASE_URL:?Set isolated QA database URL.}"
: "${PMCS_QA_AUTH_KEY:?Set QA authentication key.}"
: "${PMCS_QA_S3_ENDPOINT:?Set QA object storage.}"
: "${PMCS_QA_S3_ACCESS_KEY:?Set QA object storage key.}"
: "${PMCS_QA_S3_SECRET_KEY:?Set QA object storage secret.}"
: "${PMCS_QA_S3_BUCKET:?Set QA object storage bucket.}"

database_name="$(dotnet run --project src/backend/Pmcs.TestHarness/Pmcs.TestHarness.csproj \
  --configuration Release --no-build --no-launch-profile -- guard)"
connected_database="$(psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
  --tuples-only --no-align --command 'select current_database();')"
if [[ "${database_name}" != "${connected_database}" ]]; then
  echo 'INT1 QA ADO and PostgreSQL connections target different databases.' >&2
  exit 2
fi
psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 >/dev/null <<'SQL'
insert into intelligence.provider_registrations
    (provider, version, enabled, verified_at, created_by, created_at, revision)
values
    ('OpenAI', 2, true, now(), '22222222-2222-2222-2222-222222222222', now(), 2),
    ('GoogleGemini', 2, true, now(), '22222222-2222-2222-2222-222222222222', now(), 2),
    ('AnthropicClaude', 2, true, now(), '22222222-2222-2222-2222-222222222222', now(), 2);
insert into intelligence.model_catalog
    (id, version, provider, model, capabilities, maximum_data_class, enabled,
     verified_at, created_at, created_by, revision,
     input_microunits_per_token, output_microunits_per_token)
values
    ('a1000000-0000-4000-8000-000000000001', 1, 'OpenAI', 'int1-fixture', 3, 2, true,
     now(), now(), '22222222-2222-2222-2222-222222222222', 2, 2, 4),
    ('a1000000-0000-4000-8000-000000000002', 1, 'GoogleGemini', 'int1-fixture', 3, 2, true,
     now(), now(), '22222222-2222-2222-2222-222222222222', 2, 2, 4),
    ('a1000000-0000-4000-8000-000000000003', 1, 'AnthropicClaude', 'int1-fixture', 3, 2, true,
     now(), now(), '22222222-2222-2222-2222-222222222222', 2, 2, 4);
insert into intelligence.profile_versions
    (id, version, use_case, tenant_id, project_ids_json, default_model_id,
     allowed_model_ids_json, fallback_model_ids_json, allow_fallback,
     required_capabilities, maximum_data_class, maximum_input_tokens,
     maximum_output_tokens, timeout_seconds, maximum_cost_microunits,
     prompt_version, policy_version, published_by, published_at)
values
    ('a2000000-0000-4000-8000-000000000001', 1, 'int1.reference',
     '11111111-1111-1111-1111-111111111111',
     '["33333333-3333-3333-3333-333333333333"]',
     'a1000000-0000-4000-8000-000000000001',
     '["a1000000-0000-4000-8000-000000000001","a1000000-0000-4000-8000-000000000002","a1000000-0000-4000-8000-000000000003"]',
     '[]', false, 3, 2, 1000, 128, 30, 10000, 'int1-fixture-prompt-v1',
     'int1-fixture-policy-v1', '22222222-2222-2222-2222-222222222222', now()),
    ('a2000000-0000-4000-8000-000000000002', 2, 'int1.reference',
     '11111111-1111-1111-1111-111111111111',
     '["33333333-3333-3333-3333-333333333333"]',
     'a1000000-0000-4000-8000-000000000001',
     '["a1000000-0000-4000-8000-000000000001","a1000000-0000-4000-8000-000000000002"]',
     '["a1000000-0000-4000-8000-000000000002"]', true, 3, 2, 1000, 128, 30, 10000,
     'int1-fixture-prompt-v2', 'int1-fixture-policy-v2',
     '22222222-2222-2222-2222-222222222222', now());
insert into intelligence.profile_selections
    (tenant_id, use_case, profile_version_id, model_id, updated_by, updated_at, revision)
values ('11111111-1111-1111-1111-111111111111', 'int1.reference',
    'a2000000-0000-4000-8000-000000000001',
    'a1000000-0000-4000-8000-000000000001',
    '22222222-2222-2222-2222-222222222222', now(), 1);
insert into intelligence.reference_runs
    (id, session_id, session_expires_at, tenant_id, project_id, requested_by,
     profile_version_id, profile_version, model_catalog_id, model_version,
     initial_model_catalog_id, initial_model_version, initial_provider,
     initial_provider_version, provider, provider_version, model, prompt_version,
     policy_version, request_hash, status, fallback, input_tokens, output_tokens,
     cost_microunits, requested_at, validated_at, started_at, revision)
values
    ('a3000000-0000-4000-8000-000000000007',
     'a4000000-0000-4000-8000-000000000007', now() - interval '10 minutes',
     '11111111-1111-1111-1111-111111111111',
     '33333333-3333-3333-3333-333333333333',
     '22222222-2222-2222-2222-222222222222',
     'a2000000-0000-4000-8000-000000000001', 1,
     'a1000000-0000-4000-8000-000000000001', 1,
     'a1000000-0000-4000-8000-000000000001', 1, 'OpenAI', 2,
     'OpenAI', 2, 'int1-fixture', 'int1-fixture-prompt-v1',
     'int1-fixture-policy-v1', repeat('a', 64), 'Running', false, 0, 0,
     0, now() - interval '20 minutes', now() - interval '20 minutes',
     now() - interval '20 minutes', 3);
SQL

port=5092
log_file="$(mktemp)"
response_file="$(mktemp)"
cleanup() {
  status=$?
  if (( status != 0 )); then
    sed -n '1,180p' "${log_file}" >&2
  fi
  if [[ -n "${api_pid:-}" ]]; then
    kill "${api_pid}" 2>/dev/null || true
    wait "${api_pid}" 2>/dev/null || true
  fi
  rm -f -- "${log_file}" "${response_file}"
  exit "${status}"
}
trap cleanup EXIT

ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS="http://127.0.0.1:${port}" \
ConnectionStrings__Pmcs="${PMCS_QA_CONNECTION_STRING}" \
PMCS_DEV_IDENTITY_ENABLED=false PMCS_SEED_ENABLED=true \
PMCS_QA_GATEWAY_ENABLED=true PMCS_QA_AUTH_KEY="${PMCS_QA_AUTH_KEY}" \
Intelligence__INT1ReferenceEnabled=true Intelligence__INT1FixtureEnabled=true \
OpenAI__Model=int1-fixture OpenAI__ApiKey=qa-fixture-only \
Gemini__Model=int1-fixture Gemini__ApiKey=qa-fixture-only \
Anthropic__Model=int1-fixture Anthropic__ApiKey=qa-fixture-only \
ProjectStateRefresh__Enabled=false AdvisoryIntelligence__WorkerEnabled=false \
ReportingCenter__Phase1Enabled=true ReportingCenter__WorkerEnabled=false \
RateLimiting__InsightPermitLimit=30 RateLimiting__GeneralPermitLimit=300 \
ObjectStorage__ServiceUrl="${PMCS_QA_S3_ENDPOINT}" \
ObjectStorage__AccessKey="${PMCS_QA_S3_ACCESS_KEY}" \
ObjectStorage__SecretKey="${PMCS_QA_S3_SECRET_KEY}" \
ObjectStorage__BucketName="${PMCS_QA_S3_BUCKET}" \
ObjectStorage__Region=us-east-1 ObjectStorage__ForcePathStyle=true \
ObjectStorage__CreateBucketIfMissing=true \
dotnet run --project src/backend/Pmcs.Api/Pmcs.Api.csproj \
  --configuration Release --no-build --no-launch-profile >"${log_file}" 2>&1 &
api_pid=$!
ready=false
for _ in {1..60}; do
  if ! kill -0 "${api_pid}" 2>/dev/null; then
    echo 'INT1 QA API exited before readiness.' >&2
    exit 1
  fi
  if curl --silent --fail "http://127.0.0.1:${port}/health/ready" >/dev/null; then
    ready=true
    break
  fi
  sleep 1
done
if [[ "${ready}" != true ]]; then
  echo 'INT1 QA API did not become ready.' >&2
  exit 1
fi

recovered=false
for _ in {1..30}; do
  recovery_state="$(psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
    --tuples-only --no-align --command "select status || '|' || error_code from intelligence.reference_runs where id = 'a3000000-0000-4000-8000-000000000007';")"
  if [[ "${recovery_state}" == 'Failed|ai.run.abandoned' ]]; then
    recovered=true
    break
  fi
  sleep 1
done
if [[ "${recovered}" != true ]]; then
  echo 'INT1 abandoned Run was not recovered.' >&2
  exit 1
fi

run_url="http://127.0.0.1:${port}/api/v1/projects/33333333-3333-3333-3333-333333333333/intelligence/reference-runs"
admin_status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
  --header "X-Pmcs-QA-Key: ${PMCS_QA_AUTH_KEY}" \
  --header 'X-Tenant-Id: 11111111-1111-1111-1111-111111111111' \
  --header 'X-User-Id: 22222222-2222-2222-2222-222222222222' \
  "http://127.0.0.1:${port}/api/v1/intelligence/admin/providers")"
if [[ "${admin_status}" != '403' ]]; then
  echo "QA tenant administrator gained implicit INT1 provider access: HTTP ${admin_status}." >&2
  exit 1
fi
for scope in tenant project; do
  if [[ "${scope}" == tenant ]]; then
    tenant_id='99999999-9999-4999-8999-999999999999'
    scoped_url="${run_url}"
  else
    tenant_id='11111111-1111-1111-1111-111111111111'
    scoped_url="http://127.0.0.1:${port}/api/v1/projects/44444444-4444-4444-8444-444444444444/intelligence/reference-runs"
  fi
  scoped_status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
    --request POST --header "X-Pmcs-QA-Key: ${PMCS_QA_AUTH_KEY}" \
    --header "X-Tenant-Id: ${tenant_id}" \
    --header 'X-User-Id: 22222222-2222-2222-2222-222222222222' \
    --header "Idempotency-Key: int1-scope-${scope}" \
    --header 'Content-Type: application/json' \
    --data '{"requestId":"a3000000-0000-4000-8000-000000000009","question":"scope test"}' \
    "${scoped_url}")"
  if [[ "${scoped_status}" != '401' && "${scoped_status}" != '403' &&
        "${scoped_status}" != '404' ]]; then
    echo "INT1 cross-${scope} request was not denied safely: HTTP ${scoped_status}." >&2
    exit 1
  fi
done
post_run() {
  local id="$1" question="$2" expected="$3" actual
  actual="$(curl --silent --output "${response_file}" --write-out '%{http_code}' \
    --request POST --header "X-Pmcs-QA-Key: ${PMCS_QA_AUTH_KEY}" \
    --header 'X-Tenant-Id: 11111111-1111-1111-1111-111111111111' \
    --header 'X-User-Id: 22222222-2222-2222-2222-222222222222' \
    --header "Idempotency-Key: int1-reference-${id}" \
    --header 'Content-Type: application/json' \
    --data "{\"requestId\":\"a3000000-0000-4000-8000-00000000000${id}\",\"question\":\"${question}\"}" \
    "${run_url}")"
  if [[ "${actual}" != "${expected}" ]]; then
    echo "INT1 reference fixture ${id} returned HTTP ${actual}; expected ${expected}." >&2
    cat "${response_file}" >&2
    exit 1
  fi
}
select_model() {
  psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 >/dev/null \
    --command "update intelligence.profile_selections set model_id = 'a1000000-0000-4000-8000-00000000000${1}', revision = revision + 1, updated_at = now() where use_case = 'int1.reference';"
}

post_run 1 'fixture-openai' 200
grep -q 'Fixture catalog result' "${response_file}"
observer_status="$(curl --silent --output /dev/null --write-out '%{http_code}' \
  --header "X-Pmcs-QA-Key: ${PMCS_QA_AUTH_KEY}" \
  --header 'X-Tenant-Id: 11111111-1111-1111-1111-111111111111' \
  --header 'X-User-Id: 50000000-0000-4000-8000-000000000001' \
  "${run_url}/a3000000-0000-4000-8000-000000000001")"
if [[ "${observer_status}" != '403' ]]; then
  echo "QA observer gained INT1 Run access: HTTP ${observer_status}." >&2
  exit 1
fi
post_run 1 'fixture-openai' 200
if grep -q 'Fixture catalog result' "${response_file}"; then
  echo 'INT1 idempotent replay exposed transient answer.' >&2
  exit 1
fi
select_model 2
post_run 2 'fixture-gemini' 200
select_model 3
post_run 3 'fixture-claude' 200
select_model 1
post_run 4 'int1-fixture-unknown-tool' 502
grep -q 'ai.tool.unknown' "${response_file}"

psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 >/dev/null \
  --command "update intelligence.profile_selections set profile_version_id = 'a2000000-0000-4000-8000-000000000002', revision = revision + 1, updated_at = now() where use_case = 'int1.reference';"
post_run 5 'int1-fixture-unavailable' 200
grep -q 'ai.provider.unavailable' "${response_file}"

psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 >/dev/null \
  --command "insert into intelligence.profile_versions
    (id, version, use_case, tenant_id, project_ids_json, default_model_id,
     allowed_model_ids_json, fallback_model_ids_json, allow_fallback,
     required_capabilities, maximum_data_class, maximum_input_tokens,
     maximum_output_tokens, timeout_seconds, maximum_cost_microunits,
     prompt_version, policy_version, published_by, published_at)
    select 'a2000000-0000-4000-8000-000000000003', 3, use_case, tenant_id,
      project_ids_json, default_model_id, allowed_model_ids_json,
      fallback_model_ids_json, allow_fallback, required_capabilities,
      maximum_data_class, maximum_input_tokens, maximum_output_tokens, 5,
      maximum_cost_microunits, prompt_version, policy_version, published_by, now()
    from intelligence.profile_versions where id = 'a2000000-0000-4000-8000-000000000002';
    update intelligence.profile_selections
    set profile_version_id = 'a2000000-0000-4000-8000-000000000003',
      revision = revision + 1, updated_at = now()
    where use_case = 'int1.reference';"
post_run 8 'int1-fixture-timeout' 200
grep -q 'ai.provider.timeout' "${response_file}"
post_run 9 'int1-fixture-cross-project' 502
grep -q 'ai.tool.arguments_invalid' "${response_file}"

psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 >/dev/null \
  --command "update intelligence.provider_registrations set enabled = false, version = version + 1, revision = revision + 1 where provider = 'OpenAI';"
post_run 6 'fixture-disabled' 409
grep -q 'ai.provider.disabled' "${response_file}"

state="$(psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
  --tuples-only --no-align --command "select count(*) filter (where status = 'Completed')::text || '|' || count(*) filter (where status = 'Failed' and error_code = 'ai.tool.unknown')::text || '|' || count(*) filter (where status = 'Failed' and error_code = 'ai.tool.arguments_invalid')::text || '|' || count(*) filter (where fallback and initial_provider = 'OpenAI' and provider = 'GoogleGemini' and fallback_reason = 'ai.provider.unavailable' and initial_model_catalog_id = 'a1000000-0000-4000-8000-000000000001' and provider_version = 2)::text || '|' || count(*) filter (where fallback and fallback_reason = 'ai.provider.timeout' and provider = 'GoogleGemini')::text || '|' || count(distinct provider)::text || '|' || count(*) filter (where provider_version < 1 or initial_provider_version < 1)::text from intelligence.reference_runs where id::text like 'a3000000-0000-4000-8000-%';")"
if [[ "${state}" != '5|1|1|1|1|3|0' ]]; then
  echo "INT1 reference lineage diverged: ${state}." >&2
  exit 1
fi
side_effects="$(psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
  --tuples-only --no-align --command "select (select count(*) from foundation.audit_events where event_type = 'IntelligenceReferenceRunFinished' and resource_id like 'a3000000-0000-4000-8000-%')::text || '|' || (select count(*) from foundation.outbox_messages where event_type = 'intelligence.reference-run.finished' and payload->'run'->>'Id' like 'a3000000-0000-4000-8000-%')::text || '|' || (select count(*) from foundation.idempotency_records where operation like 'intelligence.reference.run:%' and key like 'int1-reference-%')::text || '|' || (select count(*) from intelligence.reference_runs where id::text like 'a3000000-0000-4000-8000-%' and status = 'Completed' and cost_microunits = 112 and session_id <> id and session_expires_at = requested_at + interval '10 minutes')::text;")"
if [[ "${side_effects}" != '7|7|7|5' ]]; then
  echo "INT1 metadata, audit, outbox or receipt evidence diverged: ${side_effects}." >&2
  exit 1
fi
recovery_audit="$(psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
  --tuples-only --no-align --command "select count(*) from foundation.audit_events where event_type = 'IntelligenceReferenceRunAbandoned' and resource_id = 'a3000000-0000-4000-8000-000000000007';")"
if [[ "${recovery_audit}" != '1' ]]; then
  echo "INT1 abandoned Run audit diverged: ${recovery_audit}." >&2
  exit 1
fi
leaks="$(psql "${PMCS_QA_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
  --tuples-only --no-align --command "select (select count(*) from intelligence.reference_runs where row_to_json(reference_runs)::text ~ 'fixture-openai|Fixture catalog result|qa-fixture-only') + (select count(*) from foundation.audit_events where event_type = 'IntelligenceReferenceRunFinished' and data::text ~ 'fixture-openai|Fixture catalog result|qa-fixture-only') + (select count(*) from foundation.outbox_messages where event_type = 'intelligence.reference-run.finished' and payload::text ~ 'fixture-openai|Fixture catalog result|qa-fixture-only') + (select count(*) from foundation.idempotency_records where operation like 'intelligence.reference.run:%' and response_body::text ~ 'fixture-openai|Fixture catalog result|qa-fixture-only');")"
if [[ "${leaks}" != '0' ]]; then
  echo 'INT1 reference persisted request, answer or fixture credential.' >&2
  exit 1
fi
echo 'INT1 reference QA DB, three adapters, fallback including timeout, disable, replay and metadata-only checks passed.'

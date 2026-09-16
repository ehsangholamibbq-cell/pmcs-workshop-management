#!/usr/bin/env bash
set -euo pipefail

: "${PMCS_VERIFICATION_DATABASE_URL:?Set PMCS_VERIFICATION_DATABASE_URL to the isolated Checkpoint 24 test database.}"

command -v psql >/dev/null

tenant_id="11111111-1111-1111-1111-111111111111"
project_id="33333333-3333-3333-3333-333333333333"
location_id="33333333-3333-4333-8333-333333333334"
obligation_id="99999999-9999-4999-8999-999999999991"
petty_cash_id="99999999-9999-4999-8999-999999999992"
fee_policy_id="99999999-9999-4999-8999-999999999993"

scalar() {
  psql "${PMCS_VERIFICATION_DATABASE_URL}" --no-psqlrc --set ON_ERROR_STOP=1 \
    --tuples-only --no-align --command "$1"
}

expect_equal() {
  local label="$1"
  local expected="$2"
  local query="$3"
  local actual
  actual="$(scalar "${query}")"
  if [[ "${actual}" != "${expected}" ]]; then
    echo "${label}: expected '${expected}', received '${actual}'." >&2
    exit 1
  fi
}

expect_at_least_one() {
  local label="$1"
  local query="$2"
  local count
  count="$(scalar "${query}")"
  if ! [[ "${count}" =~ ^[0-9]+$ ]] || (( count < 1 )); then
    echo "${label}: expected at least one row, received '${count}'." >&2
    exit 1
  fi
}

expect_equal \
  "Checkpoint 24 migration" \
  "1" \
  "select count(*) from foundation.schema_migrations where module = 'finance' and version = '20260916-003';"

expect_equal \
  "financial record stable location lineage" \
  "4" \
  "select count(*) from finance.financial_records where tenant_id = '${tenant_id}' and project_id = '${project_id}' and location_id = '${location_id}' and location_code = 'ROOT' and wbs_reference = 'CI-WBS' and status = 'Posted';"

expect_equal \
  "settled payable and immutable settlement link" \
  "Settled|1000.00|1000.00|0.00|1" \
  "select o.status || '|' || o.amount::text || '|' || o.settled_amount::text || '|' || (o.amount - o.settled_amount)::text || '|' || count(s.id)::text from finance.financial_obligations o join finance.financial_settlements s on s.obligation_id = o.id where o.id = '${obligation_id}' and o.location_id = '${location_id}' and o.location_code = 'ROOT' group by o.status, o.amount, o.settled_amount;"

expect_equal \
  "balanced petty cash reconciliation lineage" \
  "Reconciled|500.00|400.00|100.00|3" \
  "select status || '|' || approved_amount::text || '|' || reconciled_expense_amount::text || '|' || returned_amount::text || '|' || ((advance_record_id is not null)::int + (expense_record_id is not null)::int + (return_record_id is not null)::int)::text from finance.petty_cash_requests where id = '${petty_cash_id}' and location_id = '${location_id}' and location_code = 'ROOT';"

expect_equal \
  "approved optional management fee policy" \
  "Approved|5.0000|RecognizedSpend|2026-09-01" \
  "select status || '|' || rate_percent::text || '|' || calculation_base || '|' || effective_from::text from finance.management_fee_policies where id = '${fee_policy_id}';"

expect_equal \
  "finance resources all have audit evidence" \
  "0" \
  "select count(*) from (select id::text resource_id, 'FinancialRecord' resource_type from finance.financial_records where tenant_id = '${tenant_id}' and project_id = '${project_id}' union all select id::text, 'FinancialObligation' from finance.financial_obligations where tenant_id = '${tenant_id}' and project_id = '${project_id}' union all select id::text, 'PettyCashRequest' from finance.petty_cash_requests where tenant_id = '${tenant_id}' and project_id = '${project_id}' union all select id::text, 'ManagementFeePolicy' from finance.management_fee_policies where tenant_id = '${tenant_id}' and project_id = '${project_id}') resources where not exists (select 1 from foundation.audit_events a where a.tenant_id = '${tenant_id}' and a.project_id = '${project_id}' and a.resource_type = resources.resource_type and a.resource_id = resources.resource_id);"

expect_equal \
  "finance audit correlation coverage" \
  "0" \
  "select count(*) from foundation.audit_events where tenant_id = '${tenant_id}' and project_id = '${project_id}' and resource_type in ('FinancialRecord', 'FinancialObligation', 'PettyCashRequest', 'ManagementFeePolicy') and (correlation_id is null or btrim(correlation_id) = '');"

expect_at_least_one \
  "finance transactional outbox evidence" \
  "select count(*) from foundation.outbox_messages where tenant_id = '${tenant_id}' and project_id = '${project_id}' and event_type like 'Finance.%' and correlation_id is not null and correlation_id <> '';"

expect_at_least_one \
  "finance idempotency evidence" \
  "select count(*) from foundation.idempotency_records where tenant_id = '${tenant_id}' and operation like 'finance.%' and request_hash is not null and request_hash <> '';"

printf 'Checkpoint 24 finance calculation, record, audit, permission and cross-module lineage verification passed.\n'

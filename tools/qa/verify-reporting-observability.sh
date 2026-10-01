#!/usr/bin/env bash
set -euo pipefail

command -v curl >/dev/null
command -v docker >/dev/null
command -v node >/dev/null

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "${repository_root}"

collector_image="otel/opentelemetry-collector-contrib:0.160.0"
prometheus_image="prom/prometheus:v3.14.0"
alertmanager_image="prom/alertmanager:v0.34.1"
container_suffix="${PPID}-$$"
collector_container="pmcs-rpt1-otel-${container_suffix}"
prometheus_container="pmcs-rpt1-prometheus-${container_suffix}"
alertmanager_container="pmcs-rpt1-alertmanager-${container_suffix}"
containers=("${prometheus_container}" "${collector_container}" "${alertmanager_container}")
temporary_directory="$(mktemp -d)"
receiver_log="${temporary_directory}/alert-receiver.log"
receiver_pid=""

cleanup() {
  local exit_code=$?
  set +e
  if (( exit_code != 0 )); then
    for container in "${containers[@]}"; do
      if docker inspect "${container}" >/dev/null 2>&1; then
        echo "Observability container log: ${container}" >&2
        docker logs "${container}" >&2
      fi
    done
    if [[ -f "${receiver_log}" ]]; then
      echo "Observability alert receiver log:" >&2
      sed -n '1,240p' "${receiver_log}" >&2
    fi
  fi
  for container in "${containers[@]}"; do
    docker rm --force --volumes "${container}" >/dev/null 2>&1 || true
  done
  if [[ -n "${receiver_pid}" ]] && kill -0 "${receiver_pid}" 2>/dev/null; then
    kill "${receiver_pid}" 2>/dev/null
    wait "${receiver_pid}" 2>/dev/null || true
  fi
  rm -f -- "${receiver_log}"
  rmdir "${temporary_directory}" 2>/dev/null || true
  exit "${exit_code}"
}
trap cleanup EXIT

wait_for_http() {
  local label="$1"
  local url="$2"
  local container="${3:-}"
  for _ in {1..120}; do
    if curl --silent --fail "${url}" >/dev/null; then
      return
    fi
    if [[ -n "${container}" ]] &&
       [[ "$(docker inspect --format '{{.State.Running}}' "${container}" 2>/dev/null || true)" != "true" ]]; then
      echo "${label} container exited before readiness." >&2
      exit 1
    fi
    sleep 0.5
  done
  echo "${label} did not become ready." >&2
  exit 1
}

PMCS_QA_ALERT_RECEIVER_PORT=19093 \
  node tools/qa/reporting-alert-receiver.mjs >"${receiver_log}" 2>&1 &
receiver_pid=$!
wait_for_http "QA alert receiver" "http://127.0.0.1:19093/health"

docker run --detach \
  --name "${alertmanager_container}" \
  --network host \
  --volume "${repository_root}/deploy/observability/alertmanager.yaml:/etc/alertmanager/alertmanager.yml:ro" \
  "${alertmanager_image}" \
  --config.file=/etc/alertmanager/alertmanager.yml \
  --storage.path=/alertmanager \
  --web.listen-address=127.0.0.1:9093 >/dev/null
wait_for_http "Alertmanager" "http://127.0.0.1:9093/-/ready" "${alertmanager_container}"

docker run --detach \
  --name "${collector_container}" \
  --network host \
  --volume "${repository_root}/deploy/observability/otel-collector.yaml:/etc/otelcol-contrib/config.yaml:ro" \
  "${collector_image}" \
  --config=/etc/otelcol-contrib/config.yaml >/dev/null
wait_for_http "OpenTelemetry Collector" "http://127.0.0.1:13133/" "${collector_container}"

docker run --detach \
  --name "${prometheus_container}" \
  --network host \
  --volume "${repository_root}/deploy/observability/prometheus.yaml:/etc/prometheus/prometheus.yml:ro" \
  --volume "${repository_root}/deploy/observability/reporting-alerts.yaml:/etc/prometheus/reporting-alerts.yaml:ro" \
  "${prometheus_image}" \
  --config.file=/etc/prometheus/prometheus.yml \
  --storage.tsdb.path=/prometheus \
  --web.listen-address=127.0.0.1:9090 >/dev/null
wait_for_http "Prometheus" "http://127.0.0.1:9090/-/ready" "${prometheus_container}"

PMCS_QA_OTLP_ENDPOINT="http://127.0.0.1:4317" \
PMCS_QA_OTEL_METRIC_EXPORT_INTERVAL=1000 \
PMCS_QA_PROMETHEUS_URL="http://127.0.0.1:9090" \
PMCS_QA_ALERT_RECEIVER_URL="http://127.0.0.1:19093" \
./tools/qa/verify-reporting-fairness.sh

printf '{"status":"passed","stage":"reporting-observability-connected-regression","collector":"0.160.0","prometheus":"3.14.0","alertmanager":"0.34.1"}\n'

import assert from "node:assert/strict";

const required = (key) => {
  const value = process.env[key]?.trim();
  if (!value) throw new Error(`${key} is required.`);
  return value.replace(/\/+$/u, "");
};

const prometheusUrl = required("PMCS_QA_PROMETHEUS_URL");
const alertReceiverUrl = required("PMCS_QA_ALERT_RECEIVER_URL");
const forbiddenValues = (process.env.PMCS_REPORTING_OBSERVABILITY_FORBIDDEN ?? "")
  .split("|")
  .filter(Boolean);
const queueMetric = "pmcs_reporting_worker_queue_oldest_age_seconds";
const queueAlert = "PmcsReportingQueueAgeBudgetExceeded";
const forbiddenLabelKeys = new Set(["tenantid", "projectid", "userid", "runid", "requestedby"]);

const delay = (milliseconds) => new Promise((resolve) => setTimeout(resolve, milliseconds));

async function getJson(url) {
  const response = await fetch(url, { signal: AbortSignal.timeout(5_000) });
  if (!response.ok) throw new Error(`${url} returned HTTP ${response.status}.`);
  return response.json();
}

async function waitFor(label, probe, timeoutMilliseconds = 90_000) {
  const deadline = Date.now() + timeoutMilliseconds;
  let lastError;
  while (Date.now() < deadline) {
    try {
      const value = await probe();
      if (value) return value;
    } catch (error) {
      lastError = error;
    }
    await delay(500);
  }
  const suffix = lastError instanceof Error ? ` Last error: ${lastError.message}` : "";
  throw new Error(`${label} was not observed before the timeout.${suffix}`);
}

function assertNoForbiddenValues(payload, label) {
  const serialized = JSON.stringify(payload);
  assert.equal(
    forbiddenValues.some((value) => serialized.includes(value)),
    false,
    `${label} exposed a tenant, project, user or run identifier.`,
  );
}

await waitFor("healthy PMCS reporting scrape target", async () => {
  const payload = await getJson(`${prometheusUrl}/api/v1/targets`);
  return payload.data?.activeTargets?.some((target) =>
    target.labels?.job === "pmcs-reporting" && target.health === "up");
});

const metricPayload = await waitFor("aged reporting queue metric", async () => {
  const url = new URL(`${prometheusUrl}/api/v1/query`);
  url.searchParams.set("query", queueMetric);
  const payload = await getJson(url);
  return payload.data?.result?.some((sample) => Number(sample.value?.[1]) > 120) ? payload : null;
});
assertNoForbiddenValues(metricPayload, "Prometheus metric payload");
for (const sample of metricPayload.data.result) {
  for (const key of Object.keys(sample.metric ?? {})) {
    const normalized = key.replaceAll(/[^a-zA-Z0-9]/gu, "").toLowerCase();
    assert.equal(forbiddenLabelKeys.has(normalized), false, `Forbidden metric label '${key}'.`);
  }
}

const alertPayload = await waitFor("firing reporting queue-age alert", async () => {
  const payload = await getJson(`${prometheusUrl}/api/v1/alerts`);
  return payload.data?.alerts?.some((alert) =>
    alert.labels?.alertname === queueAlert && alert.state === "firing") ? payload : null;
});
assertNoForbiddenValues(alertPayload, "Prometheus alert payload");

const deliveryPayload = await waitFor("delivered reporting queue-age alert", async () => {
  const payload = await getJson(`${alertReceiverUrl}/events`);
  return payload.events?.some((event) =>
    event.payload?.status === "firing" && event.payload?.alerts?.some((alert) =>
      alert.status === "firing" && alert.labels?.alertname === queueAlert)) ? payload : null;
});
assertNoForbiddenValues(deliveryPayload, "Alertmanager webhook payload");

process.stdout.write(`${JSON.stringify({
  status: "passed",
  stage: "reporting-observability-delivery-regression",
  assertions: 5,
  metric: queueMetric,
  alert: queueAlert,
  delivery: "webhook",
})}\n`);

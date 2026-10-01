# PMCS operational metrics deployment contract

This directory keeps the deployment-owned path from PMCS OTLP metrics to an alert receiver:

1. `otel-collector.yaml` receives OTLP/gRPC and exposes a Prometheus scrape target.
2. `prometheus.yaml` scrapes that target and evaluates `reporting-alerts.yaml`.
3. `alertmanager.yaml` delivers firing and resolved alerts to the deployment webhook.

The application exporter remains disabled unless `Observability__OtlpEndpoint` is an explicit
absolute HTTP(S) collector URI. The QA route uses loopback-only ports and version-pinned images;
production must provide its own authenticated/TLS network boundary and receiver URL.


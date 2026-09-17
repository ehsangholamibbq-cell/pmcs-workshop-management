# QA Foundation Slice 7 — Regression Runner and Test Report Generator

## State

- Product state: `PMCS V1 — Feature Complete`
- Slice state: Closed by connected Full Regression Run 69
- Qualification state: `Qualified`; source baseline locked

## Scope

This Slice adds one fail-closed regression contract across all seven connected CI suites: Architecture, Backend, Integration, Pilot Contract, Web, UI/E2E and Identity Container. Every suite is executed by the same versioned runner manifest and emits a commit-bound JSON report. The runner records only command identity, status, exit code, signal and duration; it never serializes environment variables, credentials, tokens or command output.

The Test Report Generator accepts exactly one report for every expected suite, requires the same full commit SHA in every report, cross-checks the connected GitHub job result and rejects missing, duplicate, unexpected, failed or mixed-commit evidence. A report is marked `qualified` and `baselineLockEligible` only when all seven suites and every planned command pass.

## Test hooks

- `tools/qa/regression-suites.json` is the versioned suite and command inventory.
- `tools/qa/regression-runner.mjs` executes one named suite and writes its atomic evidence file.
- `tools/qa/test-report-generator.mjs` aggregates suite artifacts into JSON and Markdown qualification reports.
- `tools/qa/run-integration-regression.sh` consolidates the connected PostgreSQL/API/MinIO/QA/backup-restore route.
- `tools/qa/verify-identity-container.mjs` replaces the inline OIDC administration probe with an independently runnable check.
- `tools/tests/regression-qualification.test.mjs` verifies pass, missing-suite, mixed-commit, failed-job and duplicate-manifest behavior.

## Qualification boundary

Run 69 proved all seven suite artifacts against source commit `26bf222d44634562ca7f3fc0931f3f8b79ca04a1`. The report accepted `7/7` suites and `12/12` runner commands with no failures, returned `qualified` and marked the source baseline lock-eligible. The exact report and artifact digest are now preserved in the release evidence files.

The managerial Agent remains outside V1. Qualification and baseline lock are now complete, so its separately approved roadmap may begin only as a new post-V1 phase.

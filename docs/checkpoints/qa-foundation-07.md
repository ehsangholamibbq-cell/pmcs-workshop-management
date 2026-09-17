# QA Foundation Slice 7 — Regression Runner and Test Report Generator

## State

- Product state: `PMCS V1 — Feature Complete`
- Slice state: Candidate implemented; connected Full Regression pending
- Qualification state: In progress; not yet Qualified, Final or Locked

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

This Candidate does not claim Qualification. Slice 7 closes only after the connected workflow proves the runner and generator on the candidate commit. The final V1 status and baseline lock require a second evidence-only commit followed by one more full connected regression.

The managerial Agent remains outside V1 and must not start before Qualification and baseline lock.

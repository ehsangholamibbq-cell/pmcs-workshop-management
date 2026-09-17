# PMCS V1 — Final Qualification and Locked Baseline

## Final state

- Product: `PMCS V1`
- Qualification: `Qualified`
- Release state: `Final`
- Baseline: `Locked`
- Locked source baseline: `26bf222d44634562ca7f3fc0931f3f8b79ca04a1`
- Qualification workflow: Run 69 (`35255343431`)

## Evidence

Run 69 executed all seven versioned regression suites and the independent fail-closed report job. Every suite report carried the same full source commit, every connected job returned `success`, all 12 planned runner commands executed, no suite was missing or duplicated and the generated report returned `status=qualified`, `baselineLockEligible=true` and `failureCount=0`.

The connected regression re-proved:

- .NET 10 Release build and all 253 backend tests;
- 124 Web tests, ESLint, Persian UI audit, Persian calendar audit, TypeScript and Next.js production build;
- 277 modular C# files, 239 endpoints and 188 mutations;
- PostgreSQL/API smoke, Checkpoints 22/23/24 and all 38 migrations;
- QA Seed/Diagnostics, 32 Permission/Workflow assertions, 21 File assertions, 28 Offline/Sync assertions and 173 Agent/Exploratory assertions;
- real MinIO upload/download, Identity container administration and four real-browser Playwright scenarios;
- PostgreSQL backup and isolated restore drill.

The generated report is preserved at `release/pmcs-v1-qualification.json`. The immutable baseline declaration and artifact digest are preserved at `release/pmcs-v1-baseline.json`.

## Lock boundary

The locked baseline is the exact source commit above. The evidence commit changes documentation, repository validation and release evidence only; it does not alter PMCS runtime code. Any future V1 maintenance change must start from the locked baseline, receive an explicit versioned decision and rerun the full qualification contract.

The PMCS managerial Agent is not part of V1. Its now-unblocked roadmap remains: Intelligence Foundation, Read-only Project Intelligence, Knowledge/RAG/Evidence/Citations, Draft Actions and Controlled Actions with Human Approval.

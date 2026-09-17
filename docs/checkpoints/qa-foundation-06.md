# QA Foundation Slice 6 — Agent and Exploratory Verification

- Date: 2026-09-17
- Status: Implemented and verified by connected CI
- Verified remote source commit: `149cd834da5045a4ab0d6408f5131a33071e1668`
- GitHub Actions evidence: [`35250778588`](https://github.com/ehsangholamibbq-cell/pmcs-workshop-management/actions/runs/35250778588)
- Product state: `PMCS V1 — Feature Complete`
- Qualification state: In progress; not Qualified, Final or Locked
- Governing roadmap: [`../roadmaps/pmcs-v1-development-and-qualification.md`](../roadmaps/pmcs-v1-development-and-qualification.md)

## Continuity review

The repository and connected evidence were rechecked from the Checkpoint 24 baseline through QA Foundation Slice 5. The final Slice 5 source and evidence commits are complete and both connected Runs 64 and 65 rebuilt the repository from a clean checkout with all seven jobs successful. Intermediate stopped or failed runs are not part of the baseline.

The review reran repository validation, the 239-endpoint/188-mutation contract audit, pilot-contract tests, shell syntax checks, Git object/index checks and scans for merge markers, zero-byte tracked sources and unfinished implementation placeholders. No incomplete source change was found. Two documentation continuity defects were corrected: the main QA status page and README still described Slice 5 connected verification as pending after its evidence had already closed the Slice. Repository validation now rejects either stale status.

## Independent qualification scope

- exact QA-key enforcement, duplicate-key rejection and rejection of mixed QA/Bearer credentials;
- active real-actor validation for unknown User and cross-Tenant identities;
- explicit proof that Role-like headers cannot elevate an Observer;
- explicit proof that Permission Preview role simulation is read-only and cannot persist access;
- fail-closed handling for an unknown operation and an unrelated Project;
- rejection of incomplete query binding and non-GET use of the read-only QA Gateway;
- proof that no HTTP Reset or Seed route exists;
- QA status, release/seed identity, `no-store`, `nosniff` and Correlation ID contract checks without secret reflection;
- invalid Correlation ID replacement;
- a deterministic matrix that compares central Permission Preview with the real HTTP result for all 12 seeded Personas across 12 representative read surfaces;
- a machine-readable per-assertion report emitted by `Pmcs.TestHarness verify-exploratory`.

This is a QA exploration runner, not the PMCS managerial Agent. It uses the existing authenticated HTTP/Application-Service boundary, never SQL, never injects a Role and cannot increase the current Actor's permissions. The managerial Agent remains prohibited until V1 is Qualified and its baseline is locked.

## Deliberate boundary

This Slice closes only Agent/Exploratory verification. Regression Runner consolidation, Test Report Generator, Full Regression and the final Qualification report remain open. PMCS V1 therefore remains Feature Complete and must not yet be described as Qualified, Final or Locked.

## Connected verification

CI Run 67 rebuilt the source from a clean checkout and all seven jobs succeeded:

- Agent/Exploratory verification passed `173/173` assertions: 17 authentication/gateway/scope checks plus 156 Permission Preview and real-API checks across 12 Personas and 12 read surfaces;
- Permission/Workflow remained green at `32/32`, File/Attachment at `21/21` and Offline/Sync at `28/28`;
- .NET 10 Release build and all `253/253` backend tests passed;
- Web validation passed with `124/124` tests, lint, Persian UI/calendar audits, TypeScript and production build;
- all four Playwright scenarios passed through the real Keycloak/BFF/Compose path;
- repository validation passed across 277 C# module files and the contract audit confirmed 239 endpoints, 188 mutations and 5 documented protocol-managed mutations;
- Identity Container, Pilot Contract, Checkpoints 22–24 direct database gates and the PostgreSQL backup/restore drill with 38 migrations all passed.

The first connected exploratory candidate, [CI Run 66](https://github.com/ehsangholamibbq-cell/pmcs-workshop-management/actions/runs/35250419554), passed 172 of 173 assertions and correctly exposed an invalid test assumption: HTTP normalizes optional whitespace around a header value before the authentication handler receives it. The probe was replaced with a one-byte key mutation check; no application authentication or authorization rule was weakened. Run 67 then passed the complete suite.

QA Foundation Slice 6 is closed. Regression Runner, Test Report Generator, Full Regression and the final Qualification report remain active. PMCS V1 remains Feature Complete and is not yet Qualified, Final or Locked.

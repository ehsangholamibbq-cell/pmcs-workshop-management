# QA Foundation Slice 6 — Agent and Exploratory Verification

- Date: 2026-09-17
- Status: Implemented; connected verification pending
- Product state: `PMCS V1 — Feature Complete`
- Qualification state: In progress; not Qualified, Final or Locked
- Governing roadmap: [`../roadmaps/pmcs-v1-development-and-qualification.md`](../roadmaps/pmcs-v1-development-and-qualification.md)

## Continuity review

The repository and connected evidence were rechecked from the Checkpoint 24 baseline through QA Foundation Slice 5. The final Slice 5 source and evidence commits are complete and both connected Runs 64 and 65 rebuilt the repository from a clean checkout with all seven jobs successful. Intermediate stopped or failed runs are not part of the baseline.

The review reran repository validation, the 239-endpoint/188-mutation contract audit, pilot-contract tests, shell syntax checks, Git object/index checks and scans for merge markers, zero-byte tracked sources and unfinished implementation placeholders. No incomplete source change was found. One documentation continuity defect was corrected: the main QA status page still described Slice 5 connected verification as pending after its evidence had already closed the Slice.

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

This Slice closes only Agent/Exploratory verification after connected evidence succeeds. Regression Runner consolidation, Test Report Generator, Full Regression and the final Qualification report remain open. PMCS V1 therefore remains Feature Complete and must not yet be described as Qualified, Final or Locked.

## Connected verification

Pending the connected CI run for this source revision.

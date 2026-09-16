# Foundation checkpoint 24 — Finance Lite, Financial Integration and Location

- Date: 2026-09-16
- Status: Implementation candidate; connected CI evidence pending
- Candidate remote source commit: `ac1c9c3aaa1edcebf0deb10694c95d0dc5c2129e`
- Candidate source tree: `c1d7c614bd5a5adaccdcb42c358edebbdd23fb46`
- Governing roadmap: [`../roadmaps/pmcs-v1-development-and-qualification.md`](../roadmaps/pmcs-v1-development-and-qualification.md)
- Product baseline: `PMCS_Blueprint_V1_21_FA.md` plus the approved additive V1 development and qualification roadmap

## Scope implemented

- explicit payable and receivable obligations with Draft, Submitted, Returned, Approved, Partially Settled and Settled states;
- immutable settlement links to posted Payment or Receipt records with currency, allocation and optimistic-revision controls;
- explicit petty-cash request, approval, advance, reconciliation submission and independent reconciliation approval;
- deterministic reconciliation rule requiring expense plus returned cash to equal the approved advance;
- optional, effective-dated and versioned management-fee policy based only on recognized spend;
- automatic supersession of a prior approved fee policy in the same database transaction;
- deterministic payable/receivable aging, overdue exposure and outstanding petty-cash calculations;
- stable Party, Contract, Commitment, Cost Center, WBS and Project Location lineage on finance facts;
- active-location validation at capture time while historical verification accepts retired locations;
- independent finance verification service that recalculates persisted state, inspects records, validates cross-module links and verifies Audit correlation;
- permission-separated Finance Operator and Finance Manager capabilities;
- Persian/Shamsi date input and display for all new Finance UI paths while API and persistence retain ISO dates and UTC timestamps.

## Tests added or extended

- domain workflow tests for obligation approval, partial/full settlement and over-settlement rejection;
- deterministic payable/receivable aging-bucket tests;
- optional/effective management-fee calculation and policy-workflow tests;
- petty-cash advance, balanced reconciliation and overdue-reconciliation tests;
- financial-record lineage test covering Party, Location and WBS snapshots;
- Web/API contract tests for control-state reads, obligations, petty-cash reconciliation and management-fee commands;
- Persian calendar audit coverage for all new date controls;
- PostgreSQL integration scenario covering posted finance records, a settled payable, a reconciled petty-cash request and an approved fee policy;
- direct database assertions for migration, record lineage, audit correlation, Outbox and Idempotency evidence;
- permission-preview assertions proving Finance Operator capture/review separation and Finance Manager verification access.

## Required Checkpoint 24 testability hooks

| Hook | Implementation evidence |
| --- | --- |
| Independent finance calculation | pure `FinanceControlCalculator` plus independent persisted-snapshot recalculation |
| Financial record inspection | permission-aware control-state and verification read services |
| Audit verification | per-resource Audit existence and correlation checks plus direct PostgreSQL assertions |
| Finance permission testing | central role grants and deterministic effective-permission preview checks |
| Cross-module integration | same-project Party/Contract/Commitment validation and stable Project Location identity/code lineage |
| E2E readiness | stable Finance panel and form selectors with no authorization bypass |

## Verification status

Local verification is complete:

- 124 Web/API/testability tests passed;
- ESLint, Persian UI audit, Persian calendar audit and TypeScript passed;
- Next.js production build passed;
- repository validation passed for 267 C# module files;
- system contract audit passed for 236 endpoints, 188 mutations and 5 documented protocol-managed mutations;
- shell syntax and repository diff checks passed.

The .NET 10 Release build, C# suite, PostgreSQL 17/MinIO integration scenario, Checkpoint 24 direct database verification, restore drill, Identity Container and Pilot/Release policy suite require connected GitHub Actions. They are intentionally not marked successful until that run is green.

## Deliberate boundary

This checkpoint does not add general accounting, tax, payroll, multi-currency conversion, bank reconciliation or an ERP ledger. It completes the approved project-control Finance Lite boundary and does not authorize AI to calculate, approve or mutate financial state.

After connected verification, the only permitted product status is `PMCS V1 — Feature Complete`. That status is not `Qualified`, `Final` or `Locked`. The next approved engineering stage is `PMCS.TestHarness / PMCS QA Foundation`, followed by the full qualification suite and regression cycle.

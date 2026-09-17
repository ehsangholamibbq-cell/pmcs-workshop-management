# QA Foundation Slice 5 — Browser UI/E2E Verification

- Date: 2026-09-17
- Status: Implemented and verified by connected CI
- Product state: `PMCS V1 — Feature Complete`
- Qualification state: In progress; not Qualified, Final or Locked
- Governing roadmap: [`../roadmaps/pmcs-v1-development-and-qualification.md`](../roadmaps/pmcs-v1-development-and-qualification.md)

## Independent qualification scope

- real Keycloak Authorization Code + PKCE login through the Next.js BFF, with no Test Authentication shortcut;
- authenticated cold start and direct proof of Tenant/User/OIDC session scope;
- Persian `lang`/RTL shell, portfolio filtering, explicit Empty state and project navigation;
- localized Loading/Error behavior without exposing an English upstream failure detail;
- real Persian calendar dialog with the canonical 42-cell month grid;
- desktop, tablet and mobile viewports with no document-level horizontal overflow;
- online acquisition of a server-issued Offline Lease before local capture;
- browser-level form validation and a Tenant/User-scoped IndexedDB operation;
- offline Service Worker fallback without exposing a cached authenticated project page;
- queued operation persistence across a real persistent-browser close/reopen cycle;
- reconnect recovery and exactly one accepted operation in the local queue lifecycle.

## Isolation and security boundary

The `ui-e2e` CI job starts the existing Compose topology with isolated PostgreSQL, Keycloak, BFF, API and MinIO services. A preparation script uses the already limited identity-administration service account to make the existing demo user deterministic only inside that disposable Keycloak database. It clears required actions and installs a permanent test-only password at runtime; the checked-in realm continues to require first-login password replacement and TOTP, direct password grants remain disabled, and no production or QA authentication boundary is weakened.

The login scenario starts from the application's canonical `/login` route with an allow-listed `returnTo` value, then follows the real browser redirect to Keycloak and back through the BFF callback. It does not depend on client-hydration timing of a protected page redirect.

Playwright retains Tenant/User-scoped browser state only in the ephemeral CI runner. The offline test uses a real persistent Chromium profile, closes it, restarts while disconnected, verifies the public offline shell and IndexedDB queue, then reconnects through the same authenticated BFF session. It does not cache authenticated HTML in the Service Worker.

## Deliberate boundary

This Slice qualifies the critical Login/Navigation/RTL/Responsive/Persian-date/Offline-Recovery browser path. Broad screen-by-screen visual comparison, assistive-technology/manual accessibility review, camera hardware behavior, multi-tab destructive races and exploratory personas remain in Agent/Exploratory and Full Regression. Seven-day endurance and production-device coverage remain Hardening/Pilot gates.

Agent/Exploratory and full-regression/reporting qualification remain open. PMCS V1 therefore remains Feature Complete and must not yet be described as Qualified, Final or Locked.

## Connected verification

Verified source revision: [`59497c9`](https://github.com/ehsangholamibbq-cell/pmcs-workshop-management/commit/59497c9a3d2609374cde58d2f805967addc79869)

GitHub Actions evidence: [CI Run 64](https://github.com/ehsangholamibbq-cell/pmcs-workshop-management/actions/runs/35244670937), with all seven jobs successful.

- all four Playwright scenarios passed through the real Keycloak Authorization Code + PKCE and Next.js BFF path;
- authenticated Persian RTL navigation, project routing, the canonical 42-cell Persian calendar and desktop/tablet/mobile overflow checks passed;
- localized Loading/Error behavior passed without exposing the simulated English upstream detail;
- the Offline queue survived a persistent Chromium close/reopen cycle and reconnected with exactly one accepted operation;
- Architecture, Backend, Web, Integration, Identity Container and Pilot Contract gates all remained green, including the existing Permission/Workflow, File/Attachment, Offline/Sync and PostgreSQL backup/restore evidence.

The review also corrected the Web route validator to accept every canonical stored .NET/PostgreSQL `Guid` value. The former RFC version/variant restriction incorrectly rejected the deterministic QA project identifier even though it is valid within the approved application identity contract.

QA Foundation Slice 5 is closed. Agent/Exploratory and full-regression/reporting qualification remain open. PMCS V1 remains Feature Complete and is not yet Qualified, Final or Locked.

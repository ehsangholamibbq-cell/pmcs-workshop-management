# Post-V1 checkpoint — Roadmap Planning Baseline

- Checkpoint ID: `PMCS-PV1-PLAN-00`
- Date: ۱۴۰۵/۰۶/۲۶ (۲۰۲۶-۰۹-۱۷)
- Status: Registered
- Runtime change: None
- Active product line: `PMCS V1.1`
- Active product-line state: `Planned / Governance`
- Parent locked product baseline: `PMCS V1`
- Parent source commit: `26bf222d44634562ca7f3fc0931f3f8b79ca04a1`

## Scope closed by this checkpoint

- preserved the completed V1 roadmap as historical evidence instead of rewriting it;
- established the active Post-V1 roadmap registry;
- defined the V1.1, V1.2 and V2.x product lines;
- defined V1.1 checkpoints, dependencies, Definition of Ready and Definition of Done;
- recorded the permanent current exclusions for private messaging, voice/video calling and social-network features;
- recorded the official/non-official boundary of Project Collaboration;
- phased Reporting into certified standard reports first and advanced report design later;
- made Extensibility Foundation, Shared Documents and UX approval explicit prerequisites;
- established the Version, Checkpoint, Qualification and Baseline policy;
- restored the exact seven-stage Managerial Agent roadmap with independent gates;
- established a product-wide Visual Excellence Program instead of treating UI quality as a final polish pass;
- recorded rejected alternatives in ADR 0027.

## Evidence documents

- `docs/roadmaps/README.md`
- `docs/roadmaps/pmcs-post-v1-product-evolution.md`
- `docs/governance/pmcs-version-and-baseline-policy.md`
- `docs/adr/0027-post-v1-extensibility-collaboration-reporting.md`
- `docs/roadmaps/pmcs-managerial-agent-seven-stage-roadmap.md`
- `docs/roadmaps/pmcs-visual-excellence-program.md`

## Explicitly not completed

This checkpoint does not close `V1.1-G0` and does not authorize feature implementation. The following G0 outputs remain required:

- exact V1.1 `repositoryStartCommit` from a clean branch;
- detailed Extensibility/Document/Reporting/Collaboration architecture contracts;
- initial Permission Catalog and data classification;
- API/Event/Migration compatibility strategy;
- V1.1 Test Strategy and Qualification Contract;
- risk register, rollout and rollback plan;
- Design System direction and high-fidelity UX review pack.

## Next gate

The next stage is `V1.1-G0 — Governance and Development Baseline`, followed by `V1.1-UX1 — Design System and High-Fidelity Product Prototype`. Product runtime implementation remains blocked until the applicable Governance and UX gates are approved.

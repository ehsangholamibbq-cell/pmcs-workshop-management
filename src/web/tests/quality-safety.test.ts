import assert from "node:assert/strict";
import test from "node:test";
import {
  beginIntakeTriage,
  captureQualitySafetyIntake,
  configureQualitySafety,
  extendCorrectiveAction,
  getQualitySafetyState,
  recordInspectionResult,
  transitionIncident,
  transitionNcr,
  transitionPermit,
  transitionCorrectiveAction,
  type CorrectiveActionModel,
  type IncidentModel,
  type InspectionModel,
  type IntakeModel,
  type NcrModel,
  type PermitModel,
} from "../lib/quality-safety.ts";

const identity = { tenantId: "tenant-id", userId: "user-id" };

test("quality and HSE state remains project and actor scoped", async () => {
  const originalFetch = globalThis.fetch; let url = ""; let headers: HeadersInit | undefined;
  globalThis.fetch = async (input, init) => { url = String(input); headers = init?.headers; return Response.json({ qualityState: "NotEnabled", hseState: "NotEnabled" }); };
  try {
    await getQualitySafetyState("https://pmcs.test/", identity, "project-id");
    assert.equal(url, "https://pmcs.test/api/v1/projects/project-id/quality-safety/state");
    const sent = new Headers(headers); assert.equal(sent.get("X-Tenant-Id"), "tenant-id"); assert.equal(sent.get("X-User-Id"), "user-id");
  } finally { globalThis.fetch = originalFetch; }
});

test("configuration uses PUT and optimistic revision", async () => {
  const originalFetch = globalThis.fetch; let init: RequestInit | undefined;
  globalThis.fetch = async (_input, request) => { init = request; return Response.json({ id: "configuration-id", revision: 4 }); };
  try {
    await configureQualitySafety("https://pmcs.test", identity, "project-id", {
      qualityMode: "FullV1", hseMode: "Disabled", qualityOwnerUserId: "quality-owner", hseOwnerUserId: null,
      qualityMatrixVersionId: "matrix-id", hseMatrixVersionId: null, workflowAndSlaDefined: true,
      templatesDefined: true, evidenceAndClosureRulesDefined: true, revision: 3,
    });
    assert.equal(init?.method, "PUT"); assert.equal(JSON.parse(String(init?.body)).baseRevision, 3);
    assert.ok(new Headers(init?.headers).get("Idempotency-Key"));
  } finally { globalThis.fetch = originalFetch; }
});

test("quick intake cannot be numbered or formalized by browser", async () => {
  const originalFetch = globalThis.fetch; let body: Record<string, unknown> = {};
  globalThis.fetch = async (_input, init) => { body = JSON.parse(String(init?.body)); return Response.json({ id: "intake-id", status: "Captured" }, { status: 201 }); };
  try {
    await captureQualitySafetyIntake("https://pmcs.test", identity, "project-id", {
      kind: "IncidentIntake", observedAt: "2026-09-11T10:00:00Z", location: "site", facts: "near miss",
      initialSeverity: "High", evidenceReferences: ["evidence:photo"], classification: "ConfidentialHse",
    });
    assert.equal(body.number, undefined); assert.equal(body.status, undefined); assert.equal(body.conversionType, undefined);
    assert.match(body.clientGeneratedId as string, /^[0-9a-f-]{36}$/iu);
  } finally { globalThis.fetch = originalFetch; }
});

test("triage and inspection result carry current server revisions", async () => {
  const originalFetch = globalThis.fetch; const calls: { url: string; body: Record<string, unknown> }[] = [];
  globalThis.fetch = async (input, init) => { calls.push({ url: String(input), body: JSON.parse(String(init?.body)) }); return Response.json({ revision: 8 }); };
  try {
    await beginIntakeTriage("https://pmcs.test", identity, "project-id", { id: "intake-id", revision: 4 } as IntakeModel);
    await recordInspectionResult("https://pmcs.test", identity, "project-id", { id: "inspection-id", revision: 7 } as InspectionModel,
      "PassWithObservation", "minor observation", ["evidence:sheet"]);
    assert.match(calls[0]!.url, /intakes\/intake-id\/triage$/u); assert.equal(calls[0]!.body.baseRevision, 4);
    assert.match(calls[1]!.url, /inspections\/inspection-id\/result$/u); assert.equal(calls[1]!.body.baseRevision, 7);
    assert.deepEqual(calls[1]!.body.evidenceReferences, ["evidence:sheet"]);
  } finally { globalThis.fetch = originalFetch; }
});

test("corrective action completion and verification are explicit separate commands", async () => {
  const originalFetch = globalThis.fetch; const bodies: Record<string, unknown>[] = [];
  globalThis.fetch = async (_input, init) => { bodies.push(JSON.parse(String(init?.body))); return Response.json({ revision: 5 }); };
  const action = { id: "action-id", revision: 3 } as CorrectiveActionModel;
  try {
    await transitionCorrectiveAction("https://pmcs.test", identity, "project-id", action, "Completed", ["evidence:completion"]);
    await transitionCorrectiveAction("https://pmcs.test", identity, "project-id", action, "Verified", ["evidence:verification"]);
    assert.deepEqual(bodies.map((body) => body.targetStatus), ["Completed", "Verified"]);
    assert.deepEqual(bodies[0]!.evidenceReferences, ["evidence:completion"]);
    assert.deepEqual(bodies[1]!.evidenceReferences, ["evidence:verification"]);
  } finally { globalThis.fetch = originalFetch; }
});

test("formal workflow commands carry server revision and human decisions", async () => {
  const originalFetch = globalThis.fetch; const calls: { url: string; body: Record<string, unknown> }[] = [];
  globalThis.fetch = async (input, init) => { calls.push({ url: String(input), body: JSON.parse(String(init?.body)) }); return Response.json({ revision: 9 }); };
  try {
    await transitionNcr("https://pmcs.test", identity, "project-id", { id: "ncr-id", revision: 4 } as NcrModel, {
      targetStatus: "ActionImplementation", disposition: "Repair", dispositionNote: "تعمیر پس از بررسی",
      rootCauseStatus: "UnderReview", closureEvidence: [],
    });
    await transitionIncident("https://pmcs.test", identity, "project-id", { id: "incident-id", revision: 5 } as IncidentModel, {
      targetStatus: "UnderInvestigation", rootCauseStatus: "Proposed", rootCause: "نیازمند راستی‌آزمایی", closureEvidence: [],
    });
    await transitionPermit("https://pmcs.test", identity, "project-id", { id: "permit-id", revision: 6 } as PermitModel, "Submitted");
    await extendCorrectiveAction("https://pmcs.test", identity, "project-id", { id: "action-id", revision: 7 } as CorrectiveActionModel,
      "2026-09-20", "تأخیر مستند در دسترسی به محل");
    assert.deepEqual(calls.map((call) => call.body.baseRevision), [4, 5, 6, 7]);
    assert.equal(calls[0]!.body.disposition, "Repair");
    assert.equal(calls[1]!.body.rootCauseStatus, "Proposed");
    assert.equal(calls[2]!.body.targetStatus, "Submitted");
    assert.match(calls[3]!.url, /corrective-actions\/action-id\/extend$/u);
  } finally { globalThis.fetch = originalFetch; }
});

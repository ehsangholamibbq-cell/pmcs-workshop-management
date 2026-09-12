import assert from "node:assert/strict";
import test from "node:test";
import {
  acknowledgeEscalation, assessRisk, createDecisionRequest, createIssue,
  getGovernanceState, recordDecision, submitDecisionRequest,
  type DecisionRequestModel, type EscalationModel, type IssueModel, type RiskModel,
} from "../lib/governance.ts";

const identity = { tenantId: "tenant-id", userId: "user-id" };

test("governance read is scoped by project and current actor", async () => {
  const originalFetch = globalThis.fetch; let url = ""; let headers: HeadersInit | undefined;
  globalThis.fetch = async (input, init) => { url = String(input); headers = init?.headers; return Response.json({ setupState: "SetupRequired" }); };
  try {
    await getGovernanceState("https://pmcs.test/", identity, "project-id");
    assert.equal(url, "https://pmcs.test/api/v1/projects/project-id/governance/");
    const sent = new Headers(headers); assert.equal(sent.get("X-Tenant-Id"), "tenant-id"); assert.equal(sent.get("X-User-Id"), "user-id");
  } finally { globalThis.fetch = originalFetch; }
});

test("issue creation sends evidence and cannot assign status or server number", async () => {
  const originalFetch = globalThis.fetch; let body: Record<string, unknown> = {};
  globalThis.fetch = async (_input, init) => { body = JSON.parse(String(init?.body)); return Response.json({ id: "issue-id" }, { status: 201 }); };
  try {
    await createIssue("https://pmcs.test", identity, "project-id", { title: "مانع تجهیز",
      observedFact: "تجهیز در موعد نرسیده", category: "تدارکات", severity: "High", urgency: "Soon",
      ownerUserId: "owner-id", targetResolutionDate: "2026-09-20", sourceModule: "Supply",
      sourceEntityType: "PurchaseOrder", sourceSnapshot: "سفارش باز و تحویل نشده",
      evidenceReferences: ["evidence:source"], confidentiality: "GeneralProject" });
    assert.equal(body.status, undefined); assert.equal(body.number, undefined);
    assert.deepEqual(body.evidenceReferences, ["evidence:source"]);
    assert.match(String(body.clientGeneratedId), /^[0-9a-f-]{36}$/iu);
  } finally { globalThis.fetch = originalFetch; }
});

test("risk assessment pins matrix and carries optimistic revision", async () => {
  const originalFetch = globalThis.fetch; let body: Record<string, unknown> = {};
  globalThis.fetch = async (_input, init) => { body = JSON.parse(String(init?.body)); return Response.json({ revision: 5 }); };
  try {
    await assessRisk("https://pmcs.test", identity, "project-id", { id: "risk-id", revision: 4 } as RiskModel,
      { matrixVersionId: "matrix-id", probability: "Possible", impact: "Major", responseStrategy: "Mitigate",
        responsePlan: "کنترل تأمین", earlyWarningIndicator: "عبور از موعد", reviewDate: "2026-09-20" });
    assert.equal(body.baseRevision, 4); assert.equal(body.matrixVersionId, "matrix-id");
    assert.equal(body.costImpact, "Major"); assert.equal(body.operationsImpact, "Major");
  } finally { globalThis.fetch = originalFetch; }
});

test("decision request keeps facts assumptions predictions and options separate", async () => {
  const originalFetch = globalThis.fetch; let body: Record<string, unknown> = {};
  globalThis.fetch = async (_input, init) => { body = JSON.parse(String(init?.body)); return Response.json({ id: "request-id" }, { status: 201 }); };
  try {
    await createDecisionRequest("https://pmcs.test", identity, "project-id", { question: "کدام مسیر؟",
      whyNow: "توقف نزدیک است", requiredBy: "2026-09-20", authorityUserId: "authority-id",
      knownFacts: ["نقشه هنوز تأیید نشده"], assumptions: ["پاسخ فردا می‌رسد"],
      predictions: ["احتمال توقف وجود دارد"], options: ["راهکار الف", "راهکار ب"], constraints: ["زمان محدود"],
      evidenceReferences: ["evidence:rfi"], confidentiality: "GeneralProject", sourceModule: "TechnicalOffice",
      sourceEntityType: "Rfi", sourceSnapshot: "درخواست اطلاعات باز" });
    assert.deepEqual(body.knownFacts, ["نقشه هنوز تأیید نشده"]); assert.deepEqual(body.assumptions, ["پاسخ فردا می‌رسد"]);
    assert.deepEqual(body.predictions, ["احتمال توقف وجود دارد"]); assert.deepEqual(body.options, ["راهکار الف", "راهکار ب"]);
  } finally { globalThis.fetch = originalFetch; }
});

test("submit and record decision use server revisions and explicit selected option", async () => {
  const originalFetch = globalThis.fetch; const calls: { url: string; body: Record<string, unknown> }[] = [];
  globalThis.fetch = async (input, init) => { calls.push({ url: String(input), body: JSON.parse(String(init?.body)) }); return Response.json({ revision: 4 }); };
  const request = { id: "request-id", revision: 3, options: ["راهکار الف", "راهکار ب"] } as unknown as DecisionRequestModel;
  try {
    await submitDecisionRequest("https://pmcs.test", identity, "project-id", request);
    await recordDecision("https://pmcs.test", identity, "project-id", request, "راهکار ب", "کمترین توقف");
    assert.equal(calls[0]!.body.baseRevision, 3); assert.equal(calls[1]!.body.baseRevision, 3);
    assert.equal(calls[1]!.body.selectedOption, "راهکار ب"); assert.equal(calls[1]!.body.channel, "InSystem");
    assert.match(calls[1]!.url, /decision-requests\/request-id\/decisions$/u);
  } finally { globalThis.fetch = originalFetch; }
});

test("acknowledging escalation sends acknowledgement only and does not close source", async () => {
  const originalFetch = globalThis.fetch; let body: Record<string, unknown> = {};
  globalThis.fetch = async (_input, init) => { body = JSON.parse(String(init?.body)); return Response.json({ status: "Acknowledged" }); };
  try {
    await acknowledgeEscalation("https://pmcs.test", identity, "project-id", { id: "thread-id", revision: 7 } as EscalationModel, "در حال پیگیری");
    assert.equal(body.baseRevision, 7); assert.equal(body.note, "در حال پیگیری");
    assert.equal(body.sourceStatus, undefined); assert.equal(body.ownerUserId, undefined);
  } finally { globalThis.fetch = originalFetch; }
});

test("issue transition is optimistic and keeps closure evidence explicit", async () => {
  const originalFetch = globalThis.fetch; let body: Record<string, unknown> = {};
  globalThis.fetch = async (_input, init) => { body = JSON.parse(String(init?.body)); return Response.json({ status: "Closed" }); };
  try {
    const { transitionIssue } = await import("../lib/governance.ts");
    await transitionIssue("https://pmcs.test", identity, "project-id", { id: "issue-id", revision: 8 } as IssueModel,
      "Closed", "رفع و راستی‌آزمایی شد", ["evidence:verification"]);
    assert.equal(body.baseRevision, 8); assert.deepEqual(body.closureEvidenceReferences, ["evidence:verification"]);
  } finally { globalThis.fetch = originalFetch; }
});

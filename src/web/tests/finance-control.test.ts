import assert from "node:assert/strict";
import test from "node:test";
import {
  createFinancialObligation,
  createManagementFeePolicy,
  createPettyCashRequest,
  getFinanceControlState,
  submitPettyCashReconciliation,
} from "../lib/finance-control.ts";

const identity = { tenantId: "tenant-id", userId: "user-id" };

test("finance control state is a project-scoped permission-aware read", async () => {
  const originalFetch = globalThis.fetch;
  let capturedUrl = "";
  let capturedInit: RequestInit | undefined;
  globalThis.fetch = async (input, init) => {
    capturedUrl = String(input);
    capturedInit = init;
    return Response.json({});
  };

  try {
    await getFinanceControlState("https://pmcs.test/", identity, "project-id");
    const headers = new Headers(capturedInit?.headers);
    assert.equal(capturedUrl, "https://pmcs.test/api/v1/projects/project-id/finance/control-state");
    assert.equal(headers.get("X-Tenant-Id"), "tenant-id");
    assert.equal(headers.get("X-User-Id"), "user-id");
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("obligation capture keeps ISO dates and stable cross-module identities", async () => {
  const originalFetch = globalThis.fetch;
  let capturedInit: RequestInit | undefined;
  globalThis.fetch = async (_input, init) => {
    capturedInit = init;
    return Response.json({});
  };

  try {
    await createFinancialObligation("https://pmcs.test", identity, "project-id", {
      type: "Payable",
      number: "PAY-01",
      description: "Supplier liability",
      issueDate: "2026-09-16",
      dueDate: "2026-10-16",
      amount: 500,
      partyId: "party-id",
      contractId: "contract-id",
      commitmentId: "commitment-id",
      locationId: "location-id",
      wbsReference: "WBS-1",
    });
    const body = JSON.parse(String(capturedInit?.body));
    const headers = new Headers(capturedInit?.headers);
    assert.equal(body.issueDate, "2026-09-16");
    assert.equal(body.dueDate, "2026-10-16");
    assert.equal(body.locationId, "location-id");
    assert.equal(body.partyId, "party-id");
    assert.ok(body.clientGeneratedId);
    assert.ok(headers.get("Idempotency-Key"));
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("petty cash reconciliation links posted expense and return facts", async () => {
  const originalFetch = globalThis.fetch;
  let capturedUrl = "";
  let capturedInit: RequestInit | undefined;
  globalThis.fetch = async (input, init) => {
    capturedUrl = String(input);
    capturedInit = init;
    return Response.json({});
  };

  try {
    await createPettyCashRequest("https://pmcs.test", identity, "project-id", {
      number: "PC-01",
      purpose: "Site supplies",
      custodian: "Site cashier",
      requestDate: "2026-09-16",
      reconciliationDueDate: "2026-09-23",
      requestedAmount: 100,
      locationId: "location-id",
    });
    await submitPettyCashReconciliation(
      "https://pmcs.test",
      identity,
      "project-id",
      { id: "petty-id", revision: 4 },
      80,
      20,
      "expense-record-id",
      "return-record-id",
    );
    const body = JSON.parse(String(capturedInit?.body));
    assert.equal(capturedUrl, "https://pmcs.test/api/v1/projects/project-id/finance/petty-cash-requests/petty-id/reconciliation");
    assert.deepEqual(body, {
      baseRevision: 4,
      expenseAmount: 80,
      returnedAmount: 20,
      expenseRecordId: "expense-record-id",
      returnRecordId: "return-record-id",
    });
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("management fee policy is optional, versioned and effective-dated", async () => {
  const originalFetch = globalThis.fetch;
  let capturedInit: RequestInit | undefined;
  globalThis.fetch = async (_input, init) => {
    capturedInit = init;
    return Response.json({});
  };

  try {
    await createManagementFeePolicy("https://pmcs.test", identity, "project-id", {
      title: "Construction management fee",
      ratePercent: 7.5,
      effectiveFrom: "2026-09-16",
    });
    const body = JSON.parse(String(capturedInit?.body));
    assert.equal(body.calculationBase, "RecognizedSpend");
    assert.equal(body.ratePercent, 7.5);
    assert.equal(body.effectiveFrom, "2026-09-16");
    assert.equal(body.notes, null);
  } finally {
    globalThis.fetch = originalFetch;
  }
});

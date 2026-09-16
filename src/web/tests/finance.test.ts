import assert from "node:assert/strict";
import test from "node:test";
import {
  amendFinancialRecord,
  createFinancialRecord,
  transitionBudgetBaseline,
  transitionFinancialRecord,
  type FinancialRecordModel,
} from "../lib/finance.ts";

const identity = { tenantId: "tenant-id", userId: "user-id" };

test("financial capture uses stable command metadata and keeps optional references nullable", async () => {
  const originalFetch = globalThis.fetch;
  let capturedUrl = "";
  let capturedInit: RequestInit | undefined;
  globalThis.fetch = async (input, init) => {
    capturedUrl = String(input);
    capturedInit = init;
    return Response.json(recordResponse(), { status: 201 });
  };

  try {
    await createFinancialRecord("https://pmcs.test/", identity, "project-id", {
      type: "Payment",
      transactionDate: "2026-09-09",
      amount: 125000,
      description: "Supplier payment",
    });
    const headers = new Headers(capturedInit?.headers);
    const body = JSON.parse(String(capturedInit?.body));

    assert.equal(capturedUrl, "https://pmcs.test/api/v1/projects/project-id/finance/records");
    assert.ok(headers.get("Idempotency-Key"));
    assert.ok(body.clientGeneratedId);
    assert.equal(body.contractReference, null);
    assert.equal(body.contractId, null);
    assert.equal(body.commitmentId, null);
    assert.equal(body.costCenterCode, null);
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("financial and budget workflows use explicit command endpoints", async () => {
  const originalFetch = globalThis.fetch;
  const calls: string[] = [];
  globalThis.fetch = async (input) => {
    calls.push(String(input));
    return Response.json(recordResponse());
  };

  try {
    await transitionFinancialRecord(
      "https://pmcs.test",
      identity,
      "project-id",
      { id: "record-id", revision: 2 },
      "post",
    );
    await transitionBudgetBaseline(
      "https://pmcs.test",
      identity,
      "project-id",
      { id: "baseline-id", revision: 2 },
      "approve",
    );

    assert.deepEqual(calls, [
      "https://pmcs.test/api/v1/projects/project-id/finance/records/record-id/post",
      "https://pmcs.test/api/v1/projects/project-id/finance/budget-baselines/baseline-id/approve",
    ]);
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("returned financial record correction is revision controlled", async () => {
  const originalFetch = globalThis.fetch;
  let capturedUrl = "";
  let capturedInit: RequestInit | undefined;
  globalThis.fetch = async (input, init) => {
    capturedUrl = String(input);
    capturedInit = init;
    return Response.json(recordResponse());
  };

  try {
    const record = { ...recordResponse(), status: "Returned" as const, revision: 3 };
    await amendFinancialRecord(
      "https://pmcs.test",
      identity,
      "project-id",
      record,
      150000,
      "Corrected payment",
    );
    const body = JSON.parse(String(capturedInit?.body));

    assert.equal(capturedUrl, "https://pmcs.test/api/v1/projects/project-id/finance/records/record-id");
    assert.equal(capturedInit?.method, "PUT");
    assert.equal(body.baseRevision, 3);
    assert.equal(body.amount, 150000);
  } finally {
    globalThis.fetch = originalFetch;
  }
});

function recordResponse(): FinancialRecordModel {
  return {
    id: "record-id",
    projectId: "project-id",
    type: "Payment",
    transactionDate: "2026-09-09",
    amount: 125000,
    currencyCode: "IRR",
    description: "Supplier payment",
    counterparty: null,
    documentNumber: null,
    contractReference: null,
    contractId: null,
    commitmentId: null,
    costCenterCode: null,
    partyId: null,
    locationId: null,
    locationCode: null,
    wbsReference: null,
    status: "Draft",
    reviewComment: null,
    revision: 1,
  };
}

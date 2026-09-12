import assert from "node:assert/strict";
import test from "node:test";
import {
  createContract,
  createPurchaseRequest,
  issuePurchaseOrder,
  transitionContract,
} from "../lib/commercial.ts";

const identity = { tenantId: "tenant-id", userId: "user-id" };

test("contract capture keeps an unknown initial ceiling nullable", async () => {
  const originalFetch = globalThis.fetch;
  let capturedInit: RequestInit | undefined;
  globalThis.fetch = async (_input, init) => {
    capturedInit = init;
    return Response.json({}, { status: 201 });
  };

  try {
    await createContract("https://pmcs.test", identity, "project-id", {
      partyId: "party-id",
      number: "CTR-001",
      title: "Management contract",
      type: "MainContract",
    });
    const body = JSON.parse(String(capturedInit?.body));

    assert.equal(body.originalApprovedAmount, null);
    assert.equal(body.currencyCode, null);
    assert.ok(new Headers(capturedInit?.headers).get("Idempotency-Key"));
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("purchase request does not require an early estimate", async () => {
  const originalFetch = globalThis.fetch;
  let capturedBody = "";
  globalThis.fetch = async (_input, init) => {
    capturedBody = String(init?.body);
    return Response.json({}, { status: 201 });
  };

  try {
    await createPurchaseRequest("https://pmcs.test", identity, "project-id", {
      number: "PR-001",
      title: "Rebar",
      description: "Site requirement",
    });
    assert.equal(JSON.parse(capturedBody).estimatedAmount, null);
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("order issuance carries the approved request revision and real references", async () => {
  const originalFetch = globalThis.fetch;
  let capturedUrl = "";
  let capturedBody = "";
  globalThis.fetch = async (input, init) => {
    capturedUrl = String(input);
    capturedBody = String(init?.body);
    return Response.json({}, { status: 201 });
  };

  try {
    await issuePurchaseOrder("https://pmcs.test/", identity, "project-id", {
      purchaseRequestId: "request-id",
      purchaseRequestRevision: 3,
      partyId: "party-id",
      contractId: "contract-id",
      number: "PO-001",
      title: "Steel commitment",
      amount: 5000,
    });
    const body = JSON.parse(capturedBody);

    assert.equal(capturedUrl, "https://pmcs.test/api/v1/projects/project-id/commercial/purchase-orders");
    assert.equal(body.purchaseRequestRevision, 3);
    assert.equal(body.contractId, "contract-id");
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("contract workflow uses explicit command endpoints", async () => {
  const originalFetch = globalThis.fetch;
  let capturedUrl = "";
  globalThis.fetch = async (input) => {
    capturedUrl = String(input);
    return Response.json({});
  };

  try {
    await transitionContract(
      "https://pmcs.test",
      identity,
      "project-id",
      { id: "contract-id", revision: 2 },
      "activate",
    );
    assert.equal(capturedUrl, "https://pmcs.test/api/v1/projects/project-id/commercial/contracts/contract-id/activate");
  } finally {
    globalThis.fetch = originalFetch;
  }
});

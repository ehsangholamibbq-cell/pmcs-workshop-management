import assert from "node:assert/strict";
import test from "node:test";
import {
  createGoodsReceipt,
  createMaterialIssue,
  getSupplyState,
  inspectReceipt,
  postInventoryAdjustment,
  proposeInventoryAdjustment,
  reconcileMaterialIssue,
  transitionReceipt,
  type GoodsReceiptModel,
  type InventoryAdjustmentModel,
  type MaterialIssueModel,
} from "../lib/supply.ts";

const identity = { tenantId: "tenant-id", userId: "user-id" };

test("supply state remains project and actor scoped", async () => {
  const originalFetch = globalThis.fetch;
  let url = "";
  let headers: HeadersInit | undefined;
  globalThis.fetch = async (input, init) => {
    url = String(input); headers = init?.headers;
    return Response.json({ items: [], locations: [], receipts: [], materialIssues: [], ledger: [] });
  };
  try {
    await getSupplyState("https://pmcs.test/", identity, "project-id");
    const sent = new Headers(headers);
    assert.equal(url, "https://pmcs.test/api/v1/projects/project-id/commercial/supply/state");
    assert.equal(sent.get("X-Tenant-Id"), "tenant-id");
    assert.equal(sent.get("X-User-Id"), "user-id");
  } finally { globalThis.fetch = originalFetch; }
});

test("physical receipt has server numbering and mandatory evidence lineage", async () => {
  const originalFetch = globalThis.fetch;
  let body: Record<string, unknown> = {};
  let headers: HeadersInit | undefined;
  globalThis.fetch = async (_input, init) => {
    body = JSON.parse(String(init?.body)); headers = init?.headers;
    return Response.json({ id: "receipt-id" }, { status: 201 });
  };
  try {
    await createGoodsReceipt("https://pmcs.test", identity, "project-id", {
      purchaseOrderId: "order-id", itemId: "item-id", stockLocationId: "location-id",
      dispatchNote: "DN-42", arrivedAt: "2026-09-11T10:00:00Z", deliveryLocation: "site",
      shippedQuantity: 12, receivedQuantity: 12, damagedQuantity: 0,
      evidenceReferences: ["evidence:delivery-1"],
    });
    assert.equal(body.number, undefined);
    assert.equal(body.clientGeneratedId === undefined, false);
    assert.deepEqual(body.evidenceReferences, ["evidence:delivery-1"]);
    assert.ok(new Headers(headers).get("Idempotency-Key"));
  } finally { globalThis.fetch = originalFetch; }
});

test("inspection and stock posting are distinct revision-controlled commands", async () => {
  const originalFetch = globalThis.fetch;
  const calls: { url: string; body: Record<string, unknown> }[] = [];
  globalThis.fetch = async (input, init) => {
    calls.push({ url: String(input), body: JSON.parse(String(init?.body)) });
    return Response.json({ id: "receipt-id", revision: 5 });
  };
  const receipt = { id: "receipt-id", revision: 4 } as GoodsReceiptModel;
  try {
    await inspectReceipt("https://pmcs.test", identity, "project-id", receipt, {
      acceptedBaseQuantity: 8, rejectedBaseQuantity: 1, quarantinedBaseQuantity: 1,
      inspectionType: "Visual", comment: "One rejected and one quarantined",
    });
    await transitionReceipt("https://pmcs.test", identity, "project-id", receipt, "post-stock");
    assert.match(calls[0]!.url, /receipts\/receipt-id\/inspect$/);
    assert.match(calls[1]!.url, /receipts\/receipt-id\/post-stock$/);
    assert.equal(calls[0]!.body.baseRevision, 4);
    assert.equal(calls[1]!.body.baseRevision, 4);
  } finally { globalThis.fetch = originalFetch; }
});

test("material issue does not imply consumption and keeps WBS optional", async () => {
  const originalFetch = globalThis.fetch;
  let body: Record<string, unknown> = {};
  globalThis.fetch = async (_input, init) => {
    body = JSON.parse(String(init?.body));
    return Response.json({ id: "issue-id", status: "Issued" }, { status: 201 });
  };
  try {
    await createMaterialIssue("https://pmcs.test", identity, "project-id", {
      itemId: "item-id", sourceLocationId: "store-id", quantity: 4,
      issuedTo: "crew", destinationLocation: "zone A", evidenceReferences: ["evidence:handover"],
    });
    assert.equal(body.wbsReference, null);
    assert.equal(body.consumedBaseQuantity, undefined);
    assert.match(body.clientGeneratedId as string, /^[0-9a-f-]{36}$/i);
  } finally { globalThis.fetch = originalFetch; }
});

test("reconciliation is append-like and carries the current custody revision", async () => {
  const originalFetch = globalThis.fetch;
  let url = "";
  let body: Record<string, unknown> = {};
  globalThis.fetch = async (input, init) => {
    url = String(input); body = JSON.parse(String(init?.body));
    return Response.json({ id: "issue-id", revision: 8 });
  };
  try {
    await reconcileMaterialIssue("https://pmcs.test", identity, "project-id", { id: "issue-id", revision: 7 } as MaterialIssueModel, {
      consumedBaseQuantity: 2, returnedBaseQuantity: 1, wasteBaseQuantity: 0,
      evidenceReferences: ["evidence:reconciliation"],
    });
    assert.match(url, /material-issues\/issue-id\/reconcile$/);
    assert.equal(body.baseRevision, 7);
    assert.equal(body.consumedBaseQuantity, 2);
  } finally { globalThis.fetch = originalFetch; }
});

test("inventory count sends physical count, never a client-computed system balance", async () => {
  const originalFetch = globalThis.fetch;
  const calls: { url: string; body: Record<string, unknown> }[] = [];
  globalThis.fetch = async (input, init) => {
    calls.push({ url: String(input), body: JSON.parse(String(init?.body)) });
    return Response.json({ id: "adjustment-id", revision: 3 });
  };
  try {
    await proposeInventoryAdjustment("https://pmcs.test", identity, "project-id", {
      itemId: "item-id", locationId: "store-id", cutoffAt: "2026-09-11T12:00:00Z",
      countedBaseQuantity: 19, reason: "cycle count", evidenceReferences: ["evidence:count"],
    });
    await postInventoryAdjustment("https://pmcs.test", identity, "project-id", { id: "adjustment-id", revision: 3 } as InventoryAdjustmentModel);
    assert.equal(calls[0]!.body.systemBaseQuantity, undefined);
    assert.equal(calls[0]!.body.countedBaseQuantity, 19);
    assert.match(calls[1]!.url, /adjustments\/adjustment-id\/post$/);
    assert.deepEqual(calls[1]!.body, { baseRevision: 3 });
  } finally { globalThis.fetch = originalFetch; }
});

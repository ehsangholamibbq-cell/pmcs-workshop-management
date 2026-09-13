import assert from "node:assert/strict";
import test from "node:test";
import {
  buildDailyFactPayload,
  emptyFactDraft,
  FactValidationError,
  type DailyFactDraft,
} from "../lib/field-facts.ts";

test("progress fact remains valid without a WBS reference", () => {
  const draft: DailyFactDraft = {
    ...emptyFactDraft,
    kind: "WorkProgress",
    category: "Concrete placement",
    description: "Measured on site",
    quantity: "42.5",
    unit: "m3",
  };

  const payload = buildDailyFactPayload(draft, "fact-id", "2026-09-09");

  assert.equal(payload.referenceCode, null);
  assert.equal(payload.quantity, 42.5);
  assert.equal(payload.measurementItemId, null);
});

test("measurement item can replace a free-text work item but requires measured quantity", () => {
  const draft: DailyFactDraft = {
    ...emptyFactDraft,
    kind: "WorkProgress",
    measurementItemId: "measurement-id",
    description: "مقدار در محل اندازه‌گیری شد",
    quantity: "18.25",
    unit: "مترمکعب",
  };

  const payload = buildDailyFactPayload(draft, "fact-id", "2026-09-11");

  assert.equal(payload.category, null);
  assert.equal(payload.measurementItemId, "measurement-id");
  assert.equal(payload.quantity, 18.25);
});

test("labor fact requires a trade and headcount", () => {
  const draft: DailyFactDraft = {
    ...emptyFactDraft,
    kind: "Labor",
    description: "Crew present",
    hours: "8",
  };

  assert.throws(
    () => buildDailyFactPayload(draft, "fact-id", "2026-09-09"),
    FactValidationError,
  );
});

test("material fact keeps structured quantity and unit", () => {
  const draft: DailyFactDraft = {
    ...emptyFactDraft,
    kind: "Material",
    category: "Cement Type 2",
    description: "Received at site warehouse",
    quantity: "12",
    unit: "ton",
  };

  const payload = buildDailyFactPayload(draft, "fact-id", "2026-09-09");

  assert.equal(payload.category, "Cement Type 2");
  assert.equal(payload.unit, "ton");
});

test("changing fact kind does not leak hidden structured values", () => {
  const draft: DailyFactDraft = {
    ...emptyFactDraft,
    kind: "Note",
    description: "A factual observation",
    quantity: "12",
    unit: "ton",
    resourceCount: "4",
    hours: "8",
    impactLevel: "Critical",
    referenceCode: "WBS-01",
  };

  const payload = buildDailyFactPayload(draft, "fact-id", "2026-09-09");

  assert.equal(payload.quantity, null);
  assert.equal(payload.resourceCount, null);
  assert.equal(payload.impactLevel, null);
  assert.equal(payload.referenceCode, null);
});

test("field fact preserves the selected project location identity", () => {
  const payload = buildDailyFactPayload({
    ...emptyFactDraft,
    kind: "Note",
    description: "مشاهده ثبت‌شده در محل",
    locationId: "location-id",
    locationName: "طبقه سوم",
  }, "fact-id", "2026-09-13");

  assert.equal(payload.locationId, "location-id");
  assert.equal(payload.factLocationName, "طبقه سوم");
});

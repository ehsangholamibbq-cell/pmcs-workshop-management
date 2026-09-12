import assert from "node:assert/strict";
import test from "node:test";
import { capabilityLabel, initialCapabilities, toCapabilityView } from "../lib/project-state.ts";

test("optional capabilities are not represented as healthy", () => {
  const byKey = Object.fromEntries(initialCapabilities.map((item) => [item.key, item.status]));
  assert.equal(byKey.planning, "not-configured");
  assert.equal(byKey.budget, "setup-required");
  assert.equal(byKey.calendar, "not-configured");
  assert.equal(byKey.finance, "no-data");
  assert.equal(byKey.hse, "not-enabled");
});

test("capability labels distinguish disabled and not configured", () => {
  assert.notEqual(capabilityLabel("not-enabled"), capabilityLabel("not-configured"));
});

test("configured optional module without metrics is not represented as active", () => {
  const capability = toCapabilityView({
    key: "planning",
    label: "Planning / WBS",
    configurationState: "Active",
    metricState: "NoData",
    includedInAssessment: false,
  });

  assert.equal(capability.status, "no-data");
});

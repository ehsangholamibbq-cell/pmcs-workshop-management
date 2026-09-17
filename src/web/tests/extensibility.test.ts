import assert from "node:assert/strict";
import test from "node:test";
import {
  isSafeApplicationRoute,
  listPlatformModules,
  resolveVisibleNavigation,
  type ModuleDescriptorModel,
} from "../lib/extensibility.ts";

test("module catalog request carries actor boundary and disables caching", async () => {
  const originalFetch = globalThis.fetch;
  let capturedUrl = "";
  let capturedInit: RequestInit | undefined;
  globalThis.fetch = async (input, init) => {
    capturedUrl = String(input);
    capturedInit = init;
    return Response.json([]);
  };

  try {
    await listPlatformModules("https://pmcs.test/", { tenantId: "tenant-id", userId: "user-id" });
    assert.equal(capturedUrl, "https://pmcs.test/api/v1/platform/modules");
    assert.equal(capturedInit?.cache, "no-store");
    const headers = new Headers(capturedInit?.headers);
    assert.equal(headers.get("X-Tenant-Id"), "tenant-id");
    assert.equal(headers.get("X-User-Id"), "user-id");
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("navigation is visible only with a known schema, permission, feature and safe route", () => {
  const modules = [referenceModule()];

  assert.deepEqual(resolveVisibleNavigation(
    modules,
    new Set(["platform.modules.read"]),
    new Set(["platform.module-catalog"]),
  ).map(item => item.id), ["platform.modules"]);

  assert.deepEqual(resolveVisibleNavigation(modules, new Set(), new Set(["platform.module-catalog"])), []);
  assert.deepEqual(resolveVisibleNavigation(modules, new Set(["platform.modules.read"]), new Set()), []);
});

test("unknown, legacy and unsafe navigation contracts fail closed", () => {
  const valid = referenceModule();
  const unknown = { ...valid, moduleId: "future.module", schemaVersion: "pmcs.module/v99" };
  const legacy = { ...valid, moduleId: "legacy.module", isLegacy: true };
  const unsafe = {
    ...valid,
    moduleId: "unsafe.module",
    navigationItems: [{ ...valid.navigationItems[0]!, id: "unsafe.navigation", route: "https://evil.test" }],
  };

  const items = resolveVisibleNavigation(
    [unknown, legacy, unsafe],
    new Set(["platform.modules.read"]),
    new Set(["platform.module-catalog"]),
  );

  assert.deepEqual(items, []);
  assert.equal(isSafeApplicationRoute("/admin/platform/modules"), true);
  assert.equal(isSafeApplicationRoute("//evil.test"), false);
  assert.equal(isSafeApplicationRoute("/admin/../escape"), false);
});

test("duplicate navigation identifiers cannot create two shell entries", () => {
  const first = referenceModule();
  const second = { ...referenceModule(), moduleId: "platform.reference" };

  const items = resolveVisibleNavigation(
    [first, second],
    new Set(["platform.modules.read"]),
    new Set(["platform.module-catalog"]),
  );

  assert.equal(items.length, 1);
});

function referenceModule(): ModuleDescriptorModel {
  return {
    schemaVersion: "pmcs.module/v1",
    moduleId: "platform.foundation",
    displayName: "PMCS Platform Foundation",
    version: "1.1.0",
    capabilities: ["platform.module-catalog"],
    dependencies: [],
    migrationOwner: "platform",
    minimumPlatformVersion: "1.1.0",
    isLegacy: false,
    permissions: [{
      key: "platform.modules.read",
      scope: "Tenant",
      riskClass: "Low",
      description: "Read module contracts.",
    }],
    navigationItems: [{
      id: "platform.modules",
      route: "/admin/platform/modules",
      label: "ماژول‌های سامانه",
      permission: "platform.modules.read",
      featureFlag: "platform.module-catalog",
      order: 9_000,
    }],
    tools: [{
      id: "platform.modules.describe",
      description: "Describe a module.",
      inputSchema: "{}",
      outputSchema: "{}",
      permission: "platform.modules.read",
      riskClass: "Low",
      accessMode: "ReadOnly",
    }],
    events: [{ name: "platform.module-catalog.snapshot", version: 1, classification: "Internal" }],
  };
}

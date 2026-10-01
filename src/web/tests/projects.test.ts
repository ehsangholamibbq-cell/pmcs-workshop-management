import assert from "node:assert/strict";
import test from "node:test";
import {
  activateProjectBootstrap,
  activateProject,
  configureProjectCalendar,
  configureProjectSetup,
  configureProjectPlanningMode,
  createProjectBootstrap,
  createProject,
  createProjectLocation,
  executeProjectBootstrap,
  getProjectBootstrapResult,
  getProjectReadiness,
  listProjects,
  listProjectLocations,
  refreshProjectBootstrapPreview,
  retireProjectLocation,
} from "../lib/projects.ts";

test("project registry loads the authenticated actor scope without caching", async () => {
  const originalFetch = globalThis.fetch;
  let capturedUrl = "";
  let capturedInit: RequestInit | undefined;
  globalThis.fetch = async (input, init) => {
    capturedUrl = String(input);
    capturedInit = init;
    return Response.json([]);
  };

  try {
    await listProjects("https://pmcs.test/", { tenantId: "tenant-id", userId: "user-id" });
    assert.equal(capturedUrl, "https://pmcs.test/api/v1/projects");
    assert.equal(capturedInit?.cache, "no-store");
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("project setup creates a draft and activation is revision-controlled", async () => {
  const originalFetch = globalThis.fetch;
  const calls: Array<{ url: string; init?: RequestInit }> = [];
  globalThis.fetch = async (input, init) => {
    calls.push({ url: String(input), init });
    return Response.json({ id: "project-id", revision: calls.length, status: calls.length === 1 ? "Draft" : "Active" });
  };

  try {
    await createProject("https://pmcs.test", { tenantId: "tenant-id", userId: "user-id" }, {
      code: "PRJ-01",
      name: "پروژه اول",
      contractModel: "GeneralContracting",
      planningMode: "SimpleWorkList",
      budgetMode: "SetupRequired",
      qualityMode: "SetupRequired",
      hseMode: "NotEnabled",
      financeMode: "Active",
      procurementMode: "SetupRequired",
      baseCurrencyCode: "IRR",
      timeZone: "Asia/Tehran",
      projectType: "Building",
      executionPhase: "PreConstruction",
      countryCode: "IR",
      region: "تهران",
      startDate: "2026-09-01",
      plannedFinishDate: "2027-09-01",
      shortDescription: "راه‌اندازی کنترل‌شده",
      unitSystem: "Metric",
      dailyCutoffLocalTime: "18:00",
      reportingFrequency: "WorkingDays",
      dailyReportWorkflow: "OneStepApproval",
      offlinePolicyAccepted: true,
      calendarMode: "WorkingWeek",
      workingDays: ["Saturday", "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday"],
    });
    await activateProject(
      "https://pmcs.test",
      { tenantId: "tenant-id", userId: "user-id" },
      "project-id",
      1,
    );

    assert.equal(calls[0]?.url, "https://pmcs.test/api/v1/projects");
    assert.equal(calls[0]?.init?.method, "POST");
    assert.ok(new Headers(calls[0]?.init?.headers).get("Idempotency-Key"));
    assert.equal(JSON.parse(String(calls[0]?.init?.body)).dailyCutoffLocalTime, "18:00:00");
    assert.equal(calls[1]?.url, "https://pmcs.test/api/v1/projects/project-id/activate");
    assert.deepEqual(JSON.parse(String(calls[1]?.init?.body)), { baseRevision: 1 });
    assert.ok(new Headers(calls[1]?.init?.headers).get("Idempotency-Key"));
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("setup can resume from a saved draft and readiness is read from the server gate", async () => {
  const originalFetch = globalThis.fetch;
  const calls: Array<{ url: string; init?: RequestInit }> = [];
  globalThis.fetch = async (input, init) => {
    calls.push({ url: String(input), init });
    return calls.length === 1
      ? Response.json({ id: "project-id", revision: 4, configurationVersion: 3 })
      : Response.json({ projectId: "project-id", isReady: true, completionPercent: 100, items: [] });
  };

  try {
    const identity = { tenantId: "tenant-id", userId: "user-id" };
    await configureProjectSetup("https://pmcs.test", identity, "project-id", 3, {
      code: "PRJ-01", name: "پروژه", contractModel: "GeneralContracting", planningMode: "SimpleWorkList",
      budgetMode: "SetupRequired", qualityMode: "SetupRequired", hseMode: "NotEnabled",
      financeMode: "SetupRequired", procurementMode: "SetupRequired", baseCurrencyCode: "IRR",
      timeZone: "Asia/Tehran", projectType: "Building", executionPhase: "PreConstruction",
      countryCode: "IR", region: "تهران", startDate: "2026-09-01", plannedFinishDate: "2027-09-01",
      shortDescription: "راه‌اندازی کنترل‌شده", unitSystem: "Metric", dailyCutoffLocalTime: "18:00",
      reportingFrequency: "WorkingDays", dailyReportWorkflow: "OneStepApproval", offlinePolicyAccepted: true,
      calendarMode: "WorkingWeek", workingDays: ["Saturday", "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday"],
    });
    await getProjectReadiness("https://pmcs.test", identity, "project-id");

    assert.equal(calls[0]?.url, "https://pmcs.test/api/v1/projects/project-id/setup");
    assert.equal(calls[0]?.init?.method, "PUT");
    assert.equal(JSON.parse(String(calls[0]?.init?.body)).baseRevision, 3);
    assert.equal(JSON.parse(String(calls[0]?.init?.body)).dailyCutoffLocalTime, "18:00:00");
    assert.ok(new Headers(calls[0]?.init?.headers).get("Idempotency-Key"));
    assert.equal(calls[1]?.url, "https://pmcs.test/api/v1/projects/project-id/readiness");
    assert.equal(calls[1]?.init?.cache, "no-store");
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("project locations use scoped versioned command endpoints", async () => {
  const originalFetch = globalThis.fetch;
  const calls: Array<{ url: string; init?: RequestInit }> = [];
  globalThis.fetch = async (input, init) => {
    calls.push({ url: String(input), init });
    return Response.json(calls.length === 1 ? [] : { id: "location-id", revision: 2 });
  };

  try {
    const identity = { tenantId: "tenant-id", userId: "user-id" };
    await listProjectLocations("https://pmcs.test", identity, "project-id");
    await createProjectLocation("https://pmcs.test", identity, "project-id", {
      code: "FLOOR-03",
      name: "طبقه سوم",
      parentLocationId: "root-id",
    });
    await retireProjectLocation("https://pmcs.test", identity, "project-id", {
      id: "location-id",
      revision: 2,
    });

    assert.equal(calls[0]?.url, "https://pmcs.test/api/v1/projects/project-id/locations");
    assert.equal(calls[0]?.init?.cache, "no-store");
    assert.deepEqual(JSON.parse(String(calls[1]?.init?.body)), {
      code: "FLOOR-03",
      name: "طبقه سوم",
      parentLocationId: "root-id",
    });
    assert.ok(new Headers(calls[1]?.init?.headers).get("Idempotency-Key"));
    assert.equal(calls[2]?.url, "https://pmcs.test/api/v1/projects/project-id/locations/location-id/retire");
    assert.deepEqual(JSON.parse(String(calls[2]?.init?.body)), { baseRevision: 2 });
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("calendar can be explicitly left unconfigured", async () => {
  const originalFetch = globalThis.fetch;
  let capturedInit: RequestInit | undefined;
  globalThis.fetch = async (_input, init) => {
    capturedInit = init;
    return Response.json({
      id: "project-id",
      calendarMode: "NotConfigured",
      workingDays: [],
      revision: 3,
    });
  };

  try {
    await configureProjectCalendar(
      "https://pmcs.test",
      { tenantId: "tenant-id", userId: "user-id" },
      "project-id",
      2,
      "NotConfigured",
      [],
    );
    const body = JSON.parse(String(capturedInit?.body));
    const headers = new Headers(capturedInit?.headers);

    assert.equal(capturedInit?.method, "PUT");
    assert.deepEqual(body, { baseRevision: 2, mode: "NotConfigured", workingDays: null });
    assert.ok(headers.get("Idempotency-Key"));
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("planning mode change is revision-controlled and idempotent", async () => {
  const originalFetch = globalThis.fetch;
  let capturedUrl = "";
  let capturedInit: RequestInit | undefined;
  globalThis.fetch = async (input, init) => {
    capturedUrl = String(input);
    capturedInit = init;
    return Response.json({ id: "project-id", planningMode: "Milestones", revision: 4 });
  };

  try {
    await configureProjectPlanningMode(
      "https://pmcs.test/",
      { tenantId: "tenant-id", userId: "user-id" },
      "project-id",
      3,
      "Milestones",
    );
    const headers = new Headers(capturedInit?.headers);
    assert.equal(capturedUrl, "https://pmcs.test/api/v1/projects/project-id/planning-mode");
    assert.deepEqual(JSON.parse(String(capturedInit?.body)), { baseRevision: 3, mode: "Milestones" });
    assert.ok(headers.get("Idempotency-Key"));
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("controlled bootstrap preserves preview digest through execute and independent activation", async () => {
  const originalFetch = globalThis.fetch;
  const calls: Array<{ url: string; init?: RequestInit }> = [];
  globalThis.fetch = async (input, init) => {
    calls.push({ url: String(input), init });
    if (calls.length <= 2) return Response.json({
      planId: "plan-id",
      planRevision: calls.length + 1,
      previewDigest: "digest-one",
      targetProject: { id: "target-id", revision: 1 },
    });
    return Response.json({
      planId: "plan-id",
      planRevision: 4,
      previewDigest: "digest-one",
      targetProject: { id: "target-id", revision: 2 },
      status: calls.length === 4 ? "Activated" : "Completed",
    });
  };

  try {
    const identity = { tenantId: "tenant-id", userId: "user-id" };
    const preview = await createProjectBootstrap("https://pmcs.test", identity, {
      sourceProjectId: "source-id",
      target: {
        code: "NEXT-01", name: "پروژه مقصد", projectType: "Building", executionPhase: "PreConstruction",
        countryCode: "IR", region: "قزوین", startDate: "2026-09-01", plannedFinishDate: "2027-09-01",
        shortDescription: "پروژه مستقل", timeZone: "Asia/Tehran", baseCurrencyCode: "IRR",
        unitSystem: "Metric", offlinePolicyAccepted: true,
      },
      categories: ["BaseSettings", "Locations", "Members"],
      members: [{ userId: "member-id", roleCode: "ProjectManager", accessScope: "Project" }],
      conflictPolicy: "FailOnConflict",
    });
    const refreshed = await refreshProjectBootstrapPreview(
      "https://pmcs.test", identity, "plan-id", preview.planRevision,
    );
    const executed = await executeProjectBootstrap("https://pmcs.test", identity, refreshed);
    await activateProjectBootstrap("https://pmcs.test", identity, executed);

    const createBody = JSON.parse(String(calls[0]?.init?.body));
    assert.equal(calls[0]?.url, "https://pmcs.test/api/v1/project-bootstraps");
    assert.equal(createBody.target.startDate, "2026-09-01");
    assert.deepEqual(createBody.categories, ["BaseSettings", "Locations", "Members"]);
    assert.ok(new Headers(calls[0]?.init?.headers).get("Idempotency-Key"));

    assert.equal(calls[1]?.url, "https://pmcs.test/api/v1/project-bootstraps/plan-id/preview");
    assert.deepEqual(JSON.parse(String(calls[1]?.init?.body)), { baseRevision: 2 });
    assert.deepEqual(JSON.parse(String(calls[2]?.init?.body)), {
      baseRevision: 3,
      previewDigest: "digest-one",
    });
    assert.deepEqual(JSON.parse(String(calls[3]?.init?.body)), {
      baseRevision: 4,
      targetBaseRevision: 2,
    });
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("bootstrap result is a no-cache downloadable read model", async () => {
  const originalFetch = globalThis.fetch;
  let capturedUrl = "";
  let capturedInit: RequestInit | undefined;
  globalThis.fetch = async (input, init) => {
    capturedUrl = String(input);
    capturedInit = init;
    return Response.json({ planId: "plan-id", status: "Completed" });
  };

  try {
    await getProjectBootstrapResult(
      "https://pmcs.test/",
      { tenantId: "tenant-id", userId: "user-id" },
      "plan-id",
    );
    assert.equal(capturedUrl, "https://pmcs.test/api/v1/project-bootstraps/plan-id/result");
    assert.equal(capturedInit?.cache, "no-store");
  } finally {
    globalThis.fetch = originalFetch;
  }
});

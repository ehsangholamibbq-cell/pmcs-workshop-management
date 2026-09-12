import assert from "node:assert/strict";
import test from "node:test";
import { projectNeedsAttention, selectPortfolioProjects } from "../lib/portfolio-view.ts";
import type { PortfolioProjectModel } from "../lib/portfolio.ts";

test("attention order never treats missing data as stable", () => {
  const projects = [project("stable", "Stable"), project("missing", "NoData")];

  const selected = selectPortfolioProjects(projects, {
    query: "",
    lifecycle: "All",
    attention: "All",
    sort: "attention",
  });

  assert.deepEqual(selected.map((item) => item.projectId), ["missing", "stable"]);
});

test("search normalizes Persian and Arabic letter variants", () => {
  const selected = selectPortfolioProjects([project("one", "Stable", "پروژه كيان")], {
    query: "کیان",
    lifecycle: "All",
    attention: "All",
    sort: "name",
  });

  assert.equal(selected.length, 1);
});

test("overdue action makes a project managerially actionable", () => {
  const value = project("one", "Stable");
  const withOverdue = { ...value, actions: { ...value.actions, overdueCount: 1 } };

  assert.equal(projectNeedsAttention(withOverdue), true);
});

function project(id: string, status: PortfolioProjectModel["operational"]["status"], name = id): PortfolioProjectModel {
  return {
    projectId: id,
    projectCode: id,
    projectName: name,
    timeZone: "Asia/Tehran",
    baseCurrencyCode: "IRR",
    lifecycleStatus: "Active",
    contractModel: "NotConfigured",
    planningMode: "None",
    projectManagers: [],
    operational: {
      hasSnapshot: status !== "NoData",
      isOutdated: false,
      needsCalculation: false,
      snapshotId: status === "NoData" ? null : "snapshot-id",
      calculationVersion: status === "NoData" ? null : "project-state-v2",
      asOfDate: status === "NoData" ? null : "2026-09-10",
      calculatedAt: status === "NoData" ? null : "2026-09-10T08:00:00Z",
      status,
      coverageStatus: status === "NoData" ? "NoData" : "Sufficient",
      freshnessStatus: status === "NoData" ? "NoData" : "Current",
      confidenceStatus: status === "NoData" ? "NoData" : "Adequate",
      coveragePercent: status === "NoData" ? null : 100,
      approvedReportDays: status === "NoData" ? null : 7,
      attentionCount: 0,
      highImpactCount: 0,
      criticalImpactCount: 0,
      topReasons: [],
    },
    canReadFinance: true,
    financial: null,
    canReadCommercial: true,
    commercial: null,
    actions: { isVisible: true, openCount: 0, overdueCount: 0, criticalOpenCount: 0, nextDueDate: null },
    capabilities: [],
  };
}

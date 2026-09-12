import type { PortfolioProjectModel, ProjectLifecycleStatus } from "./portfolio.ts";

export type PortfolioAttentionFilter = "All" | "NeedsAttention" | "Stale" | "NoData";
export type PortfolioLifecycleFilter = "All" | ProjectLifecycleStatus;
export type PortfolioSort = "attention" | "name" | "coverage";

export interface PortfolioViewOptions {
  readonly query: string;
  readonly attention: PortfolioAttentionFilter;
  readonly lifecycle: PortfolioLifecycleFilter;
  readonly sort: PortfolioSort;
}

export function selectPortfolioProjects(
  projects: readonly PortfolioProjectModel[],
  options: PortfolioViewOptions,
): PortfolioProjectModel[] {
  const query = normalize(options.query);
  return projects
    .filter((project) => !query || normalize([
      project.projectCode,
      project.projectName,
      ...project.projectManagers,
    ].join(" ")).includes(query))
    .filter((project) => options.lifecycle === "All" || project.lifecycleStatus === options.lifecycle)
    .filter((project) => matchesAttention(project, options.attention))
    .slice()
    .sort(comparator(options.sort));
}

export function projectNeedsAttention(project: PortfolioProjectModel): boolean {
  return project.operational.status === "Critical" ||
    project.operational.status === "AtRisk" ||
    project.operational.isOutdated ||
    project.operational.freshnessStatus === "Stale" ||
    (project.actions.overdueCount ?? 0) > 0 ||
    (project.commercial?.pendingContractApprovalCount ?? 0) > 0 ||
    (project.commercial?.pendingProcurementApprovalCount ?? 0) > 0;
}

function matchesAttention(project: PortfolioProjectModel, filter: PortfolioAttentionFilter): boolean {
  if (filter === "All") return true;
  if (filter === "NeedsAttention") return projectNeedsAttention(project);
  if (filter === "Stale") {
    return project.operational.isOutdated || project.operational.freshnessStatus === "Stale";
  }
  return !project.operational.hasSnapshot || project.operational.status === "NoData";
}

function comparator(sort: PortfolioSort): (left: PortfolioProjectModel, right: PortfolioProjectModel) => number {
  if (sort === "name") {
    return (left, right) => left.projectName.localeCompare(right.projectName, "fa");
  }
  if (sort === "coverage") {
    return (left, right) =>
      (right.operational.coveragePercent ?? -1) - (left.operational.coveragePercent ?? -1) ||
      left.projectName.localeCompare(right.projectName, "fa");
  }
  return (left, right) =>
    attentionRank(right) - attentionRank(left) ||
    (right.actions.overdueCount ?? -1) - (left.actions.overdueCount ?? -1) ||
    left.projectName.localeCompare(right.projectName, "fa");
}

function attentionRank(project: PortfolioProjectModel): number {
  const operationalRank = ({
    Critical: 60,
    AtRisk: 50,
    Watch: 40,
    InsufficientData: 30,
    NoData: 20,
    Stable: 10,
  } as const)[project.operational.status];
  return operationalRank +
    (project.operational.isOutdated ? 5 : 0) +
    (project.operational.freshnessStatus === "Stale" ? 4 : 0) +
    ((project.actions.overdueCount ?? 0) > 0 ? 3 : 0);
}

function normalize(value: string): string {
  return value.trim().toLocaleLowerCase("fa-IR").replaceAll("ي", "ی").replaceAll("ك", "ک");
}

import type {
  CommandCenterCommercialState,
  CommandCenterFinancialState,
  DataConfidenceStatus,
  DataCoverageStatus,
  DataFreshnessStatus,
  ProjectCapability,
  ProjectOperationalStatus,
} from "./command-center.ts";
import { ensureApiSuccess } from "./localization.ts";

export type ProjectLifecycleStatus = "Draft" | "Active" | "OnHold" | "Closing" | "Closed";
export type ContractModel = "NotConfigured" | "GeneralContracting" | "ConstructionManagement" | "LaborOnly" | "Hybrid";
export type PlanningMode = "None" | "SimpleWorkList" | "Milestones" | "WbsBaseline" | "ExternalSchedule";
export type ActionPriority = "Low" | "Medium" | "High" | "Critical";
export type ManagementActionStatus = "Open" | "InProgress" | "Blocked";

export interface PortfolioCommandCenterModel {
  readonly contractVersion: "portfolio-command-center-v1";
  readonly generatedAt: string;
  readonly header: PortfolioHeaderModel;
  readonly projects: readonly PortfolioProjectModel[];
  readonly actionExceptions: readonly PortfolioActionExceptionModel[];
}

export interface PortfolioHeaderModel {
  readonly projectCount: number;
  readonly activeProjectCount: number;
  readonly onHoldProjectCount: number;
  readonly closingProjectCount: number;
  readonly stableProjectCount: number;
  readonly watchProjectCount: number;
  readonly atRiskProjectCount: number;
  readonly criticalProjectCount: number;
  readonly insufficientDataProjectCount: number;
  readonly noDataProjectCount: number;
  readonly staleProjectCount: number;
  readonly outdatedProjectCount: number;
  readonly actionVisibleProjectCount: number;
  readonly openActionCount: number;
  readonly overdueActionCount: number;
  readonly pendingCommercialApprovalCount: number;
  readonly latestSnapshotAt: string | null;
  readonly currencyExposures: readonly PortfolioCurrencyExposureModel[];
}

export interface PortfolioCurrencyExposureModel {
  readonly currencyCode: string;
  readonly financialProjectCount: number;
  readonly commercialProjectCount: number;
  readonly recognizedSpend: number;
  readonly externalNetCash: number;
  readonly totalCommittedAmount: number;
  readonly openCommitmentAmount: number;
}

export interface PortfolioProjectModel {
  readonly projectId: string;
  readonly projectCode: string;
  readonly projectName: string;
  readonly timeZone: string;
  readonly baseCurrencyCode: string;
  readonly lifecycleStatus: ProjectLifecycleStatus;
  readonly contractModel: ContractModel;
  readonly planningMode: PlanningMode;
  readonly projectManagers: readonly string[];
  readonly operational: PortfolioOperationalStateModel;
  readonly canReadFinance: boolean;
  readonly financial: CommandCenterFinancialState | null;
  readonly canReadCommercial: boolean;
  readonly commercial: CommandCenterCommercialState | null;
  readonly actions: PortfolioActionSummaryModel;
  readonly capabilities: readonly ProjectCapability[];
}

export interface PortfolioOperationalStateModel {
  readonly hasSnapshot: boolean;
  readonly isOutdated: boolean;
  readonly needsCalculation: boolean;
  readonly snapshotId: string | null;
  readonly calculationVersion: string | null;
  readonly asOfDate: string | null;
  readonly calculatedAt: string | null;
  readonly status: ProjectOperationalStatus;
  readonly coverageStatus: DataCoverageStatus;
  readonly freshnessStatus: DataFreshnessStatus;
  readonly confidenceStatus: DataConfidenceStatus;
  readonly coveragePercent: number | null;
  readonly approvedReportDays: number | null;
  readonly attentionCount: number;
  readonly highImpactCount: number;
  readonly criticalImpactCount: number;
  readonly topReasons: readonly string[];
}

export interface PortfolioActionSummaryModel {
  readonly isVisible: boolean;
  readonly openCount: number | null;
  readonly overdueCount: number | null;
  readonly criticalOpenCount: number | null;
  readonly nextDueDate: string | null;
}

export interface PortfolioActionExceptionModel {
  readonly actionId: string;
  readonly projectId: string;
  readonly projectCode: string;
  readonly projectName: string;
  readonly title: string;
  readonly assigneeUserId: string;
  readonly assigneeDisplayName: string;
  readonly dueDate: string;
  readonly isOverdue: boolean;
  readonly priority: ActionPriority;
  readonly status: ManagementActionStatus;
  readonly revision: number;
}

interface ApiIdentity {
  readonly tenantId: string;
  readonly userId: string;
}

export async function getPortfolioCommandCenter(
  apiBaseUrl: string,
  identity: ApiIdentity,
): Promise<PortfolioCommandCenterModel> {
  const response = await fetch(`${apiBaseUrl.replace(/\/$/, "")}/api/v1/portfolio/command-center`, {
    headers: {
      "X-Tenant-Id": identity.tenantId,
      "X-User-Id": identity.userId,
    },
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<PortfolioCommandCenterModel>;
}

import { ensureApiSuccess } from "./localization.ts";

export type ProjectOperationalStatus =
  | "NoData"
  | "InsufficientData"
  | "Stable"
  | "Watch"
  | "AtRisk"
  | "Critical";

export type DataCoverageStatus = "NoData" | "Insufficient" | "Sufficient";
export type DataFreshnessStatus = "NoData" | "Current" | "Aging" | "Stale";
export type DataConfidenceStatus = "NoData" | "Low" | "Adequate";
export type ProjectFeatureState = "NotConfigured" | "NotEnabled" | "SetupRequired" | "Active" | "Suspended";
export type CapabilityMetricState = "NotApplicable" | "NoData" | "Available";
export type ProjectAttentionKind = "Issue" | "Stoppage";
export type ProjectAttentionPriority = "Unassessed" | "Low" | "Medium" | "High" | "Critical";
export type ProjectAttentionAgeBand = "New" | "Aging" | "Overdue";
export type AttentionDispositionState = "NeedsTriage" | "ConvertedToAction" | "Dismissed";
export type FinancialStateStatus = "NotConfigured" | "NoData" | "Available";
export type FinancialDataQualityStatus = "NoData" | "Adequate" | "NeedsAttention";
export type BudgetComparisonState = "NotConfigured" | "SetupRequired" | "NoData" | "Available" | "Suspended";
export type CommercialMetricState = "NotConfigured" | "SetupRequired" | "NoData" | "Available" | "Suspended";
export type CommercialDataQualityStatus = "NoData" | "Adequate" | "NeedsAttention";

export interface ProjectAttentionItem {
  readonly sourceReportId: string;
  readonly sourceFactId: string;
  readonly reportDate: string;
  readonly kind: ProjectAttentionKind;
  readonly description: string;
  readonly category: string | null;
  readonly locationName: string | null;
  readonly observedImpact: Exclude<ProjectAttentionPriority, "Unassessed"> | null;
  readonly priority: ProjectAttentionPriority;
  readonly ageDays: number;
  readonly ageBand: ProjectAttentionAgeBand;
  readonly status: "NeedsTriage";
  readonly referenceCode: string | null;
  readonly disposition: AttentionDispositionState;
  readonly actionId: string | null;
  readonly dispositionReason: string | null;
  readonly dispositionAt: string | null;
}

export interface ProjectStateSnapshot {
  readonly snapshotId: string;
  readonly calculationVersion: string;
  readonly projectConfigurationRevision: number;
  readonly asOfDate: string;
  readonly windowStart: string;
  readonly windowEnd: string;
  readonly calculatedAt: string;
  readonly assessmentScope: "ApprovedDailyOperations";
  readonly isPartial: boolean;
  readonly operationalStatus: ProjectOperationalStatus;
  readonly coverageStatus: DataCoverageStatus;
  readonly freshnessStatus: DataFreshnessStatus;
  readonly confidenceStatus: DataConfidenceStatus;
  readonly coverageBasis: "SevenCalendarDays" | "FallbackSevenCalendarDays" | "ConfiguredWorkingDays";
  readonly coveragePercent: number;
  readonly expectedReportDays: number;
  readonly approvedReportDays: number;
  readonly lastApprovedReportDate: string | null;
  readonly approvedFactCount: number;
  readonly progressFactCount: number;
  readonly laborFactCount: number;
  readonly equipmentFactCount: number;
  readonly materialFactCount: number;
  readonly issueCount: number;
  readonly stoppageCount: number;
  readonly highImpactCount: number;
  readonly criticalImpactCount: number;
  readonly oldestAttentionAgeDays: number | null;
  readonly sourceMaxChangedAt: string | null;
  readonly attentionItems: readonly ProjectAttentionItem[];
}

export interface ProjectCapability {
  readonly key: string;
  readonly label: string;
  readonly configurationState: ProjectFeatureState;
  readonly metricState: CapabilityMetricState;
  readonly includedInAssessment: boolean;
}

export interface ProjectStateTrendPoint {
  readonly snapshotId: string;
  readonly asOfDate: string;
  readonly calculatedAt: string;
  readonly operationalStatus: ProjectOperationalStatus;
  readonly coveragePercent: number;
  readonly freshnessStatus: DataFreshnessStatus;
  readonly attentionCount: number;
}

export interface CommandCenterModel {
  readonly projectId: string;
  readonly projectCode: string;
  readonly projectName: string;
  readonly timeZone: string;
  readonly hasSnapshot: boolean;
  readonly isOutdated: boolean;
  readonly canRecalculate: boolean;
  readonly canTriage: boolean;
  readonly canReadFinance: boolean;
  readonly canReadCommercial: boolean;
  readonly snapshot: ProjectStateSnapshot | null;
  readonly financialState: CommandCenterFinancialState | null;
  readonly commercialState: CommandCenterCommercialState | null;
  readonly capabilities: readonly ProjectCapability[];
  readonly trend: readonly ProjectStateTrendPoint[];
}

export interface CommandCenterFinancialState {
  readonly snapshotId: string | null;
  readonly calculationVersion: string;
  readonly asOfDate: string;
  readonly calculatedAt: string;
  readonly currencyCode: string;
  readonly status: FinancialStateStatus;
  readonly dataQualityStatus: FinancialDataQualityStatus;
  readonly budgetComparisonState: BudgetComparisonState;
  readonly postedRecordCount: number;
  readonly totalReceipts: number;
  readonly directPayments: number;
  readonly pettyCashFunding: number;
  readonly pettyCashExpenses: number;
  readonly externalNetCash: number;
  readonly recognizedSpend: number;
  readonly pettyCashBalance: number;
  readonly approvedBudgetAmount: number | null;
  readonly budgetRemainingAmount: number | null;
  readonly budgetConsumedPercent: number | null;
  readonly sourceMaxChangedAt: string | null;
}

export interface CommandCenterCommercialState {
  readonly snapshotId: string | null;
  readonly calculationVersion: string;
  readonly asOfDate: string;
  readonly calculatedAt: string;
  readonly currencyCode: string;
  readonly contractState: CommercialMetricState;
  readonly contractDataQualityStatus: CommercialDataQualityStatus;
  readonly procurementState: CommercialMetricState;
  readonly procurementDataQualityStatus: CommercialDataQualityStatus;
  readonly activePartyCount: number;
  readonly registeredContractCount: number;
  readonly activeContractCount: number;
  readonly contractsWithoutCeilingCount: number;
  readonly pendingContractApprovalCount: number;
  readonly expiredActiveContractCount: number;
  readonly approvedAmendmentCount: number;
  readonly approvedAmendmentDelta: number;
  readonly approvedContractCeilingAmount: number | null;
  readonly purchaseRequestCount: number;
  readonly pendingProcurementApprovalCount: number;
  readonly approvedRequestsAwaitingOrderCount: number;
  readonly openCommitmentCount: number;
  readonly overdueCommitmentCount: number;
  readonly totalCommittedAmount: number;
  readonly openCommitmentAmount: number;
  readonly sourceMaxChangedAt: string | null;
}

interface ApiIdentity {
  readonly tenantId: string;
  readonly userId: string;
}

export async function getCommandCenter(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
): Promise<CommandCenterModel> {
  const response = await fetch(`${normalize(apiBaseUrl)}/api/v1/projects/${projectId}/command-center`, {
    headers: identityHeaders(identity),
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<CommandCenterModel>;
}

export async function recalculateProjectState(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  asOfDate?: string,
): Promise<ProjectStateSnapshot> {
  const response = await fetch(`${normalize(apiBaseUrl)}/api/v1/projects/${projectId}/project-state/recalculate`, {
    method: "POST",
    headers: {
      ...identityHeaders(identity),
      "Content-Type": "application/json",
      "Idempotency-Key": crypto.randomUUID(),
    },
    body: JSON.stringify({ asOfDate: asOfDate ?? null }),
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<ProjectStateSnapshot>;
}

function identityHeaders(identity: ApiIdentity): Record<string, string> {
  return {
    "X-Tenant-Id": identity.tenantId,
    "X-User-Id": identity.userId,
  };
}

function normalize(apiBaseUrl: string): string {
  return apiBaseUrl.replace(/\/$/, "");
}

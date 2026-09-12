import { ensureApiSuccess } from "./localization.ts";

export type AdvisoryInsightType = "ExecutiveSummary" | "EmergingRisk" | "LikelyCause" | "MissingDataWarning";
export type AdvisoryConfidenceBand = "Low" | "Medium" | "High";
export type AdvisoryReviewStatus = "NeedsReview" | "Accepted" | "Dismissed";
export type InsightGenerationStatus = "Pending" | "Processing" | "Succeeded" | "Failed";

export interface AdvisorySuggestedAction {
  readonly title: string;
  readonly rationale: string;
}

export interface AdvisoryInsightOutput {
  readonly insightType: AdvisoryInsightType;
  readonly statement: string;
  readonly evidenceReferences: readonly string[];
  readonly factsUsed: readonly string[];
  readonly assumptions: readonly string[];
  readonly dataGaps: readonly string[];
  readonly confidenceBand: AdvisoryConfidenceBand;
  readonly potentialImpact: string;
  readonly suggestedActions: readonly AdvisorySuggestedAction[];
  readonly suggestedOwnerRole: string;
}

export interface AdvisoryInsightModel {
  readonly insightId: string;
  readonly projectId: string;
  readonly generationRequestId: string;
  readonly snapshotId: string;
  readonly output: AdvisoryInsightOutput;
  readonly includesFinancialData: boolean;
  readonly includesCommercialData: boolean;
  readonly includesActionData: boolean;
  readonly generatedAt: string;
  readonly expiresAt: string;
  readonly isStale: boolean;
  readonly reviewStatus: AdvisoryReviewStatus;
  readonly reviewedAt: string | null;
  readonly reviewComment: string | null;
  readonly revision: number;
}

export interface InsightGenerationRequestModel {
  readonly requestId: string;
  readonly projectId: string;
  readonly status: InsightGenerationStatus;
  readonly snapshotId: string | null;
  readonly insightId: string | null;
  readonly requestedAt: string;
  readonly startedAt: string | null;
  readonly completedAt: string | null;
  readonly attempts: number;
  readonly lastErrorCode: string | null;
  readonly revision: number;
  readonly nextAttemptAt: string | null;
}

export interface AdvisoryInsightListModel {
  readonly providerConfigured: boolean;
  readonly canGenerate: boolean;
  readonly canReview: boolean;
  readonly insights: readonly AdvisoryInsightModel[];
  readonly activeRequests: readonly InsightGenerationRequestModel[];
  readonly recentRequests: readonly InsightGenerationRequestModel[];
}

interface ApiIdentity {
  readonly tenantId: string;
  readonly userId: string;
}

export async function getAdvisoryInsights(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
): Promise<AdvisoryInsightListModel> {
  const response = await fetch(`${normalize(apiBaseUrl)}/api/v1/projects/${projectId}/insights`, {
    headers: identityHeaders(identity),
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<AdvisoryInsightListModel>;
}

export async function requestAdvisoryInsight(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
): Promise<InsightGenerationRequestModel> {
  const response = await fetch(`${normalize(apiBaseUrl)}/api/v1/projects/${projectId}/insight-generation-requests`, {
    method: "POST",
    headers: {
      ...identityHeaders(identity),
      "Content-Type": "application/json",
      "Idempotency-Key": crypto.randomUUID(),
    },
    body: JSON.stringify({}),
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<InsightGenerationRequestModel>;
}

export async function reviewAdvisoryInsight(
  apiBaseUrl: string,
  identity: ApiIdentity,
  projectId: string,
  insight: AdvisoryInsightModel,
  decision: "accept" | "dismiss",
): Promise<AdvisoryInsightModel> {
  const response = await fetch(
    `${normalize(apiBaseUrl)}/api/v1/projects/${projectId}/insights/${insight.insightId}/${decision}`,
    {
      method: "POST",
      headers: {
        ...identityHeaders(identity),
        "Content-Type": "application/json",
        "Idempotency-Key": crypto.randomUUID(),
      },
      body: JSON.stringify({ baseRevision: insight.revision, comment: null }),
    },
  );
  await ensureApiSuccess(response);
  return response.json() as Promise<AdvisoryInsightModel>;
}

function identityHeaders(identity: ApiIdentity): Record<string, string> {
  return { "X-Tenant-Id": identity.tenantId, "X-User-Id": identity.userId };
}

function normalize(apiBaseUrl: string): string {
  return apiBaseUrl.replace(/\/$/, "");
}

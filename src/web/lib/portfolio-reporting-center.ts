export interface PortfolioReportDefinitionView {
  readonly code: string;
  readonly title: string;
  readonly description: string;
  readonly scope: string;
  readonly templateVersion: string;
  readonly supportedFormats: readonly string[];
  readonly dataStatuses: readonly string[];
}

export interface PortfolioReportRunView {
  readonly id: string;
  readonly definitionCode: string;
  readonly status: string;
  readonly pipelineStage: string;
  readonly dataStatus: string | null;
  readonly createdAt: string;
  readonly outputs: readonly PortfolioReportOutputView[];
}

export interface PortfolioReportOutputView {
  readonly id: string;
  readonly format: string;
  readonly fileName: string;
  readonly contentType: string;
  readonly sizeBytes: number;
  readonly sha256: string;
  readonly verificationCode: string;
}

export type PortfolioReportingCenterView =
  | { readonly kind: "ready"; readonly definitions: readonly PortfolioReportDefinitionView[];
      readonly runs: readonly PortfolioReportRunView[] }
  | { readonly kind: "unavailable" }
  | { readonly kind: "forbidden" };

const uuid = /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/iu;
const outputTypes: Readonly<Record<string, { readonly mime: string; readonly extension: string }>> = {
  Pdf: { mime: "application/pdf", extension: ".pdf" },
  Xlsx: { mime: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", extension: ".xlsx" },
};

export function validPortfolioOutput(output: PortfolioReportOutputView): boolean {
  if (!output || !uuid.test(output.id ?? "")) return false;
  const type = outputTypes[output.format];
  return Boolean(type && output.contentType === type.mime &&
    typeof output.fileName === "string" && output.fileName.length <= 180 &&
    !output.fileName.includes("/") && !output.fileName.includes("\\") &&
    !/[\x00-\x1f]/u.test(output.fileName) &&
    output.fileName.toLowerCase().endsWith(type.extension) &&
    Number.isSafeInteger(output.sizeBytes) && output.sizeBytes > 0 &&
    output.sizeBytes <= 128 * 1024 * 1024 &&
    /^[0-9a-f]{64}$/iu.test(output.sha256 ?? ""));
}

/** Tenant-scoped catalog and history; the server filters each run by its pinned project cohort. */
export async function loadPortfolioReportingCenter(apiBaseUrl: string): Promise<PortfolioReportingCenterView> {
  const base = apiBaseUrl.replace(/\/$/u, "") + "/api/v1/portfolio/reports";
  const catalogResponse = await fetch(base + "/catalog", { cache: "no-store" });
  if (catalogResponse.status === 404) return { kind: "unavailable" };
  if ([401, 403].includes(catalogResponse.status)) return { kind: "forbidden" };
  if (!catalogResponse.ok) throw new Error("فهرست گزارش‌های سبد دریافت نشد.");
  const definitions = await catalogResponse.json() as PortfolioReportDefinitionView[];
  if (!Array.isArray(definitions) || definitions.length > 1 || definitions.some((item) =>
    !item || item.code !== "portfolio-summary-certified" || item.scope !== "Portfolio" ||
    typeof item.title !== "string" || typeof item.description !== "string" ||
    typeof item.templateVersion !== "string" || !Array.isArray(item.supportedFormats) ||
    item.supportedFormats.some((format) => format !== "Pdf" && format !== "Xlsx") ||
    !Array.isArray(item.dataStatuses))) {
    throw new Error("فهرست گزارش‌های سبد معتبر نیست.");
  }

  const runsResponse = await fetch(base + "/runs?limit=50", { cache: "no-store" });
  if (runsResponse.status === 404) return { kind: "unavailable" };
  if ([401, 403].includes(runsResponse.status)) return { kind: "forbidden" };
  if (!runsResponse.ok) throw new Error("سابقهٔ گزارش‌های سبد دریافت نشد.");
  const runs = await runsResponse.json() as PortfolioReportRunView[];
  if (!Array.isArray(runs) || runs.length > 50 || runs.some((item) =>
    !item || "projectId" in item || !uuid.test(item.id ?? "") ||
    item.definitionCode !== "portfolio-summary-certified" ||
    typeof item.status !== "string" || typeof item.pipelineStage !== "string" ||
    typeof item.createdAt !== "string" || !Number.isFinite(Date.parse(item.createdAt)) ||
    !Array.isArray(item.outputs) || item.outputs.some((output) =>
      !validPortfolioOutput(output)))) {
    throw new Error("سابقهٔ گزارش‌های سبد با محدودهٔ سازمان سازگار نیست.");
  }
  return { kind: "ready", definitions, runs };
}

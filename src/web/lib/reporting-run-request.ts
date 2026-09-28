import type { ReportRunView } from "./reporting-center.ts";

const noParameterCodes = new Set([
  "executive-project-state-certified", "project-progress-certified",
  "project-financial-position-certified", "project-commercial-procurement-supply-certified",
  "project-technical-office-certified", "project-quality-hse-certified",
  "project-governance-action-certified",
]);

export interface ProjectReportRequest {
  readonly projectId: string;
  readonly clientGeneratedId: string;
  readonly definitionCode: string;
  readonly templateVersion: string;
  readonly format: "Pdf" | "Xlsx";
}

/** One immutable request identity is reused by the caller on interrupted transport. */
export async function requestProjectReport(apiBaseUrl: string,
  input: ProjectReportRequest): Promise<ReportRunView> {
  const uuid = /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/iu;
  if (!uuid.test(input.projectId) || !uuid.test(input.clientGeneratedId) ||
      !noParameterCodes.has(input.definitionCode) || !input.templateVersion ||
      !["Pdf", "Xlsx"].includes(input.format)) {
    throw new Error("درخواست گزارش با قالب مجاز سازگار نیست.");
  }
  const response = await fetch(`${apiBaseUrl.replace(/\/$/u, "")}/api/v1/projects/` +
    `${encodeURIComponent(input.projectId)}/reports/runs`, {
    method: "POST", cache: "no-store",
    headers: { "Content-Type": "application/json", "Idempotency-Key": input.clientGeneratedId },
    body: JSON.stringify({ clientGeneratedId: input.clientGeneratedId,
      definitionCode: input.definitionCode, templateVersion: input.templateVersion,
      asOfUtc: null, formats: [input.format], parameters: {} }),
  });
  if (response.status === 401 || response.status === 403 || response.status === 404) {
    throw new ReportRequestAccessError(response.status);
  }
  if (!response.ok) throw new Error("درخواست گزارش پذیرفته نشد؛ وضعیت را بررسی و دوباره تلاش کنید.");
  const result = await response.json() as ReportRunView;
  if (result.id?.toLowerCase() !== input.clientGeneratedId.toLowerCase() ||
      result.projectId?.toLowerCase() !== input.projectId.toLowerCase() ||
      result.definitionCode !== input.definitionCode || !Array.isArray(result.outputs)) {
    throw new Error("تأیید گزارش با درخواست و پروژه مطابقت ندارد.");
  }
  return result;
}

export class ReportRequestAccessError extends Error {
  readonly status: number;

  constructor(status: number) {
    super("دسترسی به ساخت گزارش تغییر کرده است.");
    this.status = status;
  }
}

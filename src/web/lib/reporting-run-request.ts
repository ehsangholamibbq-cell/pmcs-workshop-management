import type { ReportRunView } from "./reporting-center.ts";
import { isoDateToPersian } from "./persian-date.ts";

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
  readonly parameters?:
    | { readonly dailyReportId: string; readonly includeRevisionChain: boolean }
    | { readonly periodKind: "Weekly" | "Monthly"; readonly periodStartLocalDate: string };
}

export function isValidProjectPeriodStart(kind: "Weekly" | "Monthly", date: string): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/u.test(date)) return false;
  const parsed = new Date(`${date}T00:00:00Z`);
  if (Number.isNaN(parsed.getTime()) || parsed.toISOString().slice(0, 10) !== date) return false;
  return kind === "Weekly" ? parsed.getUTCDay() === 6 : isoDateToPersian(date).day === 1;
}

function normalizedParameters(input: ProjectReportRequest): object {
  if (noParameterCodes.has(input.definitionCode)) {
    if (input.parameters !== undefined) throw new Error("پارامتر گزارش با تعریف آن سازگار نیست.");
    return {};
  }
  if (input.definitionCode === "daily-report-certified" && input.parameters &&
      "dailyReportId" in input.parameters &&
      /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/iu.test(input.parameters.dailyReportId) &&
      typeof input.parameters.includeRevisionChain === "boolean") {
    return { dailyReportId: input.parameters.dailyReportId,
      includeRevisionChain: input.parameters.includeRevisionChain };
  }
  if (input.definitionCode === "project-periodic-certified" && input.parameters &&
      "periodKind" in input.parameters && ["Weekly", "Monthly"].includes(input.parameters.periodKind) &&
      isValidProjectPeriodStart(input.parameters.periodKind, input.parameters.periodStartLocalDate)) {
    return { periodKind: input.parameters.periodKind,
      periodStartLocalDate: input.parameters.periodStartLocalDate };
  }
  throw new Error("ورودی روز یا دورهٔ گزارش معتبر نیست.");
}

/** One immutable request identity is reused by the caller on interrupted transport. */
export async function requestProjectReport(apiBaseUrl: string,
  input: ProjectReportRequest): Promise<ReportRunView> {
  const uuid = /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/iu;
  if (!uuid.test(input.projectId) || !uuid.test(input.clientGeneratedId) ||
      !input.templateVersion ||
      !["Pdf", "Xlsx"].includes(input.format)) {
    throw new Error("درخواست گزارش با قالب مجاز سازگار نیست.");
  }
  const parameters = normalizedParameters(input);
  const response = await fetch(`${apiBaseUrl.replace(/\/$/u, "")}/api/v1/projects/` +
    `${encodeURIComponent(input.projectId)}/reports/runs`, {
    method: "POST", cache: "no-store",
    headers: { "Content-Type": "application/json", "Idempotency-Key": input.clientGeneratedId },
    body: JSON.stringify({ clientGeneratedId: input.clientGeneratedId,
      definitionCode: input.definitionCode, templateVersion: input.templateVersion,
      asOfUtc: null, formats: [input.format], parameters }),
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

import type { PortfolioReportRunView } from "./portfolio-reporting-center.ts";

export interface PortfolioReportRequest {
  readonly clientGeneratedId: string;
  readonly definitionCode: "portfolio-summary-certified";
  readonly templateVersion: string;
  readonly format: "Pdf" | "Xlsx";
}

export class PortfolioReportRequestAccessError extends Error {
  readonly status: number;
  constructor(status: number) {
    super("دسترسی به ساخت گزارش سبد تغییر کرده است.");
    this.status = status;
  }
}

/** The caller retains one immutable identity and payload across transport retries. */
export async function requestPortfolioReport(apiBaseUrl: string,
  input: PortfolioReportRequest): Promise<PortfolioReportRunView> {
  const uuid = /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/iu;
  if (!uuid.test(input.clientGeneratedId) ||
      input.definitionCode !== "portfolio-summary-certified" ||
      !input.templateVersion || !["Pdf", "Xlsx"].includes(input.format)) {
    throw new Error("درخواست گزارش سبد با تعریف مجاز سازگار نیست.");
  }
  const response = await fetch(apiBaseUrl.replace(/\/$/u, "") + "/api/v1/portfolio/reports/runs", {
    method: "POST", cache: "no-store",
    headers: { "Content-Type": "application/json", "Idempotency-Key": input.clientGeneratedId },
    body: JSON.stringify({ clientGeneratedId: input.clientGeneratedId,
      definitionCode: input.definitionCode, templateVersion: input.templateVersion,
      asOfUtc: null, formats: [input.format], parameters: {} }),
  });
  if ([401, 403, 404].includes(response.status)) {
    throw new PortfolioReportRequestAccessError(response.status);
  }
  if (!response.ok) throw new Error("درخواست گزارش سبد پذیرفته نشد؛ وضعیت را بررسی و دوباره تلاش کنید.");
  const result = await response.json() as PortfolioReportRunView;
  if (!result || result.id?.toLowerCase() !== input.clientGeneratedId.toLowerCase() ||
      result.definitionCode !== input.definitionCode || "projectId" in result ||
      !Array.isArray(result.outputs)) {
    throw new Error("تأیید گزارش سبد با درخواست و محدودهٔ سازمان مطابقت ندارد.");
  }
  return result;
}

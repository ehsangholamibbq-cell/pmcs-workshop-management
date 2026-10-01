import { validPortfolioOutput, type PortfolioReportOutputView,
  type PortfolioReportRunView } from "./portfolio-reporting-center.ts";

export class PortfolioReportOutputAccessError extends Error {
  readonly status: number;
  constructor(status: number) {
    super(status === 403 || status === 401 ? "دسترسی به خروجی سبد مجاز نیست." :
      "خروجی گزارش سبد در دسترس نیست.");
    this.status = status;
  }
}

/** The tenant output endpoint rechecks pinned membership and artifact integrity before serving bytes. */
export async function downloadPortfolioReportOutput(apiBaseUrl: string,
  run: PortfolioReportRunView, output: PortfolioReportOutputView): Promise<Blob> {
  if (run.definitionCode !== "portfolio-summary-certified" || run.status !== "Succeeded" ||
      !run.outputs.some((item) => item.id === output.id) ||
      !validPortfolioOutput(output)) {
    throw new Error("مشخصات خروجی سبد معتبر نیست.");
  }
  const url = apiBaseUrl.replace(/\/$/u, "") + "/api/v1/portfolio/reports/outputs/" +
    encodeURIComponent(output.id) + "/content";
  const response = await fetch(url, { cache: "no-store" });
  if ([401, 403, 404].includes(response.status)) throw new PortfolioReportOutputAccessError(response.status);
  if (!response.ok || response.headers.get("content-type")?.split(";")[0]?.trim() !== output.contentType ||
      Number(response.headers.get("content-length") ?? 0) > output.sizeBytes) {
    throw new Error("خروجی سبد با مشخصات ثبت‌شده سازگار نیست.");
  }
  const bytes = await response.arrayBuffer();
  if (bytes.byteLength !== output.sizeBytes) {
    throw new Error("اندازهٔ خروجی سبد با مشخصات ثبت‌شده سازگار نیست.");
  }
  const digest = Array.from(new Uint8Array(await crypto.subtle.digest("SHA-256", bytes)),
    (byte) => byte.toString(16).padStart(2, "0")).join("");
  if (digest !== output.sha256.toLowerCase()) {
    throw new Error("صحت خروجی سبد تأیید نشد.");
  }
  return new Blob([bytes], { type: output.contentType });
}

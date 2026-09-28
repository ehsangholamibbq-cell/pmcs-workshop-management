export interface ReportDefinitionView {
  readonly code: string;
  readonly title: string;
  readonly description: string;
  readonly templateVersion: string;
  readonly supportedFormats: readonly string[];
  readonly dataStatuses: readonly string[];
}

export interface ReportOutputView {
  readonly id: string;
  readonly format: string;
  readonly fileName: string;
  readonly sizeBytes: number;
  readonly contentType: string;
  readonly sha256: string;
  readonly verificationCode: string;
}

export interface ReportRunView {
  readonly id: string;
  readonly projectId: string;
  readonly definitionCode: string;
  readonly status: string;
  readonly pipelineStage: string;
  readonly dataStatus: string | null;
  readonly createdAt: string;
  readonly outputs: readonly ReportOutputView[];
}

export type ProjectReportingCenterView =
  | { readonly kind: "ready"; readonly definitions: readonly ReportDefinitionView[];
      readonly runs: readonly ReportRunView[] }
  | { readonly kind: "unavailable" }
  | { readonly kind: "forbidden" };

/** The current project's authorized catalog and runs; output bytes are a separate access gate. */
export async function loadProjectReportingCenter(apiBaseUrl: string,
  projectId: string): Promise<ProjectReportingCenterView> {
  if (!/^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/iu.test(projectId)) {
    throw new Error("شناسه پروژه معتبر نیست.");
  }
  const base = `${apiBaseUrl.replace(/\/$/u, "")}/api/v1/projects/${encodeURIComponent(projectId)}/reports`;
  const catalogResponse = await fetch(`${base}/catalog`, { cache: "no-store" });
  if (catalogResponse.status === 404) return { kind: "unavailable" };
  if ([401, 403].includes(catalogResponse.status)) return { kind: "forbidden" };
  if (!catalogResponse.ok) throw new Error("فهرست گزارش‌های مجاز دریافت نشد.");
  const definitions = await catalogResponse.json() as ReportDefinitionView[];
  if (!Array.isArray(definitions) || definitions.length > 30 || definitions.some((item) =>
    !item || typeof item.code !== "string" || !/^[a-z0-9-]{3,100}$/u.test(item.code) ||
    typeof item.title !== "string" || typeof item.description !== "string" ||
    typeof item.templateVersion !== "string" ||
    !Array.isArray(item.supportedFormats) || !Array.isArray(item.dataStatuses))) {
    throw new Error("فهرست گزارش‌های مجاز معتبر نیست.");
  }

  const runsResponse = await fetch(`${base}/runs?limit=50`, { cache: "no-store" });
  if (runsResponse.status === 404) return { kind: "unavailable" };
  if ([401, 403].includes(runsResponse.status)) return { kind: "forbidden" };
  if (!runsResponse.ok) throw new Error("سابقهٔ گزارش‌های پروژه دریافت نشد.");
  const runs = await runsResponse.json() as ReportRunView[];
  if (!Array.isArray(runs) || runs.length > 50 || runs.some((item) =>
    !item || item.projectId?.toLowerCase() !== projectId.toLowerCase() ||
    typeof item.id !== "string" || typeof item.definitionCode !== "string" ||
    typeof item.status !== "string" || typeof item.pipelineStage !== "string" ||
    typeof item.createdAt !== "string" || !Number.isFinite(Date.parse(item.createdAt)) ||
    !Array.isArray(item.outputs) || item.outputs.some((output) =>
      !output || typeof output.id !== "string" || typeof output.fileName !== "string" ||
      !Number.isSafeInteger(output.sizeBytes) || output.sizeBytes <= 0 ||
      output.sizeBytes > 128 * 1024 * 1024 || !validOutputType(output) ||
      !/^[0-9a-f]{64}$/iu.test(output.sha256 ?? "") ||
      !/^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/iu.test(output.id)))) {
    throw new Error("سابقهٔ گزارش‌ها با محدودهٔ پروژه سازگار نیست.");
  }
  return { kind: "ready", definitions, runs };
}

const outputTypes: Readonly<Record<string, { readonly mime: string; readonly extension: string }>> = {
  Pdf: { mime: "application/pdf", extension: ".pdf" },
  Xlsx: { mime: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", extension: ".xlsx" },
};

function validOutputType(output: ReportOutputView): boolean {
  const type = outputTypes[output.format];
  return Boolean(type && output.contentType === type.mime &&
    output.fileName.length <= 180 && !output.fileName.includes("/") &&
    !output.fileName.includes("\\") && !/[\x00-\x1f]/u.test(output.fileName) &&
    output.fileName.toLowerCase().endsWith(type.extension));
}

export class ReportOutputAccessError extends Error {
  constructor(readonly status: number) {
    super(status === 403 || status === 401 ? "دسترسی به خروجی گزارش مجاز نیست." :
      "خروجی گزارش در دسترس نیست.");
  }
}

/** Downloads only the current project's output through the server's output gate and verifies bytes. */
export async function downloadProjectReportOutput(apiBaseUrl: string, projectId: string,
  run: ReportRunView, output: ReportOutputView): Promise<Blob> {
  const uuid = /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/iu;
  if (!uuid.test(projectId) || run.projectId.toLowerCase() !== projectId.toLowerCase() ||
      !uuid.test(run.id) || run.status !== "Succeeded" ||
      !run.outputs.some((item) => item.id === output.id) ||
      !uuid.test(output.id) || !validOutputType(output) ||
      !/^[0-9a-f]{64}$/iu.test(output.sha256) ||
      !Number.isSafeInteger(output.sizeBytes) || output.sizeBytes <= 0 ||
      output.sizeBytes > 128 * 1024 * 1024) {
    throw new Error("مشخصات خروجی گزارش معتبر نیست.");
  }
  const url = apiBaseUrl.replace(/\/$/u, "") + "/api/v1/projects/" +
    encodeURIComponent(projectId) + "/reports/outputs/" + encodeURIComponent(output.id) + "/content";
  const response = await fetch(url, { cache: "no-store" });
  if ([401, 403, 404].includes(response.status)) throw new ReportOutputAccessError(response.status);
  if (!response.ok || response.headers.get("content-type")?.split(";")[0]?.trim() !== output.contentType ||
      Number(response.headers.get("content-length") ?? 0) > output.sizeBytes) {
    throw new Error("خروجی گزارش با مشخصات ثبت‌شده سازگار نیست.");
  }
  const bytes = await response.arrayBuffer();
  if (bytes.byteLength !== output.sizeBytes) {
    throw new Error("اندازهٔ خروجی گزارش با مشخصات ثبت‌شده سازگار نیست.");
  }
  const digest = Array.from(new Uint8Array(await crypto.subtle.digest("SHA-256", bytes)),
    (byte) => byte.toString(16).padStart(2, "0")).join("");
  if (digest !== output.sha256.toLowerCase()) {
    throw new Error("صحت خروجی گزارش تأیید نشد.");
  }
  return new Blob([bytes], { type: output.contentType });
}

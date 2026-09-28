import { CollaborationAccessError } from "./collaboration-events.ts";

export interface ProjectMessageConversionLineage {
  readonly id: string;
  readonly messageId: string;
  readonly messageRevision: number;
  readonly destinationType: "Action" | "Issue" | "RFI" | "DailyFact" | "Evidence" | "TechnicalDocument";
  readonly destinationId: string;
  readonly destinationReference: string;
  readonly documents: readonly {
    readonly id: string; readonly sha256: string; readonly versionNumber: number;
    readonly fileName: string; readonly contentType: string; readonly sizeBytes: number;
  }[];
  readonly confirmedBy: string;
  readonly confirmedAt: string;
}

const guid = (value: unknown): value is string => typeof value === "string" &&
  /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/iu.test(value);

/** Authorized, bounded lineage read; no official record is created here. */
export async function loadProjectMessageConversions(apiBaseUrl: string, projectId: string,
  messageId: string, signal?: AbortSignal): Promise<readonly ProjectMessageConversionLineage[]> {
  if (!guid(projectId) || !guid(messageId)) throw new Error("شناسهٔ تبدیل معتبر نیست.");
  const url = `${apiBaseUrl.replace(/\/$/u, "")}/api/v1/projects/${encodeURIComponent(projectId)}` +
    `/collaboration/messages/${encodeURIComponent(messageId)}/conversions`;
  const response = await fetch(url, { cache: "no-store", signal });
  if ([401, 403, 404].includes(response.status)) throw new CollaborationAccessError(response.status);
  if (!response.ok) throw new Error("دریافت تبدیل‌های رسمی کامل نشد.");
  const entries = await response.json() as ProjectMessageConversionLineage[];
  if (!Array.isArray(entries) || entries.length > 100 || entries.some((item) => !item ||
      !guid(item.id) || item.messageId?.toLowerCase() !== messageId.toLowerCase() ||
      !Number.isSafeInteger(item.messageRevision) || item.messageRevision < 1 ||
      !["Action", "Issue", "RFI", "DailyFact", "Evidence", "TechnicalDocument"].includes(item.destinationType) ||
      !guid(item.destinationId) || typeof item.destinationReference !== "string" ||
      !item.destinationReference.trim() || item.destinationReference.length > 250 ||
      !guid(item.confirmedBy) || typeof item.confirmedAt !== "string" ||
      !Number.isFinite(Date.parse(item.confirmedAt)) ||
      !Array.isArray(item.documents) || item.documents.length > 10 ||
      item.documents.some((document) => !document || !guid(document.id) ||
        !/^[a-f0-9]{64}$/iu.test(document.sha256) ||
        !Number.isSafeInteger(document.versionNumber) || document.versionNumber < 1 ||
        typeof document.fileName !== "string" || !document.fileName ||
        document.fileName.length > 255 || typeof document.contentType !== "string" ||
        !document.contentType || document.contentType.length > 150 ||
        !Number.isSafeInteger(document.sizeBytes) || document.sizeBytes < 1)) ||
      new Set(entries.map((item) => item.id.toLowerCase())).size !== entries.length) {
    throw new Error("تبار تبدیل‌های رسمی معتبر نیست.");
  }
  return entries;
}

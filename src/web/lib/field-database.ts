export const legacyFieldDatabaseName = "pmcs-field-v1";
export const fieldDatabaseVersion = 5;
export const operationStoreName = "operations";
export const attachmentStoreName = "attachments";
export const qualitySafetyIntakeStoreName = "quality-safety-intakes";
export const syncMetadataStoreName = "sync-metadata";
export const appliedChangeStoreName = "applied-changes";

export interface LocalIdentityScope {
  readonly tenantId: string;
  readonly userId: string;
}

let activeScope: LocalIdentityScope | null = null;

export function setLocalIdentityScope(tenantId: string, userId: string): void {
  if (!tenantId || !userId) {
    throw new Error("هویت محلی معتبر نیست.");
  }
  activeScope = { tenantId, userId };
  if (typeof indexedDB !== "undefined") {
    indexedDB.deleteDatabase(legacyFieldDatabaseName);
  }
  purgeLegacyLocalStorage();
}

export async function clearLocalIdentityScope(purge = false): Promise<void> {
  const scope = activeScope;
  if (purge && scope && typeof indexedDB !== "undefined") {
    await deleteDatabase(scopedDatabaseName(scope));
    purgeScopedLocalStorage(scope);
  }
  activeScope = null;
}

export function scopedStorageKey(key: string): string {
  const scope = requireLocalIdentityScope();
  return `${key}:${scope.tenantId}:${scope.userId}`;
}

export function currentLocalIdentityScope(): LocalIdentityScope {
  return requireLocalIdentityScope();
}

export function openFieldDatabase(): Promise<IDBDatabase> {
  if (typeof indexedDB === "undefined") {
    return Promise.reject(new Error("پایگاه داده محلی در این مرورگر در دسترس نیست."));
  }

  return new Promise((resolve, reject) => {
    const request = indexedDB.open(scopedDatabaseName(requireLocalIdentityScope()), fieldDatabaseVersion);

    request.onupgradeneeded = () => {
      const database = request.result;
      if (!database.objectStoreNames.contains(operationStoreName)) {
        const operations = database.createObjectStore(operationStoreName, { keyPath: "operationId" });
        operations.createIndex("by-status", "status", { unique: false });
        operations.createIndex("by-project", "projectId", { unique: false });
        operations.createIndex("by-created-at", "createdAtDevice", { unique: false });
      }

      if (!database.objectStoreNames.contains(attachmentStoreName)) {
        const attachments = database.createObjectStore(attachmentStoreName, { keyPath: "attachmentId" });
        attachments.createIndex("by-status", "status", { unique: false });
        attachments.createIndex("by-project", "projectId", { unique: false });
        attachments.createIndex("by-created-at", "createdAtDevice", { unique: false });
      }

      if (!database.objectStoreNames.contains(qualitySafetyIntakeStoreName)) {
        const intakes = database.createObjectStore(qualitySafetyIntakeStoreName, { keyPath: "draftId" });
        intakes.createIndex("by-project", "projectId", { unique: false });
        intakes.createIndex("by-created-at", "createdAtDevice", { unique: false });
      }

      if (!database.objectStoreNames.contains(syncMetadataStoreName)) {
        database.createObjectStore(syncMetadataStoreName, { keyPath: "key" });
      }

      if (!database.objectStoreNames.contains(appliedChangeStoreName)) {
        const changes = database.createObjectStore(appliedChangeStoreName, { keyPath: "changeId" });
        changes.createIndex("by-project-sequence", ["projectId", "sequence"], { unique: false });
        changes.createIndex("by-server-at", "serverAt", { unique: false });
      }
    };

    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error ?? new Error("بازکردن پایگاه داده محلی ممکن نشد."));
    request.onblocked = () => reject(new Error("یک زبانه دیگر برنامه مانع ارتقای پایگاه داده محلی شده است."));
  });
}

export function getOrCreateDeviceId(): string {
  if (typeof localStorage === "undefined") {
    throw new Error("فضای ذخیره‌سازی محلی در این مرورگر در دسترس نیست.");
  }

  const key = "pmcs-device-id";
  const existing = localStorage.getItem(key);
  if (existing) return existing;

  const generated = crypto.randomUUID();
  localStorage.setItem(key, generated);
  return generated;
}

function requireLocalIdentityScope(): LocalIdentityScope {
  if (!activeScope) {
    throw new Error("برای استفاده از داده آفلاین ابتدا باید وارد سامانه شوید.");
  }
  return activeScope;
}

function scopedDatabaseName(scope: LocalIdentityScope): string {
  return `pmcs-field-v3-${scope.tenantId}-${scope.userId}`;
}

function deleteDatabase(name: string): Promise<void> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.deleteDatabase(name);
    request.onsuccess = () => resolve();
    request.onerror = () => reject(request.error ?? new Error("پاک‌سازی داده محلی ناموفق بود."));
    request.onblocked = () => reject(new Error("برای خروج امن، زبانه‌های دیگر سامانه را ببندید."));
  });
}

function purgeScopedLocalStorage(scope: LocalIdentityScope): void {
  if (typeof localStorage === "undefined") return;
  const suffix = `:${scope.tenantId}:${scope.userId}`;
  Object.keys(localStorage)
    .filter((key) => key.endsWith(suffix))
    .forEach((key) => localStorage.removeItem(key));
}

function purgeLegacyLocalStorage(): void {
  if (typeof localStorage === "undefined") return;
  const commandCenter = /^pmcs-command-center:[0-9a-f-]{36}$/iu;
  const dailyReport = /^pmcs-daily-report:[0-9a-f-]{36}:\d{4}-\d{2}-\d{2}$/iu;
  Object.keys(localStorage)
    .filter((key) => commandCenter.test(key) || dailyReport.test(key))
    .forEach((key) => localStorage.removeItem(key));
}

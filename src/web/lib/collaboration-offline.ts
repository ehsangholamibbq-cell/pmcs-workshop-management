import {
  collaborationMessageStoreName,
  currentLocalIdentityScope,
  openFieldDatabase,
} from "./field-database.ts";

export interface QueuedCollaborationMessage {
  readonly clientMessageId: string;
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly body: string;
  readonly replyToMessageId: string | null;
  readonly mentionedUserIds: readonly string[];
  readonly createdAtDevice: string;
  readonly attemptCount: number;
  readonly lastError?: string;
}

export interface CollaborationQueueInput {
  readonly tenantId: string;
  readonly userId: string;
  readonly projectId: string;
  readonly body: string;
  readonly replyToMessageId?: string | null;
  readonly mentionedUserIds?: readonly string[];
}

export interface CollaborationSyncSummary {
  readonly sent: number;
  readonly deferred: number;
  readonly remaining: number;
}

const activeSyncs = new Map<string, Promise<CollaborationSyncSummary>>();

export function createQueuedCollaborationMessage(input: CollaborationQueueInput): QueuedCollaborationMessage {
  const body = input.body.replace(/\r\n/gu, "\n").trim();
  if (!input.projectId || !input.tenantId || !input.userId ||
      body.length < 1 || body.length > 4_000 || /[\u0000-\u0008\u000b-\u001f\u007f]/u.test(body)) {
    throw new Error("متن پیام یا محدوده پروژه معتبر نیست.");
  }
  const mentions = [...new Set(input.mentionedUserIds ?? [])].sort();
  if (mentions.length > 20 || mentions.some((id) => !id || id === input.userId)) {
    throw new Error("نام‌بردن اعضای پروژه معتبر نیست.");
  }
  return {
    clientMessageId: crypto.randomUUID(),
    tenantId: input.tenantId,
    userId: input.userId,
    projectId: input.projectId,
    body,
    replyToMessageId: input.replyToMessageId ?? null,
    mentionedUserIds: mentions,
    createdAtDevice: new Date().toISOString(),
    attemptCount: 0,
  };
}

export async function enqueueCollaborationMessage(input: CollaborationQueueInput): Promise<QueuedCollaborationMessage> {
  assertCurrentIdentity(input.tenantId, input.userId);
  const message = createQueuedCollaborationMessage(input);
  const database = await openFieldDatabase();
  try {
    await writeMessage(database, message);
  } finally {
    database.close();
  }
  return message;
}

export async function listQueuedCollaborationMessages(projectId: string): Promise<readonly QueuedCollaborationMessage[]> {
  const identity = currentLocalIdentityScope();
  const database = await openFieldDatabase();
  try {
    return (await readProject(database, projectId)).filter((message) =>
      message.tenantId === identity.tenantId && message.userId === identity.userId);
  } finally {
    database.close();
  }
}

export function syncCollaborationMessages(apiBaseUrl: string,
  projectId: string): Promise<CollaborationSyncSummary> {
  const identity = currentLocalIdentityScope();
  const scope = `${identity.tenantId}:${identity.userId}:${projectId}`;
  const active = activeSyncs.get(scope);
  if (active) return active;
  const work = syncProject(apiBaseUrl, projectId, identity).finally(() => activeSyncs.delete(scope));
  activeSyncs.set(scope, work);
  return work;
}

async function syncProject(apiBaseUrl: string, projectId: string,
  identity: { tenantId: string; userId: string }): Promise<CollaborationSyncSummary> {
  const database = await openFieldDatabase();
  let sent = 0;
  let deferred = 0;
  try {
    const messages = (await readProject(database, projectId))
      .filter((item) => item.tenantId === identity.tenantId && item.userId === identity.userId)
      .sort((a, b) => a.createdAtDevice.localeCompare(b.createdAtDevice));
    for (const item of messages) {
      assertCurrentIdentity(identity.tenantId, identity.userId);
      try {
        const response = await fetch(`${apiBaseUrl.replace(/\/$/u, "")}/api/v1/projects/` +
          `${encodeURIComponent(projectId)}/collaboration/messages`, {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "Idempotency-Key": item.clientMessageId,
          },
          body: JSON.stringify({ clientMessageId: item.clientMessageId,
            body: item.body, replyToMessageId: item.replyToMessageId,
            mentionedUserIds: item.mentionedUserIds }),
        });
        assertCurrentIdentity(identity.tenantId, identity.userId);
        if (response.ok) {
          const result = await response.json() as { clientMessageId?: string };
          if (result.clientMessageId !== item.clientMessageId) {
            throw new Error("شناسه پیام پاسخ با صف آفلاین مطابقت ندارد.");
          }
          await deleteMessage(database, item.clientMessageId);
          sent += 1;
          continue;
        }
        await writeMessage(database, { ...item, attemptCount: item.attemptCount + 1,
          lastError: `HTTP ${response.status}` });
        deferred += 1;
        // Permission changes and conflicts require a fresh user decision; preserve the item.
        if (response.status >= 400 && response.status < 500) break;
      } catch (error) {
        // Interrupted transport keeps the same client ID and payload for a later retry.
        assertCurrentIdentity(identity.tenantId, identity.userId);
        await writeMessage(database, { ...item, attemptCount: item.attemptCount + 1,
          lastError: error instanceof Error ? error.message : "ارتباط قطع شد." });
        deferred += 1;
        break;
      }
    }
    const remaining = (await readProject(database, projectId)).filter((item) =>
      item.tenantId === identity.tenantId && item.userId === identity.userId).length;
    return { sent, deferred, remaining };
  } finally {
    database.close();
  }
}

function assertCurrentIdentity(tenantId: string, userId: string): void {
  const current = currentLocalIdentityScope();
  if (current.tenantId !== tenantId || current.userId !== userId) {
    throw new Error("صف گفت‌وگو با هویت نشست فعلی هم‌خوان نیست.");
  }
}

function readProject(database: IDBDatabase, projectId: string): Promise<QueuedCollaborationMessage[]> {
  return new Promise((resolve, reject) => {
    const rows: QueuedCollaborationMessage[] = [];
    const transaction = database.transaction(collaborationMessageStoreName, "readonly");
    const request = transaction.objectStore(collaborationMessageStoreName)
      .index("by-project").openCursor(IDBKeyRange.only(projectId));
    request.onsuccess = () => {
      const cursor = request.result;
      if (!cursor) return resolve(rows);
      rows.push(cursor.value as QueuedCollaborationMessage);
      cursor.continue();
    };
    request.onerror = () => reject(request.error ?? new Error("خواندن صف گفت‌وگو ممکن نشد."));
  });
}

function writeMessage(database: IDBDatabase, item: QueuedCollaborationMessage): Promise<void> {
  return new Promise((resolve, reject) => {
    const transaction = database.transaction(collaborationMessageStoreName, "readwrite");
    transaction.objectStore(collaborationMessageStoreName).put(item);
    transaction.oncomplete = () => resolve();
    transaction.onerror = () => reject(transaction.error ?? new Error("ثبت صف گفت‌وگو ممکن نشد."));
  });
}

function deleteMessage(database: IDBDatabase, clientMessageId: string): Promise<void> {
  return new Promise((resolve, reject) => {
    const transaction = database.transaction(collaborationMessageStoreName, "readwrite");
    transaction.objectStore(collaborationMessageStoreName).delete(clientMessageId);
    transaction.oncomplete = () => resolve();
    transaction.onerror = () => reject(transaction.error ?? new Error("حذف پیام ارسال‌شده از صف ممکن نشد."));
  });
}

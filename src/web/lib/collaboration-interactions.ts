import { CollaborationAccessError } from "./collaboration-events.ts";
import type { ProjectConversationMessage } from "./collaboration-room.ts";

function roomUrl(apiBaseUrl: string, projectId: string): string {
  if (!/^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/iu.test(projectId)) {
    throw new Error("شناسه پروژه معتبر نیست.");
  }
  return `${apiBaseUrl.replace(/\/$/u, "")}/api/v1/projects/${encodeURIComponent(projectId)}/collaboration`;
}

async function checked(response: Response): Promise<Response> {
  if ([401, 403, 404].includes(response.status)) throw new CollaborationAccessError(response.status);
  if (!response.ok) throw new Error("عملیات گفت‌وگوی پروژه کامل نشد.");
  return response;
}

export async function searchProjectConversation(apiBaseUrl: string, projectId: string,
  query: string): Promise<readonly ProjectConversationMessage[]> {
  const term = query.trim();
  if (term.length < 2 || term.length > 120) throw new Error("عبارت جست‌وجو باید بین ۲ تا ۱۲۰ نویسه باشد.");
  const response = await checked(await fetch(`${roomUrl(apiBaseUrl, projectId)}/messages/search?q=${encodeURIComponent(term)}`,
    { cache: "no-store" }));
  const messages = await response.json() as ProjectConversationMessage[];
  if (!Array.isArray(messages) || messages.length > 50 || messages.some((message) =>
    !message || message.projectId?.toLowerCase() !== projectId.toLowerCase() ||
    typeof message.id !== "string" || typeof message.body !== "string" ||
    message.deletedAt || message.redactedAt || !Number.isSafeInteger(message.sequence))) {
    throw new Error("نتیجهٔ جست‌وجو با محدودهٔ پروژه سازگار نیست.");
  }
  return messages;
}

export interface ProjectConversationUnread {
  readonly lastReadSequence: number;
  readonly unreadCount: number;
}

export async function loadProjectConversationUnread(apiBaseUrl: string,
  projectId: string): Promise<ProjectConversationUnread> {
  const response = await checked(await fetch(`${roomUrl(apiBaseUrl, projectId)}/unread`, { cache: "no-store" }));
  const unread = await response.json() as ProjectConversationUnread;
  if (!Number.isSafeInteger(unread.lastReadSequence) || unread.lastReadSequence < 0 ||
      !Number.isSafeInteger(unread.unreadCount) || unread.unreadCount < 0) {
    throw new Error("نشانگر خواندن گفت‌وگو معتبر نیست.");
  }
  return unread;
}

export async function markProjectConversationRead(apiBaseUrl: string, projectId: string,
  lastReadSequence: number): Promise<number> {
  if (!Number.isSafeInteger(lastReadSequence) || lastReadSequence < 0) {
    throw new Error("نشانگر خواندن گفت‌وگو معتبر نیست.");
  }
  const response = await checked(await fetch(`${roomUrl(apiBaseUrl, projectId)}/read-cursor`, {
    method: "PUT", headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ lastReadSequence }), cache: "no-store",
  }));
  const result = await response.json() as { lastReadSequence?: number };
  if (!Number.isSafeInteger(result.lastReadSequence) || result.lastReadSequence! < lastReadSequence) {
    throw new Error("تأیید نشانگر خواندن معتبر نیست.");
  }
  return result.lastReadSequence!;
}

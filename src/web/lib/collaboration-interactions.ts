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

export const PROJECT_REACTION_EMOJIS = ["👍", "✅", "⚠️", "❤️"] as const;
export type ProjectReactionEmoji = (typeof PROJECT_REACTION_EMOJIS)[number];

export class CollaborationRevisionConflict extends Error {
  readonly currentRevision: number | null;

  constructor(currentRevision: number | null) {
    super("نسخهٔ پیام تغییر کرده است؛ متن تازه را ببینید و دربارهٔ پیش‌نویس تصمیم بگیرید.");
    this.currentRevision = currentRevision;
  }
}

export interface ProjectMessageReaction {
  readonly emoji: ProjectReactionEmoji;
  readonly count: number;
  readonly reactedByMe: boolean;
}

export interface ProjectMessageReactionsView {
  readonly canReact: boolean;
  readonly reactions: readonly ProjectMessageReaction[];
}

function messageUrl(apiBaseUrl: string, projectId: string, messageId: string): string {
  if (!/^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/iu.test(messageId)) {
    throw new Error("شناسه پیام معتبر نیست.");
  }
  return `${roomUrl(apiBaseUrl, projectId)}/messages/${encodeURIComponent(messageId)}`;
}

function messageReactionsUrl(apiBaseUrl: string, projectId: string, messageId: string): string {
  return `${messageUrl(apiBaseUrl, projectId, messageId)}/reactions`;
}

export async function editOwnProjectMessage(apiBaseUrl: string, projectId: string,
  message: ProjectConversationMessage, body: string, idempotencyKey: string): Promise<ProjectConversationMessage> {
  const normalized = body.replace(/\r\n/gu, "\n").trim();
  if (!Number.isSafeInteger(message.revision) || message.revision < 1 ||
      !/^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/iu.test(idempotencyKey) ||
      message.projectId.toLowerCase() !== projectId.toLowerCase() ||
      !normalized || normalized.length > 4000 || normalized === message.body ||
      /[\x00-\x08\x0b-\x1f\x7f]/u.test(normalized)) {
    throw new Error("پیش‌نویس ویرایش پیام معتبر نیست.");
  }
  const response = await fetch(messageUrl(apiBaseUrl, projectId, message.id), {
    method: "PATCH", cache: "no-store",
    headers: { "Content-Type": "application/json", "Idempotency-Key": idempotencyKey },
    body: JSON.stringify({ baseRevision: message.revision, body: normalized }),
  });
  if (response.status === 409) {
    const conflict = await response.json().catch(() => null) as { currentRevision?: number } | null;
    const current = conflict?.currentRevision;
    throw new CollaborationRevisionConflict(Number.isSafeInteger(current) && current! > 0 ? current! : null);
  }
  await checked(response);
  const result = await response.json() as ProjectConversationMessage;
  if (result?.id?.toLowerCase() !== message.id.toLowerCase() ||
      result.projectId?.toLowerCase() !== projectId.toLowerCase() ||
      result.authorUserId?.toLowerCase() !== message.authorUserId.toLowerCase() ||
      result.sequence !== message.sequence || result.createdAt !== message.createdAt ||
      result.body !== normalized || !Number.isSafeInteger(result.revision) ||
      result.revision <= message.revision ||
      typeof result.editedAt !== "string" || !Number.isFinite(Date.parse(result.editedAt)) ||
      result.deletedAt || result.redactedAt) {
    throw new Error("تأیید ویرایش پیام معتبر نیست.");
  }
  return result;
}

export async function setProjectMessagePin(apiBaseUrl: string, projectId: string,
  messageId: string, pinned: boolean): Promise<string | null> {
  const response = await checked(await fetch(`${messageUrl(apiBaseUrl, projectId, messageId)}/pin`,
    { method: pinned ? "PUT" : "DELETE", cache: "no-store" }));
  const result = await response.json() as ProjectConversationMessage;
  if (result?.id?.toLowerCase() !== messageId.toLowerCase() ||
      result.projectId?.toLowerCase() !== projectId.toLowerCase() ||
      result.deletedAt || result.redactedAt ||
      (pinned ? typeof result.pinnedAt !== "string" || !Number.isFinite(Date.parse(result.pinnedAt))
        : result.pinnedAt !== null)) {
    throw new Error("تأیید سنجاق پیام معتبر نیست.");
  }
  return result.pinnedAt;
}

export async function loadProjectMessageReactions(apiBaseUrl: string, projectId: string,
  messageId: string, signal?: AbortSignal): Promise<ProjectMessageReactionsView> {
  const response = await checked(await fetch(messageReactionsUrl(apiBaseUrl, projectId, messageId),
    { cache: "no-store", signal }));
  const value = await response.json() as {
    messageId?: string; canReact?: boolean; reactions?: ProjectMessageReaction[];
  };
  if (value?.messageId?.toLowerCase() !== messageId.toLowerCase() ||
      typeof value.canReact !== "boolean" ||
      !Array.isArray(value.reactions) || value.reactions.length > PROJECT_REACTION_EMOJIS.length ||
      value.reactions.some((reaction) => !reaction ||
        !PROJECT_REACTION_EMOJIS.some((emoji) => emoji === reaction.emoji) ||
        !Number.isSafeInteger(reaction.count) || reaction.count < 1 ||
        typeof reaction.reactedByMe !== "boolean" ||
        (reaction.reactedByMe && reaction.count < 1)) ||
      new Set(value.reactions.map((reaction) => reaction.emoji)).size !== value.reactions.length) {
    throw new Error("وضعیت واکنش‌های پیام معتبر نیست.");
  }
  return {
    canReact: value.canReact,
    reactions: PROJECT_REACTION_EMOJIS.map((emoji) =>
      value.reactions!.find((reaction) => reaction.emoji === emoji) ??
        { emoji, count: 0, reactedByMe: false }),
  };
}

export async function setProjectMessageReaction(apiBaseUrl: string, projectId: string,
  messageId: string, emoji: ProjectReactionEmoji, reacted: boolean): Promise<void> {
  if (!PROJECT_REACTION_EMOJIS.some((allowed) => allowed === emoji)) {
    throw new Error("این واکنش پشتیبانی نمی‌شود.");
  }
  const response = await checked(await fetch(
    `${messageReactionsUrl(apiBaseUrl, projectId, messageId)}/${encodeURIComponent(emoji)}`,
    { method: reacted ? "PUT" : "DELETE", cache: "no-store" }));
  if (!reacted) return;
  const result = await response.json() as { messageId?: string; emoji?: string; reacted?: boolean };
  if (result?.messageId?.toLowerCase() !== messageId.toLowerCase() ||
      result.emoji !== emoji || result.reacted !== true) {
    throw new Error("تأیید واکنش پیام معتبر نیست.");
  }
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

export interface ProjectConversationMessage {
  readonly id: string;
  readonly projectId: string;
  readonly sequence: number;
  readonly authorUserId: string;
  readonly body: string;
  readonly createdAt: string;
  readonly replyToMessageId: string | null;
  readonly pinnedAt: string | null;
  readonly editedAt: string | null;
  readonly deletedAt: string | null;
  readonly redactedAt: string | null;
}

export type ProjectConversationView =
  | { readonly kind: "ready"; readonly messages: readonly ProjectConversationMessage[];
      readonly lastSequence: number; readonly canModerate: boolean; readonly canUpload: boolean }
  | { readonly kind: "unavailable" }
  | { readonly kind: "forbidden" };

/** Reads only the current project's newest bounded window through the BFF. */
export async function loadProjectConversation(apiBaseUrl: string, projectId: string): Promise<ProjectConversationView> {
  if (!/^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/iu.test(projectId)) {
    throw new Error("شناسه پروژه معتبر نیست.");
  }
  const url = `${apiBaseUrl.replace(/\/$/u, "")}/api/v1/projects/${encodeURIComponent(projectId)}/collaboration`;
  const room = await fetch(url, { cache: "no-store" });
  if (room.status === 404) return { kind: "unavailable" };
  if (room.status === 403) return { kind: "forbidden" };
  if (!room.ok) throw new Error("دریافت گفت‌وگوی پروژه انجام نشد.");
  const roomValue = await room.json() as {
    projectId?: string; lastSequence?: number; canModerate?: boolean; canUpload?: boolean;
  };
  const lastSequence = roomValue.lastSequence;
  if (roomValue.projectId?.toLowerCase() !== projectId.toLowerCase() ||
      typeof lastSequence !== "number" || !Number.isSafeInteger(lastSequence) || lastSequence < 0 ||
      (roomValue.canModerate !== undefined && typeof roomValue.canModerate !== "boolean") ||
      (roomValue.canUpload !== undefined && typeof roomValue.canUpload !== "boolean")) {
    throw new Error("محدوده گفت‌وگوی پروژه معتبر نیست.");
  }

  const after = Math.max(0, lastSequence - 100);
  const response = await fetch(`${url}/messages?after=${after}`, { cache: "no-store" });
  if (response.status === 404) return { kind: "unavailable" };
  if (response.status === 403) return { kind: "forbidden" };
  if (!response.ok) throw new Error("دریافت پیام‌های پروژه انجام نشد.");
  const page = await response.json() as { messages?: ProjectConversationMessage[]; nextSequence?: number };
  const messages = page.messages;
  const nextSequence = page.nextSequence;
  if (!Array.isArray(messages) || messages.length > 100 ||
      typeof nextSequence !== "number" || !Number.isSafeInteger(nextSequence) ||
      messages.some((message, index) => !message ||
        message.projectId?.toLowerCase() !== projectId.toLowerCase() ||
        typeof message.id !== "string" || typeof message.authorUserId !== "string" ||
        typeof message.body !== "string" || typeof message.createdAt !== "string" ||
        !Number.isFinite(Date.parse(message.createdAt)) ||
        !Number.isSafeInteger(message.sequence) || message.sequence <= after ||
        (index > 0 && message.sequence <= messages[index - 1].sequence)) ||
      (messages.length > 0 && nextSequence < messages[messages.length - 1].sequence)) {
    throw new Error("فهرست پیام‌های پروژه معتبر نیست.");
  }
  return { kind: "ready", messages, lastSequence,
    canModerate: roomValue.canModerate === true, canUpload: roomValue.canUpload === true };
}

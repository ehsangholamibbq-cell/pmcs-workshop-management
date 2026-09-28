export interface CollaborationEvent {
  readonly messageId: string;
  readonly projectId: string;
  readonly sequence: number;
  readonly createdAt: string;
}

interface CollaborationEventPage {
  readonly events: readonly CollaborationEvent[];
  readonly nextSequence: number;
}

// The authenticated BFF supports HTTP streaming requests. Every reconnect starts
// from the last durable sequence; live WebSocket frames use the same event shape.
export async function watchCollaborationEvents(apiBaseUrl: string, projectId: string,
  after: number, onEvent: (event: CollaborationEvent) => Promise<void> | void,
  signal: AbortSignal): Promise<void> {
  if (!projectId || !Number.isSafeInteger(after) || after < 0) {
    throw new Error("محدوده یا نشانگر گفت‌وگو معتبر نیست.");
  }
  let cursor = after;
  let retryDelay = 500;
  while (!signal.aborted) {
    try {
      const url = `${apiBaseUrl.replace(/\/$/u, "")}/api/v1/projects/` +
        `${encodeURIComponent(projectId)}/collaboration/events?after=${cursor}&waitSeconds=20`;
      const response = await fetch(url, { cache: "no-store", signal });
      if (response.status === 401 || response.status === 403 || response.status === 404) {
        throw new CollaborationAccessError(response.status);
      }
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      const page = await response.json() as CollaborationEventPage;
      if (!Array.isArray(page.events) || !Number.isSafeInteger(page.nextSequence)) {
        throw new Error("پاسخ رویدادهای پروژه معتبر نیست.");
      }
      for (const event of page.events) {
        if (event.projectId !== projectId || !Number.isSafeInteger(event.sequence) ||
            event.sequence <= cursor) throw new Error("محدوده رویداد پروژه نامعتبر است.");
        await onEvent(event);
        cursor = event.sequence;
      }
      if (page.nextSequence < cursor) throw new Error("نشانگر رویدادها عقب‌گرد کرد.");
      retryDelay = 500;
    } catch (error) {
      if (signal.aborted) return;
      if (error instanceof CollaborationAccessError) throw error;
      await new Promise<void>((resolve) => {
        const timeout = setTimeout(resolve, retryDelay);
        signal.addEventListener("abort", () => { clearTimeout(timeout); resolve(); }, { once: true });
      });
      retryDelay = Math.min(retryDelay * 2, 5_000);
    }
  }
}

export class CollaborationAccessError extends Error {
  readonly status: number;

  constructor(status: number) {
    super("دسترسی به گفت‌وگوی پروژه تغییر کرده است.");
    this.status = status;
  }
}

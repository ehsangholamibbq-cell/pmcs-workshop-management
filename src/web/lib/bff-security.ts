const allowedRequestHeaders = new Set([
  "accept",
  "content-type",
  "if-match",
  "if-none-match",
  "idempotency-key",
  "x-correlation-id",
  "x-pmcs-sync-session",
]);

const forwardedResponseHeaders = new Set([
  "content-disposition",
  "content-length",
  "content-type",
  "etag",
  "last-modified",
  "retry-after",
  "x-correlation-id",
]);

export function pmcsUpstreamPath(parts: readonly string[]): string {
  if (parts.length < 2 || parts[0] !== "api" || parts[1] !== "v1") {
    throw new Error("مسیر دروازه سرویس معتبر نیست.");
  }
  if (parts.some((part) => !part || part === "." || part === ".." || /[\\/]/u.test(part))) {
    throw new Error("مسیر دروازه سرویس معتبر نیست.");
  }
  return `/${parts.map((part) => encodeURIComponent(part)).join("/")}`;
}

export function upstreamRequestHeaders(source: Headers, accessToken: string): Headers {
  const headers = new Headers();
  source.forEach((value, key) => {
    if (allowedRequestHeaders.has(key.toLowerCase())) {
      headers.set(key, value);
    }
  });
  headers.set("Authorization", `Bearer ${accessToken}`);
  return headers;
}

export function browserResponseHeaders(source: Headers): Headers {
  const headers = new Headers();
  source.forEach((value, key) => {
    if (forwardedResponseHeaders.has(key.toLowerCase())) {
      headers.set(key, value);
    }
  });
  headers.set("Cache-Control", "no-store");
  headers.set("Pragma", "no-cache");
  return headers;
}

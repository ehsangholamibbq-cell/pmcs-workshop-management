import { auth, authEnvironment } from "@/lib/auth";
import {
  browserResponseHeaders,
  pmcsUpstreamPath,
  upstreamRequestHeaders,
} from "@/lib/bff-security";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

interface RouteContext {
  readonly params: Promise<{ path: string[] }>;
}

async function proxyToPmcs(request: Request, context: RouteContext): Promise<Response> {
  const session = await auth.api.getSession({ headers: request.headers });
  if (!session) {
    return problem(401, "برای ادامه دوباره وارد سامانه شوید.", "session.required");
  }

  const accounts = await auth.api.listUserAccounts({ headers: request.headers });
  const keycloakAccount = accounts.find((account) => account.providerId === "keycloak");
  if (!keycloakAccount) {
    return problem(401, "حساب ورود سازمانی به نشست متصل نیست.", "session.identity_missing");
  }

  let accessToken: string;
  try {
    const token = await auth.api.getAccessToken({
      headers: request.headers,
      body: { accountId: keycloakAccount.id },
    });
    accessToken = token.accessToken;
  } catch {
    return problem(401, "نشست منقضی شده است؛ دوباره وارد سامانه شوید.", "session.expired");
  }

  let path: string;
  try {
    path = pmcsUpstreamPath((await context.params).path);
  } catch {
    return problem(404, "مسیر درخواستی وجود ندارد.", "route.not_found");
  }

  const incomingUrl = new URL(request.url);
  const upstreamUrl = new URL(path, `${authEnvironment.internalApiUrl}/`);
  upstreamUrl.search = incomingUrl.search;
  const body = request.method === "GET" || request.method === "HEAD"
    ? undefined
    : await request.arrayBuffer();

  try {
    const response = await fetch(upstreamUrl, {
      method: request.method,
      headers: upstreamRequestHeaders(request.headers, accessToken),
      body,
      cache: "no-store",
      redirect: "manual",
      signal: request.signal,
    });
    return new Response(response.body, {
      status: response.status,
      statusText: response.statusText,
      headers: browserResponseHeaders(response.headers),
    });
  } catch {
    return problem(503, "سرویس اصلی در دسترس نیست؛ کمی بعد دوباره تلاش کنید.", "service.unavailable");
  }
}

function problem(status: number, title: string, code: string): Response {
  return Response.json(
    { status, title, code },
    {
      status,
      headers: {
        "Cache-Control": "no-store",
        "Content-Type": "application/problem+json",
      },
    },
  );
}

export const GET = proxyToPmcs;
export const HEAD = proxyToPmcs;
export const POST = proxyToPmcs;
export const PUT = proxyToPmcs;
export const PATCH = proxyToPmcs;
export const DELETE = proxyToPmcs;

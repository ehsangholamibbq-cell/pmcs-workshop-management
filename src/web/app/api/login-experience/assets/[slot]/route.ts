import { readAuthEnvironment } from "@/lib/auth-environment";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

interface RouteContext {
  readonly params: Promise<{ slot: string }>;
}

export async function GET(request: Request, context: RouteContext): Promise<Response> {
  const { slot } = await context.params;
  const versionText = new URL(request.url).searchParams.get("version") ?? "";
  const version = Number(versionText);
  if (!["logo", "hero"].includes(slot) || !Number.isSafeInteger(version) || version <= 0) {
    return Response.json({ code: "login-experience.asset.request.invalid" }, { status: 400 });
  }

  const environment = readAuthEnvironment();
  const endpoint = new URL(
    `/api/v1/public/login-experience/assets/${slot}`,
    environment.internalApiUrl,
  );
  endpoint.searchParams.set("tenantId", environment.loginTenantId);
  endpoint.searchParams.set("version", String(version));
  try {
    const response = await fetch(endpoint, { cache: "force-cache" });
    if (!response.ok || !response.body) {
      return Response.json({ code: "login-experience.asset.unavailable" }, { status: 404 });
    }
    return new Response(response.body, {
      status: 200,
      headers: {
        "Cache-Control": "public, max-age=31536000, immutable",
        "Content-Type": response.headers.get("content-type") ?? "application/octet-stream",
        ...(response.headers.get("etag") ? { ETag: response.headers.get("etag")! } : {}),
      },
    });
  } catch {
    return Response.json({ code: "login-experience.asset.unavailable" }, { status: 503 });
  }
}

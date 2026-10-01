import { loadLoginExperience } from "@/lib/login-experience-server";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

export async function GET(): Promise<Response> {
  return Response.json(await loadLoginExperience(), {
    headers: { "Cache-Control": "no-store" },
  });
}

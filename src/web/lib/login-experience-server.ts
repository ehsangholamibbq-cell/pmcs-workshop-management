import "server-only";
import { readAuthEnvironment } from "@/lib/auth-environment";
import {
  fallbackLoginExperience,
  normalizeLoginExperience,
  type LoginExperienceDescriptor,
} from "@/lib/login-experience";

export async function loadLoginExperience(): Promise<LoginExperienceDescriptor> {
  const environment = readAuthEnvironment();
  const endpoint = new URL("/api/v1/public/login-experience", environment.internalApiUrl);
  endpoint.searchParams.set("tenantId", environment.loginTenantId);
  try {
    const response = await fetch(endpoint, { cache: "no-store" });
    if (!response.ok) return fallbackLoginExperience();
    return normalizeLoginExperience(await response.json());
  } catch {
    return fallbackLoginExperience();
  }
}

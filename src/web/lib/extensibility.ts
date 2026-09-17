import { ensureApiSuccess } from "./localization.ts";

export const moduleManifestSchemaVersion = "pmcs.module/v1";

export interface ExtensibilityApiIdentity {
  readonly tenantId: string;
  readonly userId: string;
}

export interface PermissionManifestModel {
  readonly key: string;
  readonly scope: "Tenant" | "Project";
  readonly riskClass: "Low" | "Medium" | "High" | "Critical";
  readonly description: string;
}

export interface NavigationManifestModel {
  readonly id: string;
  readonly route: string;
  readonly label: string;
  readonly permission: string;
  readonly featureFlag: string;
  readonly order: number;
}

export interface ToolManifestModel {
  readonly id: string;
  readonly description: string;
  readonly inputSchema: string;
  readonly outputSchema: string;
  readonly permission: string;
  readonly riskClass: "Low" | "Medium" | "High" | "Critical";
  readonly accessMode: "ReadOnly" | "ControlledWrite";
}

export interface IntegrationEventManifestModel {
  readonly name: string;
  readonly version: number;
  readonly classification: "Internal" | "Confidential";
}

export interface ModuleDescriptorModel {
  readonly schemaVersion: string;
  readonly moduleId: string;
  readonly displayName: string;
  readonly version: string;
  readonly capabilities: readonly string[];
  readonly dependencies: readonly string[];
  readonly migrationOwner: string;
  readonly minimumPlatformVersion: string;
  readonly isLegacy: boolean;
  readonly permissions: readonly PermissionManifestModel[];
  readonly navigationItems: readonly NavigationManifestModel[];
  readonly tools: readonly ToolManifestModel[];
  readonly events: readonly IntegrationEventManifestModel[];
}

export async function listPlatformModules(
  apiBaseUrl: string,
  identity: ExtensibilityApiIdentity,
): Promise<readonly ModuleDescriptorModel[]> {
  const response = await fetch(`${normalize(apiBaseUrl)}/api/v1/platform/modules`, {
    headers: {
      "X-Tenant-Id": identity.tenantId,
      "X-User-Id": identity.userId,
    },
    cache: "no-store",
  });
  await ensureApiSuccess(response);
  return response.json() as Promise<readonly ModuleDescriptorModel[]>;
}

export function resolveVisibleNavigation(
  modules: readonly ModuleDescriptorModel[],
  grantedPermissions: ReadonlySet<string>,
  enabledFeatures: ReadonlySet<string>,
): readonly NavigationManifestModel[] {
  const visible = new Map<string, NavigationManifestModel>();

  for (const descriptor of modules) {
    if (descriptor.schemaVersion !== moduleManifestSchemaVersion || descriptor.isLegacy) {
      continue;
    }

    for (const item of descriptor.navigationItems) {
      if (!grantedPermissions.has(item.permission) || !enabledFeatures.has(item.featureFlag) ||
          !isSafeApplicationRoute(item.route) || visible.has(item.id)) {
        continue;
      }

      visible.set(item.id, item);
    }
  }

  return [...visible.values()].sort((left, right) =>
    left.order - right.order || left.id.localeCompare(right.id, "en"));
}

export function isSafeApplicationRoute(route: string): boolean {
  return route.startsWith("/") && !route.startsWith("//") && !route.includes("..") &&
    !route.includes("\\") && !route.includes("://") && route.length <= 240;
}

function normalize(value: string): string {
  return value.endsWith("/") ? value.slice(0, -1) : value;
}

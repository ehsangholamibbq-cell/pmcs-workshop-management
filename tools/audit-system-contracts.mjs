import assert from "node:assert/strict";
import { readFileSync, readdirSync } from "node:fs";
import { join, relative, resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const backendRoot = join(root, "src/backend");
const endpointFiles = walk(backendRoot)
  .filter((file) => file.endsWith(".cs") && file.includes("/Endpoints/"));
const registrations = endpointFiles.flatMap(readRegistrations);
const mutations = registrations.filter((endpoint) => endpoint.method !== "GET");

assert.ok(registrations.length >= 100, `Endpoint inventory unexpectedly shrank to ${registrations.length}.`);

const protocolManagedMutations = new Map([
  ["POST /api/v1/sync/handshake", "Handshake issues a superseding lease under a serializable transaction."],
  ["POST /api/v1/sync/operations", "Each immutable operation carries its own operation id and stored receipt."],
  ["POST /api/v1/sync/checkpoints", "Checkpoint offers are single-use and replay-safe by offer identity."],
  ["POST /api/v1/sync/conflicts/{conflictId:guid}/resolve", "Conflict resolution is revision-controlled and creates an immutable resolution."],
  ["POST /api/v1/sync/devices/{registrationId:guid}/revoke", "Device revocation is revision-controlled and monotonic."],
]);

const missingIdempotency = mutations.filter((endpoint) =>
  !endpoint.handlerSource.includes("IIdempotencyStore") &&
  !protocolManagedMutations.has(`${endpoint.method} ${endpoint.path}`));
assert.deepEqual(
  missingIdempotency.map(formatEndpoint),
  [],
  "A mutation endpoint has neither the shared idempotency contract nor an explicit protocol-level exception.",
);

for (const endpoint of mutations) {
  assert.ok(
    !/AllowAnonymous\s*\(/.test(endpoint.registrationSource),
    `Mutation endpoint must not be anonymous: ${formatEndpoint(endpoint)}`,
  );
}

const permissionSource = readFileSync(
  join(backendRoot, "Pmcs.Modules.IdentityAccess/Services/ProjectPermissionService.cs"),
  "utf8",
);
assert.doesNotMatch(
  permissionSource.match(/role == TenantRole\.PortfolioViewer[\s\S]*?\)\);/)?.[0] ?? "",
  /insights\.(generate|review)/,
  "PortfolioViewer must remain read-only for advisory intelligence.",
);

const projectPage = readFileSync(join(root, "src/web/app/page.tsx"), "utf8");
const dashboard = readFileSync(join(root, "src/web/components/foundation-dashboard.tsx"), "utf8");
assert.match(projectPage, /ProjectLanding/);
assert.doesNotMatch(projectPage, /FoundationDashboard/);
assert.doesNotMatch(dashboard, /33333333-3333-3333-3333-333333333333|پروژه نمونه|نمونه-۰۱/);

const lifecycle = readFileSync(
  join(backendRoot, "Pmcs.Api/Infrastructure/ProjectLifecycleMiddleware.cs"),
  "utf8",
);
assert.match(lifecycle, /FindProfileAsync\(actor\.TenantId, projectId/);
assert.match(lifecycle, /profile\.Status != ProjectStatus\.Active/);
assert.doesNotMatch(
  lifecycle,
  /HasProjectPermissionAsync/,
  "The lifecycle invariant must not become bypassable through a missing read permission.",
);

const fieldSync = readFileSync(
  join(backendRoot, "Pmcs.Modules.FieldOperations/Endpoints/SyncEndpoints.cs"),
  "utf8",
);
assert.match(fieldSync, /project\.Status != ProjectStatus\.Active/);
assert.match(fieldSync, /projectLocationDirectory\.FindActiveAsync/);
assert.match(fieldSync, /project\.location\.required/);

const directFacts = readFileSync(
  join(backendRoot, "Pmcs.Modules.FieldOperations/Endpoints/DailyReportEndpoints.cs"),
  "utf8",
);
assert.match(directFacts, /project\.location\.required/);
assert.match(directFacts, /projectLocationDirectory\.FindActiveAsync/);

const fieldMapping = readFileSync(
  join(backendRoot, "Pmcs.Modules.FieldOperations/Persistence/FieldOperationsDbContext.cs"),
  "utf8",
);
const factMapping = fieldMapping.slice(fieldMapping.indexOf("modelBuilder.Entity<DailyReportFact>"));
assert.match(factMapping, /Property\(x => x\.LocationId\)\.HasColumnName\("location_id"\)/);

const projectStateCalculation = readFileSync(
  join(backendRoot, "Pmcs.Modules.ProjectIntelligence/Domain/ProjectStateCalculation.cs"),
  "utf8",
);
const projectStateMapping = readFileSync(
  join(backendRoot, "Pmcs.Modules.ProjectIntelligence/Persistence/ProjectIntelligenceDbContext.cs"),
  "utf8",
);
assert.match(projectStateCalculation, /fact\.LocationId/);
assert.match(projectStateMapping, /Property\(x => x\.LocationId\)\.HasColumnName\("location_id"\)/);

const platformStore = readFileSync(
  join(backendRoot, "Pmcs.Modules.Platform/Services/IdempotencyStore.cs"),
  "utf8",
);
const transactionalEffects = readFileSync(
  join(backendRoot, "Pmcs.Modules.Platform/Services/TransactionalSideEffectWriter.cs"),
  "utf8",
);
assert.match(platformStore, /record\.ExpiresAt <= clock\.UtcNow/);
assert.match(platformStore, /on conflict \(tenant_id, key, operation\) do update/);
assert.match(transactionalEffects, /on conflict \(tenant_id, key, operation\) do update/);

const operationStore = readFileSync(join(root, "src/web/lib/operation-store.ts"), "utf8");
const syncClient = readFileSync(join(root, "src/web/lib/sync-client.ts"), "utf8");
assert.match(operationStore, /serverConflict\.revision/);
assert.doesNotMatch(operationStore, /baseRevision:\s*0[,}]/);
assert.match(operationStore, /activeSyncs = new Map/);
assert.match(syncClient, /activeHandshakes = new Map/);

console.log(
  `System contract audit passed: ${registrations.length} endpoints, ${mutations.length} mutations, ` +
  `${protocolManagedMutations.size} documented protocol-managed mutations.`,
);

function readRegistrations(file) {
  const source = readFileSync(file, "utf8");
  const groups = new Map(
    [...source.matchAll(/var\s+(\w+)\s*=\s*endpoints\.MapGroup\(\s*"([^"]+)"\s*\)/g)]
      .map((match) => [match[1], match[2]]),
  );
  const endpointPattern = /(\w+)\.Map(Get|Post|Put|Patch|Delete)\(\s*"([^"]*)"\s*,\s*([A-Za-z_][A-Za-z0-9_.]*)/g;
  return [...source.matchAll(endpointPattern)].map((match) => {
    const [, owner, verb, route, qualifiedHandler] = match;
    const prefix = owner === "endpoints" ? "" : groups.get(owner) ?? inferInheritedGroup(file, owner);
    assert.notEqual(prefix, undefined, `Cannot resolve route group '${owner}' in ${relative(root, file)}.`);
    const handler = qualifiedHandler.split(".").at(-1);
    const handlerSource = findHandlerSource(source, qualifiedHandler, file);
    return {
      method: verb.toUpperCase(),
      path: normalizePath(`${prefix ?? ""}/${route}`),
      handler,
      handlerSource,
      registrationSource: source.slice(match.index, source.indexOf(";", match.index) + 1),
      file,
    };
  });
}

function inferInheritedGroup(file, owner) {
  const path = relative(root, file).replaceAll("\\", "/");
  if (owner === "governance" && path.includes("Pmcs.Modules.ActionControl/Endpoints/")) {
    return "/api/v1/projects/{projectId:guid}/governance";
  }
  if (owner === "group" && path.includes("Pmcs.Modules.Planning/Endpoints/PlanningBaselineEndpoints.cs")) {
    return "/api/v1/projects/{projectId:guid}/planning";
  }
  if (owner === "group" && path.includes("Pmcs.Modules.TechnicalOffice/Endpoints/Technical")) {
    return "/api/v1/projects/{projectId:guid}/technical-office";
  }
  if (owner === "group" && path.includes("Pmcs.Modules.Commercial/Endpoints/Supply")) {
    return "/api/v1/projects/{projectId:guid}/commercial/supply";
  }
  if (owner === "group" && path.includes("Pmcs.Modules.Commercial/Endpoints/")) {
    return "/api/v1/projects/{projectId:guid}/commercial";
  }
  return undefined;
}

function findHandlerSource(source, qualifiedHandler, file) {
  const handler = qualifiedHandler.split(".").at(-1);
  if (qualifiedHandler.includes(".")) {
    const className = qualifiedHandler.split(".").at(-2);
    const implementation = endpointFiles.find((candidate) => {
      const candidateSource = readFileSync(candidate, "utf8");
      return new RegExp(`\\bclass\\s+${escapeRegex(className)}\\b`).test(candidateSource);
    });
    assert.ok(implementation, `Cannot find handler owner '${className}' referenced by ${relative(root, file)}.`);
    source = readFileSync(implementation, "utf8");
    file = implementation;
  }
  let declaration = handlerDeclaration(handler);
  let match = declaration.exec(source);
  if (!match) {
    const className = source.match(/\bclass\s+(\w+)/)?.[1];
    const implementation = className && endpointFiles.find((candidate) => {
      if (candidate === file) return false;
      const candidateSource = readFileSync(candidate, "utf8");
      return new RegExp(`\\bclass\\s+${escapeRegex(className)}\\b`).test(candidateSource) &&
        handlerDeclaration(handler).test(candidateSource);
    });
    if (implementation) {
      source = readFileSync(implementation, "utf8");
      file = implementation;
      declaration = handlerDeclaration(handler);
      match = declaration.exec(source);
    }
  }
  assert.ok(match, `Cannot find handler '${handler}' in ${relative(root, file)}.`);
  const start = match.index;
  const bodyStart = source.indexOf("{", declaration.lastIndex);
  const expressionStart = source.indexOf("=>", declaration.lastIndex);
  if (expressionStart >= 0 && (bodyStart < 0 || expressionStart < bodyStart)) {
    const end = source.indexOf(";", expressionStart);
    return source.slice(start, end + 1);
  }

  assert.ok(bodyStart >= 0, `Cannot find body for handler '${handler}' in ${relative(root, file)}.`);
  let depth = 0;
  for (let index = bodyStart; index < source.length; index += 1) {
    if (source[index] === "{") depth += 1;
    if (source[index] === "}") depth -= 1;
    if (depth === 0) return source.slice(start, index + 1);
  }
  assert.fail(`Unbalanced handler '${handler}' in ${relative(root, file)}.`);
}

function handlerDeclaration(handler) {
  return new RegExp(
    `(?:private|internal|public)\\s+static[\\s\\S]{0,300}?\\b${escapeRegex(handler)}\\s*\\(`,
    "g",
  );
}

function normalizePath(path) {
  const normalized = path.replace(/\/{2,}/g, "/").replace(/\/$/, "");
  return normalized || "/";
}

function formatEndpoint(endpoint) {
  return `${endpoint.method} ${endpoint.path} (${relative(root, endpoint.file)}#${endpoint.handler})`;
}

function escapeRegex(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function walk(directory) {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const path = join(directory, entry.name);
    return entry.isDirectory() ? walk(path) : [path];
  });
}

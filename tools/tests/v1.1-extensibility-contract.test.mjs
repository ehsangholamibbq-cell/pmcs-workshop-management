import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const root = new URL("../../", import.meta.url);

async function source(path) {
  return readFile(new URL(path, root), "utf8");
}

test("module catalog is a startup-validated fail-closed contract", async () => {
  const [moduleInterface, contracts, program] = await Promise.all([
    source("src/backend/Pmcs.BuildingBlocks/Modules/IModule.cs"),
    source("src/backend/Pmcs.BuildingBlocks/Modules/ModuleDescriptor.cs"),
    source("src/backend/Pmcs.Api/Program.cs"),
  ]);

  assert.match(moduleInterface, /ModuleDescriptor Descriptor/u);
  assert.match(contracts, /pmcs\.module\/v1/u);
  assert.match(contracts, /pmcs\.module\/legacy-v1/u);
  assert.match(contracts, /unsupported manifest schema/u);
  assert.match(contracts, /ValidateDependencyGraph/u);
  assert.match(contracts, /Unsafe navigation route/u);
  assert.match(program, /ModuleCatalog\.Create\(modules\.Select\(module => module\.Descriptor\)\)/u);
  assert.match(program, /AddSingleton<IModuleCatalog>/u);
});

test("reference module connects permission, navigation, event and read-only tool manifests", async () => {
  const [platform, endpoints, web] = await Promise.all([
    source("src/backend/Pmcs.Modules.Platform/PlatformModule.cs"),
    source("src/backend/Pmcs.Modules.Platform/Endpoints/PlatformModuleEndpoints.cs"),
    source("src/web/lib/extensibility.ts"),
  ]);

  for (const contract of [
    "platform.foundation",
    "platform.modules.read",
    "platform.module-catalog",
    "platform.modules.describe",
    "platform.module-catalog.snapshot",
    "ToolAccessMode.ReadOnly",
  ]) {
    assert.match(platform, new RegExp(contract.replaceAll(".", "\\."), "u"));
  }
  assert.match(endpoints, /HasTenantPermissionAsync/u);
  assert.doesNotMatch(endpoints, /DbContext|\.Persistence|Map(?:Post|Put|Patch|Delete)\(/u);
  assert.match(web, /module\.schemaVersion !== moduleManifestSchemaVersion/u);
  assert.match(web, /grantedPermissions\.has\(item\.permission\)/u);
  assert.match(web, /enabledFeatures\.has\(item\.featureFlag\)/u);
  assert.match(web, /isSafeApplicationRoute/u);
});

test("EXT1 explicitly excludes unsafe runtime plugins and preserves platform boundaries", async () => {
  const [architecture, checkpoint, release] = await Promise.all([
    source("docs/architecture/v1.1-extensibility-foundation.md"),
    source("docs/checkpoints/v1.1-ext1-candidate.md"),
    source("release/pmcs-v1.1-ext1-candidate.json").then(JSON.parse),
  ]);

  assert.match(architecture, /هیچ Binary، Assembly، HTML، CSS یا JavaScript دلخواه/u);
  assert.match(architecture, /Agent → Permission-aware Tool → Application Service → Business Rules → Database/u);
  assert.match(architecture, /Preview، Publish، Rollback و Fallback/u);
  assert.match(checkpoint, /Dynamic binary\/plugin loading/u);
  assert.equal(release.security.unknownSchemaFailsClosed, true);
  assert.equal(release.security.dynamicBinaryLoadingAllowed, false);
  assert.equal(release.security.crossModulePersistenceAllowed, false);
  assert.equal(release.security.agentDirectDatabaseAccessAllowed, false);
  assert.ok(["validation-pending", "verified"].includes(release.status));
  if (release.status === "verified") {
    assert.match(release.candidateSourceCommit, /^[0-9a-f]{40}$/u);
    assert.match(release.validationRunId, /^\d+$/u);
    assert.equal(release.validationConclusion, "success");
    assert.equal(release.gates.fullCi, "passed");
  }
});

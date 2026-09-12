import { writeFileSync } from "node:fs";

const commitPattern = /^[0-9a-f]{40}$/u;
const versionPattern = /^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$/u;

export function createReleaseIdentity(environment = process.env) {
  const commit = environment.PMCS_RELEASE_COMMIT?.trim() || "development";
  const version = environment.PMCS_RELEASE_VERSION?.trim() || "0.0.0-dev";
  const builtAt = environment.PMCS_RELEASE_BUILT_AT?.trim() || "unknown";
  const strict = environment.PMCS_RELEASE_REQUIRED === "true" || commit !== "development";

  if (strict && !commitPattern.test(commit)) {
    throw new Error("PMCS_RELEASE_COMMIT must be a full lowercase 40-character commit hash.");
  }
  if (strict && !versionPattern.test(version)) {
    throw new Error("PMCS_RELEASE_VERSION must be a semantic version.");
  }
  if (strict && !isUtcTimestamp(builtAt)) {
    throw new Error("PMCS_RELEASE_BUILT_AT must be an ISO-8601 UTC timestamp.");
  }

  return Object.freeze({ schemaVersion: 1, artifact: "web", commit, version, builtAt });
}

export function writeReleaseIdentity(destination, environment = process.env) {
  const identity = createReleaseIdentity(environment);
  writeFileSync(destination, `${JSON.stringify(identity, null, 2)}\n`, { encoding: "utf8", mode: 0o644 });
  return identity;
}

function isUtcTimestamp(value) {
  if (!value.endsWith("Z") || Number.isNaN(Date.parse(value))) return false;
  return new Date(value).toISOString() === value;
}

#!/usr/bin/env bash
set -euo pipefail

: "${PMCS_CANDIDATE_FILE:?Set PMCS_CANDIDATE_FILE to the approved pilot candidate JSON.}"
: "${PMCS_API_URL:?Set PMCS_API_URL to the deployed API origin.}"
: "${PMCS_WEB_URL:?Set PMCS_WEB_URL to the deployed web origin.}"

if [[ "${PMCS_CANDIDATE_FILE}" != /* || ! -f "${PMCS_CANDIDATE_FILE}" ]]; then
  echo "PMCS_CANDIDATE_FILE must be an existing absolute file path." >&2
  exit 2
fi

command -v curl >/dev/null
command -v node >/dev/null

node tools/validate-pilot-release.mjs --candidate "${PMCS_CANDIDATE_FILE}" >/dev/null

temporary_directory="$(mktemp -d)"
cleanup() { rm -rf -- "${temporary_directory}"; }
trap cleanup EXIT

curl --fail --silent --show-error \
  --connect-timeout 10 --max-time 30 \
  "${PMCS_API_URL%/}/api/v1/release" > "${temporary_directory}/api.json"
curl --fail --silent --show-error \
  --connect-timeout 10 --max-time 30 \
  "${PMCS_WEB_URL%/}/release.json" > "${temporary_directory}/web.json"

node --input-type=module \
  - \
  "${PMCS_CANDIDATE_FILE}" \
  "${temporary_directory}/api.json" \
  "${temporary_directory}/web.json" <<'NODE'
import { readFileSync } from "node:fs";

const [candidatePath, apiPath, webPath] = process.argv.slice(2);
const candidate = JSON.parse(readFileSync(candidatePath, "utf8"));
const api = JSON.parse(readFileSync(apiPath, "utf8"));
const web = JSON.parse(readFileSync(webPath, "utf8"));

for (const [name, identity, expectedArtifact] of [
  ["API", api, "api"],
  ["Web", web, "web"],
]) {
  if (identity.schemaVersion !== 1 || identity.artifact !== expectedArtifact) {
    throw new Error(`${name} returned an unsupported release identity.`);
  }
  for (const field of ["commit", "version", "builtAt"]) {
    if (identity[field] !== candidate[field]) {
      throw new Error(`${name} ${field} does not match the approved candidate.`);
    }
  }
}

console.log(JSON.stringify({
  status: "passed",
  releaseId: candidate.releaseId,
  commit: candidate.commit,
  apiArtifact: api.artifact,
  webArtifact: web.artifact,
}));
NODE

import assert from "node:assert/strict";
import { execFile } from "node:child_process";
import { mkdtemp, rm, writeFile } from "node:fs/promises";
import { createServer } from "node:http";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import test from "node:test";
import { promisify } from "node:util";

const execute = promisify(execFile);
const root = resolve(import.meta.dirname, "../..");
const commit = "6dea26efbc75eac5d195c630bb4c4b9b64ccb7b2";

test("release smoke accepts API and Web artifacts built from the approved candidate", async () => {
  await withReleaseEnvironment(commit, async ({ candidatePath, apiUrl, webUrl }) => {
    const result = await execute(resolve(root, "tools/release-smoke.sh"), [], {
      cwd: root,
      env: { ...process.env, PMCS_CANDIDATE_FILE: candidatePath, PMCS_API_URL: apiUrl, PMCS_WEB_URL: webUrl },
    });
    assert.match(result.stdout, /"status":"passed"/u);
  });
});

test("release smoke rejects a mixed Web and API deployment", async () => {
  await withReleaseEnvironment("d".repeat(40), async ({ candidatePath, apiUrl, webUrl }) => {
    await assert.rejects(
      execute(resolve(root, "tools/release-smoke.sh"), [], {
        cwd: root,
        env: { ...process.env, PMCS_CANDIDATE_FILE: candidatePath, PMCS_API_URL: apiUrl, PMCS_WEB_URL: webUrl },
      }),
      /Web commit does not match/u,
    );
  });
});

async function withReleaseEnvironment(webCommit, assertion) {
  const directory = await mkdtemp(join(tmpdir(), "pmcs-release-smoke-"));
  const candidate = createCandidate();
  const candidatePath = join(directory, "candidate.json");
  await writeFile(candidatePath, JSON.stringify(candidate), "utf8");
  const api = await startIdentityServer({ schemaVersion: 1, artifact: "api", commit, version: candidate.version, builtAt: candidate.builtAt }, "/api/v1/release");
  const web = await startIdentityServer({ schemaVersion: 1, artifact: "web", commit: webCommit, version: candidate.version, builtAt: candidate.builtAt }, "/release.json");
  try {
    await assertion({ candidatePath, apiUrl: api.url, webUrl: web.url });
  } finally {
    await Promise.all([api.close(), web.close()]);
    await rm(directory, { recursive: true, force: true });
  }
}

function createCandidate() {
  const now = Date.now();
  const iso = offset => new Date(now + offset).toISOString();
  const executedAt = iso(-60 * 60 * 1000);
  const gates = ["repository-validation", "backend-tests", "web-check", "postgres-integration", "object-storage-roundtrip", "backup-restore-drill", "environment-smoke", "device-uat"];
  return {
    schemaVersion: 1,
    releaseId: `pmcs-${new Date(now).toISOString().slice(0, 10).replaceAll("-", ".")}-rc.1`,
    version: "1.20.0-rc.1",
    commit,
    builtAt: iso(-2 * 60 * 60 * 1000),
    createdAt: iso(-10 * 60 * 1000),
    expiresAt: iso(7 * 24 * 60 * 60 * 1000),
    artifacts: ["api-image", "web-image", "source-bundle"].map((name, index) => ({ name, sha256: String(index + 1).repeat(64) })),
    evidence: gates.map((gateId, index) => ({
      gateId,
      status: "passed",
      commit,
      executedAt,
      expiresAt: iso((gateId === "backup-restore-drill" ? 89 : gateId === "device-uat" ? 13 : 0.9) * 24 * 60 * 60 * 1000),
      reportSha256: String(index + 1).repeat(64),
      reportUri: `evidence://pilot/${gateId}.json`,
    })),
    approvals: ["product", "security", "operations"].map((role, index) => ({
      role,
      decision: "approved",
      approvedBy: `${role}-owner`,
      approvedAt: iso((-30 + index * 5) * 60 * 1000),
      commit,
    })),
  };
}

async function startIdentityServer(identity, expectedPath) {
  const server = createServer((request, response) => {
    if (request.url !== expectedPath) {
      response.writeHead(404).end();
      return;
    }
    response.writeHead(200, { "content-type": "application/json" });
    response.end(JSON.stringify(identity));
  });
  await new Promise(resolveListen => server.listen(0, "127.0.0.1", resolveListen));
  const address = server.address();
  if (address === null || typeof address === "string") throw new Error("Test server did not expose a TCP port.");
  return {
    url: `http://127.0.0.1:${address.port}`,
    close: () => new Promise((resolveClose, rejectClose) => server.close(error => error ? rejectClose(error) : resolveClose())),
  };
}

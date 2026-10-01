#!/usr/bin/env node

import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { mkdirSync, renameSync, writeFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { repositoryRoot } from "./regression-runner.mjs";

export function validateReleaseBuild({ api, web, expected, images }) {
  for (const [artifact, identity] of Object.entries({ api, web })) {
    assert.equal(identity.schemaVersion, 1);
    assert.equal(identity.artifact, artifact);
    for (const key of ["commit", "version", "builtAt"]) assert.equal(identity[key], expected[key], `${artifact}.${key} mismatch`);
    assert.match(images[artifact].id, /^sha256:[0-9a-f]{64}$/u);
    assert.equal(images[artifact].revision, expected.commit);
    assert.equal(images[artifact].version, expected.version);
  }
  assert.deepEqual([api.commit, api.version, api.builtAt], [web.commit, web.version, web.builtAt]);
}

function git(...args) { return execFileSync("git", args, { cwd: repositoryRoot, encoding: "utf8" }).trim(); }
function docker(...args) { return execFileSync("docker", args, { cwd: repositoryRoot, encoding: "utf8" }).trim(); }

async function main() {
  const commit = git("rev-parse", "HEAD"), tree = git("rev-parse", "HEAD^{tree}");
  const expected = {
    commit: process.env.PMCS_RELEASE_COMMIT,
    version: process.env.PMCS_RELEASE_VERSION,
    builtAt: process.env.PMCS_RELEASE_BUILT_AT,
  };
  assert.match(expected.commit ?? "", /^[0-9a-f]{40}$/u);
  assert.equal(expected.commit, process.env.PMCS_SOURCE_HEAD_SHA);
  assert.equal(expected.version, "1.1.0");
  assert.equal(new Date(expected.builtAt).toISOString(), expected.builtAt);
  const endpoints = { api: "http://127.0.0.1:8080/api/v1/release", web: "http://127.0.0.1:3000/release.json" };
  const identities = {};
  for (const [name, url] of Object.entries(endpoints)) {
    const response = await fetch(url);
    assert.equal(response.status, 200, `${name} release endpoint`);
    identities[name] = await response.json();
  }
  const images = {};
  for (const name of ["api", "web"]) {
    const image = docker("compose", "images", "-q", name);
    assert.ok(image, `Missing ${name} image`);
    const inspect = JSON.parse(docker("image", "inspect", image));
    assert.equal(inspect.length, 1);
    images[name] = {
      id: inspect[0].Id,
      revision: inspect[0].Config.Labels?.["org.opencontainers.image.revision"],
      version: inspect[0].Config.Labels?.["org.opencontainers.image.version"],
    };
  }
  validateReleaseBuild({ ...identities, expected, images });
  const output = resolve(process.argv[2] ?? join(repositoryRoot, "artifacts/qa/v1.1-evidence"));
  mkdirSync(output, { recursive: true });
  const path = join(output, "release-build.artifact.json"), temp = `${path}.${process.pid}.tmp`;
  writeFileSync(temp, `${JSON.stringify({
    contractVersion: 1, reportType: "pmcs-v1.1-release-build",
    commit, tree, sourceHead: expected.commit, status: "passed",
    identity: expected, api: identities.api, web: identities.web, images, endpoints,
  }, null, 2)}\n`, { mode: 0o600 });
  renameSync(temp, path);
  console.log(JSON.stringify({ status: "passed", commit, sourceHead: expected.commit, apiImage: images.api.id, webImage: images.web.id }));
}

if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
  main().catch(error => { console.error(error.message); process.exitCode = 1; });
}

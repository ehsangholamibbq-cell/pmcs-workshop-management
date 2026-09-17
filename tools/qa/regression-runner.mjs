#!/usr/bin/env node

import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import { mkdirSync, readFileSync, renameSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
export const repositoryRoot = resolve(scriptDirectory, "../..");
export const defaultManifestPath = join(scriptDirectory, "regression-suites.json");
export const defaultOutputDirectory = join(repositoryRoot, "artifacts/qa/regression");
const commitPattern = /^[0-9a-f]{40}$/u;
const identifierPattern = /^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$/u;

export function loadRegressionManifest(path = defaultManifestPath) {
  const manifest = JSON.parse(readFileSync(path, "utf8"));
  validateRegressionManifest(manifest);
  return manifest;
}

export function validateRegressionManifest(manifest) {
  assert.equal(manifest?.contractVersion, 1, "Regression manifest contractVersion must be 1.");
  assert.match(manifest?.runnerVersion ?? "", identifierPattern, "Regression runnerVersion is invalid.");
  assert.ok(Array.isArray(manifest?.suites) && manifest.suites.length > 0, "Regression suites are required.");

  const suiteIds = new Set();
  for (const suite of manifest.suites) {
    assert.match(suite?.id ?? "", identifierPattern, "Regression suite id is invalid.");
    assert.ok(!suiteIds.has(suite.id), `Duplicate regression suite: ${suite.id}`);
    suiteIds.add(suite.id);
    assert.ok(typeof suite.displayName === "string" && suite.displayName.trim(), `Display name is required for ${suite.id}.`);
    assert.match(suite?.layer ?? "", identifierPattern, `Layer is invalid for ${suite.id}.`);
    assert.ok(Array.isArray(suite.commands) && suite.commands.length > 0, `Commands are required for ${suite.id}.`);

    const commandIds = new Set();
    for (const command of suite.commands) {
      assert.match(command?.id ?? "", identifierPattern, `Command id is invalid in ${suite.id}.`);
      assert.ok(!commandIds.has(command.id), `Duplicate command ${command.id} in ${suite.id}.`);
      commandIds.add(command.id);
      assert.ok(typeof command.file === "string" && command.file.trim(), `Executable is required for ${command.id}.`);
      assert.ok(Array.isArray(command.args) && command.args.every(value => typeof value === "string"), `Arguments are invalid for ${command.id}.`);
      if (command.cwd !== undefined) {
        assert.ok(typeof command.cwd === "string" && command.cwd && !command.cwd.startsWith("/"), `Working directory is invalid for ${command.id}.`);
      }
    }
  }
  return manifest;
}

export function resolveRegressionCommit(environment = process.env, root = repositoryRoot) {
  const supplied = (environment.PMCS_REGRESSION_COMMIT || environment.GITHUB_SHA || "").trim().toLowerCase();
  const commit = supplied || execFileSync("git", ["rev-parse", "HEAD"], { cwd: root, encoding: "utf8" }).trim().toLowerCase();
  assert.match(commit, commitPattern, "Regression evidence requires one full lowercase commit SHA.");
  return commit;
}

export function runRegressionSuite({
  manifest,
  suiteId,
  root = repositoryRoot,
  outputDirectory = defaultOutputDirectory,
  commit = resolveRegressionCommit(process.env, root),
  execute = spawnSync,
  clock = () => new Date(),
}) {
  validateRegressionManifest(manifest);
  assert.match(commit, commitPattern, "Regression suite commit is invalid.");
  const suite = manifest.suites.find(candidate => candidate.id === suiteId);
  assert.ok(suite, `Unknown regression suite: ${suiteId}`);

  const started = clock();
  const commands = [];
  for (const command of suite.commands) {
    const commandStarted = clock();
    const result = execute(command.file, command.args, {
      cwd: resolve(root, command.cwd ?? "."),
      env: process.env,
      stdio: "inherit",
    });
    const commandCompleted = clock();
    const exitCode = Number.isInteger(result.status) ? result.status : 1;
    const commandResult = {
      id: command.id,
      status: exitCode === 0 && !result.error ? "passed" : "failed",
      exitCode,
      signal: result.signal ?? null,
      durationMs: Math.max(0, commandCompleted.getTime() - commandStarted.getTime()),
      failureCode: result.error?.code ?? null,
    };
    commands.push(commandResult);
    if (commandResult.status === "failed") break;
  }

  const completed = clock();
  const passed = commands.length === suite.commands.length && commands.every(command => command.status === "passed");
  const report = {
    contractVersion: 1,
    reportType: "pmcs-regression-suite",
    runnerVersion: manifest.runnerVersion,
    suite: suite.id,
    displayName: suite.displayName,
    layer: suite.layer,
    commit,
    status: passed ? "passed" : "failed",
    startedAt: started.toISOString(),
    completedAt: completed.toISOString(),
    durationMs: Math.max(0, completed.getTime() - started.getTime()),
    plannedCommandCount: suite.commands.length,
    executedCommandCount: commands.length,
    commands,
  };
  writeJsonAtomically(join(outputDirectory, `${suite.id}.json`), report);
  return report;
}

function writeJsonAtomically(path, value) {
  mkdirSync(dirname(path), { recursive: true });
  const temporaryPath = `${path}.${process.pid}.tmp`;
  writeFileSync(temporaryPath, `${JSON.stringify(value, null, 2)}\n`, { encoding: "utf8", mode: 0o600 });
  renameSync(temporaryPath, path);
}

function parseArguments(args) {
  if (args[0] !== "run" || !args[1]) {
    throw new Error("Usage: node tools/qa/regression-runner.mjs run <suite> [--output-dir <path>] [--manifest <path>]");
  }
  const options = { suiteId: args[1], outputDirectory: defaultOutputDirectory, manifestPath: defaultManifestPath };
  for (let index = 2; index < args.length; index += 2) {
    const flag = args[index];
    const value = args[index + 1];
    if (!value) throw new Error(`Missing value for ${flag}.`);
    if (flag === "--output-dir") options.outputDirectory = resolve(value);
    else if (flag === "--manifest") options.manifestPath = resolve(value);
    else throw new Error(`Unknown option: ${flag}`);
  }
  return options;
}

async function main() {
  const options = parseArguments(process.argv.slice(2));
  const report = runRegressionSuite({
    manifest: loadRegressionManifest(options.manifestPath),
    suiteId: options.suiteId,
    outputDirectory: options.outputDirectory,
  });
  console.log(JSON.stringify({ suite: report.suite, status: report.status, commands: report.executedCommandCount, report: join(options.outputDirectory, `${report.suite}.json`) }));
  process.exitCode = report.status === "passed" ? 0 : 1;
}

if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
  main().catch(error => {
    console.error(error instanceof Error ? error.message : String(error));
    process.exitCode = 1;
  });
}

import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const myWork = readFileSync(new URL("../components/my-work-center.tsx", import.meta.url), "utf8");
const reportHistory = readFileSync(new URL("../components/daily-report-history.tsx", import.meta.url), "utf8");

test("My Work and notification controls expose stable E2E selectors", () => {
  for (const testId of [
    "my-work-center",
    "my-work-item",
    "daily-report-review-approve",
    "daily-report-review-return",
    "notification-item",
    "notification-read",
    "notification-acknowledge",
  ]) {
    assert.match(myWork, new RegExp(`data-testid=\\"${testId}\\"`));
  }
  assert.match(myWork, /data-entity-id=\{item\.targetId\}/);
  assert.match(myWork, /data-entity-id=\{notification\.id\}/);
});

test("Daily Report correction lineage exposes stable E2E selectors", () => {
  for (const testId of [
    "daily-report-history",
    "daily-report-version",
    "daily-report-correction-start",
    "daily-report-correction-editor",
    "daily-report-correction-fact",
    "daily-report-correction-fact-remove",
    "daily-report-correction-fact-add",
    "daily-report-correction-submit",
  ]) {
    assert.match(reportHistory, new RegExp(`data-testid=\\"${testId}\\"`));
  }
  assert.match(reportHistory, /data-entity-id=\{report\.id\}/);
  assert.match(reportHistory, /data-entity-id=\{selected\.id\}/);
  assert.match(reportHistory, /data-entity-id=\{fact\.id\}/);
});

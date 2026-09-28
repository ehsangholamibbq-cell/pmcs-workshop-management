import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { test, expect } from "@playwright/test";
import ts from "typescript";
import { projectId, projectPath, tenantId, userId } from "./support";

// Run the production queue and database source directly in Chromium without
// adding a test-only product route before the dedicated UX2 screen exists.
const databaseSource = readFileSync(fileURLToPath(new URL("../lib/field-database.ts", import.meta.url)), "utf8")
  .replace(/^export /gmu, "");
const queueSource = readFileSync(fileURLToPath(new URL("../lib/collaboration-offline.ts", import.meta.url)), "utf8")
  .replace(/import \{[\s\S]*?\} from "\.\/field-database\.ts";/u, "")
  .replace(/^export /gmu, "");
const browserQueue = ts.transpileModule(
  `${databaseSource}\n${queueSource}\n` +
  "window.__pmcsCollaborationQueue = { setLocalIdentityScope, enqueueCollaborationMessage, " +
  "listQueuedCollaborationMessages, syncCollaborationMessages };",
  { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.None } },
).outputText.replace(/^export \{\};?$/gmu, "");

test("collaboration offline message survives reload, preserves retry ID and isolates account", async ({ page, context }) => {
  await page.goto(projectPath);
  await page.addScriptTag({ content: browserQueue });
  await context.setOffline(true);
  const queued = await page.evaluate(async ({ tenantId, userId, projectId }) => {
    const queue = (window as typeof window & { __pmcsCollaborationQueue: {
      setLocalIdentityScope: (tenant: string, user: string) => void;
      enqueueCollaborationMessage: (input: object) => Promise<{ clientMessageId: string }>;
    } }).__pmcsCollaborationQueue;
    queue.setLocalIdentityScope(tenantId, userId);
    return queue.enqueueCollaborationMessage({ tenantId, userId, projectId, body: "پیام آفلاین مرورگر" });
  }, { tenantId, userId, projectId });
  await context.setOffline(false);
  await page.reload();
  await page.addScriptTag({ content: browserQueue });
  const stored = await page.evaluate(async ({ tenantId, userId, projectId }) => {
    const queue = (window as typeof window & { __pmcsCollaborationQueue: {
      setLocalIdentityScope: (tenant: string, user: string) => void;
      listQueuedCollaborationMessages: (project: string) => Promise<readonly { clientMessageId: string }[]>;
    } }).__pmcsCollaborationQueue;
    queue.setLocalIdentityScope(tenantId, userId);
    const own = await queue.listQueuedCollaborationMessages(projectId);
    queue.setLocalIdentityScope(tenantId, "00000000-0000-4000-8000-000000000099");
    const foreign = await queue.listQueuedCollaborationMessages(projectId);
    queue.setLocalIdentityScope(tenantId, userId);
    return { own, foreign };
  }, { tenantId, userId, projectId });
  expect(stored.own).toHaveLength(1);
  expect(stored.own[0].clientMessageId).toBe(queued.clientMessageId);
  expect(stored.foreign).toHaveLength(0);

  const attempts: { key: string | undefined; id: string }[] = [];
  await page.route(`**/api/pmcs/api/v1/projects/${projectId}/collaboration/messages`, async (route) => {
    const payload = route.request().postDataJSON() as { clientMessageId: string };
    attempts.push({ key: route.request().headers()["idempotency-key"], id: payload.clientMessageId });
    await route.fulfill({ status: attempts.length === 1 ? 503 : 201,
      contentType: "application/json", body: JSON.stringify({ clientMessageId: payload.clientMessageId }) });
  });
  const send = () => page.evaluate(async (project) => {
    const queue = (window as typeof window & { __pmcsCollaborationQueue: {
      syncCollaborationMessages: (base: string, project: string) => Promise<{
        sent: number; remaining: number;
      }>;
    } }).__pmcsCollaborationQueue;
    return queue.syncCollaborationMessages("/api/pmcs", project);
  }, projectId);
  expect((await send()).remaining).toBe(1);
  expect((await send()).sent).toBe(1);
  expect(attempts).toEqual([
    { key: queued.clientMessageId, id: queued.clientMessageId },
    { key: queued.clientMessageId, id: queued.clientMessageId },
  ]);
});

import path from "node:path";
import { fileURLToPath } from "node:url";
import { defineConfig, devices } from "@playwright/test";

const currentDirectory = path.dirname(fileURLToPath(import.meta.url));
const authenticationState = path.join(currentDirectory, ".playwright", "authentication.json");

export default defineConfig({
  testDir: "./e2e",
  outputDir: "./test-results",
  timeout: 120_000,
  expect: { timeout: 15_000 },
  fullyParallel: false,
  forbidOnly: Boolean(process.env.CI),
  retries: 0,
  workers: 1,
  reporter: process.env.CI
    ? [["line"], ["html", { outputFolder: "playwright-report", open: "never" }]]
    : [["list"], ["html", { outputFolder: "playwright-report", open: "never" }]],
  use: {
    baseURL: process.env.PMCS_E2E_BASE_URL ?? "http://localhost:3000",
    locale: "fa-IR",
    timezoneId: "Asia/Tehran",
    colorScheme: "light",
    serviceWorkers: "allow",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    video: "retain-on-failure",
  },
  projects: [
    {
      name: "authenticate",
      testMatch: /.*\.setup\.ts/u,
      use: { ...devices["Desktop Chrome"] },
    },
    {
      name: "chromium",
      dependencies: ["authenticate"],
      testIgnore: /.*\.setup\.ts/u,
      use: {
        ...devices["Desktop Chrome"],
        storageState: authenticationState,
      },
    },
  ],
});

export { authenticationState };

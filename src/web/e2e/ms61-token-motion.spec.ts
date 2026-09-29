import { expect, test } from "@playwright/test";

test("active semantic tokens and reduced motion resolve in the browser", async ({ page }) => {
  await page.goto("/login");
  await expect(page.locator("html")).toHaveAttribute("lang", "fa");
  const tokens = await page.evaluate(() => {
    const root = getComputedStyle(document.documentElement);
    return ["--surface-canvas", "--surface-card", "--text-primary", "--text-secondary",
      "--status-info", "--status-success", "--status-warning", "--status-danger",
      "--focus-color"].map(name => root.getPropertyValue(name).trim());
  });
  expect(tokens.map(value => value === "#fff" ? "#ffffff" : value)).toEqual(["#fbf8f1", "#ffffff", "#17242e", "#5c6971",
    "#235c99", "#17684c", "#996017", "#aa3740", "#11643b"]);

  await page.evaluate(() => {
    const button = document.createElement("button");
    button.id = "ms61-focus-probe";
    button.textContent = "بررسی تمرکز";
    document.body.prepend(button);
  });
  const probe = page.locator("#ms61-focus-probe");
  await probe.focus();
  await page.keyboard.press("Tab");
  await page.keyboard.press("Shift+Tab");
  await expect(probe).toBeFocused();
  expect(await probe.evaluate(element => getComputedStyle(element).outlineWidth)).toBe("3px");
  await probe.evaluate(element => element.remove());

  await page.emulateMedia({ reducedMotion: "reduce" });
  const motion = await page.evaluate(() => {
    const button = document.createElement("button");
    button.textContent = "آزمون حرکت";
    button.style.animation = "none 2s infinite";
    button.style.transition = "color 2s";
    document.body.append(button);
    const style = getComputedStyle(button);
    const result = { preferred: matchMedia("(prefers-reduced-motion: reduce)").matches,
      animation: style.animationDuration, transition: style.transitionDuration };
    button.remove();
    return result;
  });
  expect(motion.preferred).toBe(true);
  expect(parseFloat(motion.animation)).toBeLessThanOrEqual(0.001);
  expect(parseFloat(motion.transition)).toBeLessThanOrEqual(0.001);
});

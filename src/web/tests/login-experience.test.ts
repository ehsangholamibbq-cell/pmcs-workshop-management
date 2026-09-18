import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import {
  fallbackLoginExperience,
  normalizeLoginExperience,
} from "../lib/login-experience.ts";

test("published login descriptor is reduced to allowlisted presentation tokens", () => {
  const descriptor = normalizeLoginExperience({
    version: 4,
    fallbackUsed: false,
    compositionVariant: "BlueprintSplit",
    surfaceTone: "WarmStone",
    accentPalette: "CorporateNavyGreen",
    motionPolicy: "Balanced",
    eyebrow: "سامانه جامع مدیریت پروژه",
    headline: "ساختن، فراتر از امروز",
    supportingText: "مرکز فرمان یکپارچه پروژه",
    logoUrl: "/api/v1/public/login-experience/assets/logo?tenantId=x&version=4",
    heroUrl: null,
  });

  assert.equal(descriptor.fallbackUsed, false);
  assert.equal(descriptor.logoUrl, "/api/login-experience/assets/logo?version=4");
  assert.equal(descriptor.heroUrl, null);
  assert.equal(descriptor.motionPolicy, "Balanced");
});

test("unknown tokens and markup fail closed to embedded login", () => {
  const fallback = fallbackLoginExperience();
  assert.deepEqual(normalizeLoginExperience({
    version: 9,
    fallbackUsed: false,
    compositionVariant: "RemoteScript",
    surfaceTone: "WarmStone",
    accentPalette: "CorporateNavyGreen",
    motionPolicy: "Balanced",
    eyebrow: "PMCS",
    headline: "<script>alert(1)</script>",
    supportingText: "bad",
  }), fallback);
});

test("login presentation cannot mutate authentication contract", () => {
  const model = readFileSync(new URL("../lib/login-experience.ts", import.meta.url), "utf8");
  const panel = readFileSync(new URL("../components/login-panel.tsx", import.meta.url), "utf8");
  const styles = readFileSync(new URL("../app/globals.css", import.meta.url), "utf8");

  for (const forbidden of ["issuer", "clientSecret", "authorizationUrl", "redirectUri", "html", "scriptUrl"]) {
    assert.equal(model.includes(forbidden), false, `presentation contract leaked ${forbidden}`);
  }
  assert.match(panel, /authClient\.signIn\.social/u);
  assert.match(panel, /provider: "keycloak"/u);
  assert.match(styles, /prefers-reduced-motion/u, "motion accessibility must remain represented");
});

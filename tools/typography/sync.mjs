import { createHash } from "node:crypto";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../..");
const contractPath = "assets/typography/pmcs-fonts.json";
const cssPath = "src/web/public/typography/pmcs-fonts.css";
const appCssPath = "src/web/app/typography.generated.css";
const csPath = "src/backend/Pmcs.Modules.Reporting/Rendering/PmcsTypographyContract.g.cs";
const manifestPath = "assets/reporting/fonts/manifest.json";
const envPath = "tools/qa/typography-env.sh";
const swPath = "src/web/public/sw.js";
const digest = /^[0-9a-f]{64}$/u;
const fileName = /^[A-Za-z0-9][A-Za-z0-9._-]*$/u;
const family = /^[\p{L}\p{N} ._-]+$/u;

function validName(value) { return typeof value === "string" && family.test(value) && value.length <= 100; }
function validFile(value) { return typeof value === "string" && fileName.test(value) && value.length <= 120; }
function assertContract(value) {
  if (!value || !/^\d+\.\d+\.\d+$/u.test(value.contractVersion) ||
      !validName(value.web?.family) || !Array.isArray(value.web?.fallback) ||
      value.web.fallback.length < 1 || value.web.fallback.length > 5 ||
      value.web.fallback.some((name) => !validName(name)) ||
      !validName(value.pdf?.family) || !validName(value.xlsx?.family) ||
      ![value.pdf?.regular, value.pdf?.bold].every((item) =>
        item && validFile(item.file) && digest.test(item.sha256) &&
        validName(item.style) && Number.isSafeInteger(item.fontVersion) && item.fontVersion > 0) ||
      ![value.pdf?.upstreamVersion, value.pdf?.sourcePackage, value.pdf?.source,
        value.pdf?.license].every((item) => typeof item === "string" && item.length > 0)) {
    throw new Error("Invalid versioned typography contract.");
  }
  const asset = value.web.asset;
  if (asset !== null && (!asset || !validFile(asset.file) ||
      !asset.file.endsWith(".woff2") || !digest.test(asset.sha256) ||
      !/^(?:[1-9]00|100 900)$/u.test(asset.weight))) {
    throw new Error("Invalid web font asset contract.");
  }
}

export function compileTypography(contract) {
  assertContract(contract);
  const names = [contract.web.family, ...contract.web.fallback];
  const cssNames = names.map((name) => name === "sans-serif" ? name : JSON.stringify(name)).join(", ");
  const asset = contract.web.asset;
  const css = `/* Generated from ${contractPath}; run node tools/typography/sync.mjs --write. */\n` +
    (asset ? `@font-face { font-family: ${JSON.stringify(contract.web.family)}; ` +
      `src: url("/typography/${asset.file}") format("woff2"); ` +
      `font-weight: ${asset.weight}; font-display: swap; }\n` : "") +
    `:root { --pmcs-font-family: ${cssNames}; }\n`;
  const cs = `// Generated from ${contractPath}; run node tools/typography/sync.mjs --write.\n` +
    `namespace Pmcs.Modules.Reporting.Rendering;\n\n` +
    `internal static class PmcsTypographyContract\n{\n` +
    `    public const string Version = ${JSON.stringify(contract.contractVersion)};\n` +
    `    public const string PdfFamily = ${JSON.stringify(contract.pdf.family)};\n` +
    `    public const string PdfRegularFileName = ${JSON.stringify(contract.pdf.regular.file)};\n` +
    `    public const string PdfBoldFileName = ${JSON.stringify(contract.pdf.bold.file)};\n` +
    `    public const string PdfRegularSha256 = ${JSON.stringify(contract.pdf.regular.sha256)};\n` +
    `    public const string PdfBoldSha256 = ${JSON.stringify(contract.pdf.bold.sha256)};\n` +
    `    public const string XlsxFamily = ${JSON.stringify(contract.xlsx.family)};\n}\n`;
  const manifest = JSON.stringify({ contractVersion: 1, family: contract.pdf.family,
    upstreamVersion: contract.pdf.upstreamVersion, sourcePackage: contract.pdf.sourcePackage,
    source: contract.pdf.source, license: contract.pdf.license,
    files: [contract.pdf.regular, contract.pdf.bold].map((item) => ({
      path: item.file, style: item.style, fontVersion: item.fontVersion, sha256: item.sha256,
    })) }, null, 2) + "\n";
  const env = `# Generated from ${contractPath}; run node tools/typography/sync.mjs --write.\n` +
    `PMCS_PDF_REGULAR_FONT_PATH="\${PWD}/assets/reporting/fonts/${contract.pdf.regular.file}"\n` +
    `PMCS_PDF_BOLD_FONT_PATH="\${PWD}/assets/reporting/fonts/${contract.pdf.bold.file}"\n` +
    `PMCS_PDF_REGULAR_FONT_SHA256=${contract.pdf.regular.sha256}\n` +
    `PMCS_PDF_BOLD_FONT_SHA256=${contract.pdf.bold.sha256}\n`;
  const assets = ["/offline.html", "/manifest.webmanifest", "/icon.svg", "/typography/pmcs-fonts.css",
    ...(asset ? [`/typography/${asset.file}`] : [])];
  return { css, cs, manifest, env, assets,
    cacheName: `pmcs-public-shell-font-v${contract.contractVersion}` };
}

function synchronize(mode) {
  if (!["--check", "--write"].includes(mode)) throw new Error("Use --check or --write.");
  const contract = JSON.parse(readFileSync(resolve(root, contractPath), "utf8"));
  const result = compileTypography(contract);
  const files = new Map([[cssPath, result.css], [appCssPath, result.css], [csPath, result.cs],
    [manifestPath, result.manifest], [envPath, result.env]]);
  for (const pdf of [contract.pdf.regular, contract.pdf.bold]) {
    const path = `assets/reporting/fonts/${pdf.file}`;
    const actual = createHash("sha256").update(readFileSync(resolve(root, path))).digest("hex");
    if (actual !== pdf.sha256) throw new Error(`PDF font digest mismatch: ${path}`);
  }
  if (contract.web.asset) {
    const source = `assets/typography/web/${contract.web.asset.file}`;
    const bytes = readFileSync(resolve(root, source));
    const actual = createHash("sha256").update(bytes).digest("hex");
    if (actual !== contract.web.asset.sha256) throw new Error(`Web font digest mismatch: ${source}`);
    files.set(`src/web/public/typography/${contract.web.asset.file}`, bytes);
  }
  const sw = readFileSync(resolve(root, swPath), "utf8");
  const nextSw = sw.replace(/^const cacheName = "[^"]+";$/mu,
    `const cacheName = ${JSON.stringify(result.cacheName)};`)
    .replace(/^const shellAssets = \[[^\n]+\];$/mu,
      `const shellAssets = ${JSON.stringify(result.assets)};`);
  if (nextSw === sw && (!sw.includes(result.cacheName) || !sw.includes(JSON.stringify(result.assets)))) {
    throw new Error("Service worker shell contract is missing.");
  }
  files.set(swPath, nextSw);
  const drift = [];
  for (const [path, expected] of files) {
    const target = resolve(root, path);
    const current = existsSync(target) ? readFileSync(target) : null;
    const output = Buffer.isBuffer(expected) ? expected : Buffer.from(expected);
    if (current?.equals(output)) continue;
    drift.push(path);
    if (mode === "--write") {
      mkdirSync(dirname(target), { recursive: true });
      writeFileSync(target, output);
    }
  }
  if (drift.length && mode === "--check") throw new Error(`Typography outputs drifted: ${drift.join(", ")}`);
  return drift;
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const changed = synchronize(process.argv[2]);
  process.stdout.write(changed.length ? `Typography synchronized: ${changed.join(", ")}\n` :
    "Typography contract verified.\n");
}

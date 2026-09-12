const fallbackPath = "/portfolio";

export function safeApplicationReturnPath(candidate: string | null): string {
  if (!candidate || !candidate.startsWith("/") || candidate.startsWith("//") || candidate.includes("\\")) {
    return fallbackPath;
  }

  let parsed: URL;
  try {
    parsed = new URL(candidate, "https://pmcs.invalid");
  } catch {
    return fallbackPath;
  }

  if (parsed.origin !== "https://pmcs.invalid" || !isApplicationPath(parsed.pathname)) {
    return fallbackPath;
  }

  return `${parsed.pathname}${parsed.search}`;
}

function isApplicationPath(pathname: string): boolean {
  return pathname === "/" ||
    pathname === "/portfolio" ||
    pathname.startsWith("/portfolio/") ||
    pathname === "/projects" ||
    pathname.startsWith("/projects/") ||
    pathname === "/admin/users";
}

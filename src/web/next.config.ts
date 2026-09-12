import type { NextConfig } from "next";
import { webSecurityHeaders } from "./lib/web-security.ts";

const nextConfig: NextConfig = {
  output: "standalone",
  poweredByHeader: false,
  reactStrictMode: true,
  async headers() {
    return [{ source: "/:path*", headers: [...webSecurityHeaders] }];
  },
};

export default nextConfig;

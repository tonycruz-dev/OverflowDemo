import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Produce the .next/standalone output used by the Dockerfile
  output: "standalone",
  logging: {
    fetches: {
      fullUrl: true,
    },
  },
  // Optional: avoid failing container builds due to lint errors
  eslint: {
    ignoreDuringBuilds: true,
  },
};


export default nextConfig;

#!/usr/bin/env bash
# Package Builds/WebGL + instructor page into Web/dist and deploy to Cloudflare Pages (functions/ + D1 ride along).
set -euo pipefail
cd "$(dirname "$0")"
rm -rf dist && mkdir -p dist
cp -r ../Builds/WebGL/. dist/
cp public/instructor.html dist/
cat > dist/_headers <<'H'
/Build/*
  Cache-Control: public, max-age=86400
/*.wasm*
  Content-Type: application/wasm
/api/*
  Cache-Control: no-store
H
find dist -type f -size +25M -printf "TOO BIG for Pages (25 MiB): %p\n" | grep . && exit 1 || true
npx wrangler d1 migrations apply competent-person --remote
npx wrangler pages deploy dist --project-name competent-person --branch main --commit-dirty=true

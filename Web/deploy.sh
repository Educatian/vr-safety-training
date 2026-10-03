#!/usr/bin/env bash
# Package Builds/WebGL + instructor page into Web/dist and deploy to Cloudflare Pages (functions/ + D1 ride along).
set -euo pipefail
cd "$(dirname "$0")"
rm -rf dist && mkdir -p dist
cp -r ../Builds/WebGL/. dist/
cp public/instructor.html public/index.html dist/   # full-window page replaces Unity's template
cat > dist/_headers <<'H'
/Build/*
  Cache-Control: public, max-age=86400
/*.wasm*
  Content-Type: application/wasm
/api/*
  Cache-Control: no-store
H
# Pages serves files <= 25 MiB; larger build files go to R2 and functions/Build/[[path]].js serves them same-origin.
for f in $(find dist/Build -type f -size +24M); do
  key="Build/$(basename "$f")"
  echo "R2 upload: $key ($(du -h "$f" | cut -f1))"
  npx wrangler r2 object put "competent-person-build/$key" --file "$f" --remote
  rm "$f"
done
npx wrangler d1 migrations apply competent-person --remote
npx wrangler pages deploy dist --project-name competent-person --branch main --commit-dirty=true

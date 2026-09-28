// Shared helpers for the Pages Functions.
export const json = (body, status = 200, headers = {}) =>
  new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json", "Cache-Control": "no-store", ...headers } });

// Same-origin only: the game and the API are served from the same Pages site.
export function sameOrigin(request) {
  const self = new URL(request.url).origin;
  const origin = request.headers.get("Origin");
  if (origin) return origin === self;
  const ref = request.headers.get("Referer");
  return !!ref && ref.startsWith(self + "/");
}

export async function sha256hex(s) {
  const d = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(s));
  return [...new Uint8Array(d)].map((b) => b.toString(16).padStart(2, "0")).join("");
}

export async function hmac(secret, msg) {
  const key = await crypto.subtle.importKey("raw", new TextEncoder().encode(secret), { name: "HMAC", hash: "SHA-256" }, false, ["sign"]);
  return new Uint8Array(await crypto.subtle.sign("HMAC", key, new TextEncoder().encode(msg)));
}

// Fixed-window counter in D1. Returns true if this call is within `limit` per `windowSec` for key.
export async function allow(db, key, limit, windowSec) {
  const w = Math.floor(Date.now() / 1000 / windowSec);
  const row = await db
    .prepare("INSERT INTO ratelimit (k, w, n) VALUES (?1, ?2, 1) ON CONFLICT(k, w) DO UPDATE SET n = n + 1 RETURNING n")
    .bind(key, w).first();
  return (row?.n ?? 1) <= limit;
}

export const clip = (v, n) => (typeof v === "string" ? v.slice(0, n) : "");
export const num = (v) => (Number.isFinite(Number(v)) ? Number(v) : 0);

export async function body(request, max = 64_000) {
  const raw = await request.text();
  if (raw.length > max) throw new Error("too large");
  return JSON.parse(raw);
}

export function instructorOk(request, env) {
  const key = request.headers.get("X-Instructor-Key") || new URL(request.url).searchParams.get("key") || "";
  return !!env.INSTRUCTOR_KEY && key.length >= 16 && key === env.INSTRUCTOR_KEY;
}

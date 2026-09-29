// Play-event ingest from the game (batched every ~10 s). PII-free by construction.
import { json, sameOrigin, sha256hex, allow, clip, num, body } from "./_lib.js";

// -1 (unscored) -> NULL; otherwise clamp to 0..1.
const score = (v) => { const n = Number(v); return Number.isFinite(n) && n >= 0 ? Math.min(1, n) : null; };

export async function onRequestPost({ request, env }) {
  if (!sameOrigin(request)) return json({ error: "forbidden" }, 403);
  const ip = request.headers.get("CF-Connecting-IP") || "0";
  if (!(await allow(env.DB, "ev:" + (await sha256hex(ip)).slice(0, 16), 120, 600))) return json({ error: "slow down" }, 429);
  let b;
  try { b = await body(request, 128_000); } catch { return json({ error: "bad request" }, 400); }
  const rows = Array.isArray(b.rows) ? b.rows.slice(0, 400) : [];
  const session = clip(b.session, 40);
  if (!session || !rows.length) return json({ ok: true, stored: 0 });
  const stmt = env.DB.prepare(
    "INSERT INTO events (session, class_code, learner, episode, seed, t, kind, condition, detail, clock, cfr, ksa, score) VALUES (?1,?2,?3,?4,?5,?6,?7,?8,?9,?10,?11,?12,?13)");
  await env.DB.batch(rows.map((r) =>
    stmt.bind(session, clip(b.classCode, 24), clip(b.learner, 24), num(b.episode), num(b.seed),
      clip(r.t, 40), clip(r.kind, 40), clip(r.condition, 60), clip(r.detail, 200), num(r.clock),
      clip(r.cfr, 40), clip(r.ksa, 20), score(r.score))));
  return json({ ok: true, stored: rows.length });
}

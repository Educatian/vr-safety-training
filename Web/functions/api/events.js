// Play-event ingest from the game (batched every ~10 s). PII-free by construction.
// Schema v1: versioned batches, kind allowlist, roster-code check, opportunistic retention purge.
import { json, sameOrigin, sha256hex, allow, clip, num, body } from "./_lib.js";
import { SCHEMA, KINDS, CODE, RETENTION_DAYS_DEFAULT } from "./_kinds.js";

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
  const schema = typeof b.schema === "string" ? b.schema : "v0";
  if (schema !== SCHEMA && schema !== "v0") return json({ error: "unknown schema " + clip(schema, 20) }, 400);
  const bad = rows.filter((r) => typeof r?.kind !== "string" || !KINDS.has(r.kind)).map((r) => clip(String(r?.kind), 40));
  if (bad.length) return json({ error: "unknown event kind", kinds: [...new Set(bad)].slice(0, 10) }, 400);
  const classCode = clip(b.classCode, 24), learner = clip(b.learner, 24);
  if (!CODE.test(classCode) || !CODE.test(learner)) return json({ error: "class/learner must be roster codes (no names)" }, 400);

  const stmt = env.DB.prepare(
    "INSERT INTO events (session, class_code, learner, episode, seed, t, kind, condition, detail, clock, cfr, ksa, score, schema, build, consent, ecd)" +
    " VALUES (?1,?2,?3,?4,?5,?6,?7,?8,?9,?10,?11,?12,?13,?14,?15,?16,?17)");
  await env.DB.batch(rows.map((r) =>
    stmt.bind(session, classCode, learner, num(b.episode), num(b.seed),
      clip(r.t, 40), clip(r.kind, 40), clip(r.condition, 60), clip(r.detail, 200), num(r.clock),
      clip(r.cfr, 40), clip(r.ksa, 20), score(r.score), schema, clip(b.build, 40), clip(b.consent, 24), clip(b.ecd, 24))));

  // Retention: ~1 in 50 ingests purges rows older than RETENTION_DAYS (env override).
  if (Math.random() < 0.02) {
    const days = Math.max(30, num(env.RETENTION_DAYS) || RETENTION_DAYS_DEFAULT);
    await env.DB.prepare("DELETE FROM events WHERE received < datetime('now', ?1)").bind(`-${days} days`).run();
  }
  return json({ ok: true, stored: rows.length });
}

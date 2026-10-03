// Instructor report for one class code: completions + per-student event summary; ?format=csv for raw events.
import { json, instructorOk } from "./_lib.js";

export async function onRequestGet({ request, env }) {
  if (!instructorOk(request, env)) return json({ error: "instructor key required" }, 401);
  const url = new URL(request.url);
  const cls = (url.searchParams.get("class") || "").trim().toUpperCase().slice(0, 24);
  if (!cls) return json({ error: "class required" }, 400);

  if (url.searchParams.get("format") === "csv") {
    const { results } = await env.DB.prepare(
      "SELECT received, session, learner, episode, seed, t, kind, condition, detail, clock, cfr, ksa, score, schema, build, consent, ecd FROM events WHERE class_code = ?1 ORDER BY id LIMIT 50000")
      .bind(cls).all();
    const cols = ["received", "session", "learner", "episode", "seed", "t", "kind", "condition", "detail", "clock", "cfr", "ksa", "score", "schema", "build", "consent", "ecd"];
    const esc = (v) => `"${String(v ?? "").replaceAll('"', '""')}"`;
    const csv = [cols.join(","), ...results.map((r) => cols.map((c) => esc(r[c])).join(","))].join("\n");
    return new Response(csv, { headers: { "Content-Type": "text/csv", "Content-Disposition": `attachment; filename="${cls}_events.csv"`, "Cache-Control": "no-store" } });
  }

  const completions = (await env.DB.prepare(
    "SELECT code, received, learner, episode, xp, hii, precision, incidents, quiz_correct, quiz_total FROM completions WHERE class_code = ?1 ORDER BY learner, episode, received")
    .bind(cls).all()).results;
  const activity = (await env.DB.prepare(
    `SELECT learner, episode, COUNT(DISTINCT session) AS sessions, SUM(kind = 'report') AS reports, SUM(kind = 'hint') AS hints,
            SUM(kind = 'stop_work') AS stops, SUM(kind = 'install_success') AS installs, SUM(kind IN ('NearMiss','Recordable')) AS incidents,
            MAX(received) AS last_seen
       FROM events WHERE class_code = ?1 GROUP BY learner, episode ORDER BY learner, episode`)
    .bind(cls).all()).results;
  // Performance per OSHA standard and per KSA (Knowledge / Skills / Attitudes), from every scored action.
  const osha = (await env.DB.prepare(
    `SELECT cfr, COUNT(*) AS n, AVG(score) AS mean, COUNT(DISTINCT learner) AS learners,
            SUM(kind IN ('NearMiss','Recordable')) AS incidents, SUM(kind = 'ksa' AND detail = 'missed') AS missed
       FROM events WHERE class_code = ?1 AND score IS NOT NULL AND cfr IS NOT NULL AND cfr <> ''
      GROUP BY cfr ORDER BY mean ASC`).bind(cls).all()).results;
  const ksa = (await env.DB.prepare(
    `SELECT learner, ksa, COUNT(*) AS n, AVG(score) AS mean FROM events
      WHERE class_code = ?1 AND score IS NOT NULL AND ksa IS NOT NULL AND ksa <> '' AND kind <> 'ksa_profile'
      GROUP BY learner, ksa ORDER BY learner, ksa`).bind(cls).all()).results;
  const missions = (await env.DB.prepare(
    `SELECT learner, episode, SUM(kind = 'mission_step') AS steps, SUM(kind = 'mission_complete') AS completed
       FROM events WHERE class_code = ?1 GROUP BY learner, episode ORDER BY learner, episode`).bind(cls).all()).results;
  return json({ class: cls, completions, activity, osha, ksa, missions });
}

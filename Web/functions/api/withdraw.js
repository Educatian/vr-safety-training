// Research-data withdrawal (quality review 2026-09-30, area 12).
//  - Participant: POST {sessions:[...]} with the session ids the game kept on their device (unguessable GUIDs act as the
//    capability; no key needed). Deletes those sessions' events and completions.
//  - Instructor/researcher: POST {classCode, learner} with X-Instructor-Key. Deletes everything for that roster code.
// Every withdrawal is recorded (counts only) in the withdrawals table.
import { json, sameOrigin, allow, sha256hex, clip, body, instructorOk } from "./_lib.js";
import { CODE } from "./_kinds.js";

const SID = /^[a-f0-9]{32}$/;

export async function onRequestPost({ request, env }) {
  if (!sameOrigin(request) && !instructorOk(request, env)) return json({ error: "forbidden" }, 403);
  const ip = request.headers.get("CF-Connecting-IP") || "0";
  if (!(await allow(env.DB, "wd:" + (await sha256hex(ip)).slice(0, 16), 20, 600))) return json({ error: "slow down" }, 429);
  let b;
  try { b = await body(request, 16_000); } catch { return json({ error: "bad request" }, 400); }

  if (instructorOk(request, env) && b.learner) {
    const cls = clip(b.classCode, 24).toUpperCase(), learner = clip(b.learner, 24);
    if (!cls || !CODE.test(cls) || !CODE.test(learner)) return json({ error: "classCode and learner codes required" }, 400);
    const ev = await env.DB.prepare("DELETE FROM events WHERE class_code = ?1 AND learner = ?2").bind(cls, learner).run();
    const co = await env.DB.prepare("DELETE FROM completions WHERE class_code = ?1 AND learner = ?2").bind(cls, learner).run();
    await env.DB.prepare("INSERT INTO withdrawals (scope, class_code, sessions, events_deleted, completions_deleted) VALUES ('learner', ?1, NULL, ?2, ?3)")
      .bind(cls, ev.meta?.changes ?? 0, co.meta?.changes ?? 0).run();
    return json({ ok: true, events: ev.meta?.changes ?? 0, completions: co.meta?.changes ?? 0 });
  }

  const sessions = (Array.isArray(b.sessions) ? b.sessions : []).filter((s) => typeof s === "string" && SID.test(s)).slice(0, 200);
  if (!sessions.length) return json({ error: "sessions required" }, 400);
  let events = 0, completions = 0;
  for (const s of sessions) {
    events += (await env.DB.prepare("DELETE FROM events WHERE session = ?1").bind(s).run()).meta?.changes ?? 0;
    completions += (await env.DB.prepare("DELETE FROM completions WHERE session = ?1").bind(s).run()).meta?.changes ?? 0;
  }
  await env.DB.prepare("INSERT INTO withdrawals (scope, class_code, sessions, events_deleted, completions_deleted) VALUES ('participant', NULL, ?1, ?2, ?3)")
    .bind(sessions.length, events, completions).run();
  return json({ ok: true, events, completions });
}

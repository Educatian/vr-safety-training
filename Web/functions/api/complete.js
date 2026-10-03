// Episode completion: the server signs the summary and issues a short code the student hands in.
import { json, sameOrigin, hmac, clip, num, body } from "./_lib.js";

const ALPHA = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

export async function onRequestPost({ request, env }) {
  if (!sameOrigin(request)) return json({ error: "forbidden" }, 403);
  if (!env.CODE_SECRET) return json({ error: "not configured" }, 503);
  let c;
  try { c = await body(request, 8_000); } catch { return json({ error: "bad request" }, 400); }
  const rec = {
    session: clip(c.session, 40), class_code: clip(c.classCode, 24), learner: clip(c.learner, 24), episode: num(c.episode),
    xp: Math.round(num(c.xp)), hii: num(c.hii), precision: num(c.precision), incidents: Math.round(num(c.incidents)),
    quiz_correct: Math.round(num(c.quizCorrect)), quiz_total: Math.round(num(c.quizTotal)),
  };
  if (!rec.session || rec.episode < 1) return json({ error: "bad request" }, 400);
  const sig = await hmac(env.CODE_SECRET, JSON.stringify(rec) + "|" + Date.now());
  let code = "CP" + rec.episode + "-";
  for (let i = 0; i < 8; i++) code += ALPHA[sig[i] % ALPHA.length] + (i === 3 ? "-" : "");
  await env.DB.prepare(
    "INSERT INTO completions (code, session, class_code, learner, episode, xp, hii, precision, incidents, quiz_correct, quiz_total) VALUES (?1,?2,?3,?4,?5,?6,?7,?8,?9,?10,?11)")
    .bind(code, rec.session, rec.class_code, rec.learner, rec.episode, rec.xp, rec.hii, rec.precision, rec.incidents, rec.quiz_correct, rec.quiz_total)
    .run();
  return json({ code });
}

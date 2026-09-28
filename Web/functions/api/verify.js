// Public check of a completion code (shows only what the code certifies).
import { json } from "./_lib.js";

export async function onRequestGet({ request, env }) {
  const code = (new URL(request.url).searchParams.get("code") || "").trim().toUpperCase().slice(0, 20);
  if (!/^CP\d-[A-Z0-9]{4}-[A-Z0-9]{4}$/.test(code)) return json({ valid: false }, 400);
  const r = await env.DB.prepare(
    "SELECT code, received, class_code, learner, episode, xp, hii, precision, incidents, quiz_correct, quiz_total FROM completions WHERE code = ?1")
    .bind(code).first();
  return json(r ? { valid: true, ...r } : { valid: false });
}

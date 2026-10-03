// Hazard Hunt daily leaderboard. GET ?day=N -> top 20 for that daily site; POST -> one first-attempt score per round.
// Public and opt-in: the player types a display handle (3-12 letters/digits/_), checked against a short blocklist.
// The game cannot be verified server-side, so the checks are plausibility limits, not proof: one row per round
// session, first attempts only, today or yesterday only, and a per-IP budget.
import { json, sameOrigin, sha256hex, allow, clip, num, body } from "./_lib.js";

export const EPOCH = Date.UTC(2026, 9, 1);           // Daily Site #1 = 2026-10-01 UTC (Core/Arcade.cs DailySite.Epoch)
export const dayNumber = (ms = Date.now()) => Math.max(1, Math.floor((ms - EPOCH) / 86400000) + 1);
export const HANDLE = /^[A-Za-z0-9_]{3,12}$/;
const BLOCK = ["fuck", "shit", "cunt", "bitch", "nigg", "fag", "rape", "nazi", "hitler", "porn", "dick", "cock", "pussy", "slut", "whore", "retard", "kkk", "admin", "osha"];
export const handleOk = (h) => typeof h === "string" && HANDLE.test(h) && !BLOCK.some((w) => h.toLowerCase().includes(w));
const GRADES = new Set(["S", "A", "B", "C", "D"]);
// Mirrors ArcadeRules: 350 per hazard, a time bonus of up to 180 s x 5, and +50 per confirmed look-alike (<= 20).
export const maxScore = (total) => total * 350 + 900 + 20 * 50;

export function validate(b, today = dayNumber()) {
  const day = num(b.day), total = num(b.total), found = num(b.found), score = num(b.score);
  const fa = num(b.falseAlarms), inc = num(b.incidents), secs = num(b.seconds);
  if (day !== today && day !== today - 1) return "that daily site is closed";
  if (b.replay) return "only first attempts go on the board";
  if (!handleOk(b.handle)) return "handle: 3-12 letters, digits or _ (and keep it clean)";
  if (!clip(b.session, 40)) return "missing session";
  if (!(total >= 1 && total <= 12 && found >= 0 && found <= total)) return "implausible hazard count";
  if (!(fa >= 0 && fa <= 50 && inc >= 0 && inc <= total && secs >= 0 && secs <= 200)) return "implausible round";
  if (!(score >= 0 && score <= maxScore(total)) || !Number.isInteger(score)) return "implausible score";
  if (!GRADES.has(b.grade)) return "bad grade";
  return null;
}

export async function onRequestGet({ request, env }) {
  const url = new URL(request.url);
  const day = num(url.searchParams.get("day")) || dayNumber();
  const top = await env.DB.prepare(
    "SELECT handle, score, grade, found, total, false_alarms AS falseAlarms, seconds FROM arcade_scores WHERE day = ?1 ORDER BY score DESC, seconds ASC, id ASC LIMIT 20"
  ).bind(day).all();
  const count = await env.DB.prepare("SELECT COUNT(*) AS n FROM arcade_scores WHERE day = ?1").bind(day).first();
  // Explicit projection: only display fields ever leave the server (never the round session or build).
  const entries = (top.results ?? []).map(({ handle, score, grade, found, total, falseAlarms, seconds }) => ({ handle, score, grade, found, total, falseAlarms, seconds }));
  return json({ day, entries, players: count?.n ?? 0 }, 200, { "Cache-Control": "public, max-age=20" });
}

export async function onRequestPost({ request, env }) {
  if (!sameOrigin(request)) return json({ error: "forbidden" }, 403);
  const ip = (await sha256hex(request.headers.get("CF-Connecting-IP") || "0")).slice(0, 16);
  if (!(await allow(env.DB, "sc:" + ip, 10, 3600))) return json({ error: "slow down" }, 429);
  let b;
  try { b = await body(request, 4_000); } catch { return json({ error: "bad request" }, 400); }
  const problem = validate(b);
  if (problem) return json({ error: problem }, 400);
  try {
    await env.DB.prepare(
      "INSERT INTO arcade_scores (day, handle, score, found, total, false_alarms, incidents, grade, seconds, session, build) VALUES (?1,?2,?3,?4,?5,?6,?7,?8,?9,?10,?11)"
    ).bind(num(b.day), b.handle, num(b.score), num(b.found), num(b.total), num(b.falseAlarms), num(b.incidents), b.grade, num(b.seconds), clip(b.session, 40), clip(b.build, 40)).run();
  } catch {
    return json({ error: "this round is already on the board" }, 409);
  }
  const rank = await env.DB.prepare(
    "SELECT COUNT(*) + 1 AS r FROM arcade_scores WHERE day = ?1 AND (score > ?2 OR (score = ?2 AND seconds < ?3))"
  ).bind(num(b.day), num(b.score), num(b.seconds)).first();
  const count = await env.DB.prepare("SELECT COUNT(*) AS n FROM arcade_scores WHERE day = ?1").bind(num(b.day)).first();
  if (Math.random() < 0.05) await env.DB.prepare("DELETE FROM arcade_scores WHERE received < datetime('now', '-60 days')").run();
  return json({ ok: true, rank: rank?.r ?? 1, players: count?.n ?? 1 });
}

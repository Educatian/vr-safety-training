// Node self-test for the Hazard Hunt leaderboard against an in-memory mock of D1.
// Run: node Web/qa/api_scores.test.mjs   (exit 1 on failure)
import assert from "node:assert/strict";
const lib = new URL("../functions/api/", import.meta.url);
const S = await import(new URL("scores.js", lib));

function mockDb() {
  const rows = [];
  const db = {
    rows,
    prepare(sql) {
      const st = { sql, args: [], bind(...a) { return { ...st, args: a }; },
        async run() {
          if (sql.startsWith("INSERT INTO arcade_scores")) {
            const [day, handle, score, found, total, fa, inc, grade, seconds, session] = this.args;
            if (rows.some((r) => r.day === day && r.session === session)) throw new Error("UNIQUE");
            rows.push({ day, handle, score, found, total, falseAlarms: fa, grade, seconds, session, id: rows.length + 1 });
          }
          return { meta: { changes: 1 } };
        },
        async first() {
          if (sql.includes("ratelimit")) return { n: 1 };
          const day = this.args[0];
          if (sql.includes("COUNT(*) + 1")) { const [, sc, secs] = this.args; return { r: 1 + rows.filter((r) => r.day === day && (r.score > sc || (r.score === sc && r.seconds < secs))).length }; }
          if (sql.includes("COUNT(*)")) return { n: rows.filter((r) => r.day === day).length };
          return null;
        },
        async all() {
          const day = this.args[0];
          return { results: rows.filter((r) => r.day === day).sort((a, b) => b.score - a.score || a.seconds - b.seconds).slice(0, 20) };
        } };
      return st;
    },
  };
  return db;
}

const origin = "https://competent-person.pages.dev";
const post = (db, b, o = origin) => S.onRequestPost({ request: new Request(origin + "/api/scores", { method: "POST", headers: { Origin: o, "Content-Type": "application/json" }, body: JSON.stringify(b) }), env: { DB: db } });
const get = (db, day) => S.onRequestGet({ request: new Request(origin + "/api/scores?day=" + day), env: { DB: db } });

// Day numbering matches Core/Arcade.cs DailySite (#1 on 2026-10-01 UTC).
assert.equal(S.dayNumber(Date.UTC(2026, 9, 1, 0, 0)), 1);
assert.equal(S.dayNumber(Date.UTC(2026, 9, 3, 23, 59)), 3);

const today = S.dayNumber();
const round = (over = {}) => ({ day: today, handle: "SpotterJo", score: 1240, found: 4, total: 5, falseAlarms: 0, incidents: 0, grade: "A", seconds: 161, session: "s1", replay: false, ...over });

const db = mockDb();
let r = await post(db, round());
assert.equal(r.status, 200); let j = await r.json(); assert.equal(j.rank, 1);
r = await post(db, round()); assert.equal(r.status, 409, "one row per round");
r = await post(db, round({ session: "s2", handle: "Beta_2", score: 1500 })); j = await r.json(); assert.equal(j.rank, 1); assert.equal(j.players, 2);
r = await post(db, round({ session: "s3", replay: true })); assert.equal(r.status, 400, "replays stay off the board");
r = await post(db, round({ session: "s4", day: today - 3 })); assert.equal(r.status, 400, "old daily closed");
r = await post(db, round({ session: "s5", handle: "a b" })); assert.equal(r.status, 400, "handle charset");
r = await post(db, round({ session: "s6", handle: "xxFuCkxx" })); assert.equal(r.status, 400, "blocklist");
r = await post(db, round({ session: "s7", score: S.maxScore(5) + 1 })); assert.equal(r.status, 400, "score cap");
r = await post(db, round({ session: "s8", found: 6 })); assert.equal(r.status, 400, "found <= total");
r = await post(db, round({ session: "s9" }), "https://evil.example"); assert.equal(r.status, 403, "same origin only");
r = await get(db, today); j = await r.json();
assert.deepEqual(j.entries.map((e) => e.handle), ["Beta_2", "SpotterJo"]);
assert.equal(j.players, 2);
assert.ok(!("session" in j.entries[0]), "sessions are never returned");
console.log("api_scores: all checks passed");

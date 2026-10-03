// Node self-test for the schema-v1 ingest and the withdrawal endpoint against an in-memory mock of D1.
// Run: node Web/qa/api_events_withdraw.test.mjs   (exit 1 on failure)
import assert from "node:assert/strict";
const lib = new URL("../functions/api/", import.meta.url);
const { onRequestPost: ingest } = await import(new URL("events.js", lib));
const { onRequestPost: withdraw } = await import(new URL("withdraw.js", lib));

function mockDb() {
  const events = [], completions = [], withdrawals = [];
  const db = {
    events, completions, withdrawals,
    prepare(sql) {
      const st = { sql, args: [], bind(...a) { return { ...st, args: a }; },
        async run() {
          if (sql.startsWith("DELETE FROM events WHERE session")) { const n = events.filter((e) => e.session === this.args[0]).length; remove(events, (e) => e.session === this.args[0]); return { meta: { changes: n } }; }
          if (sql.startsWith("DELETE FROM completions WHERE session")) { const n = completions.filter((e) => e.session === this.args[0]).length; remove(completions, (e) => e.session === this.args[0]); return { meta: { changes: n } }; }
          if (sql.startsWith("DELETE FROM events WHERE class_code")) { const f = (e) => e.class_code === this.args[0] && e.learner === this.args[1]; const n = events.filter(f).length; remove(events, f); return { meta: { changes: n } }; }
          if (sql.startsWith("DELETE FROM completions WHERE class_code")) return { meta: { changes: 0 } };
          if (sql.startsWith("INSERT INTO withdrawals")) { withdrawals.push(this.args); return { meta: { changes: 1 } }; }
          if (sql.includes("ratelimit") || sql.startsWith("DELETE FROM events WHERE received")) return { meta: { changes: 0 } };
          return { meta: { changes: 0 } };
        },
        async first() { return { n: 1 }; } };
      return st;
    },
    async batch(stmts) { for (const s of stmts) if (s.sql.startsWith("INSERT INTO events")) { const [session, class_code, learner] = s.args; events.push({ session, class_code, learner, kind: s.args[6], schema: s.args[13] }); } return []; },
  };
  return db;
}
const remove = (arr, f) => { for (let i = arr.length - 1; i >= 0; i--) if (f(arr[i])) arr.splice(i, 1); };
const req = (body, headers = {}) => new Request("https://competent-person.pages.dev/api/x", { method: "POST", body: JSON.stringify(body),
  headers: { Origin: "https://competent-person.pages.dev", Host: "competent-person.pages.dev", "Content-Type": "application/json", ...headers } });

const env = { DB: mockDb(), INSTRUCTOR_KEY: "k".repeat(20) };
const sid = "a".repeat(32);
const row = (kind) => ({ t: "2026-09-30T00:00:00Z", kind, condition: "x", detail: "", clock: 1, cfr: "", ksa: "", score: -1 });

let r = await ingest({ request: req({ schema: "cp-events-v1", session: sid, classCode: "LTPS210", learner: "S01", episode: 1, rows: [row("photo"), row("report")] }), env });
assert.equal(r.status, 200, "valid batch stored"); assert.equal(env.DB.events.length, 2); assert.equal(env.DB.events[0].schema, "cp-events-v1");
r = await ingest({ request: req({ schema: "cp-events-v1", session: sid, classCode: "LTPS210", learner: "S01", rows: [row("made_up")] }), env });
assert.equal(r.status, 400, "unknown kind rejected");
r = await ingest({ request: req({ schema: "cp-events-v1", session: sid, classCode: "LTPS210", learner: "John Smith", rows: [row("photo")] }), env });
assert.equal(r.status, 400, "typed real name rejected");
r = await ingest({ request: req({ schema: "cp-events-v9", session: sid, rows: [row("photo")] }), env });
assert.equal(r.status, 400, "unknown schema rejected");
r = await withdraw({ request: req({ sessions: [sid] }), env });
assert.equal(r.status, 200); assert.equal((await r.json()).events, 2); assert.equal(env.DB.events.length, 0, "participant withdrawal deletes the session");
assert.equal(env.DB.withdrawals.length, 1, "withdrawal audited");
r = await withdraw({ request: req({ sessions: ["not-a-session"] }), env });
assert.equal(r.status, 400, "bad session id refused");
console.log("api_events_withdraw: all checks passed");

"""Instructor debrief dashboard generator for VR Safety Training telemetry.

Reads the JSONL session logs written under Unity's persistentDataPath
(SafetyTrainingLogs) and emits a self-contained dashboard.html with
per-session and per-site summaries: evidence precision, inspection
outcomes, decision first-attempt rates, placement error distribution,
coach turns, and position heatmaps.

Usage:
  py build_dashboard.py [--logs <dir>] [--out <dashboard.html>]

Robustness: malformed (torn) lines are skipped and counted, matching the
in-editor CSV exporter behaviour.
"""
from __future__ import annotations

import argparse
import json
import math
import os
import sys
from collections import defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from bkt import fit_bkt, mastery_summary  # noqa: E402

DEFAULT_LOGS = Path(os.environ.get("LOCALAPPDATA", "")).parent / \
    "LocalLow" / "DefaultCompany" / "vr-safety-training" / "SafetyTrainingLogs"

SITES = ["Construction", "Warehouse", "FireResponse", "ChemicalProcessing", "ElectricalMaintenance", "TowerCrane"]


def read_jsonl(directory: Path):
    entries, skipped = [], 0
    for path in sorted(directory.glob("*.jsonl")):
        source = "inquiry" if path.name.startswith("inquiry_") else "spatial"
        try:
            lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
        except OSError:
            continue
        for line in lines:
            line = line.strip()
            if not line:
                continue
            try:
                record = json.loads(line)
            except json.JSONDecodeError:
                skipped += 1
                continue
            record["_source"] = source
            entries.append(record)
    return entries, skipped


def aggregate(entries):
    sessions = defaultdict(lambda: {
        "inspections": 0, "hazards": 0, "false_positives": 0,
        "evidence": 0, "distractors": 0, "placement_ok": 0, "placement_retry": 0,
        "coach_turns": 0, "sites": set(), "first_ts": None, "last_ts": None,
    })
    site_evidence = {s: {"evidence": 0, "distractors": 0} for s in SITES}
    site_coach = {s: 0 for s in SITES}
    site_inspect = {s: {"hazard": 0, "false_positive": 0} for s in SITES}
    release_distances = []
    heat = {s: defaultdict(int) for s in SITES}
    decision_first = defaultdict(lambda: {"met": 0, "not_met": 0})
    seen_decision = set()
    objective_sequences = defaultdict(lambda: defaultdict(list))
    first_attempt_marks = defaultdict(int)

    for r in entries:
        sid = r.get("sessionId", "")
        site = r.get("site", "")
        event = r.get("eventType", "")
        s = sessions[sid]
        ts = r.get("timestampUtc", "")
        if ts:
            s["first_ts"] = min(s["first_ts"], ts) if s["first_ts"] else ts
            s["last_ts"] = max(s["last_ts"], ts) if s["last_ts"] else ts
        if site in site_evidence:
            s["sites"].add(site)

        if event == "inspection":
            s["inspections"] += 1
            outcome = r.get("outcome", "")
            if site in site_inspect:
                if "False" in outcome:
                    site_inspect[site]["false_positive"] += 1
                    s["false_positives"] += 1
                elif outcome:
                    site_inspect[site]["hazard"] += 1
                    s["hazards"] += 1
        elif event == "evidence_collected":
            s["evidence"] += 1
            if site in site_evidence:
                site_evidence[site]["evidence"] += 1
        elif event == "distractor_selected":
            s["distractors"] += 1
            if site in site_evidence:
                site_evidence[site]["distractors"] += 1
        elif event == "placement_attempt" and r["_source"] == "spatial":
            distance = r.get("durationOrDistance", 0.0)
            if r.get("outcome") == "success":
                s["placement_ok"] += 1
            else:
                s["placement_retry"] += 1
            if isinstance(distance, (int, float)):
                release_distances.append(round(float(distance), 3))
        elif event == "coach_turn":
            s["coach_turns"] += 1
            if site in site_coach:
                site_coach[site] += 1
        elif event == "spatial_sample" and site in heat:
            x, z = r.get("siteX"), r.get("siteZ")
            if isinstance(x, (int, float)) and isinstance(z, (int, float)):
                heat[site][(round(x / 1.5), round(z / 1.5))] += 1
        elif event == "assessment_evidence":
            criterion = r.get("criterionId", "")
            objective = r.get("objectiveId", "")
            if criterion.endswith(":first_attempt"):
                first_attempt_marks[criterion.rsplit(":", 1)[0]] += 1
            elif objective:
                objective_sequences[objective][sid].append(
                    1 if r.get("outcome") == "met" else 0)
            if criterion.startswith("decision:") and not criterion.endswith(":first_attempt"):
                key = (sid, criterion)
                if key not in seen_decision:
                    seen_decision.add(key)
                    bucket = "met" if r.get("outcome") == "met" else "not_met"
                    decision_first[criterion][bucket] += 1

    session_rows = []
    for sid, s in sessions.items():
        total_ev = s["evidence"] + s["distractors"]
        session_rows.append({
            "session": sid[:8], "sites": len(s["sites"]),
            "inspections": s["inspections"], "hazards": s["hazards"],
            "falsePositives": s["false_positives"],
            "precision": round(s["evidence"] / total_ev, 3) if total_ev else None,
            "placementOk": s["placement_ok"], "placementRetry": s["placement_retry"],
            "coachTurns": s["coach_turns"], "start": (s["first_ts"] or "")[:19],
        })
    session_rows.sort(key=lambda r: r["start"], reverse=True)

    heat_out = {}
    for site, cells in heat.items():
        heat_out[site] = [[k[0], k[1], v] for k, v in cells.items()]

    bkt_rows = []
    for objective, by_session in sorted(objective_sequences.items()):
        sequences = [seq for seq in by_session.values() if seq]
        if len(sequences) < 3:
            continue
        params = fit_bkt(sequences)
        mastery = mastery_summary(sequences, params)
        bkt_rows.append({
            "objective": objective,
            "mastery": round(mastery, 3) if mastery is not None else None,
            "pL0": round(params[0], 3), "pT": round(params[1], 3),
            "pS": round(params[2], 3), "pG": round(params[3], 3),
            "sequences": len(sequences),
            "observations": sum(len(seq) for seq in sequences),
        })

    return {
        "sessions": session_rows,
        "siteEvidence": [
            {"site": s,
             "precision": round(v["evidence"] / (v["evidence"] + v["distractors"]), 3)
             if (v["evidence"] + v["distractors"]) else None,
             "evidence": v["evidence"], "distractors": v["distractors"]}
            for s, v in site_evidence.items()],
        "siteInspect": [
            {"site": s, "hazard": v["hazard"], "falsePositive": v["false_positive"]}
            for s, v in site_inspect.items()],
        "siteCoach": [{"site": s, "turns": site_coach[s]} for s in SITES],
        "releaseDistances": release_distances,
        "decisions": [
            {"id": k.replace("decision:", ""), "met": v["met"], "notMet": v["not_met"],
             "rate": round(v["met"] / (v["met"] + v["not_met"]), 3)
             if (v["met"] + v["not_met"]) else None}
            for k, v in sorted(decision_first.items())],
        "heat": heat_out,
        "bkt": bkt_rows,
        "firstAttempts": [
            {"criterion": k, "count": v} for k, v in sorted(first_attempt_marks.items())],
    }


HTML_TEMPLATE = """<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>VR Safety Training — Instructor Dashboard</title>
<style>
:root {
  --surface: #fcfcfb; --page: #f9f9f7; --ink: #0b0b0b; --ink2: #52514e;
  --muted: #898781; --grid: #e1e0d9; --axis: #c3c2b7;
  --s1: #2a78d6; --s2: #eb6834; --s3: #1baf7a; --s4: #eda100; --s5: #e87ba4;
  --warn: #fab219; --crit: #d03b3b; --good: #0ca30c;
  --border: rgba(11,11,11,0.10);
}
@media (prefers-color-scheme: dark) {
  :root {
    --surface: #1a1a19; --page: #0d0d0d; --ink: #ffffff; --ink2: #c3c2b7;
    --muted: #898781; --grid: #2c2c2a; --axis: #383835;
    --s1: #3987e5; --s2: #d95926; --s3: #199e70; --s4: #c98500; --s5: #d55181;
    --border: rgba(255,255,255,0.10);
  }
}
* { box-sizing: border-box; margin: 0; }
body { font: 14px/1.5 system-ui, sans-serif; background: var(--page); color: var(--ink); padding: 24px; }
h1 { font-size: 20px; margin-bottom: 4px; }
.sub { color: var(--ink2); margin-bottom: 20px; }
.tiles { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 12px; margin-bottom: 20px; }
.tile { background: var(--surface); border: 1px solid var(--border); border-radius: 10px; padding: 14px 16px; }
.tile .v { font-size: 26px; font-weight: 650; letter-spacing: -0.01em; }
.tile .l { color: var(--ink2); font-size: 12px; margin-top: 2px; }
.grid2 { display: grid; grid-template-columns: repeat(auto-fit, minmax(340px, 1fr)); gap: 16px; }
.card { background: var(--surface); border: 1px solid var(--border); border-radius: 10px; padding: 16px; margin-bottom: 16px; }
.card h2 { font-size: 14px; font-weight: 600; margin-bottom: 2px; }
.card .note { color: var(--muted); font-size: 12px; margin-bottom: 10px; }
canvas { width: 100%; }
table { width: 100%; border-collapse: collapse; font-size: 13px; }
th, td { text-align: right; padding: 6px 8px; border-bottom: 1px solid var(--grid); }
th:first-child, td:first-child { text-align: left; }
th { color: var(--ink2); font-weight: 600; }
.heatrow { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 12px; }
.heatcell h3 { font-size: 12px; color: var(--ink2); font-weight: 600; margin-bottom: 6px; }
#tip { position: fixed; pointer-events: none; background: var(--surface); border: 1px solid var(--border);
  border-radius: 6px; padding: 6px 9px; font-size: 12px; box-shadow: 0 2px 10px rgba(0,0,0,0.18);
  display: none; z-index: 10; color: var(--ink); }
</style>
</head>
<body>
<h1>VR Safety Training — Instructor Dashboard</h1>
<div class="sub">Generated __GENERATED__ · __NSESSIONS__ sessions · skipped lines: __SKIPPED__</div>
<div class="tiles" id="tiles"></div>
<div class="grid2">
  <div class="card"><h2>Evidence precision by site</h2>
    <div class="note">relevant / (relevant + look-alike selections), all sessions pooled</div>
    <canvas id="cPrecision" height="180"></canvas></div>
  <div class="card"><h2>Inspection outcomes by site</h2>
    <div class="note">hazard identifications vs first false positives</div>
    <canvas id="cInspect" height="180"></canvas></div>
  <div class="card"><h2>Decision stations — first-attempt correct rate</h2>
    <div class="note">first submission per session per station</div>
    <canvas id="cDecision" height="180"></canvas></div>
  <div class="card"><h2>Placement release error (m)</h2>
    <div class="note">release distance from target across all hands-on attempts</div>
    <canvas id="cRelease" height="180"></canvas></div>
  <div class="card"><h2>Coach turns by site</h2>
    <div class="note">chat submissions routed to the site coach</div>
    <canvas id="cCoach" height="180"></canvas></div>
  <div class="card"><h2>BKT mastery by objective</h2>
    <div class="note">mean final P(L) per objective, simplified EM over session sequences (min 3 sequences)</div>
    <canvas id="cBkt" height="180"></canvas></div>
</div>
<div class="card"><h2>BKT parameters</h2>
  <div style="overflow-x:auto"><table id="tBkt"></table></div></div>
<div class="card"><h2>Movement heatmaps</h2>
  <div class="note">1 Hz position samples, site-local XZ, pooled across sessions</div>
  <div class="heatrow" id="heatmaps"></div></div>
<div class="card"><h2>Sessions</h2><div style="overflow-x:auto"><table id="tSessions"></table></div></div>
<div id="tip"></div>
<script>
const DATA = __DATA__;
const css = name => getComputedStyle(document.documentElement).getPropertyValue(name).trim();
const tip = document.getElementById('tip');
function showTip(e, html) { tip.innerHTML = html; tip.style.display = 'block';
  tip.style.left = (e.clientX + 12) + 'px'; tip.style.top = (e.clientY + 12) + 'px'; }
function hideTip() { tip.style.display = 'none'; }

function setupCanvas(canvas) {
  const ratio = window.devicePixelRatio || 1;
  const w = canvas.clientWidth, h = canvas.getAttribute('height') | 0;
  canvas.width = w * ratio; canvas.height = h * ratio; canvas.style.height = h + 'px';
  const ctx = canvas.getContext('2d'); ctx.scale(ratio, ratio);
  return [ctx, w, h];
}

function barChart(id, rows, valueOf, labelOf, tipOf, opts = {}) {
  const canvas = document.getElementById(id);
  const [ctx, w, h] = setupCanvas(canvas);
  const pad = { l: 8, r: 8, t: 14, b: 34 };
  const innerW = w - pad.l - pad.r, innerH = h - pad.t - pad.b;
  const max = opts.max ?? Math.max(0.001, ...rows.map(valueOf));
  const bw = Math.min(46, innerW / rows.length - 10);
  const marks = [];
  ctx.strokeStyle = css('--axis'); ctx.lineWidth = 1;
  ctx.beginPath(); ctx.moveTo(pad.l, pad.t + innerH + 0.5); ctx.lineTo(w - pad.r, pad.t + innerH + 0.5); ctx.stroke();
  rows.forEach((row, i) => {
    const v = valueOf(row);
    const x = pad.l + (innerW / rows.length) * i + (innerW / rows.length - bw) / 2;
    const bh = Math.max(2, (v / max) * innerH);
    const y = pad.t + innerH - bh;
    const color = opts.colorOf ? opts.colorOf(row) : css('--s1');
    ctx.fillStyle = color;
    ctx.beginPath(); ctx.roundRect(x, y, bw, bh, [4, 4, 0, 0]); ctx.fill();
    ctx.fillStyle = css('--ink2'); ctx.font = '11px system-ui'; ctx.textAlign = 'center';
    ctx.fillText(labelOf(row), x + bw / 2, h - 18);
    ctx.fillStyle = css('--ink'); ctx.font = '600 11px system-ui';
    ctx.fillText(opts.format ? opts.format(v) : v, x + bw / 2, y - 4);
    marks.push({ x, y: pad.t, wpx: bw, hpx: innerH, row });
  });
  canvas.onmousemove = e => {
    const rect = canvas.getBoundingClientRect();
    const mx = e.clientX - rect.left;
    const hit = marks.find(m => mx >= m.x && mx <= m.x + m.wpx);
    hit ? showTip(e, tipOf(hit.row)) : hideTip();
  };
  canvas.onmouseleave = hideTip;
}

function groupedBar(id, rows, series, labelOf) {
  const canvas = document.getElementById(id);
  const [ctx, w, h] = setupCanvas(canvas);
  const pad = { l: 8, r: 8, t: 26, b: 34 };
  const innerW = w - pad.l - pad.r, innerH = h - pad.t - pad.b;
  const max = Math.max(1, ...rows.flatMap(r => series.map(s => r[s.key])));
  const slot = innerW / rows.length;
  const bw = Math.min(20, (slot - 14) / series.length - 2);
  const marks = [];
  let lx = pad.l;
  series.forEach(s => {
    ctx.fillStyle = s.color; ctx.beginPath(); ctx.roundRect(lx, 6, 9, 9, 2); ctx.fill();
    ctx.fillStyle = css('--ink2'); ctx.font = '11px system-ui'; ctx.textAlign = 'left';
    ctx.fillText(s.name, lx + 13, 14); lx += 13 + ctx.measureText(s.name).width + 14;
  });
  ctx.strokeStyle = css('--axis'); ctx.lineWidth = 1;
  ctx.beginPath(); ctx.moveTo(pad.l, pad.t + innerH + 0.5); ctx.lineTo(w - pad.r, pad.t + innerH + 0.5); ctx.stroke();
  rows.forEach((row, i) => {
    series.forEach((s, j) => {
      const v = row[s.key];
      const x = pad.l + slot * i + (slot - series.length * (bw + 2)) / 2 + j * (bw + 2);
      const bh = Math.max(2, (v / max) * innerH);
      const y = pad.t + innerH - bh;
      ctx.fillStyle = s.color;
      ctx.beginPath(); ctx.roundRect(x, y, bw, bh, [4, 4, 0, 0]); ctx.fill();
      marks.push({ x, wpx: bw, row, s, v });
    });
    ctx.fillStyle = css('--ink2'); ctx.font = '11px system-ui'; ctx.textAlign = 'center';
    ctx.fillText(labelOf(row), pad.l + slot * i + slot / 2, h - 18);
  });
  canvas.onmousemove = e => {
    const rect = canvas.getBoundingClientRect();
    const mx = e.clientX - rect.left;
    const hit = marks.find(m => mx >= m.x && mx <= m.x + m.wpx);
    hit ? showTip(e, `${labelOf(hit.row)} — ${hit.s.name}: <b>${hit.v}</b>`) : hideTip();
  };
  canvas.onmouseleave = hideTip;
}

function histogram(id, values, binWidth) {
  const canvas = document.getElementById(id);
  const [ctx, w, h] = setupCanvas(canvas);
  if (!values.length) { ctx.fillStyle = css('--muted'); ctx.fillText('No placement data', 12, 24); return; }
  const pad = { l: 8, r: 8, t: 14, b: 34 };
  const innerW = w - pad.l - pad.r, innerH = h - pad.t - pad.b;
  const maxV = Math.max(...values);
  const bins = [];
  for (let b = 0; b <= maxV + binWidth; b += binWidth) bins.push(0);
  values.forEach(v => bins[Math.min(bins.length - 1, Math.floor(v / binWidth))]++);
  const maxCount = Math.max(...bins);
  const bw = Math.max(2, innerW / bins.length - 2);
  ctx.strokeStyle = css('--axis'); ctx.lineWidth = 1;
  ctx.beginPath(); ctx.moveTo(pad.l, pad.t + innerH + 0.5); ctx.lineTo(w - pad.r, pad.t + innerH + 0.5); ctx.stroke();
  const marks = [];
  bins.forEach((count, i) => {
    const x = pad.l + (innerW / bins.length) * i + 1;
    const bh = Math.max(count ? 2 : 0, (count / maxCount) * innerH);
    const y = pad.t + innerH - bh;
    ctx.fillStyle = css('--s1');
    if (count) { ctx.beginPath(); ctx.roundRect(x, y, bw, bh, [3, 3, 0, 0]); ctx.fill(); }
    marks.push({ x, wpx: bw, count, from: (i * binWidth).toFixed(1), to: ((i + 1) * binWidth).toFixed(1) });
    if (i % Math.ceil(bins.length / 8) === 0) {
      ctx.fillStyle = css('--muted'); ctx.font = '10px system-ui'; ctx.textAlign = 'left';
      ctx.fillText((i * binWidth).toFixed(1), x, h - 18);
    }
  });
  canvas.onmousemove = e => {
    const rect = canvas.getBoundingClientRect();
    const mx = e.clientX - rect.left;
    const hit = marks.find(m => mx >= m.x && mx <= m.x + m.wpx);
    hit ? showTip(e, `${hit.from}–${hit.to} m: <b>${hit.count}</b> attempts`) : hideTip();
  };
  canvas.onmouseleave = hideTip;
}

function heatmap(container, site, cells) {
  const wrap = document.createElement('div'); wrap.className = 'heatcell';
  wrap.innerHTML = `<h3>${site}</h3>`;
  const canvas = document.createElement('canvas'); canvas.setAttribute('height', 160);
  wrap.appendChild(canvas); container.appendChild(wrap);
  const [ctx, w, h] = setupCanvas(canvas);
  ctx.fillStyle = css('--page'); ctx.fillRect(0, 0, w, h);
  if (!cells.length) { ctx.fillStyle = css('--muted'); ctx.fillText('No samples', 10, 20); return; }
  const xs = cells.map(c => c[0]), zs = cells.map(c => c[1]);
  const minX = Math.min(...xs), maxX = Math.max(...xs);
  const minZ = Math.min(...zs), maxZ = Math.max(...zs);
  const maxV = Math.max(...cells.map(c => c[2]));
  const cw = w / (maxX - minX + 1), ch = h / (maxZ - minZ + 1);
  const size = Math.min(cw, ch);
  const ramp = ['#cde2fb', '#9ec5f4', '#6da7ec', '#3987e5', '#256abf', '#184f95'];
  cells.forEach(([x, z, v]) => {
    const t = Math.log(1 + v) / Math.log(1 + maxV);
    ctx.fillStyle = ramp[Math.min(ramp.length - 1, Math.floor(t * ramp.length))];
    ctx.fillRect((x - minX) * size, h - (z - minZ + 1) * size, size - 0.5, size - 0.5);
  });
}

const fmtPct = v => v == null ? '—' : (v * 100).toFixed(0) + '%';
const S = DATA.sessions;
const pooledPrecision = (() => {
  const ev = DATA.siteEvidence.reduce((a, r) => a + r.evidence, 0);
  const di = DATA.siteEvidence.reduce((a, r) => a + r.distractors, 0);
  return ev + di ? ev / (ev + di) : null;
})();
const tiles = [
  ['Sessions', S.length],
  ['Inspections', S.reduce((a, r) => a + r.inspections, 0)],
  ['Evidence precision', fmtPct(pooledPrecision)],
  ['Placement success', fmtPct((() => { const ok = S.reduce((a, r) => a + r.placementOk, 0);
      const t = ok + S.reduce((a, r) => a + r.placementRetry, 0); return t ? ok / t : null; })())],
  ['Coach turns / session', S.length ? (S.reduce((a, r) => a + r.coachTurns, 0) / S.length).toFixed(1) : '—'],
];
document.getElementById('tiles').innerHTML = tiles.map(([l, v]) =>
  `<div class="tile"><div class="v">${v}</div><div class="l">${l}</div></div>`).join('');

barChart('cPrecision', DATA.siteEvidence.filter(r => r.precision != null),
  r => r.precision, r => r.site.slice(0, 9),
  r => `${r.site}<br>precision <b>${fmtPct(r.precision)}</b><br>${r.evidence} relevant, ${r.distractors} look-alike`,
  { max: 1, format: fmtPct });
groupedBar('cInspect', DATA.siteInspect,
  [{ key: 'hazard', name: 'Hazard found', color: css('--s1') },
   { key: 'falsePositive', name: 'False positive', color: css('--s2') }],
  r => r.site.slice(0, 9));
barChart('cDecision', DATA.decisions.filter(r => r.rate != null),
  r => r.rate, r => r.id.slice(0, 10),
  r => `${r.id}<br>first-attempt correct <b>${fmtPct(r.rate)}</b><br>${r.met} met / ${r.notMet} not met`,
  { max: 1, format: fmtPct });
histogram('cRelease', DATA.releaseDistances, 0.25);
barChart('cCoach', DATA.siteCoach, r => r.turns, r => r.site.slice(0, 9),
  r => `${r.site}: <b>${r.turns}</b> coach turns`);
if (DATA.bkt.length) {
  barChart('cBkt', DATA.bkt.filter(r => r.mastery != null),
    r => r.mastery, r => r.objective,
    r => `${r.objective}<br>mean final P(L) <b>${fmtPct(r.mastery)}</b>` +
         `<br>pL0 ${r.pL0} · pT ${r.pT} · pS ${r.pS} · pG ${r.pG}` +
         `<br>${r.sequences} sequences, ${r.observations} observations`,
    { max: 1, format: fmtPct });
  const bktCols = ['objective', 'mastery', 'pL0', 'pT', 'pS', 'pG', 'sequences', 'observations'];
  document.getElementById('tBkt').innerHTML =
    '<tr>' + bktCols.map(c => `<th>${c}</th>`).join('') + '</tr>' +
    DATA.bkt.map(r => '<tr>' + bktCols.map(c =>
      `<td>${c === 'mastery' ? fmtPct(r[c]) : (r[c] ?? '—')}</td>`).join('') + '</tr>').join('');
} else {
  const bktCanvas = document.getElementById('cBkt');
  const [bctx] = setupCanvas(bktCanvas);
  bctx.fillStyle = css('--muted');
  bctx.font = '12px system-ui';
  bctx.fillText('Not enough assessment sequences yet (needs 3+ per objective).', 12, 24);
}

const heatContainer = document.getElementById('heatmaps');
Object.entries(DATA.heat).forEach(([site, cells]) => heatmap(heatContainer, site, cells));

const cols = ['session', 'start', 'sites', 'inspections', 'hazards', 'falsePositives',
  'precision', 'placementOk', 'placementRetry', 'coachTurns'];
document.getElementById('tSessions').innerHTML =
  '<tr>' + cols.map(c => `<th>${c}</th>`).join('') + '</tr>' +
  S.map(r => '<tr>' + cols.map(c =>
    `<td>${c === 'precision' ? fmtPct(r[c]) : (r[c] ?? '—')}</td>`).join('') + '</tr>').join('');
</script>
</body>
</html>
"""


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--logs", default=str(DEFAULT_LOGS))
    parser.add_argument("--out", default=str(Path(__file__).parent / "dashboard.html"))
    args = parser.parse_args()

    logs = Path(args.logs)
    if not logs.is_dir():
        sys.exit(f"Log directory not found: {logs}")
    entries, skipped = read_jsonl(logs)
    data = aggregate(entries)

    import datetime
    html = (HTML_TEMPLATE
            .replace("__DATA__", json.dumps(data))
            .replace("__GENERATED__", datetime.datetime.now().strftime("%Y-%m-%d %H:%M"))
            .replace("__NSESSIONS__", str(len(data["sessions"])))
            .replace("__SKIPPED__", str(skipped)))
    Path(args.out).write_text(html, encoding="utf-8")
    print(f"entries={len(entries)} skipped={skipped} sessions={len(data['sessions'])}")
    print(f"wrote {args.out}")


if __name__ == "__main__":
    main()

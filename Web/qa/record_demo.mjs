// Records the scripted trailer run (?demo=1, Runtime/DemoAutoplay.cs) from the deployed web build.
// node Web/qa/record_demo.mjs [url]  -> Captures/video/raw.webm + timing.json (start offset for trimming)
import { launchGpu } from "file:///C:/Users/jewoo/Desktop/_projects/CyberPlay_Lab/qa/gpu_browser.mjs";
const { chromium } = await import("file:///C:/Users/jewoo/Desktop/_projects/CyberPlay_Lab/games/04_password_forge/node_modules/playwright/index.mjs");
const fs = await import("node:fs");

const url = (process.argv[2] || "https://competent-person.pages.dev/") + "?demo=1";
const out = "C:/Users/jewoo/GameDev/vr-safety-training/Captures/video";
fs.mkdirSync(out, { recursive: true });

const browser = await launchGpu(chromium);
const ctx = await browser.newContext({ viewport: { width: 1280, height: 720 }, recordVideo: { dir: out, size: { width: 1280, height: 720 } } });
const page = await ctx.newPage();
const t0 = Date.now();
let doneAt = 0, startAt = 0; const marks = [];
page.on("console", (m) => { const s = m.text(), now = (Date.now() - t0) / 1000;
  if (s.includes("[Demo] done")) doneAt = now;
  const i = s.indexOf("[Demo] mark "); if (i >= 0) marks.push({ t: now, label: s.slice(i + 12).trim() }); });
await page.goto(url, { waitUntil: "load", timeout: 120000 });
await page.waitForSelector("#start", { state: "visible", timeout: 240000 });
await page.click("#start");
startAt = (Date.now() - t0) / 1000;
for (let i = 0; i < 120 && !doneAt; i++) await page.waitForTimeout(1000);
await page.waitForTimeout(500);
const video = page.video();
await ctx.close(); await browser.close();
const path = await video.path();
fs.renameSync(path, `${out}/raw.webm`);
fs.writeFileSync(`${out}/timing.json`, JSON.stringify({ startAt, doneAt, marks }, null, 1));
console.log("start", startAt, "done", doneAt);

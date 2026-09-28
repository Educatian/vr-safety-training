import { launchGpu } from "file:///C:/Users/jewoo/Desktop/_projects/CyberPlay_Lab/qa/gpu_browser.mjs";
const { chromium } = await import("file:///C:/Users/jewoo/Desktop/_projects/CyberPlay_Lab/games/04_password_forge/node_modules/playwright/index.mjs");
const b = await launchGpu(chromium); const p = await b.newPage();
const seen = new Map();
p.on("console", (m) => { const t = m.text(); const k = t.slice(0, 160); if (!seen.has(k)) seen.set(k, `[${m.type()}] ${t.slice(0, 900)}`); });
await p.goto(process.argv[2] || "https://competent-person.pages.dev/", { waitUntil: "load" }); await p.waitForSelector("#start", { state: "visible", timeout: 120000 }).then(() => p.click("#start")).catch(() => {});
await p.waitForTimeout(25000);
for (const v of seen.values()) console.log(v, "\n---");
await b.close();

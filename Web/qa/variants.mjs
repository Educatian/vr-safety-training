// Load a local WebGL build with URL render switches and screenshot each; prints the centre pixel so pink is obvious.
// node Web/qa/variants.mjs http://localhost:8765/index.html "" "?pp=0" ...
import { launchGpu } from "file:///C:/Users/jewoo/Desktop/_projects/CyberPlay_Lab/qa/gpu_browser.mjs";
const { chromium } = await import("file:///C:/Users/jewoo/Desktop/_projects/CyberPlay_Lab/games/04_password_forge/node_modules/playwright/index.mjs");
const [base, ...qs] = process.argv.slice(2);
const b = await launchGpu(chromium);
for (const q of qs.length ? qs : [""]) {
  const p = await b.newPage({ viewport: { width: 960, height: 540 } });
  const logs = [];
  p.on("console", (m) => { const t = m.text(); if (/WebRender|not supported|Error|error/i.test(t) && !/memorysetup/.test(t)) logs.push(t.slice(0, 160)); });
  await p.goto(base + q);
  await p.waitForSelector("#start", { state: "visible", timeout: 180000 });
  await p.click("#start");
  await p.waitForTimeout(5000);
  const name = "Captures/web/variant_" + (q.replace(/[^a-z0-9]/gi, "_") || "default") + ".png";
  const buf = await p.screenshot({ path: name });
  // Sample the canvas centre through a screenshot clip (WebGL buffers are not readable after present).
  const px = await p.screenshot({ clip: { x: 480, y: 200, width: 1, height: 1 } });
  console.log(q || "(default)", "->", name, "| logs:", [...new Set(logs)].slice(0, 4).join(" || "));
  await p.close();
}
await b.close();

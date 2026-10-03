// Headless GPU smoke test of the deployed WebGL game: load time, console errors, FPS, screenshots.
// node Web/qa/smoke.mjs [url]   (Playwright from the CyberPlay QA install)
import { launchGpu, webglRenderer } from "file:///C:/Users/jewoo/Desktop/_projects/CyberPlay_Lab/qa/gpu_browser.mjs";
const { chromium } = await import("file:///C:/Users/jewoo/Desktop/_projects/CyberPlay_Lab/games/04_password_forge/node_modules/playwright/index.mjs");

const url = process.argv[2] || "https://competent-person.pages.dev/";
const out = "C:/Users/jewoo/GameDev/vr-safety-training/Captures/web";
const fs = await import("node:fs"); fs.mkdirSync(out, { recursive: true });

const browser = await launchGpu(chromium);
const page = await browser.newPage({ viewport: { width: 1600, height: 900 } });
const errors = [], logs = [];
const all = [];
page.on("console", (m) => { const t = m.text(); all.push(t); if (m.type() === "error") errors.push(t); else if (/error|exception|fail/i.test(t)) logs.push(t); });
page.on("pageerror", (e) => errors.push("pageerror: " + e.message));
console.log("renderer:", await webglRenderer(page));

const t0 = Date.now();
await page.goto(url, { waitUntil: "load", timeout: 120000 });
// Unity's template exposes the progress bar; wait until the loading bar is gone and the canvas is running.
await page.waitForSelector("#start", { state: "visible", timeout: 240000 });
await page.click("#start");
const loadS = (Date.now() - t0) / 1000;
console.log("loaded in", loadS.toFixed(1), "s");
await page.waitForTimeout(6000);
await page.screenshot({ path: `${out}/01_menu.png` });

// FPS over 5 s on the menu (camera orbit over the whole site = worst case).
const fps = await page.evaluate(() => new Promise((res) => {
  let n = 0; const s = performance.now();
  const f = () => { n++; if (performance.now() - s < 5000) requestAnimationFrame(f); else res(n / ((performance.now() - s) / 1000)); };
  requestAnimationFrame(f);
}));
console.log("menu fps:", fps.toFixed(1));

// Start Episode 1: click its START button (card 1, bottom). Coordinates are for 1600x900 and the menu layout.
await page.mouse.click(0.06 * 1600 + (0.88 * 1600 / 5) * 0.5, 900 - 0.08 * 900 - (0.62 * 900) * 0.07);
await page.waitForTimeout(9000);
await page.screenshot({ path: `${out}/02_intro.png` });
await page.keyboard.press("Space");
await page.waitForTimeout(4000);
await page.screenshot({ path: `${out}/03_gate.png` });
const fps2 = await page.evaluate(() => new Promise((res) => {
  let n = 0; const s = performance.now();
  const f = () => { n++; if (performance.now() - s < 5000) requestAnimationFrame(f); else res(n / ((performance.now() - s) / 1000)); };
  requestAnimationFrame(f);
}));
console.log("in-game fps:", fps2.toFixed(1));
const mem = await page.evaluate(() => performance.memory ? Math.round(performance.memory.usedJSHeapSize / 1e6) : -1);
console.log("js heap MB:", mem);
console.log("console errors:", errors.length); errors.slice(0, 10).forEach((e) => console.log("  ", e.slice(0, 200)));
all.forEach((e, i) => { if (/ERROR: Shader/.test(e)) console.log("  shader:", JSON.stringify(all.slice(i, i + 3).join(" | ").slice(0, 300))); });
logs.slice(0, 5).forEach((e) => console.log("  log:", JSON.stringify(e.slice(0, 400))));
await browser.close();

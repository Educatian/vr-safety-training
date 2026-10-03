// Phone-sized touch emulation of the live site: loads, enters EP1, captures the touch HUD.
import { launchGpu } from "file:///C:/Users/jewoo/Desktop/_projects/CyberPlay_Lab/qa/gpu_browser.mjs";
const { chromium, devices } = await import("file:///C:/Users/jewoo/Desktop/_projects/CyberPlay_Lab/games/04_password_forge/node_modules/playwright/index.mjs");
const b = await launchGpu(chromium);
const ctx = await b.newContext({ ...devices["Pixel 7"], viewport: { width: 915, height: 412 }, isMobile: true, hasTouch: true });
const p = await ctx.newPage();
const errors = []; p.on("pageerror", (e) => errors.push(e.message));
const t0 = Date.now();
await p.goto("https://competent-person.pages.dev/?touch=1&v=" + Date.now());
await p.waitForSelector("#start", { state: "visible", timeout: 240000 });
console.log("loaded in", ((Date.now() - t0) / 1000).toFixed(1), "s");
await p.tap("#start");
await p.waitForTimeout(5000);
await p.screenshot({ path: "Captures/web/m01_menu.png" });
// START on the Episode 1 card (landscape phone layout).
await p.touchscreen.tap(0.06 * 915 + (0.88 * 915 / 5) * 0.5, 412 - 0.08 * 412 - (0.62 * 412) * 0.07);
await p.waitForTimeout(6000);
await p.touchscreen.tap(457, 200);   // skip is Space on desktop; tap does nothing harmful here
await p.waitForTimeout(40000);        // let the voiced intro finish
await p.screenshot({ path: "Captures/web/m02_touch_hud.png" });
const fps = await p.evaluate(() => new Promise((res) => { let n = 0; const s = performance.now(); const f = () => { n++; performance.now() - s < 4000 ? requestAnimationFrame(f) : res(n / 4); }; requestAnimationFrame(f); }));
console.log("fps", fps, "errors", errors.length, errors.slice(0, 3));
await b.close();

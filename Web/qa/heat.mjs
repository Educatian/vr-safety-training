// Live check of heat symptoms + sweat with crisp HUD (overlay UI) : EP1, ?heat=0.9
import { launchGpu } from "file:///C:/Users/jewoo/Desktop/_projects/CyberPlay_Lab/qa/gpu_browser.mjs";
const { chromium } = await import("file:///C:/Users/jewoo/Desktop/_projects/CyberPlay_Lab/games/04_password_forge/node_modules/playwright/index.mjs");
const b = await launchGpu(chromium); const p = await b.newPage({ viewport: { width: 1600, height: 900 } });
await p.goto("https://competent-person.pages.dev/?heat=0.95&v=" + Date.now());
await p.waitForSelector("#start", { state: "visible", timeout: 240000 }); await p.click("#start");
await p.waitForTimeout(4000);
await p.mouse.click(0.06 * 1600 + (0.88 * 1600 / 5) * 0.5, 900 - 0.08 * 900 - (0.62 * 900) * 0.07);   // EP1 START
await p.waitForTimeout(3000); await p.keyboard.press("Space"); await p.waitForTimeout(1500);
// Skip check-in/briefing for the capture: walk out into the sun a bit (W) after focusing the canvas.
await p.mouse.click(800, 450); await p.keyboard.down("KeyW"); await p.waitForTimeout(2500); await p.keyboard.up("KeyW");
await p.waitForTimeout(6000);
await p.screenshot({ path: "Captures/web/heat_live.png" });
await b.close(); console.log("ok");

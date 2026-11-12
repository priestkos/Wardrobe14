import { chromium } from "playwright";
import { mkdir } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const outDir = path.resolve(__dirname, "..");

const scenes = [
  { html: "01-mod-grid.html", png: "01-mod-grid.png" },
  { html: "02-differs.html", png: "02-differs.png" },
  { html: "03-folder-on-in.html", png: "03-folder-on-in.png" },
  { html: "04-add-collection.html", png: "04-add-collection.png" },
  { html: "05-probe.html", png: "05-probe.png" },
  { html: "06-penumbra-missing.html", png: "06-penumbra-missing.png" },
];

await mkdir(outDir, { recursive: true });

const browser = await chromium.launch();
const page = await browser.newPage();
await page.setViewportSize({ width: 1280, height: 720 });

for (const { html, png } of scenes) {
  const fileUrl = `file://${path.join(__dirname, html)}`;
  await page.goto(fileUrl, { waitUntil: "load" });
  await page.waitForTimeout(100);
  await page.screenshot({
    path: path.join(outDir, png),
    type: "png",
  });
  console.log("Wrote", png);
}

await browser.close();

// Regenerates the README screenshots.
//
//   node scripts/screenshots.mjs [webUrl] [apiKey]
//
// Expects the API and this UI to already be running. Scripted rather than
// captured by hand so the images can be regenerated when the UI changes, instead
// of quietly drifting out of date.

import { mkdir } from "node:fs/promises";
import { chromium } from "playwright";

const WEB = process.argv[2] ?? "http://localhost:5174";
const API_KEY = process.argv[3] ?? "dev-key-not-a-real-secret";
const OUT = "../docs";

const browser = await chromium.launch();

try {
  await mkdir(OUT, { recursive: true });

  const context = await browser.newContext({
    // Sized to the content: a taller viewport leaves a third of the image empty.
    viewport: { width: 1360, height: 730 },
    deviceScaleFactor: 2,
    colorScheme: "light",
  });

  // Seed the key the same way the app stores it, so the shot shows the write
  // controls enabled rather than the read-only state.
  await context.addInitScript(
    ([key]) => localStorage.setItem("inventory.apiKey", key),
    [API_KEY],
  );

  const page = await context.newPage();
  await page.goto(WEB, { waitUntil: "networkidle" });
  await page.waitForSelector("tbody tr", { timeout: 20_000 });

  // 1. The list, before anything is selected.
  await page.screenshot({ path: `${OUT}/ui-products.png` });

  // 2. A product selected, so the movement history and derived total are shown.
  await page.locator("tbody tr", { hasText: "TL-DRL-001" }).first().click();
  await page.waitForSelector(".stock");
  await page.waitForTimeout(400);
  await page.screenshot({ path: `${OUT}/ui-stock.png` });

  // 3. The invariant being refused. This is the behaviour worth a picture: the
  //    API's own message, and a stock level that did not move.
  await page.locator("#type").selectOption("Out");
  await page.locator("#qty").fill("9999");
  await page.locator("#reason").fill("more than exists");
  await page.getByRole("button", { name: /record movement/i }).click();

  await page.waitForSelector(".error");
  await page.waitForTimeout(300);

  const panel = page.locator(".panel", { has: page.locator(".stock") });
  await panel.screenshot({ path: `${OUT}/ui-invariant.png` });

  console.log(`Wrote ui-products.png, ui-stock.png, ui-invariant.png to ${OUT} from ${WEB}`);
} finally {
  await browser.close();
}

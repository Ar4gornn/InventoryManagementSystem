import { chromium } from "playwright";

// Drives the published demo and checks it still enforces the API's rules.
//
//   npm run build   (with VITE_DEMO=1 and VITE_BASE=/InventoryManagementSystem/)
//   npx vite preview --port 4180 --base /InventoryManagementSystem/
//   node scripts/demo-check.mjs
//
// Point it elsewhere with DEMO_URL, for instance at the deployed page.
const URL = process.env.DEMO_URL ?? "http://localhost:4180/InventoryManagementSystem/";
const OUT = process.env.SHOT_DIR ?? ".";

const b = await chromium.launch();
const p = await b.newPage({ viewport: { width: 1400, height: 1050 } });

const errs = [];
const offPage = [];
p.on("console", (m) => m.type() === "error" && errs.push(m.text()));
// The whole claim of the demo is that nothing leaves the page. Record anything
// requested from an origin other than the one serving it.
p.on("request", (r) => {
  if (!r.url().startsWith("http://localhost:4180/")) offPage.push(`${r.method()} ${r.url()}`);
});

const step = (n, m) => console.log(`\n[${n}] ${m}`);
const ok = (m) => console.log(`    PASS  ${m}`);
const fail = (m) => { console.log(`    FAIL  ${m}`); process.exitCode = 1; };

const panel = (h) => p.locator("section.panel").filter({ has: p.getByRole("heading", { name: h }) });
const catPanel = panel("Categories");
const prodPanel = panel("Products");
const catRow = (n) => catPanel.getByRole("row").filter({ hasText: n });
const prodRow = (s) => prodPanel.getByRole("row").filter({ hasText: s });
const formPanel = () => p.locator("section.panel").filter({ hasText: "New product" });

await p.goto(URL, { waitUntil: "networkidle" });
await p.waitForTimeout(600);

step(1, "the page loads at the project-page base path and says what it is");
console.log(`    banner: ${(await p.locator(".notice").innerText()).replace(/\s+/g, " ").slice(0, 130)}…`);
console.log(`    header: ${(await p.locator(".masthead p").innerText()).replace(/\s+/g, " ")}`);
console.log(`    key state: ${await p.locator(".masthead .controls span").first().innerText()}`);
console.log(`    rows seeded: ${await prodPanel.locator("tbody tr").count()}`);

step(2, "seeded stock is summed from the log, not stored");
await prodRow("TL-DRL-001").click();
await p.waitForTimeout(500);
console.log(`    stock: ${await p.locator(".stock").innerText()}`);
console.log(`    ${(await p.locator(".panel-body p.muted.small").first().innerText()).trim()}`);
(await p.locator(".stock").innerText()) === "21" ? ok("25 in, 4 out = 21") : fail("expected 21");

step(3, "an Out movement larger than stock is refused, not clamped");
await p.locator("#type").selectOption("Out");
await p.locator("#qty").fill("9999");
await p.getByRole("button", { name: "Record movement" }).click();
await p.waitForTimeout(600);
const neg = await p.locator(".panel .error").first().innerText();
console.log(`    ${neg}`);
neg.includes("Stock cannot go negative") ? ok("refused with the balance named") : fail("not refused");
(await p.locator(".stock").innerText()) === "21" ? ok("stock unmoved at 21") : fail("stock changed");

step(4, "create a product with opening stock 25");
await p.getByRole("button", { name: "New product" }).click();
await p.locator("#p-sku").fill("QA-DEMO-1");
await p.locator("#p-name").fill("Demo widget");
await p.locator("#p-opening").fill("25");
await p.getByRole("button", { name: "Create product" }).click();
await p.waitForTimeout(900);
console.log(`    note: ${await p.locator(".app > .ok").first().innerText()}`);
console.log(`    stock: ${await p.locator(".stock").innerText()}`);
const h = await p.locator("table tbody tr").filter({ hasText: "Opening stock" }).first().innerText();
console.log(`    history: ${h.replace(/\s+/g, " ")}`);

step(5, "duplicate SKU refused");
await p.getByRole("button", { name: "New product" }).click();
await p.locator("#p-sku").fill("QA-DEMO-1");
await p.locator("#p-name").fill("dupe");
await p.getByRole("button", { name: "Create product" }).click();
await p.waitForTimeout(600);
const d = await formPanel().locator(".error").first().innerText();
console.log(`    ${d}`);
d.includes("already exists") ? ok("409") : fail("no conflict");
await formPanel().getByRole("button", { name: "Cancel" }).click();

step(6, "delete a product that has movements - refused");
await prodRow("QA-DEMO-1").getByRole("button", { name: "Delete" }).click();
await prodRow("QA-DEMO-1").getByRole("button", { name: "Yes" }).click();
await p.waitForTimeout(700);
const dp = await p.locator(".app > .error").first().innerText();
console.log(`    ${dp}`);
dp.includes("cannot be deleted") ? ok("history protected") : fail("not protected");

step(7, "delete a category that still has products - refused");
await catRow("Tools").getByRole("button", { name: "Delete" }).click();
await catRow("Tools").getByRole("button", { name: "Yes" }).click();
await p.waitForTimeout(700);
const dc = await catPanel.locator(".error").first().innerText();
console.log(`    ${dc}`);
dc.includes("still has") ? ok("refused with the blocking count") : fail("not refused");

step(8, "create and delete a category, and rename one");
await catPanel.locator("#c-name").fill("Consumables");
await catPanel.getByRole("button", { name: "Add category" }).click();
await p.waitForTimeout(700);
console.log(`    note: ${await catPanel.locator(".ok").first().innerText()}`);
await catRow("Consumables").getByRole("button", { name: "Rename" }).click();
await p.getByLabel("New name for Consumables").fill("Consumables R");
await catPanel.getByRole("row").filter({ has: p.getByLabel("New name for Consumables") })
  .getByRole("button", { name: "Save" }).click();
await p.waitForTimeout(700);
console.log(`    note: ${await catPanel.locator(".ok").first().innerText()}`);
await catRow("Consumables R").getByRole("button", { name: "Delete" }).click();
await catRow("Consumables R").getByRole("button", { name: "Yes" }).click();
await p.waitForTimeout(700);
console.log(`    note: ${await catPanel.locator(".ok").first().innerText()}`);
(await catRow("Consumables R").count()) === 0 ? ok("empty category deleted") : fail("still there");

if (process.env.SHOT_DIR) await p.screenshot({ path: `${OUT}/demo-page.png`, fullPage: true });

step(9, "clearing the key must produce the same 401 the real API gives");
await p.getByLabel("API key for write operations").fill("");
await p.waitForTimeout(200);
await catPanel.locator("#c-name").fill("Should not exist");
await catPanel.getByRole("button", { name: "Add category" }).click();
await p.waitForTimeout(600);
console.log(`    ${await catPanel.locator(".error").first().innerText()}`);

step(10, "nothing left the page");
console.log(`    off-origin requests: ${offPage.length ? offPage.join(", ") : "(none)"}`);
offPage.length === 0 ? ok("no network calls to any API") : fail("the demo talked to something");

console.log(`\nconsole errors: ${errs.length ? errs.join(" | ") : "(none)"}`);
await b.close();

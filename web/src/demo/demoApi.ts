import {
  ApiError,
  type Category,
  type ImportRow,
  type InventoryApi,
  type Movement,
  type MovementType,
  type Paged,
  type Product,
} from "../apiTypes";

/**
 * The API, reimplemented in the browser, for the published demo.
 *
 * GitHub Pages serves static files, so there is no server to talk to. Rather than
 * publish a UI whose every request fails, this stands in for the API: same shapes,
 * same status codes, and — the part that matters — the same rules.
 *
 * It is a stand-in, not a second implementation to trust. The real rules live in
 * the C# services and are covered by 86 tests; this exists so the refusals can be
 * demonstrated to someone who is not going to clone the repository. Anything it
 * gets wrong is a bug in the demo, not in the API.
 *
 * Deliberately mirrored, because these are the interesting parts:
 *   - stock on hand is summed from the movement log and never stored;
 *   - a movement that would drive stock below zero is refused, never clamped;
 *   - a product with movements cannot be deleted, and a category with products
 *     cannot be deleted, both with the count that is blocking it;
 *   - writes need the key, and the check happens before the record is looked up.
 *
 * State is per tab and resets on reload. Nothing is persisted and nothing leaves
 * the page.
 */

const DEMO_KEY = "demo";

/** Enough delay that the UI's busy states are visible, as they would be on a network. */
const LATENCY_MS = 140;

interface ProductRow {
  id: number;
  sku: string;
  name: string;
  description: string | null;
  categoryId: number;
  createdAt: string;
}

interface CategoryRow {
  id: number;
  name: string;
  description: string | null;
}

interface MovementRow {
  id: number;
  productId: number;
  type: MovementType;
  quantityDelta: number;
  reason: string | null;
  occurredAt: string;
}

const day = 24 * 60 * 60 * 1000;
const now = Date.now();
const iso = (daysAgo: number) => new Date(now - daysAgo * day).toISOString();

// The same three categories, three products and five movements the API seeds on
// first run, so the demo opens on the numbers the README screenshots show.
let categories: CategoryRow[] = [
  { id: 1, name: "Tools", description: "Hand and power tools" },
  { id: 2, name: "Fasteners", description: "Screws, bolts and fixings" },
  { id: 3, name: "Safety", description: "Personal protective equipment" },
];

let products: ProductRow[] = [
  {
    id: 1,
    sku: "TL-DRL-001",
    name: "Cordless drill 18V",
    description: "Two-speed, brushless, supplied without battery",
    categoryId: 1,
    createdAt: iso(30),
  },
  {
    id: 2,
    sku: "FS-SCR-050",
    name: "Wood screw 4x50mm (box of 200)",
    description: null,
    categoryId: 2,
    createdAt: iso(30),
  },
  {
    id: 3,
    sku: "SF-GOG-010",
    name: "Safety goggles, clear",
    description: null,
    categoryId: 3,
    createdAt: iso(30),
  },
];

let movements: MovementRow[] = [
  { id: 1, productId: 1, type: "In", quantityDelta: 25, reason: "Opening stock", occurredAt: iso(30) },
  { id: 2, productId: 1, type: "Out", quantityDelta: -4, reason: "Sales order 1041", occurredAt: iso(6) },
  { id: 3, productId: 2, type: "In", quantityDelta: 500, reason: "Opening stock", occurredAt: iso(30) },
  {
    id: 4,
    productId: 2,
    type: "Adjustment",
    quantityDelta: -12,
    reason: "Stock count correction",
    occurredAt: iso(2),
  },
  { id: 5, productId: 3, type: "In", quantityDelta: 80, reason: "Opening stock", occurredAt: iso(30) },
];

let nextProductId = 4;
let nextCategoryId = 4;
let nextMovementId = 6;

const wait = () => new Promise((resolve) => setTimeout(resolve, LATENCY_MS));

/** Checked before anything is looked up, so a caller without the key cannot use
 *  the difference between 401 and 404 to discover which ids exist. */
function requireKey(apiKey: string) {
  if (apiKey !== DEMO_KEY) {
    throw new ApiError("A valid X-Api-Key header is required for this request.", 401);
  }
}

const conflict = (message: string) => new ApiError(message, 409);
const notFound = (message: string) => new ApiError(message, 404);
const badRequest = (message: string) => new ApiError(message, 400);

/** Stock is the sum of the log. There is no quantity column here either. */
const stockOf = (productId: number) =>
  movements.filter((m) => m.productId === productId).reduce((sum, m) => sum + m.quantityDelta, 0);

const categoryName = (id: number) => categories.find((c) => c.id === id)?.name ?? "";

const toDto = (p: ProductRow): Product => ({
  id: p.id,
  sku: p.sku,
  name: p.name,
  description: p.description,
  categoryId: p.categoryId,
  categoryName: categoryName(p.categoryId),
  quantityOnHand: stockOf(p.id),
  createdAt: p.createdAt,
});

function requireProduct(id: number): ProductRow {
  const product = products.find((p) => p.id === id);
  if (!product) throw notFound(`Product ${id} does not exist.`);
  return product;
}

function requireCategory(id: number): CategoryRow {
  const category = categories.find((c) => c.id === id);
  if (!category) throw notFound(`Category ${id} does not exist.`);
  return category;
}

/** Same mapping the API applies: the caller sends a positive quantity and a type,
 *  and the sign is derived from the type rather than typed by hand. */
function toSignedDelta(type: MovementType, quantity: number): number {
  if (type === "In") {
    if (quantity > 0) return quantity;
    throw badRequest("An In movement needs a quantity above zero.");
  }
  if (type === "Out") {
    if (quantity > 0) return -quantity;
    throw badRequest("An Out movement needs a positive quantity - the direction comes from the type.");
  }
  if (type === "Adjustment") {
    if (quantity !== 0) return quantity;
    throw badRequest("An Adjustment needs a non-zero quantity, positive or negative.");
  }
  throw badRequest("Type must be In, Out or Adjustment.");
}

/** Handles quoted fields, because a product name may contain a comma. */
function splitCsvLine(line: string): string[] {
  const fields: string[] = [];
  let current = "";
  let quoted = false;

  for (let i = 0; i < line.length; i++) {
    const ch = line[i];
    if (quoted) {
      if (ch === '"') {
        if (line[i + 1] === '"') {
          current += '"';
          i++;
        } else {
          quoted = false;
        }
      } else {
        current += ch;
      }
    } else if (ch === '"') {
      quoted = true;
    } else if (ch === ",") {
      fields.push(current);
      current = "";
    } else {
      current += ch;
    }
  }

  fields.push(current);
  return fields;
}

const EXPECTED_HEADER = "sku,name,description,category,quantity";

export const demoApi: InventoryApi = {
  baseUrl: "an in-browser stand-in — nothing leaves this page",
  demo: true,
  demoKey: DEMO_KEY,

  async products(query) {
    await wait();
    const page = query.page ?? 1;
    const pageSize = query.pageSize ?? 10;
    const search = (query.search ?? "").trim().toLowerCase();

    let rows = products.slice();
    if (query.categoryId) rows = rows.filter((p) => p.categoryId === query.categoryId);
    if (search) {
      rows = rows.filter(
        (p) => p.sku.toLowerCase().includes(search) || p.name.toLowerCase().includes(search),
      );
    }

    // Ordered by SKU, and counted before paging, exactly as the service does.
    rows.sort((a, b) => a.sku.localeCompare(b.sku));
    const totalCount = rows.length;
    const totalPages = Math.ceil(totalCount / pageSize);

    return {
      items: rows.slice((page - 1) * pageSize, page * pageSize).map(toDto),
      page,
      pageSize,
      totalCount,
      totalPages,
      hasNextPage: page < totalPages,
    } satisfies Paged<Product>;
  },

  async categories() {
    await wait();
    const items: Category[] = categories
      .slice()
      .sort((a, b) => a.name.localeCompare(b.name))
      .map((c) => ({
        id: c.id,
        name: c.name,
        description: c.description,
        productCount: products.filter((p) => p.categoryId === c.id).length,
      }));

    return {
      items,
      page: 1,
      pageSize: items.length || 1,
      totalCount: items.length,
      totalPages: 1,
      hasNextPage: false,
    };
  },

  async movements(productId) {
    await wait();
    requireProduct(productId);

    // The running total is a fold over the history in order, not a stored figure.
    let runningTotal = 0;
    return movements
      .filter((m) => m.productId === productId)
      .sort((a, b) => a.occurredAt.localeCompare(b.occurredAt) || a.id - b.id)
      .map((m) => {
        runningTotal += m.quantityDelta;
        return {
          id: m.id,
          productId: m.productId,
          type: m.type,
          quantityDelta: m.quantityDelta,
          runningTotal,
          reason: m.reason,
          occurredAt: m.occurredAt,
        } satisfies Movement;
      });
  },

  async stock(productId) {
    await wait();
    const product = requireProduct(productId);
    return { productId, sku: product.sku, quantityOnHand: stockOf(productId) };
  },

  async createProduct(body, apiKey) {
    requireKey(apiKey);
    await wait();

    const sku = body.sku.trim();
    if (!sku) throw badRequest("Sku is required.");
    if (!body.name.trim()) throw badRequest("Name is required.");
    if (products.some((p) => p.sku === sku)) {
      throw conflict(`A product with SKU '${sku}' already exists.`);
    }
    requireCategory(body.categoryId);

    const row: ProductRow = {
      id: nextProductId++,
      sku,
      name: body.name.trim(),
      description: body.description?.trim() || null,
      categoryId: body.categoryId,
      createdAt: new Date().toISOString(),
    };
    products.push(row);
    return toDto(row);
  },

  async updateProduct(id, body, apiKey) {
    requireKey(apiKey);
    await wait();

    const product = requireProduct(id);
    if (!body.name.trim()) throw badRequest("Name is required.");
    requireCategory(body.categoryId);

    // The SKU is absent on purpose: it is immutable, because other systems may
    // already have recorded it.
    product.name = body.name.trim();
    product.description = body.description?.trim() || null;
    product.categoryId = body.categoryId;
    return toDto(product);
  },

  async deleteProduct(id, apiKey) {
    requireKey(apiKey);
    await wait();

    requireProduct(id);
    const count = movements.filter((m) => m.productId === id).length;
    if (count > 0) {
      throw conflict(
        `Product ${id} has stock movements and cannot be deleted. Its history would be lost.`,
      );
    }
    products = products.filter((p) => p.id !== id);
  },

  async createCategory(body, apiKey) {
    requireKey(apiKey);
    await wait();

    const name = body.name.trim();
    if (!name) throw badRequest("Name is required.");
    if (categories.some((c) => c.name.toLowerCase() === name.toLowerCase())) {
      throw conflict(`A category named '${name}' already exists.`);
    }

    const row: CategoryRow = {
      id: nextCategoryId++,
      name,
      description: body.description?.trim() || null,
    };
    categories.push(row);
    return { id: row.id, name: row.name, description: row.description, productCount: 0 };
  },

  async updateCategory(id, body, apiKey) {
    requireKey(apiKey);
    await wait();

    const category = requireCategory(id);
    const name = body.name.trim();
    if (!name) throw badRequest("Name is required.");

    // Excluding itself, so renaming a category to the name it already has is not
    // reported as a clash with itself.
    if (categories.some((c) => c.id !== id && c.name.toLowerCase() === name.toLowerCase())) {
      throw conflict(`A category named '${name}' already exists.`);
    }

    category.name = name;
    category.description = body.description?.trim() || null;
    return {
      id: category.id,
      name: category.name,
      description: category.description,
      productCount: products.filter((p) => p.categoryId === id).length,
    };
  },

  async deleteCategory(id, apiKey) {
    requireKey(apiKey);
    await wait();

    requireCategory(id);
    const count = products.filter((p) => p.categoryId === id).length;
    if (count > 0) {
      throw conflict(`Category ${id} still has ${count} product(s). Move or delete them first.`);
    }
    categories = categories.filter((c) => c.id !== id);
  },

  async recordMovement(productId, body, apiKey) {
    requireKey(apiKey);
    await wait();

    requireProduct(productId);
    const delta = toSignedDelta(body.type, body.quantity);
    const currentStock = stockOf(productId);
    const resulting = currentStock + delta;

    if (resulting < 0) {
      // Refused, never clamped: writing a smaller movement would make the history
      // disagree with what the caller was told happened.
      throw badRequest(
        `Stock cannot go negative. Product ${productId} has ${currentStock} on hand ` +
          `and this movement would leave ${resulting}.`,
      );
    }

    const row: MovementRow = {
      id: nextMovementId++,
      productId,
      type: body.type,
      quantityDelta: delta,
      reason: body.reason?.trim() || null,
      occurredAt: new Date().toISOString(),
    };
    movements.push(row);

    return { ...row, runningTotal: resulting };
  },

  async importCsv(file, apiKey) {
    requireKey(apiKey);
    await wait();

    const text = await file.text();
    const lines = text.split(/\r?\n/).filter((l) => l.trim().length > 0);

    if (lines.length === 0) throw badRequest("The file is empty.");

    const header = splitCsvLine(lines[0])
      .map((f) => f.trim().toLowerCase())
      .join(",");
    if (header !== EXPECTED_HEADER) {
      throw badRequest(`The header must be exactly '${EXPECTED_HEADER}'.`);
    }

    const byName = new Map(categories.map((c) => [c.name.toLowerCase(), c]));
    const seen = new Set(products.map((p) => p.sku));
    const rows: ImportRow[] = [];
    const staged: Array<{ row: ProductRow; quantity: number }> = [];

    for (let i = 1; i < lines.length; i++) {
      // Line numbers include the header, so they match a text editor's gutter.
      const line = i + 1;
      const fields = splitCsvLine(lines[i]);
      const sku = (fields[0] ?? "").trim();

      const error = (() => {
        if (fields.length < 5) return `Expected 5 columns, found ${fields.length}.`;
        if (!sku) return "Sku is required.";
        if (seen.has(sku)) return `Sku '${sku}' already exists.`;
        if (!fields[1].trim()) return "Name is required.";

        const catName = fields[3].trim();
        if (!byName.has(catName.toLowerCase())) return `Unknown category '${catName}'.`;

        const qtyField = fields[4].trim();
        const quantity = qtyField === "" ? 0 : Number(qtyField);
        if (qtyField !== "" && !Number.isInteger(quantity)) {
          return `Quantity '${qtyField}' is not a whole number.`;
        }
        if (quantity < 0) return "Quantity cannot be negative.";

        staged.push({
          row: {
            id: 0,
            sku,
            name: fields[1].trim(),
            description: fields[2].trim() || null,
            categoryId: byName.get(catName.toLowerCase())!.id,
            createdAt: new Date().toISOString(),
          },
          quantity,
        });
        return null;
      })();

      if (!error) seen.add(sku);
      rows.push({ line, sku, imported: !error, error });
    }

    for (const { row, quantity } of staged) {
      row.id = nextProductId++;
      products.push(row);
      // Opening stock is a movement, never a column, so an imported product's
      // quantity has the same provenance as any other.
      if (quantity !== 0) {
        movements.push({
          id: nextMovementId++,
          productId: row.id,
          type: "In",
          quantityDelta: quantity,
          reason: "Opening stock",
          occurredAt: new Date().toISOString(),
        });
      }
    }

    const importedCount = rows.filter((r) => r.imported).length;
    return {
      totalRows: rows.length,
      importedCount,
      failedCount: rows.length - importedCount,
      rows,
    };
  },
};

export { DEMO_KEY };

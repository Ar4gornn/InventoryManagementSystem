/**
 * Shapes the API returns, and the error the UI shows.
 *
 * These live apart from `api.ts` so that the in-browser demo backend can use them
 * without importing the module that imports it.
 */

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
}

export interface Product {
  id: number;
  sku: string;
  name: string;
  description: string | null;
  categoryId: number;
  categoryName: string;
  quantityOnHand: number;
  createdAt: string;
}

export interface Category {
  id: number;
  name: string;
  description: string | null;
  productCount: number;
}

export type MovementType = "In" | "Out" | "Adjustment";

export interface Movement {
  id: number;
  productId: number;
  type: MovementType;
  quantityDelta: number;
  runningTotal: number;
  reason: string | null;
  occurredAt: string;
}

export interface StockLevel {
  productId: number;
  sku: string;
  quantityOnHand: number;
}

export interface ImportRow {
  line: number;
  sku: string;
  imported: boolean;
  error: string | null;
}

export interface ImportResult {
  totalRows: number;
  importedCount: number;
  failedCount: number;
  rows: ImportRow[];
}

/** An error carrying the status and the API's own message, so the UI can show it. */
export class ApiError extends Error {
  // Declared rather than a constructor parameter property: the project builds
  // with erasableSyntaxOnly, which forbids syntax that emits runtime code.
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.status = status;
  }
}

/** The surface both the real client and the demo backend implement. */
export interface InventoryApi {
  baseUrl: string;
  /** True when reads and writes never leave the browser. */
  demo: boolean;
  /** Only set by the demo backend, which pre-fills it so a visitor can write. */
  demoKey?: string;
  products(query: {
    page?: number;
    pageSize?: number;
    search?: string;
    categoryId?: number;
  }): Promise<Paged<Product>>;
  categories(): Promise<Paged<Category>>;
  movements(productId: number): Promise<Movement[]>;
  stock(productId: number): Promise<StockLevel>;
  createProduct(
    body: { sku: string; name: string; description?: string; categoryId: number },
    apiKey: string,
  ): Promise<Product>;
  updateProduct(
    id: number,
    body: { name: string; description?: string; categoryId: number },
    apiKey: string,
  ): Promise<Product>;
  deleteProduct(id: number, apiKey: string): Promise<void>;
  createCategory(body: { name: string; description?: string }, apiKey: string): Promise<Category>;
  updateCategory(
    id: number,
    body: { name: string; description?: string },
    apiKey: string,
  ): Promise<Category>;
  deleteCategory(id: number, apiKey: string): Promise<void>;
  recordMovement(
    productId: number,
    body: { type: MovementType; quantity: number; reason?: string },
    apiKey: string,
  ): Promise<Movement>;
  importCsv(file: File, apiKey: string): Promise<ImportResult>;
}

/**
 * Thin client over the inventory API.
 *
 * Reads are open; writes need the key, which the user supplies in the UI and
 * which is kept in memory and localStorage on their machine only. It is never
 * sent anywhere except this API, and there is no telemetry in this app.
 */

const BASE = (import.meta.env.VITE_API_URL ?? "http://localhost:5180").replace(/\/$/, "");

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

async function request<T>(path: string, init: RequestInit = {}, apiKey?: string): Promise<T> {
  const headers = new Headers(init.headers);
  if (apiKey) headers.set("X-Api-Key", apiKey);

  let response: Response;
  try {
    response = await fetch(`${BASE}${path}`, { ...init, headers });
  } catch {
    // A network-level failure is by far the most common thing to go wrong here,
    // and "Failed to fetch" tells the user nothing actionable.
    throw new ApiError(`Cannot reach the API at ${BASE}. Is it running?`, 0);
  }

  if (!response.ok) {
    // The API answers errors as ProblemDetails; fall back for anything that is not.
    let detail = `${response.status} ${response.statusText}`;
    try {
      const problem = await response.json();
      if (problem?.detail) detail = problem.detail;
      else if (problem?.title) detail = problem.title;
      else if (problem?.errors) {
        detail = Object.values(problem.errors as Record<string, string[]>)
          .flat()
          .join(" ");
      }
    } catch {
      /* keep the status line */
    }

    throw new ApiError(detail, response.status);
  }

  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

const json = (body: unknown): RequestInit => ({
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify(body),
});

export const api = {
  baseUrl: BASE,

  products(query: { page?: number; pageSize?: number; search?: string; categoryId?: number }) {
    const params = new URLSearchParams();
    if (query.page) params.set("page", String(query.page));
    if (query.pageSize) params.set("pageSize", String(query.pageSize));
    if (query.search?.trim()) params.set("search", query.search.trim());
    if (query.categoryId) params.set("categoryId", String(query.categoryId));

    return request<Paged<Product>>(`/api/products?${params}`);
  },

  categories() {
    return request<Paged<Category>>("/api/categories?pageSize=100");
  },

  movements(productId: number) {
    return request<Movement[]>(`/api/products/${productId}/movements`);
  },

  stock(productId: number) {
    return request<StockLevel>(`/api/products/${productId}/stock`);
  },

  createProduct(
    body: { sku: string; name: string; description?: string; categoryId: number },
    apiKey: string,
  ) {
    return request<Product>("/api/products", json(body), apiKey);
  },

  recordMovement(
    productId: number,
    body: { type: MovementType; quantity: number; reason?: string },
    apiKey: string,
  ) {
    return request<Movement>(`/api/products/${productId}/movements`, json(body), apiKey);
  },

  importCsv(file: File, apiKey: string) {
    const form = new FormData();
    form.append("file", file);
    return request<ImportResult>("/api/products/import", { method: "POST", body: form }, apiKey);
  },
};

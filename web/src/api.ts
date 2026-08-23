/**
 * Thin client over the inventory API.
 *
 * Reads are open; writes need the key, which the user supplies in the UI and
 * which is kept in memory and localStorage on their machine only. It is never
 * sent anywhere except this API, and there is no telemetry in this app.
 *
 * The published demo has no server to talk to, so when VITE_DEMO is set the whole
 * client is swapped for an in-browser stand-in. Vite replaces the flag at build
 * time, so the branch not taken is dropped from the bundle rather than shipped
 * alongside the one that is.
 */

import {
  ApiError,
  type Category,
  type ImportResult,
  type InventoryApi,
  type Movement,
  type MovementType,
  type Paged,
  type Product,
  type StockLevel,
} from "./apiTypes";
import { demoApi } from "./demo/demoApi";

export * from "./apiTypes";

const BASE = (import.meta.env.VITE_API_URL ?? "http://localhost:5180").replace(/\/$/, "");

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

const json = (method: string, body: unknown): RequestInit => ({
  method,
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify(body),
});

const realApi: InventoryApi = {
  baseUrl: BASE,
  demo: false,

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
    return request<Product>("/api/products", json("POST", body), apiKey);
  },

  /** The SKU is immutable, so it is not in the body. Sending one would be ignored. */
  updateProduct(
    id: number,
    body: { name: string; description?: string; categoryId: number },
    apiKey: string,
  ) {
    return request<Product>(`/api/products/${id}`, json("PUT", body), apiKey);
  },

  /** 409 if the product has any movements — its history is not thrown away. */
  deleteProduct(id: number, apiKey: string) {
    return request<void>(`/api/products/${id}`, { method: "DELETE" }, apiKey);
  },

  createCategory(body: { name: string; description?: string }, apiKey: string) {
    return request<Category>("/api/categories", json("POST", body), apiKey);
  },

  updateCategory(id: number, body: { name: string; description?: string }, apiKey: string) {
    return request<Category>(`/api/categories/${id}`, json("PUT", body), apiKey);
  },

  /** 409 while any product still belongs to it, and the message says how many. */
  deleteCategory(id: number, apiKey: string) {
    return request<void>(`/api/categories/${id}`, { method: "DELETE" }, apiKey);
  },

  recordMovement(
    productId: number,
    body: { type: MovementType; quantity: number; reason?: string },
    apiKey: string,
  ) {
    return request<Movement>(`/api/products/${productId}/movements`, json("POST", body), apiKey);
  },

  importCsv(file: File, apiKey: string) {
    const form = new FormData();
    form.append("file", file);
    return request<ImportResult>("/api/products/import", { method: "POST", body: form }, apiKey);
  },
};

/** Swapped at build time, not at runtime: `import.meta.env.VITE_DEMO` is a literal
 *  by the time the bundler sees this, so only one of the two is shipped. */
export const api: InventoryApi = import.meta.env.VITE_DEMO === "1" ? demoApi : realApi;

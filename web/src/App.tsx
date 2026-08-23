import { useCallback, useEffect, useState } from "react";

import { ApiError, api, type Category, type Product } from "./api";
import { ApiKeyBar } from "./components/ApiKeyBar";
import { CategoryPanel } from "./components/CategoryPanel";
import { ImportPanel } from "./components/ImportPanel";
import { MovementPanel } from "./components/MovementPanel";
import { ProductForm, type FormMode } from "./components/ProductForm";
import { ProductTable } from "./components/ProductTable";

const PAGE_SIZE = 10;

export default function App() {
  const [apiKey, setApiKey] = useState(() => localStorage.getItem("inventory.apiKey") ?? "");

  const [products, setProducts] = useState<Product[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(0);
  const [totalCount, setTotalCount] = useState(0);
  const [search, setSearch] = useState("");
  const [categoryId, setCategoryId] = useState<number | undefined>();
  const [selected, setSelected] = useState<Product | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  // Kept apart from `error`, which a successful reload clears. The outcome of a
  // delete would otherwise be wiped by the reload the delete itself triggers.
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionNote, setActionNote] = useState<string | null>(null);

  const [formMode, setFormMode] = useState<FormMode | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const result = await api.products({ page, pageSize: PAGE_SIZE, search, categoryId });
      setProducts(result.items);
      setTotalPages(result.totalPages);
      setTotalCount(result.totalCount);
      setError(null);

      // Keep the selected product's numbers in step after a movement is recorded.
      setSelected((current) =>
        current ? (result.items.find((p) => p.id === current.id) ?? current) : null,
      );
      return result.items;
    } catch (e) {
      setError(e instanceof ApiError ? e.message : String(e));
      return null;
    } finally {
      setLoading(false);
    }
  }, [page, search, categoryId]);

  const loadCategories = useCallback(async () => {
    try {
      const result = await api.categories();
      setCategories(result.items);
    } catch {
      /* the product load already surfaces an unreachable API */
    }
  }, []);

  useEffect(() => {
    // Debounced so typing in the search box does not fire a request per keystroke.
    const timer = window.setTimeout(() => void load(), 200);
    return () => window.clearTimeout(timer);
  }, [load]);

  useEffect(() => {
    void loadCategories();
  }, [loadCategories]);

  const onKeyChange = (key: string) => {
    setApiKey(key);
    localStorage.setItem("inventory.apiKey", key);
  };

  const onSaved = async (product: Product, note: string) => {
    setFormMode(null);
    setActionError(null);
    setSelected(product);

    // A product's category can change, and a new one shifts a category's count.
    const [items] = await Promise.all([load(), loadCategories()]);

    // Say so rather than leaving the user hunting for something they just created
    // on a page or under a filter that does not show it.
    const visible = items?.some((p) => p.id === product.id) ?? true;
    setActionNote(
      visible
        ? note
        : `${note} It is not on this page — clear the search or the category filter to find it.`,
    );
  };

  const onDelete = async (product: Product) => {
    setActionError(null);
    setActionNote(null);

    if (!apiKey) {
      setActionError("Deleting a product needs the API key. Enter it at the top right.");
      return;
    }

    try {
      await api.deleteProduct(product.id, apiKey);
      if (selected?.id === product.id) setSelected(null);
      if (formMode?.kind === "edit" && formMode.product.id === product.id) setFormMode(null);
      await Promise.all([load(), loadCategories()]);
      setActionNote(`Deleted ${product.sku}.`);
    } catch (e) {
      // The 409 for a product that has movements lands here. Its wording explains
      // that the history is why, which is better than anything invented here.
      setActionError(e instanceof ApiError ? e.message : String(e));
    }
  };

  return (
    <div className="app">
      <header className="masthead">
        <div>
          <h1>Inventory</h1>
          <p>
            Stock on hand is derived from the movement log, never stored. API at{" "}
            <code>{api.baseUrl}</code>
          </p>
        </div>
        <ApiKeyBar apiKey={apiKey} onChange={onKeyChange} />
      </header>

      {error && <p className="error">{error}</p>}
      {actionError && <p className="error">{actionError}</p>}
      {actionNote && <p className="ok">{actionNote}</p>}

      <div className="grid">
        <div>
          <section className="panel">
            <div className="panel-head">
              <h2>Products</h2>
              <div className="controls">
                <button
                  type="button"
                  className="primary"
                  onClick={() => {
                    setFormMode({ kind: "create" });
                    setActionNote(null);
                    setActionError(null);
                  }}
                >
                  New product
                </button>
                <input
                  type="text"
                  placeholder="Search SKU or name"
                  value={search}
                  onChange={(e) => {
                    setSearch(e.target.value);
                    setPage(1);
                  }}
                  aria-label="Search products"
                />
                <select
                  value={categoryId ?? ""}
                  onChange={(e) => {
                    setCategoryId(e.target.value ? Number(e.target.value) : undefined);
                    setPage(1);
                  }}
                  aria-label="Filter by category"
                >
                  <option value="">All categories</option>
                  {categories.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.name} ({c.productCount})
                    </option>
                  ))}
                </select>
              </div>
            </div>
            <div className="panel-body">
              <ProductTable
                products={products}
                loading={loading}
                selectedId={selected?.id ?? null}
                onSelect={setSelected}
                onEdit={(product) => {
                  setFormMode({ kind: "edit", product });
                  setActionNote(null);
                  setActionError(null);
                }}
                onDelete={(product) => void onDelete(product)}
              />

              <div className="pager">
                <span className="muted small">
                  {totalCount === 0
                    ? "No matches"
                    : `${totalCount} product${totalCount === 1 ? "" : "s"}`}
                </span>
                <span className="controls">
                  <button onClick={() => setPage((p) => p - 1)} disabled={page <= 1}>
                    ← Prev
                  </button>
                  <span className="muted small">
                    {totalPages === 0 ? "0 / 0" : `${page} / ${totalPages}`}
                  </span>
                  <button
                    onClick={() => setPage((p) => p + 1)}
                    disabled={totalPages === 0 || page >= totalPages}
                  >
                    Next →
                  </button>
                </span>
              </div>
            </div>
          </section>

          {formMode && (
            <ProductForm
              key={formMode.kind === "edit" ? `edit-${formMode.product.id}` : "create"}
              mode={formMode}
              categories={categories}
              apiKey={apiKey}
              onSaved={(product, note) => void onSaved(product, note)}
              onCancel={() => setFormMode(null)}
            />
          )}

          <CategoryPanel
            categories={categories}
            apiKey={apiKey}
            onChanged={() => {
              void loadCategories();
              void load();
            }}
          />

          <ImportPanel
            apiKey={apiKey}
            onImported={() => {
              void load();
              void loadCategories();
            }}
          />
        </div>

        <MovementPanel product={selected} apiKey={apiKey} onRecorded={() => void load()} />
      </div>
    </div>
  );
}

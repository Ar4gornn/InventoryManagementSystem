import { useCallback, useEffect, useState } from "react";

import { ApiError, api, type Category, type Product } from "./api";
import { ApiKeyBar } from "./components/ApiKeyBar";
import { ImportPanel } from "./components/ImportPanel";
import { MovementPanel } from "./components/MovementPanel";
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
    } catch (e) {
      setError(e instanceof ApiError ? e.message : String(e));
    } finally {
      setLoading(false);
    }
  }, [page, search, categoryId]);

  useEffect(() => {
    // Debounced so typing in the search box does not fire a request per keystroke.
    const timer = window.setTimeout(() => void load(), 200);
    return () => window.clearTimeout(timer);
  }, [load]);

  useEffect(() => {
    api.categories().then(
      (result) => setCategories(result.items),
      () => {
        /* the product load already surfaces an unreachable API */
      },
    );
  }, []);

  const onKeyChange = (key: string) => {
    setApiKey(key);
    localStorage.setItem("inventory.apiKey", key);
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

      <div className="grid">
        <div>
          <section className="panel">
            <div className="panel-head">
              <h2>Products</h2>
              <div className="controls">
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

          <ImportPanel apiKey={apiKey} onImported={() => void load()} />
        </div>

        <MovementPanel product={selected} apiKey={apiKey} onRecorded={() => void load()} />
      </div>
    </div>
  );
}

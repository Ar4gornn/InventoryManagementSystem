import { useState } from "react";

import { ApiError, api, type Category, type Product } from "../api";

export type FormMode = { kind: "create" } | { kind: "edit"; product: Product };

interface Props {
  mode: FormMode;
  categories: Category[];
  apiKey: string;
  /** Called after a successful save, with the product the API returned. */
  onSaved: (product: Product, note: string) => void;
  onCancel: () => void;
}

/**
 * Create a product, or edit one that exists. One component for both, because the
 * fields are the same three plus a SKU.
 *
 * The caller gives this a `key` that changes with the mode, so switching from
 * creating to editing — or between two products — remounts it and every field
 * starts from the right value. That is why no effect copies props into state.
 *
 * Two things the API decides and this form only reflects:
 *
 *   - The SKU is immutable. On edit it is shown, disabled, so you can see which
 *     product you are changing without being invited to change the one field that
 *     external systems may already have recorded.
 *   - `POST /api/products` takes no quantity, because stock is never a column. An
 *     opening quantity typed here is sent afterwards as an In movement, which is
 *     exactly what the CSV importer does server-side. That makes it two requests,
 *     and the failure between them is handled below rather than hidden.
 */
export function ProductForm({ mode, categories, apiKey, onSaved, onCancel }: Props) {
  const editing = mode.kind === "edit" ? mode.product : null;

  const [sku, setSku] = useState(editing?.sku ?? "");
  const [name, setName] = useState(editing?.name ?? "");
  const [description, setDescription] = useState(editing?.description ?? "");
  const [categoryId, setCategoryId] = useState<number>(
    editing?.categoryId ?? categories[0]?.id ?? 0,
  );
  const [opening, setOpening] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const noCategories = categories.length === 0;

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!apiKey) {
      setError("Saving a product needs the API key. Enter it at the top right.");
      return;
    }

    setBusy(true);
    try {
      if (editing) {
        const updated = await api.updateProduct(
          editing.id,
          { name: name.trim(), description: description.trim() || undefined, categoryId },
          apiKey,
        );
        onSaved(updated, `Saved ${updated.sku}.`);
        return;
      }

      const created = await api.createProduct(
        {
          sku: sku.trim(),
          name: name.trim(),
          description: description.trim() || undefined,
          categoryId,
        },
        apiKey,
      );

      if (opening > 0) {
        try {
          await api.recordMovement(
            created.id,
            { type: "In", quantity: opening, reason: "Opening stock" },
            apiKey,
          );
        } catch (movementError) {
          // The product exists; only the opening movement failed. Say so plainly
          // rather than reporting a failure that would send the user to create it
          // a second time and hit a duplicate SKU.
          const detail =
            movementError instanceof ApiError ? movementError.message : String(movementError);
          onSaved(
            created,
            `Created ${created.sku}, but its opening stock of ${opening} was not recorded: ` +
              `${detail} The product is selected — record the movement on the right.`,
          );
          return;
        }
      }

      onSaved(
        created,
        opening > 0
          ? `Created ${created.sku} with an opening stock of ${opening}.`
          : `Created ${created.sku} at zero. Record a movement to give it stock.`,
      );
    } catch (e) {
      // A duplicate SKU arrives here as a 409 with the API's own wording.
      setError(e instanceof ApiError ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>{editing ? `Edit ${editing.sku}` : "New product"}</h2>
        <button type="button" onClick={onCancel}>
          Cancel
        </button>
      </div>
      <div className="panel-body">
        {error && <p className="error">{error}</p>}
        {noCategories && (
          <p className="error">
            A product needs a category and none exist yet. Create one first, below.
          </p>
        )}

        <form onSubmit={submit}>
          <div className="row">
            <div className="field">
              <label htmlFor="p-sku">SKU</label>
              <input
                id="p-sku"
                type="text"
                value={sku}
                disabled={!!editing}
                maxLength={50}
                required
                placeholder="TL-DRL-002"
                spellCheck={false}
                onChange={(e) => setSku(e.target.value)}
              />
              {editing && <span className="hint">Immutable — other systems may hold it.</span>}
            </div>
            <div className="field">
              <label htmlFor="p-category">Category</label>
              <select
                id="p-category"
                value={categoryId || ""}
                onChange={(e) => setCategoryId(Number(e.target.value))}
                required
              >
                {categories.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div className="field">
            <label htmlFor="p-name">Name</label>
            <input
              id="p-name"
              type="text"
              value={name}
              maxLength={200}
              required
              placeholder="Cordless drill 18V"
              onChange={(e) => setName(e.target.value)}
            />
          </div>

          <div className="field">
            <label htmlFor="p-desc">Description</label>
            <input
              id="p-desc"
              type="text"
              value={description}
              maxLength={1000}
              placeholder="Optional"
              onChange={(e) => setDescription(e.target.value)}
            />
          </div>

          {!editing && (
            <div className="field">
              <label htmlFor="p-opening">Opening stock</label>
              <input
                id="p-opening"
                type="number"
                min={0}
                value={opening}
                onChange={(e) => setOpening(Math.max(0, Number(e.target.value)))}
              />
              <span className="hint">
                Recorded as an In movement, not stored as a quantity. Leave at 0 to start empty.
              </span>
            </div>
          )}

          <button
            type="submit"
            className="primary"
            disabled={busy || noCategories || !name.trim() || (!editing && !sku.trim())}
          >
            {busy ? "Saving…" : editing ? "Save changes" : "Create product"}
          </button>
        </form>
      </div>
    </section>
  );
}

import { useState } from "react";

import { ApiError, api, type Category } from "../api";

interface Props {
  categories: Category[];
  apiKey: string;
  /** Reload both categories and products — a rename changes what the table shows. */
  onChanged: () => void;
}

/**
 * Categories: list, create, rename, delete.
 *
 * The product count next to each name is not decoration. A category cannot be
 * deleted while any product belongs to it, so the count is exactly what a delete
 * would be blocked by, and the API returns it on every read for that reason.
 *
 * Delete asks for confirmation inline rather than through `window.confirm`, which
 * cannot be styled, cannot be tested, and is suppressed in some embedded contexts.
 */
export function CategoryPanel({ categories, apiKey, onChanged }: Props) {
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [editingId, setEditingId] = useState<number | null>(null);
  const [draftName, setDraftName] = useState("");
  const [confirmId, setConfirmId] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [note, setNote] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  /** Every mutation here is the same shape: need a key, run it, report either way. */
  const run = async (what: string, action: () => Promise<unknown>) => {
    setError(null);
    setNote(null);

    if (!apiKey) {
      setError(`${what} needs the API key. Enter it at the top right.`);
      return false;
    }

    setBusy(true);
    try {
      await action();
      onChanged();
      return true;
    } catch (e) {
      setError(e instanceof ApiError ? e.message : String(e));
      return false;
    } finally {
      setBusy(false);
    }
  };

  const create = async (e: React.FormEvent) => {
    e.preventDefault();
    const trimmed = name.trim();
    const ok = await run("Creating a category", () =>
      api.createCategory({ name: trimmed, description: description.trim() || undefined }, apiKey),
    );
    if (ok) {
      setName("");
      setDescription("");
      setNote(`Created ${trimmed}.`);
    }
  };

  const rename = async (category: Category) => {
    const trimmed = draftName.trim();
    if (!trimmed || trimmed === category.name) {
      setEditingId(null);
      return;
    }
    const ok = await run("Renaming a category", () =>
      api.updateCategory(
        category.id,
        { name: trimmed, description: category.description ?? undefined },
        apiKey,
      ),
    );
    if (ok) {
      setEditingId(null);
      setNote(`Renamed to ${trimmed}.`);
    }
  };

  const remove = async (category: Category) => {
    const ok = await run("Deleting a category", () => api.deleteCategory(category.id, apiKey));
    setConfirmId(null);
    if (ok) setNote(`Deleted ${category.name}.`);
  };

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Categories</h2>
        <span className="muted small">
          {categories.length} categor{categories.length === 1 ? "y" : "ies"}
        </span>
      </div>
      <div className="panel-body">
        {error && <p className="error">{error}</p>}
        {note && <p className="ok">{note}</p>}

        {categories.length === 0 ? (
          <p className="muted small">None yet. A product needs one, so add the first below.</p>
        ) : (
          <table>
            <tbody>
              {categories.map((c) => (
                <tr key={c.id} style={{ cursor: "default" }}>
                  <td>
                    {editingId === c.id ? (
                      <input
                        type="text"
                        value={draftName}
                        maxLength={100}
                        autoFocus
                        aria-label={`New name for ${c.name}`}
                        onChange={(e) => setDraftName(e.target.value)}
                        onKeyDown={(e) => {
                          if (e.key === "Enter") void rename(c);
                          if (e.key === "Escape") setEditingId(null);
                        }}
                      />
                    ) : (
                      c.name
                    )}
                  </td>
                  <td className="num muted small" title="Products in this category">
                    {c.productCount}
                  </td>
                  <td className="actions">
                    {editingId === c.id ? (
                      <>
                        <button type="button" disabled={busy} onClick={() => void rename(c)}>
                          Save
                        </button>
                        <button type="button" onClick={() => setEditingId(null)}>
                          Cancel
                        </button>
                      </>
                    ) : confirmId === c.id ? (
                      <>
                        <span className="small">Delete?</span>
                        <button
                          type="button"
                          className="danger"
                          disabled={busy}
                          onClick={() => void remove(c)}
                        >
                          Yes
                        </button>
                        <button type="button" onClick={() => setConfirmId(null)}>
                          No
                        </button>
                      </>
                    ) : (
                      <>
                        <button
                          type="button"
                          onClick={() => {
                            setEditingId(c.id);
                            setDraftName(c.name);
                            setConfirmId(null);
                          }}
                        >
                          Rename
                        </button>
                        <button
                          type="button"
                          onClick={() => {
                            setConfirmId(c.id);
                            setEditingId(null);
                          }}
                        >
                          Delete
                        </button>
                      </>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        <form onSubmit={create} style={{ marginTop: "0.9rem" }}>
          <div className="row">
            <div className="field">
              <label htmlFor="c-name">New category</label>
              <input
                id="c-name"
                type="text"
                value={name}
                maxLength={100}
                placeholder="Consumables"
                onChange={(e) => setName(e.target.value)}
              />
            </div>
            <div className="field">
              <label htmlFor="c-desc">Description</label>
              <input
                id="c-desc"
                type="text"
                value={description}
                maxLength={500}
                placeholder="Optional"
                onChange={(e) => setDescription(e.target.value)}
              />
            </div>
          </div>
          <button type="submit" disabled={busy || !name.trim()}>
            {busy ? "Working…" : "Add category"}
          </button>
        </form>
      </div>
    </section>
  );
}

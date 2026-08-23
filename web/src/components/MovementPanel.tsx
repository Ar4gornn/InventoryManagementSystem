import { useEffect, useState } from "react";

import { ApiError, api, type Movement, type MovementType, type Product } from "../api";

interface Props {
  product: Product | null;
  apiKey: string;
  onRecorded: () => void;
}

/**
 * The stock history for one product, and the form that adds to it.
 *
 * The form sends a positive quantity plus a direction; the API converts that to a
 * signed delta. Asking a user to type -5 to remove five units would be a bad API
 * and a worse form.
 */
export function MovementPanel({ product, apiKey, onRecorded }: Props) {
  const [movements, setMovements] = useState<Movement[]>([]);
  const [type, setType] = useState<MovementType>("In");
  const [quantity, setQuantity] = useState(1);
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [note, setNote] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    setError(null);
    setNote(null);

    if (!product) {
      setMovements([]);
      return;
    }

    let live = true;
    api.movements(product.id).then(
      (result) => live && setMovements(result),
      (e) => live && setError(e instanceof ApiError ? e.message : String(e)),
    );

    return () => {
      live = false;
    };
  }, [product]);

  if (!product) {
    return (
      <section className="panel">
        <div className="panel-head">
          <h2>Stock</h2>
        </div>
        <div className="panel-body">
          <p className="muted">Select a product to see its movement history.</p>
        </div>
      </section>
    );
  }

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setNote(null);

    if (!apiKey) {
      setError("Recording a movement needs the API key. Enter it at the top right.");
      return;
    }

    setBusy(true);
    try {
      await api.recordMovement(product.id, { type, quantity, reason: reason.trim() || undefined }, apiKey);
      setMovements(await api.movements(product.id));
      setReason("");
      setNote(`Recorded ${type} ${quantity}.`);
      onRecorded();
    } catch (e) {
      // The 400 from the non-negative invariant lands here, and its message from
      // the API is more useful than anything this component could invent.
      setError(e instanceof ApiError ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>{product.sku}</h2>
        <span className="muted small">{product.name}</span>
      </div>
      <div className="panel-body">
        <div className="stock">{product.quantityOnHand}</div>
        <p className="muted small" style={{ marginTop: 0 }}>
          on hand · summed from {movements.length} movement{movements.length === 1 ? "" : "s"}
        </p>

        {error && <p className="error">{error}</p>}
        {note && <p className="ok">{note}</p>}

        <form onSubmit={submit}>
          <div className="row">
            <div className="field">
              <label htmlFor="type">Direction</label>
              <select
                id="type"
                value={type}
                onChange={(e) => setType(e.target.value as MovementType)}
              >
                <option value="In">In</option>
                <option value="Out">Out</option>
                <option value="Adjustment">Adjustment</option>
              </select>
            </div>
            <div className="field">
              <label htmlFor="qty">Quantity</label>
              <input
                id="qty"
                type="number"
                min={1}
                value={quantity}
                onChange={(e) => setQuantity(Number(e.target.value))}
              />
            </div>
          </div>

          <div className="field">
            <label htmlFor="reason">Reason</label>
            <input
              id="reason"
              type="text"
              value={reason}
              placeholder="Delivery 4471"
              onChange={(e) => setReason(e.target.value)}
            />
          </div>

          <button type="submit" className="primary" disabled={busy || quantity < 1}>
            {busy ? "Recording…" : "Record movement"}
          </button>
        </form>

        <h3 style={{ margin: "1.25rem 0 0.5rem", fontSize: "0.8rem" }} className="muted">
          History
        </h3>
        {movements.length === 0 ? (
          <p className="muted small">No movements yet.</p>
        ) : (
          <table>
            <tbody>
              {movements.map((m) => (
                <tr key={m.id} style={{ cursor: "default" }}>
                  <td>
                    <span className={`badge ${m.type}`}>{m.type}</span>
                  </td>
                  <td className={`num delta ${m.quantityDelta >= 0 ? "pos" : "neg"}`}>
                    {m.quantityDelta > 0 ? `+${m.quantityDelta}` : m.quantityDelta}
                  </td>
                  <td className="muted small">{m.reason ?? "—"}</td>
                  <td className="muted small">
                    {new Date(m.occurredAt).toLocaleDateString()}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </section>
  );
}

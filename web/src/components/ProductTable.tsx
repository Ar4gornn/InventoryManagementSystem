import { useState } from "react";

import type { Product } from "../api";

interface Props {
  products: Product[];
  loading: boolean;
  selectedId: number | null;
  onSelect: (product: Product) => void;
  onEdit: (product: Product) => void;
  onDelete: (product: Product) => void;
}

export function ProductTable({
  products,
  loading,
  selectedId,
  onSelect,
  onEdit,
  onDelete,
}: Props) {
  const [confirmId, setConfirmId] = useState<number | null>(null);

  if (!loading && products.length === 0) {
    return <p className="muted">Nothing matches. Clear the search or the category filter.</p>;
  }

  return (
    <table>
      <thead>
        <tr>
          <th>SKU</th>
          <th>Name</th>
          <th>Category</th>
          <th style={{ textAlign: "right" }}>On hand</th>
          <th aria-label="Row actions" />
        </tr>
      </thead>
      <tbody style={{ opacity: loading ? 0.55 : 1 }}>
        {products.map((product) => (
          <tr
            key={product.id}
            className={product.id === selectedId ? "selected" : undefined}
            onClick={() => onSelect(product)}
            tabIndex={0}
            onKeyDown={(e) => {
              if (e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                onSelect(product);
              }
            }}
            aria-selected={product.id === selectedId}
          >
            <td className="sku">{product.sku}</td>
            <td>{product.name}</td>
            <td className="muted">{product.categoryName}</td>
            <td className="num">{product.quantityOnHand}</td>
            {/* The row itself selects, so every control in here has to stop the
                click from bubbling or editing would also change the stock panel. */}
            <td className="actions" onClick={(e) => e.stopPropagation()}>
              {confirmId === product.id ? (
                <>
                  <span className="small">Delete?</span>
                  <button
                    type="button"
                    className="danger"
                    onClick={() => {
                      setConfirmId(null);
                      onDelete(product);
                    }}
                  >
                    Yes
                  </button>
                  <button type="button" onClick={() => setConfirmId(null)}>
                    No
                  </button>
                </>
              ) : (
                <>
                  <button type="button" onClick={() => onEdit(product)}>
                    Edit
                  </button>
                  <button type="button" onClick={() => setConfirmId(product.id)}>
                    Delete
                  </button>
                </>
              )}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

import type { Product } from "../api";

interface Props {
  products: Product[];
  loading: boolean;
  selectedId: number | null;
  onSelect: (product: Product) => void;
}

export function ProductTable({ products, loading, selectedId, onSelect }: Props) {
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
          </tr>
        ))}
      </tbody>
    </table>
  );
}

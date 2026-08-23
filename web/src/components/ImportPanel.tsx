import { useRef, useState } from "react";

import { ApiError, api, type ImportResult } from "../api";

interface Props {
  apiKey: string;
  onImported: () => void;
}

/**
 * CSV bulk import.
 *
 * The API reports each row separately, so this shows exactly which lines failed
 * and why rather than a single "import failed". A two hundred row file with one
 * typo should import one hundred and ninety nine rows and name the one.
 */
export function ImportPanel({ apiKey, onImported }: Props) {
  const [result, setResult] = useState<ImportResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const input = useRef<HTMLInputElement>(null);

  const upload = async (file: File | undefined) => {
    if (!file) return;

    setError(null);
    setResult(null);

    if (!apiKey) {
      setError("Importing needs the API key. Enter it at the top right.");
      return;
    }

    setBusy(true);
    try {
      setResult(await api.importCsv(file, apiKey));
      onImported();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="panel">
      <div className="panel-head">
        <h2>Bulk import</h2>
        <span className="muted small">sku,name,description,category,quantity</span>
      </div>
      <div
        className="panel-body"
        onDragOver={(e) => e.preventDefault()}
        onDrop={(e) => {
          e.preventDefault();
          void upload(e.dataTransfer.files[0]);
        }}
      >
        {error && <p className="error">{error}</p>}

        <div className="controls">
          <button type="button" onClick={() => input.current?.click()} disabled={busy}>
            {busy ? "Importing…" : "Choose a CSV…"}
          </button>
          <span className="muted small">or drop a file here</span>
          <input
            ref={input}
            type="file"
            accept=".csv,text/csv"
            hidden
            onChange={(e) => {
              void upload(e.target.files?.[0]);
              e.target.value = "";
            }}
          />
        </div>

        {result && (
          <>
            <p className={result.failedCount === 0 ? "ok" : "error"} style={{ marginTop: "0.85rem" }}>
              {result.importedCount} of {result.totalRows} rows imported
              {result.failedCount > 0 && `, ${result.failedCount} rejected`}.
            </p>

            {result.failedCount > 0 && (
              <div className="import-rows">
                <table>
                  <thead>
                    <tr>
                      <th>Line</th>
                      <th>SKU</th>
                      <th>Why it was rejected</th>
                    </tr>
                  </thead>
                  <tbody>
                    {result.rows
                      .filter((row) => !row.imported)
                      .map((row) => (
                        <tr key={row.line} style={{ cursor: "default" }}>
                          <td className="num">{row.line}</td>
                          <td className="sku">{row.sku || "—"}</td>
                          <td>{row.error}</td>
                        </tr>
                      ))}
                  </tbody>
                </table>
              </div>
            )}
          </>
        )}
      </div>
    </section>
  );
}

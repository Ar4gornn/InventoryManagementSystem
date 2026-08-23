import { useState } from "react";

interface Props {
  apiKey: string;
  onChange: (key: string) => void;
}

/**
 * Where the user supplies the API key.
 *
 * Kept in localStorage on their own machine and sent only to this API. Masked by
 * default, because a key on screen during a screen share is a real way to leak
 * one.
 */
export function ApiKeyBar({ apiKey, onChange }: Props) {
  const [visible, setVisible] = useState(false);

  return (
    <div className="controls">
      <span className={apiKey ? "small muted" : "small"} title="Reads work without a key">
        {apiKey ? "Writes enabled" : "Read-only"}
      </span>
      <input
        type={visible ? "text" : "password"}
        value={apiKey}
        placeholder="X-Api-Key"
        onChange={(e) => onChange(e.target.value)}
        aria-label="API key for write operations"
        autoComplete="off"
        spellCheck={false}
        style={{ width: "12rem" }}
      />
      <button type="button" onClick={() => setVisible((v) => !v)}>
        {visible ? "Hide" : "Show"}
      </button>
    </div>
  );
}

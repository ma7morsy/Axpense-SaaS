import { useEffect, useState, type FormEvent } from "react";
import { Check } from "lucide-react";
import { Modal } from "../ui/Modal";
import { Button } from "../ui/Button";
import { ApiError } from "../../lib/api";

export const PALETTE = ["#19B394", "#1F7BD8", "#7CC0F5", "#1D4E89", "#F2A93B", "#B8C4D0", "#8E6CD8", "#E0533D", "#2E86DE", "#16A085"];

/** A single name field, optionally with a colour swatch picker (expense types). */
export function SimpleNameModal({
  open,
  title,
  description,
  label,
  placeholder,
  submitLabel,
  initialName,
  initialColor,
  withColor,
  onClose,
  onSubmit,
}: {
  open: boolean;
  title: string;
  description?: string;
  label: string;
  placeholder?: string;
  submitLabel: string;
  initialName?: string;
  initialColor?: string;
  withColor?: boolean;
  onClose: () => void;
  onSubmit: (value: { name: string; color?: string }) => Promise<void>;
}) {
  const [name, setName] = useState("");
  const [color, setColor] = useState<string | undefined>(undefined);
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!open) return;
    setName(initialName ?? "");
    setColor(initialColor);
    setError("");
  }, [open, initialName, initialColor]);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!name.trim()) return setError(`${label} is required.`);
    setSaving(true);
    try {
      await onSubmit({ name: name.trim(), color });
    } catch (err) {
      setError(err instanceof ApiError ? Object.values(err.details)[0] ?? err.message : err instanceof Error ? err.message : "Could not save.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={title}
      description={description}
      size="slim"
      footer={
        <>
          <Button variant="secondary" type="button" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" form="simple-name-form" disabled={saving}>
            {saving ? "Saving…" : submitLabel}
          </Button>
        </>
      }
    >
      <form id="simple-name-form" onSubmit={submit} noValidate>
        <div className="f" style={{ marginBottom: withColor ? 16 : 0 }}>
          <label htmlFor="sn-name">
            {label}
            <span className="req"> *</span>
          </label>
          <input
            id="sn-name"
            autoFocus
            value={name}
            placeholder={placeholder}
            onChange={(e) => setName(e.target.value)}
            style={error ? { borderColor: "var(--red)" } : undefined}
          />
          {error && (
            <span className="hint" style={{ color: "var(--red)" }}>
              {error}
            </span>
          )}
        </div>
        {withColor && (
          <div className="f">
            <label>Colour</label>
            <div className="swatches" role="radiogroup" aria-label="Colour">
              {PALETTE.map((c) => (
                <button
                  key={c}
                  type="button"
                  role="radio"
                  aria-checked={color?.toUpperCase() === c}
                  aria-label={c}
                  className="sw"
                  style={{ background: c }}
                  onClick={() => setColor(c)}
                >
                  {color?.toUpperCase() === c && <Check />}
                </button>
              ))}
            </div>
            <span className="hint">Used for this type in charts and the budget allocation.</span>
          </div>
        )}
      </form>
    </Modal>
  );
}

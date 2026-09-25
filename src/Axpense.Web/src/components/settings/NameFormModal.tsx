import { useEffect, useState, type FormEvent } from "react";
import { Modal } from "../ui/Modal";
import { Button } from "../ui/Button";
import { ApiError } from "../../lib/api";

/** Name + Arabic name form, used for catalogue categories and preset parts. */
export function NameFormModal({
  open,
  title,
  description,
  nameLabel,
  namePlaceholder,
  arPlaceholder,
  submitLabel,
  initial,
  onClose,
  onSubmit,
}: {
  open: boolean;
  title: string;
  description?: string;
  nameLabel: string;
  namePlaceholder?: string;
  arPlaceholder?: string;
  submitLabel: string;
  initial?: { name: string; nameAr?: string | null };
  onClose: () => void;
  /** Throw ApiError to show field errors; resolve to close. */
  onSubmit: (value: { name: string; nameAr: string | null }) => Promise<void>;
}) {
  const [name, setName] = useState("");
  const [nameAr, setNameAr] = useState("");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!open) return;
    setName(initial?.name ?? "");
    setNameAr(initial?.nameAr ?? "");
    setErrors({});
  }, [open, initial]);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      setErrors({ name: `${nameLabel} is required.` });
      return;
    }
    setSaving(true);
    try {
      await onSubmit({ name: name.trim(), nameAr: nameAr.trim() || null });
    } catch (err) {
      setErrors(err instanceof ApiError && Object.keys(err.details).length ? err.details : { name: err instanceof Error ? err.message : "Could not save." });
    } finally {
      setSaving(false);
    }
  };

  const err = (k: string) =>
    errors[k] ? (
      <span className="hint" style={{ color: "var(--red)" }}>
        {errors[k]}
      </span>
    ) : null;

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={title}
      description={description}
      footer={
        <>
          <Button variant="secondary" type="button" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" form="name-form" disabled={saving}>
            {saving ? "Saving…" : submitLabel}
          </Button>
        </>
      }
    >
      <form id="name-form" onSubmit={submit} noValidate>
        <div className="form-grid">
          <div className="f">
            <label htmlFor="nf-name">
              {nameLabel}
              <span className="req"> *</span>
            </label>
            <input
              id="nf-name"
              autoFocus
              value={name}
              placeholder={namePlaceholder}
              onChange={(e) => setName(e.target.value)}
              style={errors.name ? { borderColor: "var(--red)" } : undefined}
            />
            {err("name")}
          </div>
          <div className="f">
            <label htmlFor="nf-ar">Arabic name</label>
            <input id="nf-ar" dir="rtl" lang="ar" value={nameAr} placeholder={arPlaceholder} onChange={(e) => setNameAr(e.target.value)} />
            {err("nameAr")}
          </div>
        </div>
      </form>
    </Modal>
  );
}

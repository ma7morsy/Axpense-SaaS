import { useEffect, useState, type FormEvent, type ReactNode } from "react";
import { Modal } from "../ui/Modal";
import { Button } from "../ui/Button";
import { useToast } from "../ui/Toast";
import { ApiError } from "../../lib/api";

export type Option = string | { value: string; label: string };

export interface FieldSpec {
  name: string;
  label: string;
  type?: "text" | "number" | "date" | "select" | "textarea";
  options?: Option[];
  /** Placeholder option for selects ("Select a driver"). */
  blank?: string;
  req?: boolean;
  full?: boolean;
  hint?: string;
  placeholder?: string;
  step?: string;
}

export type FormValues = Record<string, string>;

/**
 * Form modal for the vehicle record tabs (parts, fuel, readings, issues, maintenance, expenses, hand-off).
 * Converts the values to a request body (numbers → number, blanks → null) and maps the API's
 * field-level errors from the standard envelope back onto the inputs. Validation lives on the server.
 */
export function RecordFormModal({
  open,
  title,
  description,
  submitLabel = "Save",
  fields,
  initial,
  intro,
  onValuesChange,
  onSubmit,
  onClose,
  size,
}: {
  open: boolean;
  title: string;
  description?: string;
  submitLabel?: string;
  fields: FieldSpec[];
  initial: FormValues;
  intro?: ReactNode;
  /** Lets a form react to a change (e.g. category → part list). Return the next values. */
  onValuesChange?: (next: FormValues, changed: string) => FormValues;
  onSubmit: (body: Record<string, unknown>, values: FormValues) => Promise<void>;
  onClose: () => void;
  size?: "wide" | "slim";
}) {
  const toast = useToast();
  const [values, setValues] = useState<FormValues>(initial);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (open) {
      setValues(initial);
      setErrors({});
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  const set = (name: string, value: string) => {
    setValues((v) => {
      const next = { ...v, [name]: value };
      return onValuesChange ? onValuesChange(next, name) : next;
    });
    if (errors[name]) setErrors(({ [name]: _r, ...rest }) => rest);
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const missing: Record<string, string> = {};
    for (const f of fields) if (f.req && !String(values[f.name] ?? "").trim()) missing[f.name] = "Required";
    if (Object.keys(missing).length) {
      setErrors(missing);
      toast("Fill in the highlighted fields to continue", "bad");
      return;
    }
    const body: Record<string, unknown> = {};
    for (const f of fields) {
      const raw = (values[f.name] ?? "").trim();
      body[f.name] = raw === "" ? null : f.type === "number" ? Number(raw) : raw;
    }
    for (const [k, v] of Object.entries(values)) if (!(k in body)) body[k] = v === "" ? null : v;
    setSaving(true);
    try {
      await onSubmit(body, values);
    } catch (err) {
      if (err instanceof ApiError && Object.keys(err.details).length) setErrors(err.details);
      toast(err instanceof Error ? err.message : "Could not save", "bad");
    } finally {
      setSaving(false);
    }
  };

  const formId = `rf-${title.replace(/\W+/g, "-").toLowerCase()}`;
  return (
    <Modal
      open={open}
      onClose={onClose}
      title={title}
      description={description}
      size={size}
      footer={
        <>
          <Button variant="secondary" type="button" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" form={formId} disabled={saving}>
            {saving ? "Saving…" : submitLabel}
          </Button>
        </>
      }
    >
      <form id={formId} onSubmit={submit} noValidate>
        {intro}
        <div className="form-grid">
          {fields.map((f) => {
            const id = `${formId}-${f.name}`;
            const style = errors[f.name] ? { borderColor: "var(--red)" } : undefined;
            const value = values[f.name] ?? "";
            const opts = (f.options ?? []).map((o) => (typeof o === "string" ? { value: o, label: o } : o));
            return (
              <div key={f.name} className={`f ${f.full || f.type === "textarea" ? "full" : ""}`}>
                <label htmlFor={id}>
                  {f.label}
                  {f.req && <span className="req"> *</span>}
                </label>
                {f.type === "select" ? (
                  <select id={id} value={value} onChange={(e) => set(f.name, e.target.value)} style={style}>
                    {f.blank !== undefined && <option value="">{f.blank}</option>}
                    {opts.map((o) => (
                      <option key={o.value} value={o.value}>
                        {o.label}
                      </option>
                    ))}
                  </select>
                ) : f.type === "textarea" ? (
                  <textarea id={id} rows={3} value={value} placeholder={f.placeholder} onChange={(e) => set(f.name, e.target.value)} style={style} />
                ) : (
                  <input
                    id={id}
                    type={f.type ?? "text"}
                    step={f.step}
                    value={value}
                    placeholder={f.placeholder}
                    onChange={(e) => set(f.name, e.target.value)}
                    style={style}
                  />
                )}
                {errors[f.name] && errors[f.name] !== "Required" ? (
                  <span className="hint" style={{ color: "var(--red)" }}>
                    {errors[f.name]}
                  </span>
                ) : (
                  f.hint && <span className="hint">{f.hint}</span>
                )}
              </div>
            );
          })}
        </div>
      </form>
    </Modal>
  );
}

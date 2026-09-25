import { useEffect, useState, type FormEvent } from "react";
import { Plus, Trash2 } from "lucide-react";
import { Modal } from "../ui/Modal";
import { Button } from "../ui/Button";
import { useToast } from "../ui/Toast";
import { ApiError, catalogApi, inspectionTemplatesApi } from "../../lib/api";
import type { InspectionOptions, InspectionTemplate, PartCategory } from "../../lib/types";

interface Row {
  key: number;
  label: string;
  presetPartId: string;
  fieldType: string;
  critical: string;
  unit: string;
  min: string;
  max: string;
  issueOnFail: string;
}

let seq = 0;
const blankRow = (partId = ""): Row => ({
  key: ++seq, label: "", presetPartId: partId, fieldType: "passfail", critical: "false", unit: "", min: "", max: "", issueOnFail: "true",
});

/** New / edit inspection template. Every check is linked to a part of the catalogue. */
export function TemplateFormModal({
  open,
  template,
  onClose,
  onSaved,
}: {
  open: boolean;
  template: InspectionTemplate | null;
  onClose: () => void;
  onSaved: (t: InspectionTemplate) => void;
}) {
  const toast = useToast();
  const [options, setOptions] = useState<InspectionOptions | null>(null);
  const [categories, setCategories] = useState<PartCategory[]>([]);
  const [name, setName] = useState("");
  const [scope, setScope] = useState("All");
  const [active, setActive] = useState("true");
  const [rows, setRows] = useState<Row[]>([]);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!open) return;
    inspectionTemplatesApi.options().then(setOptions).catch(() => undefined);
    catalogApi
      .get()
      .then((c) => {
        setCategories(c.categories);
        const first = c.categories[0]?.parts[0]?.id ?? "";
        setRows((r) => (r.length === 1 && !r[0].presetPartId ? [{ ...r[0], presetPartId: first }] : r));
      })
      .catch(() => setCategories([]));
    setName(template?.name ?? "");
    setScope(template?.scope ?? "All");
    setActive(template ? String(template.active) : "true");
    setRows(
      template?.items.length
        ? template.items.map((i) => ({
            key: ++seq, label: i.label, presetPartId: i.presetPartId, fieldType: i.fieldType, critical: String(i.critical),
            unit: i.unit ?? "", min: i.min == null ? "" : String(i.min), max: i.max == null ? "" : String(i.max), issueOnFail: String(i.issueOnFail),
          }))
        : [blankRow()]
    );
    setErrors({});
  }, [open, template]);

  const setRow = (key: number, patch: Partial<Row>) => setRows((rs) => rs.map((r) => (r.key === key ? { ...r, ...patch } : r)));
  const removeRow = (key: number) => {
    if (rows.length <= 1) {
      toast("A template needs at least one check", "bad");
      return;
    }
    setRows((rs) => rs.filter((r) => r.key !== key));
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      setErrors({ name: "Required" });
      toast("Fill in the highlighted fields to continue", "bad");
      return;
    }
    const body = {
      name: name.trim(),
      scope,
      active: active === "true",
      items: rows.map((r) => ({
        label: r.label.trim() || null,
        presetPartId: r.presetPartId || null,
        fieldType: r.fieldType,
        critical: r.critical === "true",
        unit: r.fieldType === "gauge" ? r.unit.trim() || null : null,
        min: r.fieldType === "gauge" && r.min !== "" ? Number(r.min) : null,
        max: r.fieldType === "gauge" && r.max !== "" ? Number(r.max) : null,
        issueOnFail: r.issueOnFail === "true",
      })),
    };
    setSaving(true);
    try {
      const saved = template ? await inspectionTemplatesApi.update(template.id, body) : await inspectionTemplatesApi.create(body);
      toast(template ? "Template saved" : "Template created");
      onSaved(saved);
    } catch (err) {
      if (err instanceof ApiError && Object.keys(err.details).length) setErrors(err.details);
      toast(err instanceof Error ? err.message : "Could not save the template", "bad");
    } finally {
      setSaving(false);
    }
  };

  const err = (k: string) => errors[k];
  const bad = (k: string) => (errors[k] ? { borderColor: "var(--red)" } : undefined);
  const hint = (k: string) =>
    err(k) && err(k) !== "Required" ? (
      <span className="hint" style={{ color: "var(--red)" }}>
        {err(k)}
      </span>
    ) : null;

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={template ? `Edit ${template.name}` : "New inspection template"}
      size="wide"
      footer={
        <>
          <Button variant="secondary" type="button" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" form="tpl-form" disabled={saving || !categories.length}>
            {saving ? "Saving…" : template ? "Save template" : "Create template"}
          </Button>
        </>
      }
    >
      <form id="tpl-form" onSubmit={submit} noValidate>
        <div className="form-grid">
          <div className="f">
            <label htmlFor="tpl-name">
              Template name<span className="req"> *</span>
            </label>
            <input id="tpl-name" autoFocus value={name} placeholder="Daily pre-trip — heavy truck" onChange={(e) => setName(e.target.value)} style={bad("name")} />
            {hint("name")}
          </div>
          <div className="f">
            <label htmlFor="tpl-scope">Applies to</label>
            <select id="tpl-scope" value={scope} disabled={template?.isSystem} onChange={(e) => setScope(e.target.value)} style={bad("scope")}>
              {(options?.scopes ?? ["All"]).map((s) => (
                <option key={s}>{s}</option>
              ))}
            </select>
            {hint("scope")}
          </div>
          <div className="f full">
            <label htmlFor="tpl-state">State</label>
            <select id="tpl-state" value={active} disabled={template?.isSystem} onChange={(e) => setActive(e.target.value)}>
              <option value="true">Active</option>
              <option value="false">Draft</option>
            </select>
            <span className="hint">{template?.isSystem ? "Built-in hand-off checklist: always active and applies to every vehicle." : "Only active templates can be run."}</span>
          </div>
        </div>

        <div className="fieldset">
          <div className="fs-t">Checklist</div>
          {errors.items && (
            <div className="t-sub" style={{ color: "var(--red)", marginBottom: 8 }}>
              {errors.items}
            </div>
          )}
          {rows.map((r, ix) => {
            const k = `items[${ix}]`;
            const gauge = r.fieldType === "gauge";
            return (
              <div key={r.key} className="smart-card" style={{ padding: "11px 12px" }}>
                <div className="form-grid" style={{ gap: 8 }}>
                  <div className="f">
                    <label>Label</label>
                    <input value={r.label} placeholder="Defaults to the part name" onChange={(e) => setRow(r.key, { label: e.target.value })} style={bad(`${k}.label`)} />
                    {hint(`${k}.label`)}
                  </div>
                  <div className="f">
                    <label>
                      Linked part<span className="req"> *</span>
                    </label>
                    <select value={r.presetPartId} onChange={(e) => setRow(r.key, { presetPartId: e.target.value })} style={bad(`${k}.presetPartId`)}>
                      <option value="">Select a part</option>
                      {categories.map((c) => (
                        <optgroup key={c.id} label={c.name}>
                          {c.parts.map((p) => (
                            <option key={p.id} value={p.id}>
                              {p.name}
                              {p.nameAr ? ` — ${p.nameAr}` : ""}
                            </option>
                          ))}
                        </optgroup>
                      ))}
                    </select>
                    {hint(`${k}.presetPartId`)}
                  </div>
                  <div className="f">
                    <label>Field type</label>
                    <select value={r.fieldType} onChange={(e) => setRow(r.key, { fieldType: e.target.value })}>
                      {(options?.fieldTypes ?? [{ value: "passfail", label: "Pass / Fail" }]).map((t) => (
                        <option key={t.value} value={t.value}>
                          {t.label}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div className="f">
                    <label>Criticality</label>
                    <select value={r.critical} onChange={(e) => setRow(r.key, { critical: e.target.value })}>
                      <option value="true">Critical — grounds the vehicle</option>
                      <option value="false">Non-critical</option>
                    </select>
                  </div>
                  <div className="f">
                    <label>Unit (gauge only){gauge && <span className="req"> *</span>}</label>
                    <input value={r.unit} disabled={!gauge} placeholder="PSI, mm, bar" onChange={(e) => setRow(r.key, { unit: e.target.value })} style={bad(`${k}.unit`)} />
                    {hint(`${k}.unit`)}
                  </div>
                  <div className="f">
                    <label>Expected range{gauge && <span className="req"> *</span>}</label>
                    <div className="flex">
                      <input className="mono" type="number" step="0.1" disabled={!gauge} value={r.min} placeholder="min" onChange={(e) => setRow(r.key, { min: e.target.value })} style={bad(`${k}.min`)} />
                      <input className="mono" type="number" step="0.1" disabled={!gauge} value={r.max} placeholder="max" onChange={(e) => setRow(r.key, { max: e.target.value })} style={bad(`${k}.min`)} />
                    </div>
                    {hint(`${k}.min`)}
                  </div>
                  <div className="f full">
                    <label>Trigger logic</label>
                    <select value={r.issueOnFail} onChange={(e) => setRow(r.key, { issueOnFail: e.target.value })}>
                      <option value="true">If failed, create an issue automatically</option>
                      <option value="false">Record the failure only</option>
                    </select>
                  </div>
                </div>
                <div style={{ textAlign: "right", marginTop: 6 }}>
                  <Button type="button" variant="danger" size="sm" onClick={() => removeRow(r.key)}>
                    <Trash2 /> Remove check
                  </Button>
                </div>
              </div>
            );
          })}
          <Button type="button" variant="secondary" size="sm" style={{ marginTop: 8 }} onClick={() => setRows((rs) => [...rs, blankRow(categories[0]?.parts[0]?.id ?? "")])}>
            <Plus /> Add check
          </Button>
        </div>
      </form>
    </Modal>
  );
}

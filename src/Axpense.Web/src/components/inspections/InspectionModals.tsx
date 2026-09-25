import { useCallback, useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { AlertTriangle, Camera, Check } from "lucide-react";
import { Modal } from "../ui/Modal";
import { Button } from "../ui/Button";
import { PageSpinner } from "../ui/Spinner";
import { ConfirmDialog } from "../ui/ConfirmDialog";
import { useToast } from "../ui/Toast";
import { useAuth } from "../../contexts/AuthContext";
import { ApiError, inspectionTemplatesApi, inspectionsApi, vehiclesApi } from "../../lib/api";
import type { InspectionDetail, InspectionItem, InspectionTemplate, Vehicle } from "../../lib/types";
import { formatDay } from "../../lib/utils";
import "./inspections.css";

const num = (v: number | null | undefined) => (v == null ? "—" : Number(v).toLocaleString("en-US", { maximumFractionDigits: 1 }));

/** Loads an evidence photo through the authenticated API. */
function useEvidence(inspectionId: string, item: InspectionItem) {
  const [url, setUrl] = useState<string | null>(null);
  useEffect(() => {
    if (!item.hasPhoto) {
      setUrl(null);
      return;
    }
    let alive = true;
    let u: string | null = null;
    inspectionsApi
      .photoUrl(inspectionId, item.id)
      .then((x) => {
        u = x;
        if (alive) setUrl(x);
        else URL.revokeObjectURL(x);
      })
      .catch(() => alive && setUrl(null));
    return () => {
      alive = false;
      if (u) URL.revokeObjectURL(u);
    };
  }, [inspectionId, item.id, item.hasPhoto]);
  return url;
}

function Evidence({ inspectionId, item, size = "lg" }: { inspectionId: string; item: InspectionItem; size?: "sm" | "lg" }) {
  const url = useEvidence(inspectionId, item);
  if (!url) return null;
  return (
    <a href={url} target="_blank" rel="noreferrer" onClick={(e) => e.stopPropagation()}>
      <img className={`ins-ev ${size}`} src={url} alt={`Evidence — ${item.label}`} />
    </a>
  );
}

const itemSub = (it: InspectionItem, withReading = false) =>
  [
    it.partName,
    it.fieldTypeLabel,
    it.fieldType === "gauge" && it.min != null && it.max != null && !withReading ? `expected ${num(it.min)}–${num(it.max)} ${it.unit ?? ""}`.trim() : null,
    withReading && it.value != null ? `reading ${num(it.value)}${it.unit ? ` ${it.unit}` : ""}` : null,
  ]
    .filter(Boolean)
    .join(" · ");

// =====================================================================
// Start
// =====================================================================

/**
 * "Run inspection": pick vehicle + active template (filtered to the vehicle's category), odometer and inspector,
 * then the checklist opens. A vehicle with a run in progress resumes it instead of starting a new one.
 */
export function StartInspectionModal({
  open,
  vehicleId,
  templateId,
  onClose,
  onStarted,
}: {
  open: boolean;
  vehicleId?: string;
  templateId?: string;
  onClose: () => void;
  onStarted: (inspectionId: string) => void;
}) {
  const toast = useToast();
  const { user } = useAuth();
  const [vehicles, setVehicles] = useState<Vehicle[]>([]);
  const [templates, setTemplates] = useState<InspectionTemplate[]>([]);
  const [form, setForm] = useState({ vehicleId: "", templateId: "", odometer: "", inspectorName: "" });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!open) return;
    setErrors({});
    Promise.all([vehiclesApi.list(), inspectionTemplatesApi.list()])
      .then(([v, t]) => {
        const vs = v.filter((x) => x.status !== "Retired");
        setVehicles(vs);
        setTemplates(t.filter((x) => x.active));
        const veh = vs.find((x) => x.id === vehicleId);
        setForm({
          vehicleId: veh?.id ?? "",
          templateId: templateId ?? "",
          odometer: veh?.currentOdometer != null ? String(veh.currentOdometer) : "",
          inspectorName: user ? `${user.firstName} ${user.lastName}`.trim() || user.userName : "",
        });
      })
      .catch(() => toast("Vehicles or templates could not be loaded", "bad"));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, vehicleId, templateId]);

  const vehicle = vehicles.find((v) => v.id === form.vehicleId);
  const usable = templates.filter((t) => !vehicle || t.scope === "All" || t.scope === vehicle.category);
  const set = (k: keyof typeof form, v: string) => {
    setForm((f) => {
      const next = { ...f, [k]: v };
      if (k === "vehicleId") {
        const veh = vehicles.find((x) => x.id === v);
        next.odometer = veh?.currentOdometer != null ? String(veh.currentOdometer) : "";
        const tpl = templates.find((t) => t.id === f.templateId);
        if (tpl && veh && tpl.scope !== "All" && tpl.scope !== veh.category) next.templateId = "";
      }
      return next;
    });
    setErrors(({ [k]: _r, ...rest }) => rest);
  };

  const submit = async () => {
    const missing: Record<string, string> = {};
    (["vehicleId", "templateId", "odometer", "inspectorName"] as const).forEach((k) => !form[k].trim() && (missing[k] = "Required"));
    if (Object.keys(missing).length) {
      setErrors(missing);
      toast("Fill in the highlighted fields to continue", "bad");
      return;
    }
    setSaving(true);
    try {
      const r = await inspectionsApi.start({ vehicleId: form.vehicleId, templateId: form.templateId, odometer: Number(form.odometer), inspectorName: form.inspectorName.trim() });
      onStarted(r.id);
    } catch (err) {
      if (err instanceof ApiError && err.code === "INSPECTION_IN_PROGRESS") {
        const drafts = await inspectionsApi.list({ vehicleId: form.vehicleId, status: "In progress" }).catch(() => null);
        const draft = drafts?.items[0];
        if (draft) {
          toast(`Resumed ${draft.code} — already in progress on this vehicle`);
          onStarted(draft.id);
          return;
        }
      }
      if (err instanceof ApiError && Object.keys(err.details).length) setErrors(err.details);
      toast(err instanceof Error ? err.message : "Could not start the inspection", "bad");
    } finally {
      setSaving(false);
    }
  };

  const bad = (k: string) => (errors[k] ? { borderColor: "var(--red)" } : undefined);
  const msg = (k: string) =>
    errors[k] && errors[k] !== "Required" ? (
      <span className="hint" style={{ color: "var(--red)" }}>
        {errors[k]}
      </span>
    ) : null;

  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Run inspection"
      description="Pick the vehicle and checklist, then work through the cards."
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={submit} disabled={saving}>
            {saving ? "Starting…" : "Start inspection"}
          </Button>
        </>
      }
    >
      <div className="form-grid">
        <div className="f full">
          <label htmlFor="ri-v">
            Vehicle<span className="req"> *</span>
          </label>
          <select id="ri-v" value={form.vehicleId} onChange={(e) => set("vehicleId", e.target.value)} style={bad("vehicleId")}>
            <option value="">Select a vehicle</option>
            {vehicles.map((v) => (
              <option key={v.id} value={v.id}>
                {v.name} · {v.plateNumber}
              </option>
            ))}
          </select>
          {msg("vehicleId")}
        </div>
        <div className="f full">
          <label htmlFor="ri-t">
            Template<span className="req"> *</span>
          </label>
          <select id="ri-t" value={form.templateId} onChange={(e) => set("templateId", e.target.value)} style={bad("templateId")}>
            <option value="">{usable.length ? "Select a template" : "No active template applies to this vehicle"}</option>
            {usable.map((t) => (
              <option key={t.id} value={t.id}>
                {t.name} · {t.checkCount} checks
              </option>
            ))}
          </select>
          {msg("templateId")}
        </div>
        <div className="f">
          <label htmlFor="ri-o">
            Odometer at inspection ({vehicle?.readingUnit ?? "km"})<span className="req"> *</span>
          </label>
          <input id="ri-o" type="number" value={form.odometer} onChange={(e) => set("odometer", e.target.value)} style={bad("odometer")} />
          {msg("odometer")}
        </div>
        <div className="f">
          <label htmlFor="ri-i">
            Inspector<span className="req"> *</span>
          </label>
          <input id="ri-i" value={form.inspectorName} onChange={(e) => set("inspectorName", e.target.value)} style={bad("inspectorName")} />
          {msg("inspectorName")}
        </div>
      </div>
    </Modal>
  );
}

// =====================================================================
// Run (checklist)
// =====================================================================

function RunCard({ run, item, index, onChange }: { run: InspectionDetail; item: InspectionItem; index: number; onChange: (it: InspectionItem) => void }) {
  const toast = useToast();
  const fileRef = useRef<HTMLInputElement>(null);
  const [value, setValue] = useState(item.value == null ? "" : String(item.value));
  const [comment, setComment] = useState(item.comment ?? "");
  const [busy, setBusy] = useState(false);

  useEffect(() => setValue(item.value == null ? "" : String(item.value)), [item.value]);

  const save = async (patch: { result?: string | null; value?: string; comment?: string }) => {
    const result = patch.result !== undefined ? patch.result : item.result ?? null;
    const v = patch.value !== undefined ? patch.value : value;
    const c = patch.comment !== undefined ? patch.comment : comment;
    setBusy(true);
    try {
      const saved = await inspectionsApi.answer(run.id, item.id, { result, value: v === "" ? null : Number(v), comment: c.trim() || null });
      if (saved.result === "fail" && result !== "fail" && item.fieldType === "gauge")
        toast(`${item.label} is outside ${num(item.min)}–${num(item.max)} ${item.unit ?? ""} — marked failed`, "bad");
      onChange(saved);
    } catch (err) {
      toast(err instanceof Error ? err.message : "Could not save the answer", "bad");
    } finally {
      setBusy(false);
    }
  };

  const upload = async (file: File | undefined) => {
    if (!file) return;
    setBusy(true);
    try {
      onChange(await inspectionsApi.uploadPhoto(run.id, item.id, file));
      toast("Photo attached");
    } catch (err) {
      toast(err instanceof Error ? err.message : "Could not attach the photo", "bad");
    } finally {
      setBusy(false);
      if (fileRef.current) fileRef.current.value = "";
    }
  };

  const r = item.result;
  const showCam = r === "fail" || item.fieldType === "photo" || item.hasPhoto;
  return (
    <div className={`smart-card ${r === "fail" ? "failed" : r === "pass" ? "passed" : ""} ${item.critical ? "crit" : ""}`} style={{ opacity: busy ? 0.75 : 1 }}>
      <div className="between" style={{ alignItems: "flex-start" }}>
        <div style={{ minWidth: 0 }}>
          <div className="flex" style={{ gap: 7 }}>
            <span className="mono" style={{ fontSize: 11, color: "var(--muted)" }}>
              {String(index + 1).padStart(2, "0")}
            </span>
            <span style={{ fontWeight: 600 }}>{item.label}</span>
            {item.critical && <span className="badge bad">Critical</span>}
          </div>
          <div className="t-sub" style={{ marginLeft: 24 }}>
            {itemSub(item)}
          </div>
        </div>
        <div className="flex">
          {item.fieldType === "gauge" && (
            <input
              className="mono ins-val"
              type="number"
              step="0.1"
              placeholder={item.unit ?? ""}
              value={value}
              aria-label={`${item.label} reading`}
              onChange={(e) => setValue(e.target.value)}
              onBlur={() => value !== (item.value == null ? "" : String(item.value)) && save({ value })}
              onKeyDown={(e) => e.key === "Enter" && (e.target as HTMLInputElement).blur()}
            />
          )}
          {item.fieldType === "scale" && (
            <select
              className="ins-val"
              value={value}
              aria-label={`${item.label} score`}
              onChange={(e) => {
                setValue(e.target.value);
                save({ value: e.target.value });
              }}
            >
              <option value="">1–5</option>
              {[1, 2, 3, 4, 5].map((n) => (
                <option key={n} value={n}>
                  {n}
                </option>
              ))}
            </select>
          )}
          <div className="pill-toggle pf" role="group" aria-label={`${item.label} result`}>
            {(["pass", "fail", "na"] as const).map((k) => (
              <button key={k} type="button" data-v={k} className={r === k ? "on" : ""} disabled={busy} onClick={() => save({ result: r === k ? null : k })}>
                {k === "na" ? "N/A" : k === "pass" ? "Pass" : "Fail"}
              </button>
            ))}
          </div>
          {showCam && (
            <button type="button" className={`cam ${item.hasPhoto ? "has" : ""}`} title={item.hasPhoto ? "Replace photo" : "Attach photo"} onClick={() => fileRef.current?.click()}>
              <Camera />
            </button>
          )}
          <input ref={fileRef} type="file" accept="image/jpeg,image/png,image/webp" capture="environment" hidden onChange={(e) => upload(e.target.files?.[0])} />
        </div>
      </div>
      {r === "fail" && (
        <>
          <div style={{ marginTop: 11, display: "flex", gap: 10, alignItems: "flex-start" }}>
            <Evidence inspectionId={run.id} item={item} />
            <textarea
              className="ins-comment"
              placeholder="What exactly failed? Measurements, location, severity."
              value={comment}
              onChange={(e) => setComment(e.target.value)}
              onBlur={() => comment !== (item.comment ?? "") && save({ comment })}
            />
          </div>
          {!item.hasPhoto && (
            <div className="t-sub" style={{ color: "var(--red)", marginTop: 6 }}>
              Photo evidence required before this inspection can be completed.
            </div>
          )}
          {item.issueOnFail && (
            <div className="t-sub flex" style={{ marginTop: 6, gap: 5 }}>
              <AlertTriangle style={{ width: 14, height: 14 }} /> Will create a {item.critical ? "high" : "medium"}-priority issue on completion.
            </div>
          )}
        </>
      )}
      {r !== "fail" && item.fieldType === "photo" && item.hasPhoto && (
        <div style={{ marginTop: 10 }}>
          <Evidence inspectionId={run.id} item={item} size="sm" />
        </div>
      )}
      {r && item.problem && r !== "fail" && (
        <div className="t-sub" style={{ color: "#B45309", marginTop: 6 }}>
          {item.problem}
        </div>
      )}
    </div>
  );
}

/** The checklist of a run in progress. Answers and photos are saved as they are given, so closing keeps the draft. */
export function InspectionRunModal({ inspectionId, onClose, onDone }: { inspectionId: string | null; onClose: () => void; onDone: () => void }) {
  const toast = useToast();
  const navigate = useNavigate();
  const [run, setRun] = useState<InspectionDetail | null>(null);
  const [error, setError] = useState(false);
  const [completing, setCompleting] = useState(false);
  const [discarding, setDiscarding] = useState(false);

  useEffect(() => {
    if (!inspectionId) {
      setRun(null);
      return;
    }
    setError(false);
    inspectionsApi
      .get(inspectionId)
      .then(setRun)
      .catch(() => setError(true));
  }, [inspectionId]);

  const replaceItem = useCallback((it: InspectionItem) => {
    setRun((r) => (r ? { ...r, items: r.items.map((x) => (x.id === it.id ? it : x)) } : r));
  }, []);

  const items = run?.items ?? [];
  const done = items.filter((i) => i.result).length;
  const pass = items.filter((i) => i.result === "pass").length;
  const fail = items.filter((i) => i.result === "fail").length;
  const na = items.filter((i) => i.result === "na").length;
  const critical = items.some((i) => i.critical && i.result === "fail");
  const ready = items.length > 0 && items.every((i) => !i.problem);

  const complete = async () => {
    if (!run) return;
    setCompleting(true);
    try {
      const r = await inspectionsApi.complete(run.id);
      toast(
        r.vehicleGrounded
          ? `${run.code} completed — critical failure, ${run.vehicleName} grounded`
          : `${run.code} completed — ${r.issuesRaised} issue${r.issuesRaised === 1 ? "" : "s"} raised`,
        r.vehicleGrounded ? "bad" : undefined
      );
      onDone();
      navigate(`/vehicles/${run.vehicleId}?tab=inspections`);
    } catch (err) {
      toast(err instanceof Error ? err.message : "Could not complete the inspection", "bad");
      inspectionsApi.get(run.id).then(setRun).catch(() => undefined);
    } finally {
      setCompleting(false);
    }
  };

  const discard = async () => {
    if (!run) return;
    try {
      await inspectionsApi.remove(run.id);
      toast("Inspection draft discarded");
      setDiscarding(false);
      onDone();
    } catch (err) {
      toast(err instanceof Error ? err.message : "Could not discard the draft", "bad");
    }
  };

  return (
    <>
      <Modal
        open={!!inspectionId}
        onClose={onClose}
        size="wide"
        title={run?.templateName ?? "Inspection"}
        description={run ? `${run.vehicleName} · ${run.plateNumber} · odometer ${num(run.odometer)} ${run.readingUnit} · inspector ${run.inspectorName ?? "—"}` : undefined}
        footer={
          <>
            <div style={{ marginRight: "auto" }}>
              <span className="t-sub">Closing this window keeps your progress.</span>
              <Button variant="ghost" size="sm" style={{ color: "var(--red)" }} onClick={() => setDiscarding(true)} disabled={!run}>
                Discard draft
              </Button>
            </div>
            <Button variant="secondary" onClick={onClose}>
              Cancel
            </Button>
          </>
        }
      >
        {error ? (
          <div className="empty">
            <AlertTriangle />
            <h3>The inspection could not be loaded</h3>
          </div>
        ) : !run ? (
          <PageSpinner />
        ) : (
          <div>
            {critical && (
              <div className="banner bad" role="alert">
                <AlertTriangle />
                <div>
                  <div className="bt">A critical check has failed</div>
                  <div className="bd">On completion this vehicle moves to Inoperable / Down and a high-priority issue is raised.</div>
                </div>
              </div>
            )}
            {run.items.map((it, ix) => (
              <RunCard key={it.id} run={run} item={it} index={ix} onChange={replaceItem} />
            ))}
            <div className="summary-bar">
              <span className="s">
                Done<b>
                  {done}/{items.length}
                </b>
              </span>
              <span className="s p">
                Passed<b>{pass}</b>
              </span>
              <span className="s f">
                Failed<b>{fail}</b>
              </span>
              <span className="s">
                N/A<b>{na}</b>
              </span>
              <div className="prog">
                <i style={{ width: `${items.length ? (done / items.length) * 100 : 0}%` }} />
              </div>
              <Button onClick={complete} disabled={!ready || completing} title={ready ? undefined : "Answer every check and attach the required photos"}>
                <Check /> {completing ? "Completing…" : "Complete inspection"}
              </Button>
            </div>
          </div>
        )}
      </Modal>
      <ConfirmDialog
        open={discarding}
        title="Discard inspection"
        message="The answers and photos of this draft will be deleted. Nothing is recorded on the vehicle."
        onConfirm={discard}
        onClose={() => setDiscarding(false)}
      />
    </>
  );
}

// =====================================================================
// View (completed)
// =====================================================================

export function InspectionViewModal({ inspectionId, onClose }: { inspectionId: string | null; onClose: () => void }) {
  const navigate = useNavigate();
  const [x, setX] = useState<InspectionDetail | null>(null);
  const [error, setError] = useState(false);
  useEffect(() => {
    setX(null);
    setError(false);
    if (inspectionId) inspectionsApi.get(inspectionId).then(setX).catch(() => setError(true));
  }, [inspectionId]);

  return (
    <Modal
      open={!!inspectionId}
      onClose={onClose}
      size="wide"
      title={x?.code ?? "Inspection"}
      description={x ? `${x.vehicleName} · ${x.templateName} · ${formatDay(x.date)} · ${x.inspectorName ?? "—"}` : undefined}
      footer={
        <>
          {x && (
            <Button variant="ghost" onClick={() => navigate(`/vehicles/${x.vehicleId}?tab=issues`)}>
              Open vehicle issues
            </Button>
          )}
          <Button variant="secondary" onClick={onClose}>
            Close
          </Button>
        </>
      }
    >
      {error ? (
        <div className="empty">
          <AlertTriangle />
          <h3>The inspection could not be loaded</h3>
        </div>
      ) : !x ? (
        <PageSpinner />
      ) : (
        <>
          <div className="grid g-4" style={{ marginBottom: 16 }}>
            <div className="stat">
              <div className="k">Passed</div>
              <div className="v">{x.summary.passed}</div>
            </div>
            <div className="stat bad">
              <div className="k">Failed</div>
              <div className="v">{x.summary.failed}</div>
            </div>
            <div className="stat neutral">
              <div className="k">N/A</div>
              <div className="v">{x.summary.notApplicable}</div>
            </div>
            <div className="stat neutral">
              <div className="k">Odometer</div>
              <div className="v" style={{ fontSize: 20 }}>
                {num(x.odometer)}
              </div>
            </div>
          </div>
          {x.items.map((it) => (
            <div key={it.id} className={`smart-card ${it.result === "fail" ? "failed" : it.result === "pass" ? "passed" : ""} ${it.critical ? "crit" : ""}`}>
              <div className="between">
                <div>
                  <div style={{ fontWeight: 600 }}>
                    {it.label} {it.critical && <span className="badge bad">Critical</span>}
                  </div>
                  <div className="t-sub">{itemSub(it, true)}</div>
                  {it.comment && (
                    <div className="note" style={{ marginTop: 7 }}>
                      {it.comment}
                    </div>
                  )}
                  {it.issueId && (
                    <div className="t-sub" style={{ marginTop: 5, color: "var(--teal-d)", fontWeight: 600 }}>
                      Issue raised on the vehicle
                    </div>
                  )}
                </div>
                <div className="flex">
                  <Evidence inspectionId={x.id} item={it} size="sm" />
                  <span className={`badge ${it.result === "fail" ? "bad" : it.result === "pass" ? "ok" : "plain"}`}>
                    {it.result === "fail" ? "Fail" : it.result === "pass" ? "Pass" : it.result === "na" ? "N/A" : "—"}
                  </span>
                </div>
              </div>
            </div>
          ))}
        </>
      )}
    </Modal>
  );
}

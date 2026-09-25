import { useEffect, useState } from "react";
import { Modal } from "../ui/Modal";
import { Button } from "../ui/Button";
import { useToast } from "../ui/Toast";
import { useAuth } from "../../contexts/AuthContext";
import { ApiError, driversApi, vehiclesApi } from "../../lib/api";
import type { DriverListItem, VehicleDriver } from "../../lib/types";
import { formatDay, todayPlusDays } from "../../lib/utils";
import "./vehicles.css";

export interface HandOffTarget {
  id: string;
  name: string;
  plateNumber: string;
  currentDriver: VehicleDriver | null;
  /** assign = pick a driver (required); handoff = close the current assignment, new driver optional, optional hand-off inspection. */
  mode: "assign" | "handoff";
}

/**
 * Assign a driver, or hand the vehicle off. On a hand-off the current assignment closes on the effective date;
 * the new driver is optional and a hand-off inspection can be started right after (the caller opens it).
 */
export function HandOffModal({
  vehicle,
  onClose,
  onSaved,
}: {
  vehicle: HandOffTarget | null;
  onClose: () => void;
  /** inspectionId is set when a hand-off inspection was started. */
  onSaved: (inspectionId?: string | null) => void;
}) {
  const toast = useToast();
  const { user } = useAuth();
  const [drivers, setDrivers] = useState<DriverListItem[]>([]);
  const [form, setForm] = useState({ driverId: "", effectiveDate: "", note: "", inspect: false });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!vehicle) return;
    setForm({ driverId: "", effectiveDate: todayPlusDays(0), note: "", inspect: false });
    setErrors({});
    driversApi
      .list({ pageSize: 500 })
      .then((r) => setDrivers(r.items))
      .catch(() => setDrivers([]));
  }, [vehicle]);

  if (!vehicle) return null;
  const handoff = vehicle.mode === "handoff" && !!vehicle.currentDriver;
  const current = vehicle.currentDriver;
  const options = drivers
    .filter((d) => d.id !== current?.id && d.status !== "Suspended")
    .map((d) => ({
      value: d.id,
      label: `${d.fullName}${d.assignedVehicle ? ` — has ${d.assignedVehicle.plateNumber}` : ""}${d.status !== "Active" ? ` (${d.status})` : ""}`,
    }));

  const submit = async () => {
    if (!handoff && !form.driverId) {
      setErrors({ driverId: "Required" });
      toast("Select the driver to assign", "bad");
      return;
    }
    setSaving(true);
    try {
      const r = await vehiclesApi.handOff(vehicle.id, {
        driverId: form.driverId || null,
        effectiveDate: form.effectiveDate,
        note: form.note.trim() || null,
        performInspection: handoff && form.inspect,
        inspectorName: user ? `${user.firstName} ${user.lastName}`.trim() || user.userName : undefined,
      });
      const d = r.vehicle.currentDriver;
      toast(d ? `${d.fullName} now drives ${r.vehicle.name}` : `${r.vehicle.name} is back in the pool`);
      if (r.inspectionMessage) toast(r.inspectionMessage, r.inspectionId ? undefined : "bad");
      onSaved(r.inspectionId);
    } catch (err) {
      if (err instanceof ApiError && Object.keys(err.details).length) setErrors(err.details);
      toast(err instanceof Error ? err.message : "Could not save", "bad");
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
      open
      onClose={onClose}
      title={handoff ? "Hand off vehicle" : "Assign driver"}
      description={`${vehicle.name} · ${vehicle.plateNumber}`}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={submit} disabled={saving}>
            {saving ? "Saving…" : handoff ? "Hand off" : "Assign"}
          </Button>
        </>
      }
    >
      {current && (
        <div className="note" style={{ marginBottom: 14 }}>
          Currently with <b>{current.fullName}</b>
          {current.since ? ` since ${formatDay(current.since)}` : ""}. The current assignment closes on the effective date.
        </div>
      )}
      <div className="form-grid">
        <div className="f full">
          <label htmlFor="ho-driver">
            {handoff ? "New driver (optional)" : "Driver"}
            {!handoff && <span className="req"> *</span>}
          </label>
          <select id="ho-driver" value={form.driverId} onChange={(e) => (setForm((f) => ({ ...f, driverId: e.target.value })), setErrors({}))} style={bad("driverId")}>
            <option value="">{handoff ? "No new driver — leave unassigned" : "Select a driver"}</option>
            {options.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </select>
          {msg("driverId") ?? (handoff && <span className="hint">Leave empty to hand the vehicle back to the pool and assign someone later.</span>)}
        </div>
        <div className="f">
          <label htmlFor="ho-date">Effective from</label>
          <input id="ho-date" type="date" value={form.effectiveDate} onChange={(e) => setForm((f) => ({ ...f, effectiveDate: e.target.value }))} style={bad("effectiveDate")} />
          {msg("effectiveDate")}
        </div>
        <div className="f">
          <label htmlFor="ho-note">Note</label>
          <input id="ho-note" value={form.note} placeholder="Route, shift or reason" onChange={(e) => setForm((f) => ({ ...f, note: e.target.value }))} style={bad("note")} />
          {msg("note")}
        </div>
        {handoff && (
          <label className="f full ho-check">
            <input type="checkbox" checked={form.inspect} onChange={(e) => setForm((f) => ({ ...f, inspect: e.target.checked }))} />
            <span>
              <b>Perform a handoff inspection</b>
              <span className="hint">Starts the “Handoff inspection” checklist straight after the handoff. Optional.</span>
            </span>
          </label>
        )}
      </div>
    </Modal>
  );
}

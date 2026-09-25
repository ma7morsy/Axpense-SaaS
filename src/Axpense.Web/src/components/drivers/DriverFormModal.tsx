import { useEffect, useState, type FormEvent, type ReactNode } from "react";
import { Modal } from "../ui/Modal";
import { Button } from "../ui/Button";
import { useToast } from "../ui/Toast";
import { ApiError, driversApi } from "../../lib/api";
import { DRIVER_STATUSES, LICENSE_CLASSES, type DriverProfile, type DriverUpsert } from "../../lib/types";
import { todayPlusDays, toDateInput } from "../../lib/utils";

const REQUIRED: (keyof DriverUpsert)[] = ["fullName", "licenseNumber", "licenseClass", "licenseExpiryDate"];

function fromProfile(d: DriverProfile | null): DriverUpsert {
  return {
    fullName: d?.fullName ?? "",
    employeeNumber: d?.employeeNumber ?? "",
    status: d?.status ?? "Active",
    phone: d?.phone ?? "",
    email: d?.email ?? "",
    nationalId: d?.nationalId ?? "",
    hireDate: d ? toDateInput(d.hireDate) : todayPlusDays(0),
    licenseNumber: d?.licenseNumber ?? "",
    licenseClass: d?.licenseClass ?? LICENSE_CLASSES[0],
    licenseIssuedDate: toDateInput(d?.licenseIssuedDate),
    licenseExpiryDate: toDateInput(d?.licenseExpiryDate),
    rating: d ? String(d.rating) : "4",
  };
}

/** Add / edit driver. Pass `driver` to edit; null to create. */
export function DriverFormModal({
  open,
  driver,
  onClose,
  onSaved,
}: {
  open: boolean;
  driver: DriverProfile | null;
  onClose: () => void;
  onSaved: (saved: DriverProfile) => void;
}) {
  const toast = useToast();
  const [form, setForm] = useState<DriverUpsert>(fromProfile(driver));
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!open) return;
    setForm(fromProfile(driver));
    setErrors({});
    if (!driver) {
      driversApi
        .nextEmployeeNumber()
        .then((r) => setForm((f) => (f.employeeNumber ? f : { ...f, employeeNumber: r.employeeNumber })))
        .catch(() => undefined);
    }
  }, [open, driver]);

  const set = (k: keyof DriverUpsert) => (e: { target: { value: string } }) => {
    setForm((f) => ({ ...f, [k]: e.target.value }));
    if (errors[k]) setErrors(({ [k]: _removed, ...rest }) => rest);
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const missing: Record<string, string> = {};
    for (const k of REQUIRED) if (!String(form[k]).trim()) missing[k] = "Required";
    if (Object.keys(missing).length) {
      setErrors(missing);
      toast("Fill in the highlighted fields to continue", "bad");
      return;
    }

    const body = {
      ...form,
      email: form.email.trim() || null,
      nationalId: form.nationalId.trim() || null,
      hireDate: form.hireDate || null,
      licenseIssuedDate: form.licenseIssuedDate || null,
      licenseExpiryDate: form.licenseExpiryDate || null,
      rating: form.rating === "" ? null : Number(form.rating),
    };

    setSaving(true);
    try {
      const saved = driver ? await driversApi.update(driver.id, body) : await driversApi.create(body);
      toast(driver ? "Driver updated" : `${saved.fullName} added`);
      onSaved(saved);
    } catch (err) {
      if (err instanceof ApiError && Object.keys(err.details).length) setErrors(err.details);
      toast(err instanceof Error ? err.message : "Could not save the driver", "bad");
    } finally {
      setSaving(false);
    }
  };

  const field = (k: keyof DriverUpsert, label: string, input: ReactNode, opts: { req?: boolean; full?: boolean; hint?: string } = {}) => (
    <div className={`f ${opts.full ? "full" : ""}`}>
      <label htmlFor={`drv-${k}`}>
        {label}
        {opts.req && <span className="req"> *</span>}
      </label>
      {input}
      {errors[k] && errors[k] !== "Required" ? (
        <span className="hint" style={{ color: "var(--red)" }}>
          {errors[k]}
        </span>
      ) : (
        opts.hint && <span className="hint">{opts.hint}</span>
      )}
    </div>
  );
  const bad = (k: string) => (errors[k] ? { borderColor: "var(--red)" } : undefined);
  const text = (k: keyof DriverUpsert, type = "text", placeholder = "", extra: Record<string, string> = {}) => (
    <input id={`drv-${k}`} name={k} type={type} value={form[k]} placeholder={placeholder} onChange={set(k)} style={bad(k)} {...extra} />
  );
  const select = (k: keyof DriverUpsert, options: readonly string[]) => (
    <select id={`drv-${k}`} name={k} value={form[k]} onChange={set(k)} style={bad(k)}>
      {options.map((o) => (
        <option key={o}>{o}</option>
      ))}
    </select>
  );

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={driver ? `Edit ${driver.fullName}` : "Add driver"}
      size="wide"
      footer={
        <>
          <Button variant="secondary" type="button" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" form="driver-form" disabled={saving}>
            {saving ? "Saving…" : driver ? "Save changes" : "Add driver"}
          </Button>
        </>
      }
    >
      <form id="driver-form" onSubmit={submit} noValidate>
        <div className="fieldset">
          <div className="fs-t">Details</div>
          <div className="form-grid">
            {field("fullName", "Full name", text("fullName", "text", "", { autoFocus: "true" }), { req: true, full: true })}
            {field("employeeNumber", "Employee number", text("employeeNumber"))}
            {field("status", "Status", select("status", DRIVER_STATUSES))}
            {field("phone", "Phone", text("phone", "tel", "+20 100 000 0000"))}
            {field("email", "Email", text("email", "email"))}
            {field("nationalId", "National ID", text("nationalId"))}
            {field("hireDate", "Joined", text("hireDate", "date"))}
          </div>
        </div>
        <div className="fieldset">
          <div className="fs-t">Licence</div>
          <div className="form-grid">
            {field("licenseNumber", "Licence number", text("licenseNumber"), { req: true })}
            {field("licenseClass", "Licence class", select("licenseClass", LICENSE_CLASSES), { req: true })}
            {field("licenseIssuedDate", "Issued", text("licenseIssuedDate", "date"))}
            {field("licenseExpiryDate", "Expires", text("licenseExpiryDate", "date"), { req: true, hint: "Renewal reminder fires 30 days ahead" })}
            {field("rating", "Performance rating", text("rating", "number", "", { step: "0.1", min: "0", max: "5" }), { hint: "0 – 5" })}
          </div>
        </div>
      </form>
    </Modal>
  );
}

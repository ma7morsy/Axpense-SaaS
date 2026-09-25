import { useEffect, useRef, useState, type FormEvent, type ReactNode } from "react";
import { Camera } from "lucide-react";
import { Modal } from "../ui/Modal";
import { Button } from "../ui/Button";
import { useToast } from "../ui/Toast";
import { ApiError, vehiclesApi } from "../../lib/api";
import type { VehicleDetail } from "../../lib/types";
import { toDateInput } from "../../lib/utils";
import { PM_TRIGGER_LABELS, useVehicleOptions, useVehiclePhoto } from "./vehicleUi";

type Form = Record<string, string>;

const NUMBER_FIELDS = new Set([
  "modelYear", "currentOdometer", "tankCapacity", "pmIntervalKm", "pmIntervalDays", "pmIntervalHours", "pmLastServiceReading",
  "purchasePrice", "expectedResidualValue", "insuranceAnnualPremium",
]);
const REQUIRED = ["make", "model", "modelYear", "category", "ownerType", "vin", "plateNumber", "currentOdometer", "engineType", "fuelType"];

function fromVehicle(v: VehicleDetail | null): Form {
  const s = (x: unknown) => (x == null ? "" : String(x));
  return {
    make: s(v?.make), model: s(v?.model), modelYear: s(v?.modelYear ?? new Date().getFullYear()),
    category: v?.category ?? "Truck", ownerType: v?.ownerType ?? "Company-owned", ownerName: s(v?.ownerName),
    vin: s(v?.vin), plateNumber: s(v?.plateNumber), status: v?.status ?? "Active",
    currentOdometer: s(v?.currentOdometer), readingUnit: v?.readingUnit ?? "km", engineType: v?.engineType ?? "Mechanic",
    fuelType: v?.fuelType ?? "Diesel", tankCapacity: s(v?.tankCapacity),
    pmTrigger: v?.pmTrigger ?? "usage", pmIntervalKm: s(v?.pmIntervalKm), pmIntervalDays: s(v?.pmIntervalDays),
    pmIntervalHours: s(v?.pmIntervalHours), pmLastServiceReading: s(v?.pmLastServiceReading), pmLastServiceDate: toDateInput(v?.pmLastServiceDate),
    purchaseDate: toDateInput(v?.purchaseDate), purchasePrice: s(v?.purchasePrice), supplier: s(v?.supplier),
    warrantyUntil: toDateInput(v?.warrantyUntil), expectedResidualValue: s(v?.expectedResidualValue),
    insuranceProvider: s(v?.insuranceProvider), insurancePolicyNumber: s(v?.insurancePolicyNumber),
    insuranceRenewalDate: toDateInput(v?.insuranceRenewalDate), insuranceAnnualPremium: s(v?.insuranceAnnualPremium),
    registrationAuthority: s(v?.registrationAuthority), registrationNumber: s(v?.registrationNumber),
    registrationRenewalDate: toDateInput(v?.registrationRenewalDate), notes: s(v?.notes),
  };
}

/** Add / edit vehicle, sectioned like the fleet register: identity, usage, PM criteria, purchase, insurance, registration, photo. */
export function VehicleFormModal({
  open,
  vehicle,
  onClose,
  onSaved,
}: {
  open: boolean;
  vehicle: VehicleDetail | null;
  onClose: () => void;
  onSaved: (saved: VehicleDetail) => void;
}) {
  const toast = useToast();
  const options = useVehicleOptions();
  const [form, setForm] = useState<Form>(fromVehicle(vehicle));
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);
  const [photo, setPhoto] = useState<File | null>(null);
  const [preview, setPreview] = useState<string | null>(null);
  const [removePhoto, setRemovePhoto] = useState(false);
  const fileRef = useRef<HTMLInputElement>(null);
  const currentPhoto = useVehiclePhoto(vehicle?.id, !!vehicle?.hasPhoto && open);

  useEffect(() => {
    if (!open) return;
    setForm(fromVehicle(vehicle));
    setErrors({});
    setPhoto(null);
    setPreview(null);
    setRemovePhoto(false);
  }, [open, vehicle]);

  useEffect(() => () => void (preview && URL.revokeObjectURL(preview)), [preview]);

  const set = (k: string) => (e: { target: { value: string } }) => {
    const value = e.target.value;
    setForm((f) => {
      const next = { ...f, [k]: value };
      if (k === "engineType" && value === "Electric") next.fuelType = "Electric";
      if (k === "pmTrigger" && value === "hours") next.readingUnit = "hr";
      return next;
    });
    if (errors[k]) setErrors(({ [k]: _r, ...rest }) => rest);
  };

  const choosePhoto = (file: File | undefined) => {
    if (!file) return;
    if (file.size > 5 * 1024 * 1024) {
      setErrors((e) => ({ ...e, photo: "The photo must be 5 MB or smaller." }));
      return;
    }
    setPhoto(file);
    setRemovePhoto(false);
    setPreview(URL.createObjectURL(file));
    setErrors(({ photo: _r, ...rest }) => rest);
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const missing: Record<string, string> = {};
    for (const k of REQUIRED) if (!form[k]?.trim()) missing[k] = "Required";
    if (Object.keys(missing).length) {
      setErrors(missing);
      toast("Fill in the highlighted fields to continue", "bad");
      return;
    }
    const body: Record<string, unknown> = {};
    for (const [k, v] of Object.entries(form)) {
      const t = v.trim();
      body[k] = t === "" ? null : NUMBER_FIELDS.has(k) ? Number(t) : t;
    }
    setSaving(true);
    try {
      let saved = vehicle ? await vehiclesApi.update(vehicle.id, body) : await vehiclesApi.create(body);
      try {
        if (photo) await vehiclesApi.uploadPhoto(saved.id, photo);
        else if (removePhoto && vehicle?.hasPhoto) await vehiclesApi.removePhoto(saved.id);
        if (photo || removePhoto) saved = await vehiclesApi.get(saved.id);
      } catch (err) {
        toast(`Vehicle saved, but the photo was not: ${err instanceof Error ? err.message : "upload failed"}`, "bad");
      }
      toast(vehicle ? "Vehicle updated" : `${saved.name} added to the fleet`);
      onSaved(saved);
    } catch (err) {
      if (err instanceof ApiError && Object.keys(err.details).length) setErrors(err.details);
      toast(err instanceof Error ? err.message : "Could not save the vehicle", "bad");
    } finally {
      setSaving(false);
    }
  };

  const field = (k: string, label: string, input: ReactNode, opts: { req?: boolean; full?: boolean; hint?: string } = {}) => (
    <div className={`f ${opts.full ? "full" : ""}`}>
      <label htmlFor={`veh-${k}`}>
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
  const text = (k: string, type = "text", placeholder = "", extra: Record<string, string> = {}) => (
    <input id={`veh-${k}`} name={k} type={type} value={form[k]} placeholder={placeholder} onChange={set(k)} style={bad(k)} {...extra} />
  );
  const select = (k: string, list: readonly (string | { value: string; label: string })[] | undefined) => (
    <select id={`veh-${k}`} name={k} value={form[k]} onChange={set(k)} style={bad(k)}>
      {(list ?? [form[k]]).map((o) => {
        const opt = typeof o === "string" ? { value: o, label: o } : o;
        return (
          <option key={opt.value} value={opt.value}>
            {opt.label}
          </option>
        );
      })}
    </select>
  );
  const section = (title: string, children: ReactNode) => (
    <div className="fieldset">
      <div className="fs-t">{title}</div>
      <div className="form-grid">{children}</div>
    </div>
  );

  const unit = form.readingUnit === "hr" ? "hr" : "km";
  const shownPhoto = preview ?? (removePhoto ? null : currentPhoto);

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={vehicle ? `Edit ${vehicle.name}` : "Add vehicle"}
      description={vehicle ? vehicle.plateNumber : "Everything can be edited later from the vehicle profile."}
      size="wide"
      footer={
        <>
          <Button variant="secondary" type="button" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" form="vehicle-form" disabled={saving || !options}>
            {saving ? "Saving…" : vehicle ? "Save changes" : "Add vehicle"}
          </Button>
        </>
      }
    >
      <form id="vehicle-form" onSubmit={submit} noValidate>
        {section(
          "Identity",
          <>
            {field("make", "Make", text("make", "text", "Volvo", { autoFocus: "true" }), { req: true })}
            {field("model", "Model", text("model", "text", "FH16 460"), { req: true })}
            {field("modelYear", "Year", text("modelYear", "number"), { req: true })}
            {field("category", "Category", select("category", options?.categories), { req: true })}
            {field("ownerType", "Owner type", select("ownerType", options?.ownerTypes), { req: true })}
            {field("ownerName", "Owner name", text("ownerName", "text", "Nile Freight Co."), {
              req: form.ownerType !== "Company-owned",
            })}
            {field("vin", "VIN — vehicle identification number", text("vin", "text", "17 characters", { maxLength: "17" }), {
              req: true,
              full: true,
            })}
            {field("plateNumber", "Plate number", text("plateNumber", "text", "FLT 2214"), { req: true })}
            {field("status", "Status", select("status", options?.statuses))}
          </>
        )}
        {section(
          "Usage & power",
          <>
            {field("currentOdometer", "Odometer", text("currentOdometer", "number", "", { min: "0" }), {
              req: true,
              hint: vehicle ? "A change is logged as a reading" : undefined,
            })}
            {field("readingUnit", "Reading unit", select("readingUnit", [{ value: "km", label: "Kilometres" }, { value: "hr", label: "Engine hours" }]))}
            {field("engineType", "Engine type", select("engineType", options?.engineTypes), { req: true })}
            {field("fuelType", "Fuel type", select("fuelType", options?.fuelTypes), { req: true })}
            {field("tankCapacity", "Tank capacity", text("tankCapacity", "number", "", { step: "0.1", min: "0" }), { hint: "Litres, or kWh for electric" })}
          </>
        )}
        {section(
          "Preventive maintenance criteria",
          <>
            {field("pmTrigger", "Trigger type", select("pmTrigger", (options?.pmTriggers ?? ["usage"]).map((t) => ({ value: t, label: PM_TRIGGER_LABELS[t] ?? t }))))}
            {field("pmIntervalKm", "Distance interval (km)", text("pmIntervalKm", "number", "", { min: "1" }), {
              req: (form.pmTrigger === "usage" || form.pmTrigger === "hybrid") && unit === "km",
            })}
            {field("pmIntervalDays", "Time interval (days)", text("pmIntervalDays", "number", "", { min: "1" }), {
              req: form.pmTrigger === "time" || form.pmTrigger === "hybrid",
            })}
            {field("pmIntervalHours", "Engine hour interval", text("pmIntervalHours", "number", "", { min: "1" }), {
              req: form.pmTrigger === "hours" || (unit === "hr" && (form.pmTrigger === "usage" || form.pmTrigger === "hybrid")),
            })}
            {field("pmLastServiceReading", "Reading at last service", text("pmLastServiceReading", "number", "", { min: "0" }), {
              hint: "Defaults to the current odometer",
            })}
            {field("pmLastServiceDate", "Date of last service", text("pmLastServiceDate", "date"), { hint: "Defaults to today" })}
          </>
        )}
        {section(
          "Purchase",
          <>
            {field("purchaseDate", "Purchase date", text("purchaseDate", "date"))}
            {field("purchasePrice", "Purchase cost", text("purchasePrice", "number", "", { step: "0.01", min: "0" }))}
            {field("supplier", "Supplier", text("supplier"))}
            {field("warrantyUntil", "Warranty until", text("warrantyUntil", "date"))}
          </>
        )}
        {section(
          "Insurance",
          <>
            {field("insuranceProvider", "Provider", text("insuranceProvider"))}
            {field("insurancePolicyNumber", "Policy number", text("insurancePolicyNumber"))}
            {field("insuranceRenewalDate", "Renewal date", text("insuranceRenewalDate", "date"), {
              hint: `Reminder fires ${options?.renewalReminderDays ?? 30} days ahead`,
            })}
            {field("insuranceAnnualPremium", "Annual premium", text("insuranceAnnualPremium", "number", "", { step: "0.01", min: "0" }))}
          </>
        )}
        {section(
          "Licence & registration",
          <>
            {field("registrationAuthority", "Issuing authority", text("registrationAuthority"))}
            {field("registrationNumber", "Registration number", text("registrationNumber"))}
            {field("registrationRenewalDate", "Renewal date", text("registrationRenewalDate", "date"), {
              hint: `Reminder fires ${options?.renewalReminderDays ?? 30} days ahead`,
            })}
          </>
        )}
        <div className="fieldset">
          <div className="fs-t">Photo</div>
          <div className="form-grid">
            <div className="f full">
              <label>Vehicle image</label>
              <div
                className="drop"
                role="button"
                tabIndex={0}
                onClick={() => fileRef.current?.click()}
                onKeyDown={(e) => (e.key === "Enter" || e.key === " ") && fileRef.current?.click()}
                style={errors.photo ? { borderColor: "var(--red)" } : undefined}
              >
                {shownPhoto ? (
                  <img src={shownPhoto} alt="Preview" />
                ) : (
                  <div>
                    <Camera />
                    <div style={{ marginTop: 6 }}>Click to upload a photo</div>
                    <div className="hint">JPG, PNG or WEBP up to 5 MB — shown on the vehicle profile and list</div>
                  </div>
                )}
              </div>
              <input ref={fileRef} type="file" accept="image/jpeg,image/png,image/webp" hidden onChange={(e) => choosePhoto(e.target.files?.[0])} />
              {errors.photo ? (
                <span className="hint" style={{ color: "var(--red)" }}>
                  {errors.photo}
                </span>
              ) : (
                shownPhoto && (
                  <span className="hint">
                    <span
                      className="link"
                      onClick={() => {
                        setPhoto(null);
                        setPreview(null);
                        setRemovePhoto(true);
                      }}
                    >
                      Remove photo
                    </span>
                  </span>
                )
              )}
            </div>
          </div>
        </div>
      </form>
    </Modal>
  );
}

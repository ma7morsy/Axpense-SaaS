import { useEffect, useState } from "react";
import { ShieldCheck } from "lucide-react";
import { catalogApi, expenseTypesApi, vehiclesApi } from "../../lib/api";
import type { ExpenseType, PartCategory, VehicleDetail, VehicleExpense, VehicleIssue, VehiclePart } from "../../lib/types";
import type { WorkOrderPreset } from "../workorders/WorkOrderFormModal";
import { formatDay, todayPlusDays, toDateInput } from "../../lib/utils";
import { useToast } from "../ui/Toast";
import { RecordFormModal, type FormValues } from "./RecordFormModal";
import { num, useVehicleOptions } from "./vehicleUi";

const s = (v: unknown) => (v == null ? "" : String(v));

// =====================================================================
// Parts
// =====================================================================

function useCatalog(open: boolean) {
  const [categories, setCategories] = useState<PartCategory[]>([]);
  useEffect(() => {
    if (open) catalogApi.get().then((c) => setCategories(c.categories)).catch(() => setCategories([]));
  }, [open]);
  return categories;
}

export function PartFormModal({
  open,
  vehicle,
  part,
  onClose,
  onSaved,
}: {
  open: boolean;
  vehicle: VehicleDetail;
  part: VehiclePart | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const toast = useToast();
  const options = useVehicleOptions();
  const categories = useCatalog(open);
  const [catId, setCatId] = useState("");
  useEffect(() => {
    if (open) setCatId(part?.categoryId ?? categories[0]?.id ?? "");
  }, [open, part, categories]);
  const parts = categories.find((c) => c.id === catId)?.parts ?? [];
  const unit = vehicle.readingUnit;

  return (
    <RecordFormModal
      open={open && categories.length > 0}
      title={part ? "Edit part" : "Add part"}
      description={part ? `${part.code} · ${part.partName}` : vehicle.name}
      submitLabel={part ? "Save changes" : "Add part"}
      fields={[
        { name: "categoryId", label: "Part category", type: "select", options: categories.map((c) => ({ value: c.id, label: c.name })), req: true },
        {
          name: "presetPartId",
          label: "Part name",
          type: "select",
          options: parts.map((p) => ({ value: p.id, label: `${p.name}${p.nameAr ? ` — ${p.nameAr}` : ""}` })),
          blank: "Select a part",
          req: true,
          hint: "Drawn from the parts catalogue in Settings",
        },
        { name: "serial", label: "Serial number", placeholder: "BP-88231" },
        { name: "unitCost", label: "Unit cost", type: "number", step: "0.01" },
        { name: "lifespanKm", label: `Lifespan (${unit})`, type: "number", hint: "Counted from the activation reading" },
        { name: "lifespanMonths", label: "Lifespan (months)", type: "number", hint: "Use either distance or months" },
        { name: "installedReading", label: "Activation reading", type: "number", req: true, hint: "Odometer when the part went into service" },
        { name: "installedDate", label: "Activation date", type: "date", req: true },
        { name: "warrantyUntil", label: "Warranty valid until", type: "date", hint: "Failure before this date is replaced free" },
        { name: "warrantyKm", label: "Warranty distance cap", type: "number", hint: `Optional — cover also ends after this many ${unit}` },
        { name: "status", label: "Status", type: "select", options: options?.partStatuses ?? ["In service", "Needs attention", "Retired"] },
        { name: "notes", label: "Notes", type: "textarea" },
      ]}
      initial={{
        categoryId: part?.categoryId ?? categories[0]?.id ?? "",
        presetPartId: part?.presetPartId ?? "",
        serial: s(part?.serial),
        unitCost: s(part?.unitCost),
        lifespanKm: s(part?.lifespanKm),
        lifespanMonths: s(part?.lifespanMonths),
        installedReading: s(part?.installedReading ?? vehicle.currentOdometer ?? 0),
        installedDate: part ? toDateInput(part.installedDate) : todayPlusDays(0),
        warrantyUntil: toDateInput(part?.warrantyUntil),
        warrantyKm: s(part?.warrantyKm),
        status: part?.status ?? "In service",
        notes: s(part?.notes),
      }}
      onValuesChange={(next: FormValues, changed) => {
        if (changed === "categoryId") {
          setCatId(next.categoryId);
          return { ...next, presetPartId: "" };
        }
        return next;
      }}
      onClose={onClose}
      onSubmit={async (body) => {
        delete body.categoryId;
        if (part) await vehiclesApi.parts.update(vehicle.id, part.id, body);
        else await vehiclesApi.parts.add(vehicle.id, body);
        toast(part ? "Part updated" : "Part added");
        onSaved();
      }}
    />
  );
}

export function ReplacePartModal({
  vehicle,
  part,
  onClose,
  onSaved,
}: {
  vehicle: VehicleDetail;
  part: VehiclePart | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const toast = useToast();
  const w = part?.wear;
  const covered = !!w?.warranty.covered;
  const unit = vehicle.readingUnit;
  return (
    <RecordFormModal
      open={!!part}
      title="Replace part"
      description={part ? `${part.partName} · ${part.serial ?? part.code}` : ""}
      submitLabel="Retire and fit new"
      intro={
        part &&
        (covered ? (
          <div className="banner info" style={{ marginBottom: 16 }}>
            <ShieldCheck />
            <div>
              <div className="bt">Still under warranty — replacement is free</div>
              <div className="bd">
                {w!.warranty.label}. It has run {num(w!.distanceRun)} {unit} since activation on {formatDay(part.installedDate)}. Filing the claim sets the
                cost to zero and skips the expense entry.
              </div>
            </div>
          </div>
        ) : (
          <div className="note" style={{ marginBottom: 16 }}>
            The old part is marked <b>Retired</b> and kept in history. Wear: {w?.label}
            {w?.detail ? ` (${w.detail})` : ""}
            {w?.warranty.state !== "none" ? ` · warranty ${w?.warranty.label.toLowerCase()}` : ""}.
          </div>
        ))
      }
      fields={[
        ...(covered
          ? [
              {
                name: "claimWarranty",
                label: "Warranty claim",
                type: "select" as const,
                full: true,
                options: [
                  { value: "true", label: "Yes — free replacement under warranty" },
                  { value: "false", label: "No — charge this replacement" },
                ],
              },
            ]
          : []),
        { name: "serial", label: "New serial number", req: true, placeholder: "BP-99120" },
        { name: "unitCost", label: "Unit cost", type: "number", step: "0.01" },
        { name: "lifespanKm", label: `Lifespan (${unit})`, type: "number" },
        { name: "lifespanMonths", label: "Lifespan (months)", type: "number" },
        { name: "installedReading", label: "Activation reading", type: "number", req: true },
        { name: "installedDate", label: "Activation date", type: "date", req: true },
        { name: "warrantyUntil", label: "New warranty until", type: "date" },
        { name: "warrantyKm", label: "New warranty distance cap", type: "number" },
        {
          name: "raiseWorkOrder",
          label: "Follow-up",
          type: "select",
          full: true,
          options: [
            { value: "true", label: "Also schedule a corrective maintenance job" },
            { value: "false", label: "No maintenance job" },
          ],
        },
        {
          name: "recordExpense",
          label: "Log the cost",
          type: "select",
          full: true,
          options: [
            { value: "true", label: "Record as a Parts expense" },
            { value: "false", label: "Do not record" },
          ],
        },
      ]}
      initial={{
        claimWarranty: covered ? "true" : "false",
        serial: "",
        unitCost: covered ? "0" : s(part?.unitCost),
        lifespanKm: s(part?.lifespanKm),
        lifespanMonths: s(part?.lifespanMonths),
        installedReading: s(vehicle.currentOdometer ?? 0),
        installedDate: todayPlusDays(0),
        warrantyUntil: part?.warrantyUntil ? todayPlusDays(365) : "",
        warrantyKm: s(part?.warrantyKm),
        raiseWorkOrder: "true",
        recordExpense: covered ? "false" : "true",
      }}
      onClose={onClose}
      onSubmit={async (body) => {
        for (const k of ["claimWarranty", "raiseWorkOrder", "recordExpense"]) body[k] = body[k] === "true";
        const r = await vehiclesApi.parts.replace(vehicle.id, part!.id, body);
        toast(r.warrantyClaimed ? `${part!.partName} replaced under warranty` : `${part!.partName} replaced — ${r.fitted.code} fitted`);
        onSaved();
      }}
    />
  );
}

// =====================================================================
// Issues
// =====================================================================

export function IssueFormModal({ open, vehicle, onClose, onSaved }: { open: boolean; vehicle: VehicleDetail; onClose: () => void; onSaved: () => void }) {
  const toast = useToast();
  const options = useVehicleOptions();
  const categories = useCatalog(open);
  const partOptions = categories.flatMap((c) => c.parts.map((p) => ({ value: p.id, label: `${c.name} — ${p.name}` })));
  return (
    <RecordFormModal
      open={open}
      title="Report issue"
      description={vehicle.name}
      submitLabel="Report issue"
      fields={[
        { name: "title", label: "Issue", req: true, full: true, placeholder: "Brake judder under load" },
        { name: "presetPartId", label: "Part concerned", type: "select", options: partOptions, blank: "None / not sure", full: true },
        { name: "priority", label: "Priority", type: "select", options: options?.issuePriorities ?? ["Low", "Medium", "High"] },
        { name: "source", label: "Source", type: "select", options: options?.issueSources ?? ["Manual report"] },
        { name: "reportedDate", label: "Reported on", type: "date", req: true },
        { name: "note", label: "Note", type: "textarea" },
      ]}
      initial={{ title: "", presetPartId: "", priority: "Medium", source: "Manual report", reportedDate: todayPlusDays(0), note: "" }}
      onClose={onClose}
      onSubmit={async (body) => {
        await vehiclesApi.issues.add(vehicle.id, body);
        toast("Issue reported");
        onSaved();
      }}
    />
  );
}

export function issueToWorkOrder(i: VehicleIssue, vehicleId: string): WorkOrderPreset {
  return {
    vehicleId, type: "Corrective", priority: i.priority === "High" ? "High" : "Medium", description: i.title, issueId: i.id,
    tasks: [{ description: `Fix: ${i.title}` }],
  };
}

// =====================================================================
// Fuel & readings
// =====================================================================

export function FuelFormModal({ open, vehicle, onClose, onSaved }: { open: boolean; vehicle: VehicleDetail; onClose: () => void; onSaved: () => void }) {
  const toast = useToast();
  return (
    <RecordFormModal
      open={open}
      title="Add fuel entry"
      description={vehicle.name}
      submitLabel="Add entry"
      intro={
        <div className="note" style={{ marginBottom: 14 }}>
          Fuel counts toward spend on its own, so it is not duplicated as an expense. A higher odometer is logged as a reading.
        </div>
      }
      fields={[
        { name: "date", label: "Date", type: "date", req: true },
        { name: "station", label: "Station or depot", req: true, placeholder: "Cairo depot" },
        { name: "quantity", label: `Quantity (${vehicle.tankUnit})`, type: "number", step: "0.01", req: true },
        { name: "totalCost", label: "Total cost", type: "number", step: "0.01", req: true },
        { name: "odometer", label: `Odometer reading (${vehicle.readingUnit})`, type: "number", req: true },
      ]}
      initial={{ date: todayPlusDays(0), station: "", quantity: "", totalCost: "", odometer: s(vehicle.currentOdometer) }}
      onClose={onClose}
      onSubmit={async (body) => {
        await vehiclesApi.fuel.add(vehicle.id, body);
        toast("Fuel entry added");
        onSaved();
      }}
    />
  );
}

export function ReadingFormModal({ open, vehicle, onClose, onSaved }: { open: boolean; vehicle: VehicleDetail; onClose: () => void; onSaved: () => void }) {
  const toast = useToast();
  const options = useVehicleOptions();
  return (
    <RecordFormModal
      open={open}
      title="Add odometer reading"
      description={vehicle.name}
      submitLabel="Add reading"
      fields={[
        { name: "date", label: "Date", type: "date", req: true },
        { name: "value", label: `Reading (${vehicle.readingUnit})`, type: "number", req: true, hint: "Usage-based PM triggers recalculate immediately" },
        { name: "source", label: "Source", type: "select", options: options?.readingSources ?? ["Manual reading"] },
        { name: "recordedBy", label: "Recorded by", placeholder: "Defaults to you" },
      ]}
      initial={{ date: todayPlusDays(0), value: s(vehicle.currentOdometer), source: "Manual reading", recordedBy: "" }}
      onClose={onClose}
      onSubmit={async (body) => {
        await vehiclesApi.readings.add(vehicle.id, body);
        const fresh = await vehiclesApi.get(vehicle.id);
        toast(fresh.pm.status === "ok" || fresh.pm.status === "none" ? "Reading logged" : `Reading logged — ${fresh.pm.label.toLowerCase()}`, fresh.pm.status === "overdue" ? "bad" : undefined);
        onSaved();
      }}
    />
  );
}

// =====================================================================
// Expenses
// =====================================================================

export function ExpenseFormModal({
  open,
  vehicle,
  expense,
  onClose,
  onSaved,
}: {
  open: boolean;
  vehicle: VehicleDetail;
  expense: VehicleExpense | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const toast = useToast();
  const [types, setTypes] = useState<ExpenseType[]>([]);
  useEffect(() => {
    if (open) expenseTypesApi.list().then(setTypes).catch(() => setTypes([]));
  }, [open]);
  return (
    <RecordFormModal
      open={open && types.length > 0}
      title={expense ? "Edit expense" : "Add expense"}
      description={vehicle.name}
      submitLabel={expense ? "Save changes" : "Add expense"}
      fields={[
        { name: "title", label: "Title", req: true, full: true, placeholder: "Toll — Cairo/Suez" },
        { name: "expenseTypeId", label: "Expense type", type: "select", options: types.map((t) => ({ value: t.id, label: t.name })), req: true, hint: "Types are configured in Settings" },
        { name: "amount", label: "Amount", type: "number", step: "0.01", req: true },
        { name: "date", label: "Issue date", type: "date", req: true },
        { name: "note", label: "Note", type: "textarea", placeholder: "Invoice number, supplier" },
      ]}
      initial={{
        title: s(expense?.title),
        expenseTypeId: expense?.expenseTypeId ?? types.find((t) => t.name === "Other")?.id ?? types[0]?.id ?? "",
        amount: s(expense?.amount),
        date: expense ? toDateInput(expense.date) : todayPlusDays(0),
        note: s(expense?.note),
      }}
      onClose={onClose}
      onSubmit={async (body) => {
        if (expense) await vehiclesApi.expenses.update(vehicle.id, expense.id, body);
        else await vehiclesApi.expenses.add(vehicle.id, body);
        toast(expense ? "Expense updated" : "Expense added");
        onSaved();
      }}
    />
  );
}

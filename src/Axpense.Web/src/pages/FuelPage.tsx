import { useEffect, useState, type FormEvent } from "react";
import { Fuel as FuelIcon, Plus } from "lucide-react";
import { PageHeader } from "../components/ui/PageHeader";
import { Button } from "../components/ui/Button";
import { Card } from "../components/ui/Card";
import { Table, Tbody, Td, Th, Thead, Tr } from "../components/ui/Table";
import { Modal } from "../components/ui/Modal";
import { Input, Label, Select } from "../components/ui/Field";
import { EmptyState } from "../components/ui/EmptyState";
import { PageSpinner } from "../components/ui/Spinner";
import { fuelApi, vehiclesApi } from "../lib/api";
import type { FuelTransaction, Vehicle } from "../lib/types";
import { formatDate, formatMoney } from "../lib/utils";

export default function FuelPage() {
  const [list, setList] = useState<FuelTransaction[]>([]);
  const [vehicles, setVehicles] = useState<Vehicle[]>([]);
  const [loading, setLoading] = useState(true);
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState({ vehicleId: "", quantityLiters: "", unitPrice: "", transactionDate: new Date().toISOString().slice(0, 10), fuelType: "Diesel", station: "" });

  const vehicleName = (id: string) => vehicles.find((v) => v.id === id)?.plateNumber ?? id.slice(0, 8);

  const load = () =>
    Promise.all([fuelApi.list(), vehiclesApi.list()]).then(([f, v]) => {
      setList(f);
      setVehicles(v);
    }).finally(() => setLoading(false));

  useEffect(() => {
    load();
  }, []);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      await fuelApi.create({ ...form, quantityLiters: Number(form.quantityLiters), unitPrice: Number(form.unitPrice) });
      setOpen(false);
      setForm({ vehicleId: "", quantityLiters: "", unitPrice: "", transactionDate: new Date().toISOString().slice(0, 10), fuelType: "Diesel", station: "" });
      load();
    } finally {
      setSaving(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="Fuel"
        subtitle="Record fuel consumption, prices and odometer readings."
        action={
          <Button onClick={() => setOpen(true)}>
            <Plus className="h-4 w-4" /> Add Fuel
          </Button>
        }
      />

      <Card>
        {loading ? (
          <PageSpinner />
        ) : list.length === 0 ? (
          <EmptyState icon={FuelIcon} title="No fuel transactions yet" />
        ) : (
          <Table>
            <Thead>
              <Tr>
                <Th>Date</Th>
                <Th>Vehicle</Th>
                <Th>Liters</Th>
                <Th>Unit Price</Th>
                <Th>Total</Th>
                <Th>Station</Th>
              </Tr>
            </Thead>
            <Tbody>
              {list.map((x) => (
                <Tr key={x.id}>
                  <Td>{formatDate(x.transactionDate)}</Td>
                  <Td className="font-semibold text-ink-900">{vehicleName(x.vehicleId)}</Td>
                  <Td className="tabular">{x.quantityLiters} L</Td>
                  <Td className="tabular">{formatMoney(x.unitPrice)}</Td>
                  <Td className="tabular font-semibold">{formatMoney(x.totalAmount)}</Td>
                  <Td>{x.station || "—"}</Td>
                </Tr>
              ))}
            </Tbody>
          </Table>
        )}
      </Card>

      <Modal open={open} onClose={() => setOpen(false)} title="Add Fuel Transaction">
        <form onSubmit={submit} className="space-y-4">
          <div>
            <Label>Vehicle</Label>
            <Select required value={form.vehicleId} onChange={(e) => setForm({ ...form, vehicleId: e.target.value })}>
              <option value="">Select vehicle</option>
              {vehicles.map((v) => (
                <option key={v.id} value={v.id}>
                  {v.plateNumber} · {v.make} {v.model}
                </option>
              ))}
            </Select>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <Label>Liters</Label>
              <Input required type="number" min="0.01" step="0.01" value={form.quantityLiters} onChange={(e) => setForm({ ...form, quantityLiters: e.target.value })} />
            </div>
            <div>
              <Label>Unit price</Label>
              <Input required type="number" min="0.01" step="0.01" value={form.unitPrice} onChange={(e) => setForm({ ...form, unitPrice: e.target.value })} />
            </div>
            <div>
              <Label>Date</Label>
              <Input type="date" value={form.transactionDate} onChange={(e) => setForm({ ...form, transactionDate: e.target.value })} />
            </div>
            <div>
              <Label>Fuel type</Label>
              <Select value={form.fuelType} onChange={(e) => setForm({ ...form, fuelType: e.target.value })}>
                <option>Diesel</option>
                <option>Petrol</option>
                <option>Electric</option>
              </Select>
            </div>
            <div className="col-span-2">
              <Label>Station</Label>
              <Input value={form.station} onChange={(e) => setForm({ ...form, station: e.target.value })} />
            </div>
          </div>
          <div className="flex justify-end gap-2 pt-1">
            <Button type="button" variant="secondary" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={saving}>
              {saving ? "Saving…" : "Save Fuel"}
            </Button>
          </div>
        </form>
      </Modal>
    </div>
  );
}

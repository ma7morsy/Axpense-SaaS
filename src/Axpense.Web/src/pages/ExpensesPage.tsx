import { useEffect, useState, type FormEvent } from "react";
import { Plus, Wallet } from "lucide-react";
import { PageHeader } from "../components/ui/PageHeader";
import { Button } from "../components/ui/Button";
import { Card } from "../components/ui/Card";
import { Table, Tbody, Td, Th, Thead, Tr } from "../components/ui/Table";
import { Modal } from "../components/ui/Modal";
import { Input, Label, Select } from "../components/ui/Field";
import { EmptyState } from "../components/ui/EmptyState";
import { PageSpinner } from "../components/ui/Spinner";
import { expenseTypesApi, expensesApi } from "../lib/api";
import type { Expense } from "../lib/types";
import { formatDate, formatMoney } from "../lib/utils";


export default function ExpensesPage() {
  const [list, setList] = useState<Expense[]>([]);
  const [loading, setLoading] = useState(true);
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState({ category: "Other", description: "", amount: "", expenseDate: new Date().toISOString().slice(0, 10), vendor: "" });

  const [categories, setCategories] = useState<string[]>(["Other"]);
  const load = () => expensesApi.list().then(setList).finally(() => setLoading(false));
  // Categories come from Settings → Expense types.
  useEffect(() => {
    expenseTypesApi
      .list()
      .then((t) => setCategories(t.map((x) => x.name)))
      .catch(() => undefined);
  }, []);
  useEffect(() => {
    load();
  }, []);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      await expensesApi.create({ ...form, amount: Number(form.amount) });
      setOpen(false);
      setForm({ category: "Other", description: "", amount: "", expenseDate: new Date().toISOString().slice(0, 10), vendor: "" });
      load();
    } finally {
      setSaving(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="Expenses"
        subtitle="Track operating costs by category and vehicle."
        action={
          <Button onClick={() => setOpen(true)}>
            <Plus className="h-4 w-4" /> Add Expense
          </Button>
        }
      />

      <Card>
        {loading ? (
          <PageSpinner />
        ) : list.length === 0 ? (
          <EmptyState icon={Wallet} title="No expenses yet" />
        ) : (
          <Table>
            <Thead>
              <Tr>
                <Th>Date</Th>
                <Th>Category</Th>
                <Th>Description</Th>
                <Th>Vendor</Th>
                <Th>Amount</Th>
              </Tr>
            </Thead>
            <Tbody>
              {list.map((x) => (
                <Tr key={x.id}>
                  <Td>{formatDate(x.expenseDate)}</Td>
                  <Td className="font-semibold text-ink-900">{x.category}</Td>
                  <Td>{x.description}</Td>
                  <Td>{x.vendor || "—"}</Td>
                  <Td className="tabular font-semibold">{formatMoney(x.amount)}</Td>
                </Tr>
              ))}
            </Tbody>
          </Table>
        )}
      </Card>

      <Modal open={open} onClose={() => setOpen(false)} title="Add Expense">
        <form onSubmit={submit} className="space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div>
              <Label>Category</Label>
              <Select value={form.category} onChange={(e) => setForm({ ...form, category: e.target.value })}>
                {categories.map((c) => (
                  <option key={c}>{c}</option>
                ))}
              </Select>
            </div>
            <div>
              <Label>Amount</Label>
              <Input required type="number" min="0.01" step="0.01" value={form.amount} onChange={(e) => setForm({ ...form, amount: e.target.value })} />
            </div>
            <div>
              <Label>Date</Label>
              <Input type="date" value={form.expenseDate} onChange={(e) => setForm({ ...form, expenseDate: e.target.value })} />
            </div>
            <div>
              <Label>Vendor</Label>
              <Input value={form.vendor} onChange={(e) => setForm({ ...form, vendor: e.target.value })} />
            </div>
            <div className="col-span-2">
              <Label>Description</Label>
              <Input required value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
            </div>
          </div>
          <div className="flex justify-end gap-2 pt-1">
            <Button type="button" variant="secondary" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={saving}>
              {saving ? "Saving…" : "Save Expense"}
            </Button>
          </div>
        </form>
      </Modal>
    </div>
  );
}

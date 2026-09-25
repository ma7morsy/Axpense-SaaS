import { useState } from "react";
import { Pencil, Plus, Receipt, Trash2 } from "lucide-react";
import { Button } from "../ui/Button";
import { RowMenu, type RowMenuItem } from "../ui/RowMenu";
import { ConfirmDialog } from "../ui/ConfirmDialog";
import { useToast } from "../ui/Toast";
import { expenseTypesApi } from "../../lib/api";
import type { ExpenseType } from "../../lib/types";
import { formatMoney } from "../../lib/utils";
import { SimpleNameModal } from "./SimpleNameModal";
import "./settings.css";

export function ExpenseTypesTab({ types, isAdmin, onChanged }: { types: ExpenseType[]; isAdmin: boolean; onChanged: () => Promise<unknown> }) {
  const toast = useToast();
  const [form, setForm] = useState<{ type: ExpenseType | null } | null>(null);
  const [removing, setRemoving] = useState<ExpenseType | null>(null);

  const remove = async () => {
    if (!removing) return;
    try {
      await expenseTypesApi.remove(removing.id);
      toast(`${removing.name} deleted`);
      setRemoving(null);
      await onChanged();
    } catch (err) {
      toast(err instanceof Error ? err.message : "Could not delete", "bad");
      setRemoving(null);
    }
  };

  return (
    <div className="card">
      <div className="card-h">
        <div>
          <h2>Expense types</h2>
          <p className="t-sub">Categories used by expenses, cost reports and the budget allocation.</p>
        </div>
        {isAdmin && (
          <Button onClick={() => setForm({ type: null })}>
            <Plus /> Add type
          </Button>
        )}
      </div>
      <div className="tbl-wrap">
        <table className="st-table">
          <thead>
            <tr>
              <th>Type</th>
              <th className="num">Entries</th>
              <th className="num">Total</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {types.length ? (
              types.map((t) => {
                const items: RowMenuItem[] = [{ label: "Edit type", icon: Pencil, onSelect: () => setForm({ type: t }) }];
                if (!t.isSystem) items.push({ separator: true }, { label: "Delete type", icon: Trash2, danger: true, onSelect: () => setRemoving(t) });
                return (
                  <tr key={t.id}>
                    <td>
                      <div className="flex">
                        <i className="dotc" style={{ background: t.color }} />
                        <span className="t-main">{t.name}</span>
                        {t.isSystem && <span className="sys-tag" title="System type — can be renamed, not deleted">System</span>}
                      </div>
                    </td>
                    <td className="num mono">{t.entries.toLocaleString("en-US")}</td>
                    <td className="num mono">{formatMoney(t.total)}</td>
                    <td>{isAdmin && <RowMenu items={items} />}</td>
                  </tr>
                );
              })
            ) : (
              <tr>
                <td colSpan={4}>
                  <div className="empty">
                    <Receipt />
                    <h3>No expense types</h3>
                  </div>
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <SimpleNameModal
        open={!!form}
        title={form?.type ? `Edit ${form.type.name}` : "Add expense type"}
        description={form?.type?.isSystem ? "System type — renaming keeps its fuel/maintenance link." : undefined}
        label="Type name"
        placeholder="Tolls & fines"
        submitLabel={form?.type ? "Save changes" : "Add type"}
        initialName={form?.type?.name}
        initialColor={form?.type?.color}
        withColor
        onClose={() => setForm(null)}
        onSubmit={async (v) => {
          if (form?.type) {
            await expenseTypesApi.update(form.type.id, v);
            toast("Expense type updated");
          } else {
            const t = await expenseTypesApi.create(v);
            toast(`${t.name} added`);
          }
          setForm(null);
          await onChanged();
        }}
      />
      <ConfirmDialog
        open={!!removing}
        title="Delete expense type"
        message={`${removing?.name ?? "This type"} will be removed, along with its share in any budget.`}
        onConfirm={remove}
        onClose={() => setRemoving(null)}
      />
    </div>
  );
}

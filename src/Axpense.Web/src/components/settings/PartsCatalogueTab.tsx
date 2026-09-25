import { useMemo, useState } from "react";
import { Boxes, Pencil, Plus, Search, ShieldCheck, Trash2 } from "lucide-react";
import { Button } from "../ui/Button";
import { RowMenu } from "../ui/RowMenu";
import { ConfirmDialog } from "../ui/ConfirmDialog";
import { useToast } from "../ui/Toast";
import { catalogApi } from "../../lib/api";
import type { PartCategory, PartsCatalog, PresetPart } from "../../lib/types";
import { NameFormModal } from "./NameFormModal";
import "./settings.css";

type CategoryForm = { mode: "add" } | { mode: "edit"; category: PartCategory };
type PartForm = { mode: "add"; category: PartCategory } | { mode: "edit"; part: PresetPart; category: PartCategory };
type Removing = { kind: "category"; category: PartCategory } | { kind: "part"; part: PresetPart };

/**
 * Parts catalogue as master / detail: groups on the left, the selected group's parts on the
 * right, and one search across every part — no long page of stacked group cards.
 */
export function PartsCatalogueTab({
  catalog,
  isAdmin,
  onChanged,
}: {
  catalog: PartsCatalog;
  isAdmin: boolean;
  onChanged: () => Promise<unknown>;
}) {
  const toast = useToast();
  const [selectedId, setSelectedId] = useState<string | null>(catalog.categories[0]?.id ?? null);
  const [q, setQ] = useState("");
  const [categoryForm, setCategoryForm] = useState<CategoryForm | null>(null);
  const [partForm, setPartForm] = useState<PartForm | null>(null);
  const [removing, setRemoving] = useState<Removing | null>(null);

  const categories = catalog.categories;
  const selected = categories.find((c) => c.id === selectedId) ?? categories[0] ?? null;
  const term = q.trim().toLowerCase();

  const results = useMemo(() => {
    if (!term) return [];
    return categories.flatMap((c) =>
      c.parts
        .filter((p) => `${p.code} ${p.name} ${p.nameAr ?? ""}`.toLowerCase().includes(term))
        .map((p) => ({ part: p, category: c }))
    );
  }, [categories, term]);

  const confirmRemove = async () => {
    if (!removing) return;
    try {
      if (removing.kind === "category") {
        await catalogApi.removeCategory(removing.category.id);
        toast(`${removing.category.name} deleted`);
        if (selectedId === removing.category.id) setSelectedId(null);
      } else {
        await catalogApi.removePart(removing.part.id);
        toast(`${removing.part.name} removed`);
      }
      setRemoving(null);
      await onChanged();
    } catch (err) {
      toast(err instanceof Error ? err.message : "Could not delete", "bad");
    }
  };

  const partRow = (p: PresetPart, category: PartCategory, showGroup: boolean) => (
    <tr key={p.id}>
      <td className="code">{p.code}</td>
      <td className="t-main">{p.name}</td>
      {showGroup && <td className="grp">{category.name}</td>}
      <td className="ar" dir="rtl" lang="ar">
        {p.nameAr ?? ""}
      </td>
      <td>
        {isAdmin && (
          <RowMenu
            items={[
              { label: "Edit part", icon: Pencil, onSelect: () => setPartForm({ mode: "edit", part: p, category }) },
              { separator: true },
              { label: "Remove part", icon: Trash2, danger: true, onSelect: () => setRemoving({ kind: "part", part: p }) },
            ]}
          />
        )}
      </td>
    </tr>
  );

  return (
    <>
      <div className="pcat-intro">
        <ShieldCheck />
        <div>
          <b>One catalogue, used everywhere</b>
          <span>Categories and parts defined here appear in vehicle parts, inspection templates, work order tasks and issues.</span>
        </div>
        {isAdmin && (
          <Button variant="secondary" onClick={() => setCategoryForm({ mode: "add" })}>
            <Plus /> Add category
          </Button>
        )}
      </div>

      <div className="pcat">
        <aside className="pcat-side">
          <div className="pcat-search">
            <Search />
            <input placeholder={`Search ${catalog.totalParts} parts…`} value={q} onChange={(e) => setQ(e.target.value)} aria-label="Search parts" />
          </div>
          <div className="pcat-cats" role="listbox" aria-label="Part categories">
            {categories.map((c) => (
              <div
                key={c.id}
                role="option"
                aria-selected={!term && selected?.id === c.id}
                tabIndex={0}
                className={`pcat-cat ${!term && selected?.id === c.id ? "on" : ""}`}
                onClick={() => {
                  setSelectedId(c.id);
                  setQ("");
                }}
                onKeyDown={(e) => e.key === "Enter" && (setSelectedId(c.id), setQ(""))}
              >
                <span className="code">{c.code}</span>
                <span className="nm">
                  <b>{c.name}</b>
                  {c.nameAr && (
                    <span dir="rtl" lang="ar" style={{ textAlign: "left" }}>
                      {c.nameAr}
                    </span>
                  )}
                </span>
                <span className="n">{c.parts.length}</span>
                {isAdmin && (
                  <RowMenu
                    label={`${c.name} actions`}
                    items={[
                      { label: "Add part", icon: Plus, onSelect: () => setPartForm({ mode: "add", category: c }) },
                      { label: "Edit category", icon: Pencil, onSelect: () => setCategoryForm({ mode: "edit", category: c }) },
                      { separator: true },
                      { label: "Delete category", icon: Trash2, danger: true, onSelect: () => setRemoving({ kind: "category", category: c }) },
                    ]}
                  />
                )}
              </div>
            ))}
            {!categories.length && (
              <div className="empty" style={{ padding: "30px 10px" }}>
                <h3>No categories</h3>
                <p>Add the first group of parts.</p>
              </div>
            )}
          </div>
        </aside>

        <section className="pcat-main">
          {term ? (
            <>
              <div className="pcat-head">
                <div>
                  <h2>Search results</h2>
                  <div className="ar">
                    {results.length} part{results.length === 1 ? "" : "s"} match “{q.trim()}”
                  </div>
                </div>
              </div>
              <div className="pcat-body">
                {results.length ? (
                  <table>
                    <tbody>{results.map(({ part, category }) => partRow(part, category, true))}</tbody>
                  </table>
                ) : (
                  <div className="empty">
                    <Search />
                    <h3>No parts match</h3>
                    <p>Try another name, Arabic name or code.</p>
                  </div>
                )}
              </div>
            </>
          ) : selected ? (
            <>
              <div className="pcat-head">
                <div>
                  <h2>{selected.name}</h2>
                  {selected.nameAr && (
                    <div className="ar" dir="rtl" lang="ar" style={{ textAlign: "left" }}>
                      {selected.nameAr}
                    </div>
                  )}
                </div>
                {isAdmin && (
                  <Button variant="secondary" size="sm" onClick={() => setPartForm({ mode: "add", category: selected })}>
                    <Plus /> Add part
                  </Button>
                )}
              </div>
              <div className="pcat-body">
                {selected.parts.length ? (
                  <table>
                    <tbody>{selected.parts.map((p) => partRow(p, selected, false))}</tbody>
                  </table>
                ) : (
                  <div className="empty">
                    <Boxes />
                    <h3>No parts in {selected.name}</h3>
                    <p>{isAdmin ? "Add the parts that belong to this group." : "An administrator can add parts here."}</p>
                  </div>
                )}
              </div>
            </>
          ) : (
            <div className="empty">
              <Boxes />
              <h3>Select a category</h3>
            </div>
          )}
        </section>
      </div>

      <NameFormModal
        open={!!categoryForm}
        title={categoryForm?.mode === "edit" ? `Edit ${categoryForm.category.name}` : "Add category"}
        description="A group of parts, e.g. Engine Group or Braking System."
        nameLabel="Category name"
        namePlaceholder="Braking System"
        arPlaceholder="نظام الفرامل"
        submitLabel={categoryForm?.mode === "edit" ? "Save changes" : "Add category"}
        initial={categoryForm?.mode === "edit" ? categoryForm.category : undefined}
        onClose={() => setCategoryForm(null)}
        onSubmit={async (v) => {
          if (categoryForm?.mode === "edit") {
            await catalogApi.updateCategory(categoryForm.category.id, v);
            toast("Category updated");
          } else {
            const c = await catalogApi.createCategory(v);
            setSelectedId(c.id);
            setQ("");
            toast(`${c.name} added`);
          }
          setCategoryForm(null);
          await onChanged();
        }}
      />

      <NameFormModal
        open={!!partForm}
        title={partForm?.mode === "edit" ? `Edit ${partForm.part.name}` : "Add preset part"}
        description={partForm?.category.name}
        nameLabel="Part name"
        namePlaceholder="Brake Pads"
        arPlaceholder="تيل الفرامل"
        submitLabel={partForm?.mode === "edit" ? "Save changes" : "Add part"}
        initial={partForm?.mode === "edit" ? partForm.part : undefined}
        onClose={() => setPartForm(null)}
        onSubmit={async (v) => {
          if (partForm?.mode === "edit") {
            await catalogApi.updatePart(partForm.part.id, v);
            toast("Part updated");
          } else if (partForm) {
            const p = await catalogApi.createPart(partForm.category.id, v);
            toast(`${p.name} added as ${p.code}`);
          }
          setPartForm(null);
          await onChanged();
        }}
      />

      <ConfirmDialog
        open={!!removing}
        title={removing?.kind === "category" ? "Delete category" : "Remove part"}
        message={
          removing?.kind === "category"
            ? `${removing.category.name} and its ${removing.category.parts.length} part${removing.category.parts.length === 1 ? "" : "s"} will be removed from the catalogue.`
            : `${removing?.kind === "part" ? removing.part.name : "This part"} will be removed from the catalogue.`
        }
        confirmLabel={removing?.kind === "category" ? "Delete" : "Remove"}
        onConfirm={confirmRemove}
        onClose={() => setRemoving(null)}
      />
    </>
  );
}

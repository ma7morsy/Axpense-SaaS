import { useCallback, useEffect, useState } from "react";
import { PageHeader } from "../../components/ui/PageHeader";
import { PageSpinner } from "../../components/ui/Spinner";
import { Button } from "../../components/ui/Button";
import { PartsCatalogueTab } from "../../components/settings/PartsCatalogueTab";
import { ExpenseTypesTab } from "../../components/settings/ExpenseTypesTab";
import { MaintenanceTab } from "../../components/settings/MaintenanceTab";
import { BudgetTab } from "../../components/settings/BudgetTab";
import { budgetApi, catalogApi, expenseTypesApi, pmEngineApi, taskCategoriesApi } from "../../lib/api";
import type { AnnualBudget, ExpenseType, PartsCatalog, PmEngine, TaskCategory } from "../../lib/types";
import { formatMoneyCompact } from "../../lib/utils";
import { useIsAdmin } from "./useIsAdmin";

type Tab = "parts" | "expenses" | "maintenance" | "budget";

function useLoader<T>(fetcher: () => Promise<T>) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState(false);
  const load = useCallback(
    () =>
      fetcher()
        .then((d) => {
          setData(d);
          setError(false);
        })
        .catch(() => setError(true)),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    []
  );
  useEffect(() => {
    load();
  }, [load]);
  return { data, error, load };
}

export default function SettingsPage() {
  const { isAdmin } = useIsAdmin();
  const [tab, setTab] = useState<Tab>(() => {
    const t = new URLSearchParams(window.location.search).get("tab");
    return t === "expenses" || t === "maintenance" || t === "budget" ? t : "parts";
  });
  const catalog = useLoader<PartsCatalog>(catalogApi.get);
  const types = useLoader<ExpenseType[]>(expenseTypesApi.list);
  const taskCats = useLoader<TaskCategory[]>(taskCategoriesApi.list);
  const engine = useLoader<PmEngine>(pmEngineApi.get);
  const [budget, setBudget] = useState<AnnualBudget | null>(null);
  useEffect(() => {
    budgetApi.get(new Date().getFullYear()).then(setBudget).catch(() => undefined);
  }, []);

  const tabs: { key: Tab; label: string; count?: string | number }[] = [
    { key: "parts", label: "Parts catalogue", count: catalog.data?.totalParts },
    { key: "expenses", label: "Expense types", count: types.data?.length },
    { key: "maintenance", label: "Maintenance", count: taskCats.data?.length },
    { key: "budget", label: "Budget", count: budget?.exists ? formatMoneyCompact(budget.amount) : undefined },
  ];

  const failed = (retry: () => unknown, what: string) => (
    <div className="empty">
      <h3>{what} could not be loaded</h3>
      <p>
        <Button variant="secondary" size="sm" onClick={() => retry()}>
          Try again
        </Button>
      </p>
    </div>
  );

  return (
    <div>
      <PageHeader eyebrow="Administration" title="Settings" subtitle="Preset lists and rules that every other module draws from." />

      <div className="tabs" role="tablist">
        {tabs.map((t) => (
          <button key={t.key} role="tab" aria-selected={tab === t.key} className={`tab ${tab === t.key ? "on" : ""}`} onClick={() => setTab(t.key)}>
            {t.label}
            {t.count != null && <span className="c">{t.count}</span>}
          </button>
        ))}
      </div>

      {tab === "parts" &&
        (catalog.data ? (
          <PartsCatalogueTab catalog={catalog.data} isAdmin={isAdmin} onChanged={catalog.load} />
        ) : catalog.error ? (
          failed(catalog.load, "The parts catalogue")
        ) : (
          <PageSpinner />
        ))}

      {tab === "expenses" &&
        (types.data ? (
          <ExpenseTypesTab types={types.data} isAdmin={isAdmin} onChanged={types.load} />
        ) : types.error ? (
          failed(types.load, "Expense types")
        ) : (
          <PageSpinner />
        ))}

      {tab === "maintenance" &&
        (taskCats.data && engine.data ? (
          <MaintenanceTab
            categories={taskCats.data}
            engine={engine.data}
            isAdmin={isAdmin}
            onChanged={() => Promise.all([taskCats.load(), engine.load()])}
          />
        ) : taskCats.error || engine.error ? (
          failed(() => Promise.all([taskCats.load(), engine.load()]), "Maintenance settings")
        ) : (
          <PageSpinner />
        ))}

      {tab === "budget" && (
        <BudgetTab
          isAdmin={isAdmin}
          onChanged={(b) => {
            if (b.year === new Date().getFullYear()) setBudget(b);
          }}
        />
      )}
    </div>
  );
}

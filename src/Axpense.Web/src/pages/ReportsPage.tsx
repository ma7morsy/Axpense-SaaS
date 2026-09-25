import { useCallback, useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { CalendarDays, ChevronDown } from "lucide-react";
import { PageHeader } from "../components/ui/PageHeader";
import { Button } from "../components/ui/Button";
import { PageSpinner } from "../components/ui/Spinner";
import { PmEngineModal } from "../components/workorders/PmEngineModal";
import { reportsApi } from "../lib/api";
import { BudgetTab, CostTab, DriversTab, ExpensesTab, FuelTab, IssuesTab, OdometerTab, PmTab, UptimeTab, WorkOrdersTab } from "./reports/ReportTabs";
import "./reports/reports.css";

type TabKey = "fuel" | "budget" | "pm" | "workorders" | "cost" | "expenses" | "issues" | "uptime" | "drivers" | "odometer";

/** Tabs: key, label, API report, whether it uses the rolling window, whether it uses the year. */
const TABS: { key: TabKey; label: string; api: string; windowed?: boolean; yearly?: boolean }[] = [
  { key: "fuel", label: "Fuel log", api: "fuel", windowed: true },
  { key: "budget", label: "Budget log", api: "budget", yearly: true },
  { key: "pm", label: "PM compliance", api: "pm" },
  { key: "workorders", label: "Work orders", api: "work-orders" },
  { key: "cost", label: "Cost reports", api: "cost", windowed: true },
  { key: "expenses", label: "Expenses & forecast", api: "expenses" },
  { key: "issues", label: "Issue analysis", api: "issues" },
  { key: "uptime", label: "Vehicle uptime", api: "uptime", windowed: true },
  { key: "drivers", label: "Driver performance", api: "drivers", windowed: true },
  { key: "odometer", label: "Odometer", api: "odometer", windowed: true },
];
const WINDOWS = [30, 90, 180, 365];

/**
 * Reports & analytics across the modules. Every number is computed by /api/reports/* (ReportService);
 * each table exports to CSV from the rows shown.
 */
export default function ReportsPage() {
  const [params, setParams] = useSearchParams();
  const tab = (TABS.find((t) => t.key === params.get("tab"))?.key ?? "fuel") as TabKey;
  const meta = TABS.find((t) => t.key === tab)!;
  const [days, setDays] = useState(90);
  const [year, setYear] = useState(new Date().getFullYear());
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const [data, setData] = useState<{ tab: TabKey; d: any } | null>(null);
  const [error, setError] = useState("");
  const [pmOpen, setPmOpen] = useState(false);

  const load = useCallback(() => {
    setError("");
    setData(null);
    reportsApi
      .get(meta.api, { days: meta.windowed ? days : undefined, year: meta.yearly ? year : undefined })
      .then((d) => setData({ tab, d }))
      .catch((e) => setError(e instanceof Error ? e.message : "Could not load the report"));
  }, [meta, days, year, tab]);
  useEffect(load, [load]);

  const thisYear = new Date().getFullYear();
  const d = data?.tab === tab ? data.d : null;

  return (
    <div>
      <PageHeader
        eyebrow="Fleet"
        title="Reports"
        subtitle={
          meta.windowed
            ? `Rolling ${days}-day window across the whole fleet. Every table exports to CSV.`
            : meta.yearly
              ? `Budget performance for ${year}. Every table exports to CSV.`
              : "Current state across the whole fleet. Every table exports to CSV."
        }
        action={
          meta.windowed ? (
            <label className="range">
              <CalendarDays />
              <select aria-label="Reporting window" value={days} onChange={(e) => setDays(Number(e.target.value))}>
                {WINDOWS.map((w) => (
                  <option key={w} value={w}>
                    Last {w} days
                  </option>
                ))}
              </select>
              <ChevronDown className="cv" />
            </label>
          ) : meta.yearly ? (
            <label className="range">
              <CalendarDays />
              <select aria-label="Year" value={year} onChange={(e) => setYear(Number(e.target.value))}>
                {[thisYear - 2, thisYear - 1, thisYear, thisYear + 1].map((y) => (
                  <option key={y} value={y}>
                    {y}
                  </option>
                ))}
              </select>
              <ChevronDown className="cv" />
            </label>
          ) : undefined
        }
      />
      <div className="tabs rp-tabs" role="tablist">
        {TABS.map((t) => (
          <div
            key={t.key}
            role="tab"
            tabIndex={0}
            aria-selected={tab === t.key}
            className={`tab ${tab === t.key ? "on" : ""}`}
            onClick={() => setParams(t.key === "fuel" ? {} : { tab: t.key }, { replace: true })}
            onKeyDown={(e) => e.key === "Enter" && setParams({ tab: t.key }, { replace: true })}
          >
            {t.label}
          </div>
        ))}
      </div>

      {error ? (
        <div className="card">
          <div className="empty">
            <h3>Unable to load the report</h3>
            <p>{error}</p>
            <div style={{ marginTop: 14 }}>
              <Button onClick={load}>Try again</Button>
            </div>
          </div>
        </div>
      ) : !d ? (
        <PageSpinner />
      ) : tab === "fuel" ? (
        <FuelTab d={d} />
      ) : tab === "budget" ? (
        <BudgetTab d={d} />
      ) : tab === "pm" ? (
        <PmTab d={d} onRunPm={() => setPmOpen(true)} />
      ) : tab === "workorders" ? (
        <WorkOrdersTab d={d} />
      ) : tab === "cost" ? (
        <CostTab d={d} />
      ) : tab === "expenses" ? (
        <ExpensesTab d={d} />
      ) : tab === "issues" ? (
        <IssuesTab d={d} />
      ) : tab === "uptime" ? (
        <UptimeTab d={d} />
      ) : tab === "drivers" ? (
        <DriversTab d={d} />
      ) : (
        <OdometerTab d={d} />
      )}
      <PmEngineModal open={pmOpen} onClose={() => setPmOpen(false)} onDone={load} />
    </div>
  );
}

import type { ReactNode } from "react";
import { FileDown } from "lucide-react";
import { Button } from "../ui/Button";
import type { ReportStat } from "../../lib/types";

export const nf = (n: number | null | undefined, d = 0) => (n == null ? "—" : n.toLocaleString("en-US", { maximumFractionDigits: d, minimumFractionDigits: 0 }));
export const money = (n: number | null | undefined) => (n == null ? "—" : `EGP ${nf(n)}`);
export const fday = (s: string | null | undefined) =>
  s ? new Date(s).toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" }) : "—";

export function StatGrid({ stats }: { stats: ReportStat[] }) {
  return (
    <div className="grid g-4" style={{ marginBottom: 18 }}>
      {stats.map((s) => (
        <div key={s.label} className={`stat ${s.tone}`}>
          <div className="k">{s.label}</div>
          <div className="v" style={s.value.length > 14 ? { fontSize: 19 } : undefined}>
            {s.value}
          </div>
          <div className="m">{s.sub}</div>
        </div>
      ))}
    </div>
  );
}

export function Bar({ pct, tone = "" }: { pct: number; tone?: string }) {
  return (
    <div className={`bar-mini ${tone}`} style={{ minWidth: 70 }}>
      <i style={{ width: `${Math.max(0, Math.min(100, pct))}%` }} />
    </div>
  );
}

export type Csv = { file: string; headers: string[]; rows: (string | number | null | undefined)[][] };

/** Builds and downloads a CSV. Cells that could be read as formulas are prefixed with an apostrophe. */
export function downloadCsv({ file, headers, rows }: Csv) {
  const cell = (v: string | number | null | undefined) => {
    let s = v == null ? "" : String(v);
    if (/^[=+\-@\t\r]/.test(s) && !/^-?\d+(\.\d+)?$/.test(s)) s = "'" + s;
    return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
  };
  const text = [headers, ...rows].map((r) => r.map(cell).join(",")).join("\r\n");
  const url = URL.createObjectURL(new Blob(["﻿" + text], { type: "text/csv;charset=utf-8" }));
  const a = document.createElement("a");
  a.href = url;
  a.download = file.endsWith(".csv") ? file : `${file}.csv`;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 5000);
}

export function ReportCard({
  title,
  note,
  extra,
  csv,
  children,
  style,
}: {
  title: string;
  note?: string;
  extra?: ReactNode;
  csv?: Csv;
  children: ReactNode;
  style?: React.CSSProperties;
}) {
  return (
    <div className="card" style={style}>
      <div className="card-h">
        <div>
          <h2>{title}</h2>
          {note && <div className="t-sub">{note}</div>}
        </div>
        <div style={{ marginLeft: "auto", display: "flex", gap: 8 }}>
          {extra}
          {csv && (
            <Button variant="secondary" size="sm" onClick={() => downloadCsv(csv)} disabled={csv.rows.length === 0}>
              <FileDown /> Export CSV
            </Button>
          )}
        </div>
      </div>
      {children}
    </div>
  );
}

export function EmptyRow({ cols, title, sub }: { cols: number; title: string; sub?: string }) {
  return (
    <tr>
      <td colSpan={cols}>
        <div className="empty" style={{ padding: "24px 0" }}>
          <h3>{title}</h3>
          {sub && <p>{sub}</p>}
        </div>
      </td>
    </tr>
  );
}

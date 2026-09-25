import type { CSSProperties, ReactNode } from "react";
import type { BudgetVsActual, OperatingBucket } from "../../lib/types";

/**
 * Dashboard chart pieces, ported from the reference design (hand-drawn SVG + CSS in theme.css:
 * .tc trend chart, .dn donut, .chart/.cols stacked bars, .hbar budget tracks, .map stylised map).
 * Pure presentation — every figure comes from the API.
 */

export const cmp = (n: number) => (n >= 1e6 ? (n / 1e6).toFixed(2) + "M" : n >= 1e3 ? (n / 1e3).toFixed(1) + "K" : String(Math.round(n)));
const axisFmt = (v: number) => (v >= 1e6 ? (v / 1e6).toFixed(v % 1e6 ? 1 : 0) + "M" : v >= 1e3 ? (v / 1e3).toFixed(v % 1e3 ? 1 : 0) + "K" : String(v));
export const egp = (n: number, exact = false) =>
  exact ? `EGP ${n.toLocaleString("en-US", { maximumFractionDigits: 2 })}` : `EGP ${cmp(n)}`;
export const pctFmt = (v: number, d = 1) => `${v > 0 ? "+" : ""}${v.toFixed(d)}%`;

/** The four dashboard cost groups, as the reference lays them out. */
export const GROUP_STYLE: Record<string, { c1: string; c2: string; line: string; halo: string }> = {
  fuel: { c1: "#46DDB8", c2: "#14A98D", line: "#19B394", halo: "rgba(25,179,148,.20)" },
  maintenance: { c1: "#4AA3F2", c2: "#1565C0", line: "#1F7BD8", halo: "rgba(31,123,216,.18)" },
  expenses: { c1: "#A7D7FA", c2: "#63B4F0", line: "#74BCF3", halo: "rgba(116,188,243,.26)" },
  insurance: { c1: "#3B82C8", c2: "#164C86", line: "#1B5A9C", halo: "rgba(27,90,156,.18)" },
};

function niceScale(max: number, ticks = 4) {
  if (!(max > 0)) return { step: 1, top: 4 };
  const raw = max / ticks;
  const mag = Math.pow(10, Math.floor(Math.log10(raw)));
  const f = raw / mag;
  const step = (f <= 1 ? 1 : f <= 2 ? 2 : f <= 2.5 ? 2.5 : f <= 5 ? 5 : 10) * mag;
  return { step, top: Math.ceil(max / step) * step };
}

function smoothPath(pts: [number, number][]) {
  if (pts.length < 2) return pts.length ? `M${pts[0][0]},${pts[0][1]}` : "";
  const cl = (v: number) => Math.max(0, Math.min(100, v));
  let d = `M${pts[0][0].toFixed(2)},${pts[0][1].toFixed(2)}`;
  for (let i = 0; i < pts.length - 1; i++) {
    const p0 = pts[i - 1] || pts[i], p1 = pts[i], p2 = pts[i + 1], p3 = pts[i + 2] || p2, t = 0.19;
    const c1 = [p1[0] + (p2[0] - p0[0]) * t, cl(p1[1] + (p2[1] - p0[1]) * t)];
    const c2 = [p2[0] - (p3[0] - p1[0]) * t, cl(p2[1] - (p3[1] - p1[1]) * t)];
    d += ` C${c1[0].toFixed(2)},${c1[1].toFixed(2)} ${c2[0].toFixed(2)},${c2[1].toFixed(2)} ${p2[0].toFixed(2)},${p2[1].toFixed(2)}`;
  }
  return d;
}

export interface TrendLine {
  key: string;
  label: string;
  values: number[];
}

/** Smoothed area chart with hover columns (Total Costs Trend). */
export function TrendChart({ labels, fulls, lines }: { labels: string[]; fulls: string[]; lines: TrendLine[] }) {
  const n = labels.length;
  const { step, top } = niceScale(Math.max(1, ...lines.flatMap((l) => l.values)));
  const nT = Math.round(top / step);
  const X = (i: number) => ((i + 0.5) / n) * 100;
  const Y = (v: number) => 100 - (v / top) * 100;
  const grid = Array.from({ length: nT + 1 }, (_, i) => i * step);
  const op = [0.3, 0.16, 0.1];
  return (
    <div className="tc">
      <span className="tc-unit">EGP</span>
      <div className="tc-y">
        {grid.map((v) => (
          <span key={v} style={{ top: `${Y(v)}%` }}>
            {axisFmt(v)}
          </span>
        ))}
      </div>
      <div className="tc-plot">
        <svg className="tc-svg" viewBox="0 0 100 100" preserveAspectRatio="none" aria-hidden="true">
          <defs>
            {lines.map((l, i) => (
              <linearGradient key={l.key} id={`tca${i}`} x1="0" y1="0" x2="0" y2="1">
                <stop offset="0" stopColor={GROUP_STYLE[l.key].line} stopOpacity={op[i] ?? 0.1} />
                <stop offset="1" stopColor={GROUP_STYLE[l.key].line} stopOpacity={0} />
              </linearGradient>
            ))}
          </defs>
          {labels.map((_, i) => (
            <line key={`v${i}`} x1={X(i)} x2={X(i)} y1="0" y2="100" stroke="#EEF3F8" strokeWidth="1" vectorEffect="non-scaling-stroke" />
          ))}
          {grid.map((v) => (
            <line key={`h${v}`} x1="0" x2="100" y1={Y(v)} y2={Y(v)} stroke="#E6EDF4" strokeWidth="1" vectorEffect="non-scaling-stroke" />
          ))}
          {lines
            .map((l, i) => ({ l, i }))
            .reverse()
            .map(({ l, i }) => {
              const ln = smoothPath(l.values.map((v, ix) => [X(ix), Y(v)]));
              return (
                <g key={l.key}>
                  <path d={`${ln} L${X(n - 1).toFixed(2)},100 L${X(0).toFixed(2)},100 Z`} fill={`url(#tca${i})`} />
                  <path d={ln} fill="none" stroke={GROUP_STYLE[l.key].line} strokeWidth="2.4" strokeLinecap="round" vectorEffect="non-scaling-stroke" />
                </g>
              );
            })}
        </svg>
        {labels.map((_, i) => (
          <div key={i} className={`tc-col ${i < n / 2 ? "l" : "r"}`} style={{ left: `${X(i)}%`, width: `${100 / n}%` }}>
            <span className="guide" />
            {lines.map((l) => (
              <i
                key={l.key}
                className="mk"
                style={{ top: `${Y(l.values[i])}%`, "--c": GROUP_STYLE[l.key].line, "--ch": GROUP_STYLE[l.key].halo } as CSSProperties}
              />
            ))}
            <div className="tip">
              <div className="h">{fulls[i]}</div>
              {lines.map((l) => (
                <div className="r" key={l.key}>
                  <i style={{ background: GROUP_STYLE[l.key].line }} />
                  {l.label}
                  <b>{egp(l.values[i], true)}</b>
                </div>
              ))}
            </div>
          </div>
        ))}
      </div>
      <div className="tc-x">
        {labels.map((m, i) => (
          <span key={i} style={{ left: `${X(i)}%` }}>
            {m}
          </span>
        ))}
      </div>
    </div>
  );
}

/** Gradient ring donut (Cost Breakdown, Fleet Overview). */
export function Donut({
  items,
  total,
  uid,
  sw = 20,
  children,
}: {
  items: { value: number; c1: string; c2: string }[];
  total: number;
  uid: string;
  sw?: number;
  children?: ReactNode;
}) {
  const R = 52, C = 2 * Math.PI * R;
  const gap = items.filter((i) => i.value > 0).length > 1 ? 2.4 : 0;
  let off = 0;
  return (
    <div className="dn">
      <svg viewBox="-80 -80 160 160" aria-hidden="true">
        <defs>
          {items.map((g, i) => (
            <linearGradient key={i} id={`${uid}${i}`} x1="0" y1="0" x2="1" y2="1">
              <stop offset="0" stopColor={g.c1} />
              <stop offset="1" stopColor={g.c2} />
            </linearGradient>
          ))}
        </defs>
        <circle r="36" fill="none" stroke="#EAF2F9" strokeWidth="7" />
        <g transform="rotate(-96)">
          {items.map((g, i) => {
            const len = total ? (g.value / total) * C : 0;
            if (len <= 0) return null;
            const dash = Math.max(0.1, len - gap);
            const el = (
              <circle
                key={i}
                r={R}
                fill="none"
                stroke={`url(#${uid}${i})`}
                strokeWidth={sw}
                strokeDasharray={`${dash.toFixed(2)} ${(C - dash).toFixed(2)}`}
                strokeDashoffset={(-off).toFixed(2)}
              />
            );
            off += len;
            return el;
          })}
        </g>
      </svg>
      <div className="dn-c">{children}</div>
    </div>
  );
}

/** Stacked bars by expense type with a previous-period ghost bar (Operating Cost Trend). */
export function StackedChart({ buckets, series }: { buckets: OperatingBucket[]; series: { id: string; name: string; color: string }[] }) {
  const max = Math.max(1, ...buckets.map((b) => Math.max(b.total, b.previousTotal)));
  return (
    <div className="chart">
      <div className="cols">
        {buckets.map((b) => (
          <div className="col" key={b.key}>
            <div className="tip">
              <div className="h">{b.full}</div>
              <div className="t">Total {egp(b.total, true)}</div>
              {series
                .filter((s) => (b.values[s.id] ?? 0) > 0)
                .map((s) => (
                  <div className="r" key={s.id}>
                    <span>
                      <i style={{ background: s.color }} />
                      {s.name}
                    </span>
                    <b>{egp(b.values[s.id], true)}</b>
                  </div>
                ))}
              <div className="r" style={{ marginTop: 7, paddingTop: 7, borderTop: "1px solid rgba(255,255,255,.15)" }}>
                <span>Previous period</span>
                <b>{b.previousTotal ? egp(b.previousTotal, true) : "no data"}</b>
              </div>
            </div>
            <div className="col-in">
              <div className="ghost" style={{ height: `${(b.previousTotal / max) * 100}%` }} />
              <div className="stack" style={{ height: `${(b.total / max) * 100}%` }}>
                {series.map((s) =>
                  (b.values[s.id] ?? 0) > 0 ? (
                    <div key={s.id} className="seg" style={{ height: `${(b.values[s.id] / b.total) * 100}%`, background: s.color }} />
                  ) : null
                )}
              </div>
            </div>
          </div>
        ))}
      </div>
      <div style={{ display: "flex", gap: 6 }}>
        {buckets.map((b) => (
          <div key={b.key} className="x-lbl" style={{ flex: 1, minWidth: 0 }}>
            {b.label}
          </div>
        ))}
      </div>
      <div className="legend">
        {series.map((s) => (
          <b key={s.id}>
            <i style={{ background: s.color }} />
            {s.name}
          </b>
        ))}
        <b>
          <i style={{ background: "#EDF1F5" }} />
          Previous period
        </b>
      </div>
    </div>
  );
}

/** Budget vs actual tracks with a budget marker. */
export function BudgetTracks({ data }: { data: BudgetVsActual }) {
  const max = Math.max(1, ...data.rows.map((r) => Math.max(r.budget, r.actual)));
  return (
    <>
      {data.rows.map((r) => {
        const v = r.variancePct;
        return (
          <div className="hbar" key={r.key}>
            <div className="tip">
              <div className="h">{r.label}</div>
              <div className="t">Variance {v == null ? "—" : pctFmt(v, 1)}</div>
              <div className="r">
                <span>Budget</span>
                <b>{egp(r.budget, true)}</b>
              </div>
              <div className="r">
                <span>Actual</span>
                <b>{egp(r.actual, true)}</b>
              </div>
              <div className="r">
                <span>Remaining</span>
                <b>{egp(r.remaining, true)}</b>
              </div>
            </div>
            <div className="row">
              <span style={{ fontWeight: 600 }}>{r.label}</span>
              <span>
                <span className="mono">{egp(r.actual)}</span>{" "}
                <span className="t-sub" style={{ margin: 0 }}>
                  of {egp(r.budget)}
                </span>
                {v != null && (
                  <span className={`badge ${r.over ? "bad" : v > -10 ? "warn" : "ok"}`} style={{ marginLeft: 8 }}>
                    {pctFmt(v, 0)}
                  </span>
                )}
              </span>
            </div>
            <div className="track">
              <i style={{ width: `${Math.min(100, (r.actual / max) * 100)}%`, background: r.over ? "var(--red)" : "var(--teal)" }} />
              {r.budget > 0 && <span className="mark" style={{ left: `${Math.min(100, (r.budget / max) * 100)}%` }} title="Budget" />}
            </div>
          </div>
        );
      })}
    </>
  );
}

const BLOCKS = [[6,8,58,36],[70,8,44,26],[120,6,70,40],[198,10,52,30],[258,6,64,44],[10,56,44,30],[64,52,62,34],[138,58,40,26],
  [190,54,58,32],[258,58,62,30],[8,100,50,34],[70,98,56,30],[140,92,44,34],[196,98,52,28],[258,96,62,34],[10,146,60,52],
  [82,146,44,54],[136,142,66,58],[214,140,48,60],[270,144,50,56]];
const PARKS = [[124,52,30,16],[236,16,20,18],[62,112,18,16],[206,150,26,14],[16,156,20,14]];

/** Stylised city map behind the fleet pins (not a geographic map). */
export function MapSvg() {
  return (
    <svg className="map-svg" viewBox="0 0 330 210" preserveAspectRatio="xMidYMid slice" aria-hidden="true">
      <rect width="330" height="210" fill="#EEF2F6" />
      <g fill="#E3E9F0">
        {BLOCKS.map((b, i) => (
          <rect key={i} x={b[0]} y={b[1]} width={b[2]} height={b[3]} rx="3" />
        ))}
      </g>
      <g fill="#DCEBDF">
        {PARKS.map((b, i) => (
          <rect key={i} x={b[0]} y={b[1]} width={b[2]} height={b[3]} rx="6" />
        ))}
      </g>
      <path d="M-10 132 C 50 118, 96 150, 160 132 S 262 108, 340 124 L340 140 C 268 124, 210 152, 158 148 S 46 132, -10 150Z" fill="#D7E7F5" />
      <g fill="none" stroke="#fff" strokeLinecap="round">
        <path strokeWidth="7" d="M-4 50 C 90 44, 210 56, 336 48" />
        <path strokeWidth="7" d="M112 -4 C 118 70, 106 140, 120 214" />
        <path strokeWidth="5" d="M-4 96 L336 90" />
        <path strokeWidth="5" d="M-4 140 C 100 168, 220 160, 336 170" />
        <path strokeWidth="5" d="M200 -4 C 190 70, 214 130, 204 214" />
        <path strokeWidth="4" d="M-4 8 L336 20 M60 -4 L52 214 M254 -4 C 262 80, 246 140, 262 214" />
        <path strokeWidth="3" d="M-4 76 L336 72 M-4 118 L336 112 M156 -4 L162 214 M300 -4 L296 214 M18 -4 L14 214" />
        <path strokeWidth="3.5" d="M-4 200 L336 186 M-4 30 C 100 34, 200 24, 336 34" />
      </g>
    </svg>
  );
}

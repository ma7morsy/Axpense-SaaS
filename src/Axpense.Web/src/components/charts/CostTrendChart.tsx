import { Area, AreaChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import type { CostTrendPoint } from "../../lib/types";
import { formatMoney } from "../../lib/utils";

const SERIES = [
  { key: "fuel", label: "Fuel", color: "var(--color-chart-1)" },
  { key: "maintenance", label: "Maintenance", color: "var(--color-chart-2)" },
  { key: "expenses", label: "Expenses", color: "var(--color-chart-3)" },
] as const;

function CustomTooltip({ active, payload, label }: any) {
  if (!active || !payload?.length) return null;
  return (
    <div className="rounded-lg border border-hairline bg-white px-3.5 py-3 text-xs shadow-[var(--shadow-card-lg)]">
      <p className="mb-1.5 font-semibold text-ink-900">{label}</p>
      <div className="space-y-1">
        {payload.map((entry: any) => (
          <div key={entry.dataKey} className="flex items-center justify-between gap-6">
            <span className="flex items-center gap-1.5 text-ink-500">
              <span className="h-2 w-2 rounded-full" style={{ background: entry.color }} />
              {SERIES.find((s) => s.key === entry.dataKey)?.label}
            </span>
            <span className="tabular font-semibold text-ink-900">{formatMoney(entry.value)}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

export function CostTrendChart({ data }: { data: CostTrendPoint[] }) {
  return (
    <div className="h-[260px] w-full">
      <ResponsiveContainer width="100%" height="100%">
        <AreaChart data={data} margin={{ top: 8, right: 8, left: -12, bottom: 0 }}>
          <defs>
            {SERIES.map((s) => (
              <linearGradient key={s.key} id={`fill-${s.key}`} x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor={s.color} stopOpacity={0.18} />
                <stop offset="100%" stopColor={s.color} stopOpacity={0} />
              </linearGradient>
            ))}
          </defs>
          <CartesianGrid vertical={false} stroke="var(--color-hairline)" />
          <XAxis
            dataKey="month"
            axisLine={false}
            tickLine={false}
            tick={{ fill: "var(--color-ink-400)", fontSize: 11 }}
            dy={8}
          />
          <YAxis
            axisLine={false}
            tickLine={false}
            width={56}
            tick={{ fill: "var(--color-ink-400)", fontSize: 11 }}
            tickFormatter={(v) => `$${Number(v).toLocaleString()}`}
          />
          <Tooltip content={<CustomTooltip />} cursor={{ stroke: "var(--color-ink-300)", strokeDasharray: 3 }} />
          <Legend
            verticalAlign="top"
            align="right"
            height={28}
            iconType="circle"
            iconSize={8}
            wrapperStyle={{ fontSize: 12, color: "var(--color-ink-500)" }}
            formatter={(value) => SERIES.find((s) => s.key === value)?.label ?? value}
          />
          {SERIES.map((s) => (
            <Area
              key={s.key}
              type="monotone"
              dataKey={s.key}
              stroke={s.color}
              strokeWidth={2}
              fill={`url(#fill-${s.key})`}
              dot={{ r: 3, strokeWidth: 0, fill: s.color }}
              activeDot={{ r: 5, strokeWidth: 2, stroke: "#fff" }}
            />
          ))}
        </AreaChart>
      </ResponsiveContainer>
    </div>
  );
}

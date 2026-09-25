import { Cell, Pie, PieChart, ResponsiveContainer, Tooltip } from "recharts";
import { formatMoney } from "../../lib/utils";

const COLORS = ["var(--color-chart-1)", "var(--color-chart-2)", "var(--color-chart-3)"];

export interface BreakdownSlice {
  label: string;
  value: number;
}

function CustomTooltip({ active, payload }: any) {
  if (!active || !payload?.length) return null;
  const p = payload[0];
  return (
    <div className="rounded-lg border border-hairline bg-white px-3 py-2 text-xs shadow-[var(--shadow-card-lg)]">
      <span className="font-semibold text-ink-900">{p.name}</span>
      <span className="ml-2 tabular text-ink-500">{formatMoney(p.value)}</span>
    </div>
  );
}

export function CostBreakdownChart({ data, total }: { data: BreakdownSlice[]; total: number }) {
  const hasData = data.some((d) => d.value > 0);

  return (
    <div className="relative mx-auto h-[168px] w-[168px]">
      <ResponsiveContainer width="100%" height="100%">
        <PieChart>
          <Pie
            data={hasData ? data : [{ label: "No data", value: 1 }]}
            dataKey="value"
            nameKey="label"
            innerRadius={54}
            outerRadius={78}
            paddingAngle={hasData ? 2 : 0}
            stroke="#fff"
            strokeWidth={2}
          >
            {(hasData ? data : [{ label: "No data", value: 1 }]).map((entry, i) => (
              <Cell key={entry.label} fill={hasData ? COLORS[i % COLORS.length] : "var(--color-hairline)"} />
            ))}
          </Pie>
          {hasData && <Tooltip content={<CustomTooltip />} />}
        </PieChart>
      </ResponsiveContainer>
      <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center">
        <span className="tabular text-base font-bold text-ink-900">{formatMoney(total)}</span>
        <span className="text-[10px] text-ink-400">Total Cost</span>
      </div>
    </div>
  );
}

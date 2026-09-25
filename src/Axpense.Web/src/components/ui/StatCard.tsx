import type { LucideIcon } from "lucide-react";
import { ArrowDownRight, ArrowUpRight } from "lucide-react";
import { cn } from "../../lib/utils";

interface StatCardProps {
  icon: LucideIcon;
  label: string;
  value: string;
  tone?: "brand" | "blue";
  trendPct?: number | null;
  trendGoodDirection?: "up" | "down";
  helperText?: string;
}

export function StatCard({ icon: Icon, label, value, tone = "brand", trendPct, trendGoodDirection = "down", helperText }: StatCardProps) {
  const hasTrend = typeof trendPct === "number" && Number.isFinite(trendPct);
  const isUp = hasTrend && trendPct! >= 0;
  const isGood = hasTrend && (isUp ? trendGoodDirection === "up" : trendGoodDirection === "down");

  return (
    <div className="dk">
      <div className={cn("dk-ic", tone === "blue" && "blue")}>
        <Icon />
      </div>
      <div className="dk-t">
        <div className="dk-l">{label}</div>
        <div className="dk-v">{value}</div>
        {hasTrend ? (
          <div className={cn("dk-d", isGood ? "good" : "bad")}>
            {isUp ? <ArrowUpRight /> : <ArrowDownRight />}
            {Math.abs(trendPct!).toFixed(0)}%
          </div>
        ) : helperText ? (
          <div className="dk-s">{helperText}</div>
        ) : null}
      </div>
    </div>
  );
}

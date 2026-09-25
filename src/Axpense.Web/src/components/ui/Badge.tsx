import type { HTMLAttributes } from "react";
import { cn } from "../../lib/utils";

type Tone = "neutral" | "brand" | "good" | "warning" | "serious" | "critical";

const toneClasses: Record<Tone, string> = {
  neutral: "plain",
  brand: "info",
  good: "ok",
  warning: "warn",
  serious: "warn",
  critical: "bad",
};

export function Badge({ tone = "neutral", className, ...props }: HTMLAttributes<HTMLSpanElement> & { tone?: Tone }) {
  return <span className={cn("badge", toneClasses[tone], className)} {...props} />;
}

import type { LucideIcon } from "lucide-react";

export function EmptyState({ icon: Icon, title, description }: { icon: LucideIcon; title: string; description?: string }) {
  return (
    <div className="empty">
      <Icon />
      <h3>{title}</h3>
      {description && <p>{description}</p>}
    </div>
  );
}

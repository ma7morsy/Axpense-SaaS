import { useEffect, useState } from "react";
import { Bell, FileWarning, AlertTriangle, CheckCircle2, Wrench, RefreshCw } from "lucide-react";
import { PageHeader } from "../components/ui/PageHeader";
import { Button } from "../components/ui/Button";
import { Card } from "../components/ui/Card";
import { Badge } from "../components/ui/Badge";
import { EmptyState } from "../components/ui/EmptyState";
import { PageSpinner } from "../components/ui/Spinner";
import { notificationsApi } from "../lib/api";
import type { NotificationItem } from "../lib/types";
import { cn, formatDateTime } from "../lib/utils";

const ICON: Record<string, typeof Bell> = {
  Maintenance: Wrench,
  License: FileWarning,
  Budget: AlertTriangle,
  Inspection: CheckCircle2,
};

const SEVERITY_TONE: Record<string, "critical" | "warning" | "neutral"> = {
  Critical: "critical",
  Warning: "warning",
};

export default function NotificationsPage() {
  const [list, setList] = useState<NotificationItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [generating, setGenerating] = useState(false);

  const load = () => notificationsApi.list().then(setList).finally(() => setLoading(false));
  useEffect(() => {
    load();
  }, []);

  const generate = async () => {
    setGenerating(true);
    try {
      await notificationsApi.generate();
      await load();
    } finally {
      setGenerating(false);
    }
  };

  const markRead = async (id: string) => {
    await notificationsApi.markRead(id);
    load();
  };

  const unread = list.filter((n) => !n.isRead).length;

  return (
    <div>
      <PageHeader
        title="Notifications"
        subtitle={`${unread} unread reminder${unread === 1 ? "" : "s"} and operational alerts.`}
        action={
          <Button variant="secondary" onClick={generate} disabled={generating}>
            <RefreshCw className={cn("h-4 w-4", generating && "animate-spin")} /> Generate Reminders
          </Button>
        }
      />

      <Card className="divide-y divide-hairline">
        {loading ? (
          <PageSpinner />
        ) : list.length === 0 ? (
          <EmptyState icon={Bell} title="No notifications" description="Generate reminders to check for upcoming maintenance and license expiries." />
        ) : (
          list.map((n) => {
            const Icon = ICON[n.type] ?? Bell;
            return (
              <div key={n.id} className={cn("flex items-start gap-4 px-5 py-4", !n.isRead && "bg-brand-50/30")}>
                <span className="flex h-9 w-9 flex-none items-center justify-center rounded-full bg-brand-50 text-brand-600">
                  <Icon className="h-4 w-4" />
                </span>
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="text-sm font-semibold text-ink-900">{n.title}</p>
                    <Badge tone={SEVERITY_TONE[n.severity] ?? "neutral"}>{n.severity}</Badge>
                  </div>
                  <p className="mt-0.5 text-sm text-ink-500">{n.message}</p>
                  <p className="mt-1 text-xs text-ink-400">{formatDateTime(n.createdAt)}</p>
                </div>
                {!n.isRead && (
                  <Button size="sm" variant="secondary" onClick={() => markRead(n.id)}>
                    Mark read
                  </Button>
                )}
              </div>
            );
          })
        )}
      </Card>
    </div>
  );
}

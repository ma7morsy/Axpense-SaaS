import { useState } from "react";
import { PageHeader } from "../../components/ui/PageHeader";
import { AuditTab, UsersTab } from "./adminSections";
import { useIsAdmin } from "./useIsAdmin";

export default function RolesUsersPage() {
  const { user, isAdmin } = useIsAdmin();
  const [tab, setTab] = useState<"users" | "audit">("users");
  return (
    <div>
      <PageHeader eyebrow="Administration" title="Roles & Users" subtitle="Decide who can see and change what." />
      <div className="tabs" role="tablist">
        <button role="tab" aria-selected={tab === "users"} className={`tab ${tab === "users" ? "on" : ""}`} onClick={() => setTab("users")}>
          Users
        </button>
        <button role="tab" aria-selected={tab === "audit"} className={`tab ${tab === "audit" ? "on" : ""}`} onClick={() => setTab("audit")}>
          Audit log
        </button>
      </div>
      {tab === "users" ? <UsersTab isAdmin={isAdmin} currentUserId={user?.id} /> : <AuditTab />}
    </div>
  );
}

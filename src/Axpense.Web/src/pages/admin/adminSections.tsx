import { useEffect, useState, type FormEvent } from "react";
import { Plus, Users as UsersIcon, History, Download } from "lucide-react";
import { Button } from "../../components/ui/Button";
import { Card } from "../../components/ui/Card";
import { Table, Tbody, Td, Th, Thead, Tr } from "../../components/ui/Table";
import { Modal } from "../../components/ui/Modal";
import { Input, Label, Select } from "../../components/ui/Field";
import { Badge } from "../../components/ui/Badge";
import { EmptyState } from "../../components/ui/EmptyState";
import { PageSpinner } from "../../components/ui/Spinner";
import { settingsApi, usersApi } from "../../lib/api";
import type { AuditLogEntry, OrganizationSettings, UserSummary } from "../../lib/types";
import { formatDateTime } from "../../lib/utils";

/** Administration sections, rendered by pages/admin/* (Company, Roles & Users). */

export function UsersTab({ isAdmin, currentUserId }: { isAdmin: boolean; currentUserId?: string }) {
  const [list, setList] = useState<UserSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [open, setOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [form, setForm] = useState({ fullName: "", email: "", password: "", role: "Staff" });

  const load = () => usersApi.list().then(setList).finally(() => setLoading(false));
  useEffect(() => {
    load();
  }, []);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setError("");
    try {
      await usersApi.create(form);
      setOpen(false);
      setForm({ fullName: "", email: "", password: "", role: "Staff" });
      load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not create user");
    } finally {
      setSaving(false);
    }
  };

  const toggle = async (u: UserSummary) => {
    await usersApi.setStatus(u.id, false);
    load();
  };

  return (
    <>
      <div className="mb-4 flex justify-end">
        {isAdmin && (
          <Button onClick={() => setOpen(true)}>
            <Plus className="h-4 w-4" /> Add User
          </Button>
        )}
      </div>
      <Card>
        {loading ? (
          <PageSpinner />
        ) : list.length === 0 ? (
          <EmptyState icon={UsersIcon} title="No users yet" />
        ) : (
          <Table>
            <Thead>
              <Tr>
                <Th>Name</Th>
                <Th>Email</Th>
                <Th>Role</Th>
                <Th></Th>
              </Tr>
            </Thead>
            <Tbody>
              {list.map((u) => (
                <Tr key={u.id}>
                  <Td className="font-semibold text-ink-900">
                    {u.firstName} {u.lastName}
                  </Td>
                  <Td>{u.email}</Td>
                  <Td>
                    <Badge tone="brand">{u.role}</Badge>
                  </Td>
                  <Td>
                    {isAdmin && u.id !== currentUserId && (
                      <Button size="sm" variant="secondary" onClick={() => toggle(u)}>
                        Deactivate
                      </Button>
                    )}
                  </Td>
                </Tr>
              ))}
            </Tbody>
          </Table>
        )}
      </Card>

      <Modal open={open} onClose={() => setOpen(false)} title="Add User">
        <form onSubmit={submit} className="space-y-4">
          <div>
            <Label>Full name</Label>
            <Input required value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} />
          </div>
          <div>
            <Label>Email</Label>
            <Input required type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <Label>Temporary password</Label>
              <Input required minLength={6} type="password" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} />
            </div>
            <div>
              <Label>Role</Label>
              <Select value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value })}>
                <option>Admin</option>
                <option>Manager</option>
                <option>Staff</option>
              </Select>
            </div>
          </div>
          {error && <p className="text-xs text-status-critical">{error}</p>}
          <div className="flex justify-end gap-2 pt-1">
            <Button type="button" variant="secondary" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={saving}>
              {saving ? "Creating…" : "Create User"}
            </Button>
          </div>
        </form>
      </Modal>
    </>
  );
}

export function AuditTab() {
  const [list, setList] = useState<AuditLogEntry[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    settingsApi.auditLog(200).then(setList).finally(() => setLoading(false));
  }, []);

  return (
    <Card>
      {loading ? (
        <PageSpinner />
      ) : list.length === 0 ? (
        <EmptyState icon={History} title="No audit events yet" />
      ) : (
        <Table>
          <Thead>
            <Tr>
              <Th>Date</Th>
              <Th>Action</Th>
              <Th>Entity</Th>
              <Th>Details</Th>
            </Tr>
          </Thead>
          <Tbody>
            {list.map((x) => (
              <Tr key={x.id}>
                <Td>{formatDateTime(x.createdAt)}</Td>
                <Td className="font-semibold text-ink-900">{x.action}</Td>
                <Td>{x.entityType}</Td>
                <Td>{x.details || "—"}</Td>
              </Tr>
            ))}
          </Tbody>
        </Table>
      )}
    </Card>
  );
}

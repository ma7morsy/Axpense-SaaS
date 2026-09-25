import { useAuth } from "../../contexts/AuthContext";

/** Owner and Admin can change organization-wide configuration; everyone else is read-only. */
export function useIsAdmin() {
  const { user } = useAuth();
  return { user, isAdmin: user?.role === "Owner" || user?.role === "Admin" };
}

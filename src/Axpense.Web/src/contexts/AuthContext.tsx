import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { authApi, clearToken, getToken, setOrgId, setToken } from "../lib/api";
import type { CurrentUser } from "../lib/types";

interface AuthContextValue {
  user: CurrentUser | null;
  loading: boolean;
  login: (userNameOrEmail: string, password: string, remember?: boolean) => Promise<void>;
  register: (payload: {
    organizationName: string;
    firstName: string;
    lastName: string;
    email: string;
    userName?: string;
    password: string;
    confirmPassword: string;
  }) => Promise<void>;
  logout: () => void;
  refresh: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [loading, setLoading] = useState(true);

  const refresh = useCallback(async () => {
    if (!getToken()) {
      setUser(null);
      setLoading(false);
      return;
    }
    try {
      const me = await authApi.me();
      setOrgId(me.organizationId);
      setUser(me);
    } catch {
      clearToken();
      setUser(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    refresh();
  }, [refresh]);

  const login = useCallback(
    async (userNameOrEmail: string, password: string, remember = true) => {
      const res = await authApi.login({ userNameOrEmail, password });
      if (!res.success || !res.token) throw new Error(res.message || "Sign in failed");
      setToken(res.token, remember);
      if (res.organizationId) setOrgId(res.organizationId);
      await refresh();
    },
    [refresh]
  );

  const register = useCallback(
    async (payload: {
      organizationName: string;
      firstName: string;
      lastName: string;
      email: string;
      userName?: string;
      password: string;
      confirmPassword: string;
    }) => {
      const res = await authApi.register(payload);
      if (!res.success || !res.token) throw new Error(res.errors?.join(", ") || res.message || "Registration failed");
      setToken(res.token);
      if (res.organizationId) setOrgId(res.organizationId);
      await refresh();
    },
    [refresh]
  );

  const logout = useCallback(() => {
    clearToken();
    setUser(null);
  }, []);

  const value = useMemo(() => ({ user, loading, login, register, logout, refresh }), [user, loading, login, register, logout, refresh]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}

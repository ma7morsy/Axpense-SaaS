import { Navigate, Outlet, useLocation } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";
import { PageSpinner } from "./ui/Spinner";

export function ProtectedRoute() {
  const { user, loading } = useAuth();
  const location = useLocation();

  if (loading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-page">
        <PageSpinner />
      </div>
    );
  }

  if (!user) return <Navigate to="/login" replace />;
  // New workspaces finish (or skip) the setup wizard before using the app.
  if (user.onboardingRequired && location.pathname !== "/onboarding") return <Navigate to="/onboarding" replace />;

  return <Outlet />;
}

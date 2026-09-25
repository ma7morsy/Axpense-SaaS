import { lazy, Suspense } from "react";
import { Navigate, Route, Routes } from "react-router-dom";
import { AuthProvider } from "./contexts/AuthContext";
import { ProtectedRoute } from "./components/ProtectedRoute";
import { AppLayout } from "./components/layout/AppLayout";
import { PageSpinner } from "./components/ui/Spinner";
import { ToastProvider } from "./components/ui/Toast";
import LoginPage from "./pages/LoginPage";

const DashboardPage = lazy(() => import("./pages/DashboardPage"));
const VehiclesPage = lazy(() => import("./pages/VehiclesPage"));
const VehicleProfilePage = lazy(() => import("./pages/VehicleProfilePage"));
const AerialViewPage = lazy(() => import("./pages/AerialViewPage"));
const MaintenancePage = lazy(() => import("./pages/MaintenancePage"));
const InspectionsPage = lazy(() => import("./pages/InspectionsPage"));
const ExpensesPage = lazy(() => import("./pages/ExpensesPage"));
const BudgetsPage = lazy(() => import("./pages/BudgetsPage"));
const DriversPage = lazy(() => import("./pages/DriversPage"));
const DriverProfilePage = lazy(() => import("./pages/DriverProfilePage"));
const ReportsPage = lazy(() => import("./pages/ReportsPage"));
const NotificationsPage = lazy(() => import("./pages/NotificationsPage"));
const CompanyPage = lazy(() => import("./pages/admin/CompanyPage"));
const RolesUsersPage = lazy(() => import("./pages/admin/RolesUsersPage"));
const SettingsPage = lazy(() => import("./pages/admin/SettingsPage"));
const RegisterPage = lazy(() => import("./pages/auth/RegisterPage"));
const ForgotPasswordPage = lazy(() => import("./pages/auth/ForgotPasswordPage"));
const ResetPasswordPage = lazy(() => import("./pages/auth/ResetPasswordPage"));
const OnboardingPage = lazy(() => import("./pages/auth/OnboardingPage"));

export default function App() {
  return (
    <AuthProvider>
      <ToastProvider>
      <Suspense fallback={<PageSpinner />}>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/forgot-password" element={<ForgotPasswordPage />} />
          <Route path="/reset-password" element={<ResetPasswordPage />} />
          <Route element={<ProtectedRoute />}>
            <Route path="/onboarding" element={<OnboardingPage />} />
            <Route element={<AppLayout />}>
              <Route path="/" element={<DashboardPage />} />
              <Route path="/vehicles" element={<VehiclesPage />} />
              <Route path="/vehicles/:id" element={<VehicleProfilePage />} />
              <Route path="/aerial" element={<AerialViewPage />} />
              <Route path="/maintenance" element={<MaintenancePage />} />
              <Route path="/inspections" element={<InspectionsPage />} />
              <Route path="/fuel" element={<Navigate to="/reports?tab=fuel" replace />} />
              <Route path="/expenses" element={<ExpensesPage />} />
              <Route path="/budgets" element={<BudgetsPage />} />
              <Route path="/drivers" element={<DriversPage />} />
              <Route path="/drivers/:id" element={<DriverProfilePage />} />
              <Route path="/reports" element={<ReportsPage />} />
              <Route path="/notifications" element={<NotificationsPage />} />
              <Route path="/administration" element={<Navigate to="/administration/company" replace />} />
              <Route path="/administration/company" element={<CompanyPage />} />
              <Route path="/administration/users" element={<RolesUsersPage />} />
              <Route path="/administration/settings" element={<SettingsPage />} />
            </Route>
          </Route>
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </Suspense>
      </ToastProvider>
    </AuthProvider>
  );
}

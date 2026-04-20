import { AppShell } from "../layouts/AppShell";
import { AppRoutes } from "../routes/AppRoutes";
import { ToastProvider } from "../components/ui";

export default function App() {
  return (
    <ToastProvider>
      <AppShell>
        <AppRoutes />
      </AppShell>
    </ToastProvider>
  );
}

import { AppShell } from "../layouts/AppShell";
import { AppRoutes } from "../routes/AppRoutes";
import { ToastProvider } from "../components/ui";
import { I18nProvider } from "../i18n";

export default function App() {
  return (
    <I18nProvider>
      <ToastProvider>
        <AppShell>
          <AppRoutes />
        </AppShell>
      </ToastProvider>
    </I18nProvider>
  );
}

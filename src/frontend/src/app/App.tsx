import { AppShell } from "../layouts/AppShell";
import { AppRoutes } from "../routes/AppRoutes";
import { ToastProvider } from "../components/ui";
import { I18nProvider } from "../i18n";
import { AuthProvider } from "../features/auth/AuthProvider";
import { FeatureConfigurationProvider } from "../features/auth/FeatureConfigurationProvider";
import { DesktopRuntimeGate } from "./DesktopRuntimeGate";

export default function App() {
  return (
    <I18nProvider>
      <DesktopRuntimeGate>
        <AuthProvider>
          <FeatureConfigurationProvider>
            <ToastProvider>
              <AppShell>
                <AppRoutes />
              </AppShell>
            </ToastProvider>
          </FeatureConfigurationProvider>
        </AuthProvider>
      </DesktopRuntimeGate>
    </I18nProvider>
  );
}

import type { PropsWithChildren, ReactNode } from "react";
import { NavLink } from "react-router-dom";
import { Card, PageHeader } from "../../components/ui";
import { useAuth } from "../../features/auth/AuthProvider";
import { useI18n } from "../../i18n";
import { Permissions } from "../../services/permissions";

type SettingsNavItem = {
  to: string;
  label: string;
  permission: string;
};

const settingsNavItems: SettingsNavItem[] = [
  { to: "/settings/users", label: "settings.nav.users", permission: Permissions.SettingsUsersManage },
  { to: "/settings/roles", label: "settings.nav.roles", permission: Permissions.SettingsRolesManage },
];

interface SettingsScaffoldProps extends PropsWithChildren {
  actions?: ReactNode;
  description: string;
  title: string;
}

export function SettingsScaffold({ actions, children, description, title }: SettingsScaffoldProps) {
  const { hasPermission } = useAuth();
  const { t } = useI18n();
  const allowedItems = settingsNavItems.filter((item) => hasPermission(item.permission));

  return (
    <section className="hc-settings-page">
      <PageHeader
        eyebrow="settings.usersAccess.eyebrow"
        title={title}
        description={description}
        actions={actions}
      />

      <div className="hc-settings-layout">
        <Card className="hc-settings-nav-card" padding="md">
          <nav aria-label={t("settings.navigation")} className="hc-settings-nav">
            {allowedItems.map((item) => (
              <NavLink
                key={item.to}
                className={({ isActive }) => `hc-settings-nav__link ${isActive ? "hc-settings-nav__link--active" : ""}`}
                to={item.to}
              >
                {t(item.label)}
              </NavLink>
            ))}
          </nav>
        </Card>

        <div className="hc-settings-content">
          {children}
        </div>
      </div>
    </section>
  );
}

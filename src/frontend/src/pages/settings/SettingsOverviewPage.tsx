import { Link } from "react-router-dom";
import { Card } from "../../components/ui";
import { SettingsScaffold } from "./SettingsScaffold";
import { useAuth } from "../../features/auth/AuthProvider";
import { useI18n } from "../../i18n";
import { Permissions } from "../../services/permissions";

const cards = [
  { to: "/settings/organization", title: "settings.overview.organization.title", description: "settings.overview.organization.description", permission: Permissions.SettingsOrganizationManage },
  { to: "/settings/users", title: "settings.overview.users.title", description: "settings.overview.users.description", permission: Permissions.SettingsUsersManage },
  { to: "/settings/profiles", title: "settings.overview.profiles.title", description: "settings.overview.profiles.description", permission: Permissions.SettingsProfilesManage },
  { to: "/settings/roles", title: "settings.overview.roles.title", description: "settings.overview.roles.description", permission: Permissions.SettingsRolesManage },
  { to: "/settings/invitations", title: "settings.overview.invitations.title", description: "settings.overview.invitations.description", permission: Permissions.SettingsInvitationsManage },
  { to: "/settings/security", title: "settings.overview.security.title", description: "settings.overview.security.description", permission: Permissions.SettingsSecurityManage },
  { to: "/settings/sessions", title: "settings.overview.sessions.title", description: "settings.overview.sessions.description", permission: Permissions.SettingsSessionsManage },
  { to: "/settings/audit-log", title: "settings.overview.audit.title", description: "settings.overview.audit.description", permission: Permissions.AuditLogView },
  { to: "/settings/features", title: "settings.overview.features.title", description: "settings.overview.features.description", permission: Permissions.SettingsOrganizationManage },
];

export function SettingsOverviewPage() {
  const { hasPermission } = useAuth();
  const { t } = useI18n();
  const visibleCards = cards.filter((card) => hasPermission(card.permission));

  return (
    <SettingsScaffold title="settings.title" description="settings.description">
      <div className="hc-settings-card-grid">
        {visibleCards.map((card) => (
          <Link key={card.to} className="hc-settings-link-card" to={card.to}>
            <Card padding="lg">
              <h2 className="hc-settings-link-card__title">{t(card.title)}</h2>
              <p className="hc-settings-link-card__description">{t(card.description)}</p>
            </Card>
          </Link>
        ))}
      </div>
    </SettingsScaffold>
  );
}

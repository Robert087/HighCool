import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Badge, Card, EmptyState, PageHeader, SkeletonLoader } from "../components/ui";
import { useAuth } from "../features/auth/AuthProvider";
import { useFeatureConfiguration } from "../features/auth/FeatureConfigurationProvider";
import { useI18n } from "../i18n";
import { Permissions } from "../services/permissions";
import { ApiError } from "../services/api";
import { getOrganizationSettings, type OrganizationSettings } from "../services/settingsApi";

export function WorkspacePage() {
  const { formatDate, t } = useI18n();
  const { hasPermission, workspace } = useAuth();
  const { features } = useFeatureConfiguration();
  const [organization, setOrganization] = useState<OrganizationSettings | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(hasPermission(Permissions.SettingsOrganizationManage));

  useEffect(() => {
    let active = true;

    async function load() {
      if (!hasPermission(Permissions.SettingsOrganizationManage)) {
        setLoading(false);
        return;
      }

      try {
        setLoading(true);
        const response = await getOrganizationSettings();
        if (active) {
          setOrganization(response);
          setError("");
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : t("workspace.loadError"));
        }
      } finally {
        if (active) {
          setLoading(false);
        }
      }
    }

    void load();

    return () => {
      active = false;
    };
  }, [hasPermission, t]);

  if (!workspace) {
    return null;
  }

  return (
    <section className="hc-workspace-page">
      <PageHeader
        eyebrow="workspace.eyebrow"
        title="workspace.title"
        description="workspace.description"
        actions={<Link className="hc-button hc-button--primary hc-button--md" to="/dashboard">workspace.openDashboard</Link>}
      />

      <div className="hc-workspace-grid">
        <Card className="hc-workspace-card" padding="lg">
          <div className="hc-workspace-card__header">
            <div>
              <p className="hc-workspace-card__eyebrow">{t("workspace.cards.organization.eyebrow")}</p>
              <h2 className="hc-workspace-card__title">{workspace.organizationName}</h2>
            </div>
            <Badge tone="success">{workspace.emailVerified ? t("workspace.verified") : t("workspace.pendingVerification")}</Badge>
          </div>
          <dl className="hc-workspace-details">
            <div>
              <dt>{t("workspace.organizationId")}</dt>
              <dd>{workspace.organizationId}</dd>
            </div>
            <div>
              <dt>{t("workspace.memberId")}</dt>
              <dd>{workspace.membershipId}</dd>
            </div>
            <div>
              <dt>{t("workspace.roleCount")}</dt>
              <dd>{workspace.roles.length}</dd>
            </div>
            <div>
              <dt>{t("workspace.permissionCount")}</dt>
              <dd>{workspace.permissions.length}</dd>
            </div>
          </dl>
        </Card>

        <Card className="hc-workspace-card" padding="lg">
          <div className="hc-workspace-card__header">
            <div>
              <p className="hc-workspace-card__eyebrow">{t("workspace.cards.user.eyebrow")}</p>
              <h2 className="hc-workspace-card__title">{workspace.fullName}</h2>
            </div>
            <Badge tone="primary">{workspace.roles[0]?.name ?? t("workspace.ownerFallback")}</Badge>
          </div>
          <dl className="hc-workspace-details">
            <div>
              <dt>{t("auth.email")}</dt>
              <dd>{workspace.email}</dd>
            </div>
            <div>
              <dt>{t("workspace.organizations")}</dt>
              <dd>{workspace.organizations.length}</dd>
            </div>
            <div>
              <dt>{t("workspace.requiresTwoFactor")}</dt>
              <dd>{workspace.requiresTwoFactor ? t("common.yes") : t("common.no")}</dd>
            </div>
          </dl>
        </Card>

        <Card className="hc-workspace-card" padding="lg">
          <div className="hc-workspace-card__header">
            <div>
              <p className="hc-workspace-card__eyebrow">{t("workspace.cards.modules.eyebrow")}</p>
              <h2 className="hc-workspace-card__title">{t("workspace.cards.modules.title")}</h2>
            </div>
          </div>
          <div className="hc-workspace-badges">
            {(features?.enabledModules ?? []).map((moduleKey) => (
              <Badge key={moduleKey} tone="primary">{`feature.module.${moduleKey}`}</Badge>
            ))}
          </div>
          {features && features.disabledModules.length > 0 ? (
            <div className="hc-workspace-muted">
              {t("workspace.disabledModules", { count: features.disabledModules.length })}
            </div>
          ) : (
            <div className="hc-workspace-muted">{t("workspace.allCoreModulesEnabled")}</div>
          )}
        </Card>

        <Card className="hc-workspace-card hc-workspace-card--span-2" padding="lg">
          <div className="hc-workspace-card__header">
            <div>
              <p className="hc-workspace-card__eyebrow">{t("workspace.cards.nextSteps.eyebrow")}</p>
              <h2 className="hc-workspace-card__title">{t("workspace.cards.nextSteps.title")}</h2>
            </div>
          </div>
          <div className="hc-workspace-actions">
            <Link className="hc-workspace-action" to="/settings/organization">{t("workspace.actions.organization")}</Link>
            <Link className="hc-workspace-action" to="/settings/users">{t("workspace.actions.users")}</Link>
            <Link className="hc-workspace-action" to="/settings/roles">{t("workspace.actions.roles")}</Link>
            <Link className="hc-workspace-action" to="/settings/security">{t("workspace.actions.security")}</Link>
            <Link className="hc-workspace-action" to="/settings/audit-log">{t("workspace.actions.auditLog")}</Link>
            <Link className="hc-workspace-action" to="/dashboard">{t("workspace.actions.dashboard")}</Link>
          </div>
        </Card>
      </div>

      <Card className="hc-workspace-card" padding="lg">
        <div className="hc-workspace-card__header">
          <div>
            <p className="hc-workspace-card__eyebrow">{t("workspace.cards.configuration.eyebrow")}</p>
            <h2 className="hc-workspace-card__title">{t("workspace.cards.configuration.title")}</h2>
          </div>
        </div>
        {loading ? (
          <div className="hc-skeleton-stack">
            <SkeletonLoader variant="rect" height="4.5rem" />
            <SkeletonLoader variant="rect" height="4.5rem" />
          </div>
        ) : error ? (
          <EmptyState title="workspace.configurationUnavailable" description={error} />
        ) : organization ? (
          <dl className="hc-workspace-details">
            <div>
              <dt>{t("workspace.defaultLanguage")}</dt>
              <dd>{organization.defaultLanguage.toUpperCase()}</dd>
            </div>
            <div>
              <dt>{t("workspace.defaultCurrency")}</dt>
              <dd>{organization.defaultCurrency}</dd>
            </div>
            <div>
              <dt>{t("workspace.timezone")}</dt>
              <dd>{organization.timezone}</dd>
            </div>
            <div>
              <dt>{t("workspace.autoPostDrafts")}</dt>
              <dd>{organization.autoPostDrafts ? t("common.yes") : t("common.no")}</dd>
            </div>
            <div>
              <dt>{t("workspace.fiscalYearStart")}</dt>
              <dd>{organization.fiscalYearStartMonth}</dd>
            </div>
            <div>
              <dt>{t("workspace.createdWorkspace")}</dt>
              <dd>{formatDate(new Date().toISOString(), { day: "numeric", month: "short", year: "numeric" })}</dd>
            </div>
          </dl>
        ) : (
          <EmptyState
            title="workspace.noOrganizationSettings"
            description="workspace.noOrganizationSettingsDescription"
          />
        )}
      </Card>
    </section>
  );
}

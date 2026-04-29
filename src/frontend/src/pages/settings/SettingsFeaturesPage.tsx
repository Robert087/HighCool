import { useEffect, useState } from "react";
import { Badge, Card, EmptyState, SkeletonLoader } from "../../components/ui";
import { SettingsScaffold } from "./SettingsScaffold";
import { ApiError } from "../../services/api";
import { useI18n } from "../../i18n";
import { getFeatureConfiguration, type FeatureConfiguration } from "../../services/settingsApi";

export function SettingsFeaturesPage() {
  const { t } = useI18n();
  const [features, setFeatures] = useState<FeatureConfiguration | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        const response = await getFeatureConfiguration();
        if (active) {
          setFeatures(response);
          setError("");
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : "settings.loadError");
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
  }, []);

  return (
    <SettingsScaffold title="settings.features.title" description="settings.features.description">
      <Card padding="lg">
        {loading ? (
          <div className="hc-skeleton-stack">
            <SkeletonLoader variant="rect" height="4rem" />
            <SkeletonLoader variant="rect" height="4rem" />
          </div>
        ) : error ? (
          <EmptyState title="settings.errorTitle" description={error} />
        ) : features ? (
          <div className="hc-settings-feature-grid">
            <div className="hc-settings-feature-card">
              <h2 className="hc-settings-feature-card__title">{t("settings.features.enabledModules")}</h2>
              <div className="hc-workspace-badges">
                {features.enabledModules.map((moduleKey) => (
                  <Badge key={moduleKey} tone="success">{`feature.module.${moduleKey}`}</Badge>
                ))}
              </div>
            </div>
            <div className="hc-settings-feature-card">
              <h2 className="hc-settings-feature-card__title">{t("settings.features.runtimeFlags")}</h2>
              <dl className="hc-workspace-details">
                <div>
                  <dt>{t("settings.features.offlineDraftsOnly")}</dt>
                  <dd>{features.offlineDraftsOnly ? t("common.yes") : t("common.no")}</dd>
                </div>
                <div>
                  <dt>{t("settings.features.emailOtpEnabled")}</dt>
                  <dd>{features.emailOtpEnabled ? t("common.yes") : t("common.no")}</dd>
                </div>
                <div>
                  <dt>{t("settings.features.autoPostDrafts")}</dt>
                  <dd>{features.autoPostDrafts ? t("common.yes") : t("common.no")}</dd>
                </div>
              </dl>
            </div>
          </div>
        ) : (
          <EmptyState title="settings.features.emptyTitle" description="settings.features.emptyDescription" />
        )}
      </Card>
    </SettingsScaffold>
  );
}

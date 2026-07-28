import { useEffect, useState } from "react";
import { Button, Card, Checkbox, EmptyState, SkeletonLoader, useToast } from "../../components/ui";
import { SettingsScaffold } from "./SettingsScaffold";
import { ApiError } from "../../services/api";
import {
  getOrganizationSetup,
  updateFeatureSettings,
  type OrganizationFeatureSettings,
} from "../../services/settingsApi";
import { useFeatureConfiguration } from "../../features/auth/FeatureConfigurationProvider";
import { useI18n } from "../../i18n";

type FeatureKey = keyof OrganizationFeatureSettings;

const featureGroups: Array<{ title: string; description: string; features: Array<{ key: FeatureKey; label: string; description: string; inactive?: boolean }> }> = [
  {
    title: "settings.features.groups.core",
    description: "settings.features.groups.coreDescription",
    features: [
      { key: "enableInventory", label: "settings.features.fields.inventory", description: "settings.features.descriptions.inventory" },
      { key: "enableProcurement", label: "settings.features.fields.purchasing", description: "settings.features.descriptions.purchasing" },
      { key: "enableSales", label: "settings.features.fields.sales", description: "settings.features.descriptions.sales", inactive: true },
      { key: "enablePriceLists", label: "settings.features.fields.priceLists", description: "settings.features.descriptions.priceLists", inactive: true },
    ],
  },
  {
    title: "settings.features.groups.workforce",
    description: "settings.features.groups.workforceDescription",
    features: [
      { key: "enableEmployees", label: "settings.features.fields.employees", description: "settings.features.descriptions.employees", inactive: true },
      { key: "enableSalaries", label: "settings.features.fields.salaries", description: "settings.features.descriptions.salaries", inactive: true },
      { key: "enableEmployeeAdvances", label: "settings.features.fields.employeeAdvances", description: "settings.features.descriptions.employeeAdvances", inactive: true },
    ],
  },
  {
    title: "settings.features.groups.business",
    description: "settings.features.groups.businessDescription",
    features: [
      { key: "enableExpenses", label: "settings.features.fields.expenses", description: "settings.features.descriptions.expenses", inactive: true },
      { key: "enableReports", label: "settings.features.fields.reports", description: "settings.features.descriptions.reports", inactive: true },
      { key: "enableNotifications", label: "settings.features.fields.notifications", description: "settings.features.descriptions.notifications", inactive: true },
    ],
  },
  {
    title: "settings.features.groups.inventoryOperations",
    description: "settings.features.groups.inventoryOperationsDescription",
    features: [
      { key: "enableStockTransfers", label: "settings.features.fields.inventoryTransfers", description: "settings.features.descriptions.inventoryTransfers" },
      { key: "enableStockAdjustments", label: "settings.features.fields.inventoryAdjustments", description: "settings.features.descriptions.inventoryAdjustments", inactive: true },
      { key: "enableInventoryCounts", label: "settings.features.fields.inventoryCounts", description: "settings.features.descriptions.inventoryCounts" },
      { key: "enableInventoryIssues", label: "settings.features.fields.inventoryIssues", description: "settings.features.descriptions.inventoryIssues", inactive: true },
      { key: "enableLowStockAlerts", label: "settings.features.fields.lowStockAlerts", description: "settings.features.descriptions.lowStockAlerts", inactive: true },
    ],
  },
];

export function SettingsFeaturesPage() {
  const { t } = useI18n();
  const { showToast } = useToast();
  const { reload } = useFeatureConfiguration();
  const [form, setForm] = useState<OrganizationFeatureSettings | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const inventoryEnabled = form?.enableInventory ?? false;

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        const response = await getOrganizationSetup();
        if (active) {
          setForm(response.features);
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

  function setFeature(key: FeatureKey, checked: boolean) {
    setForm((current) => current ? { ...current, [key]: checked } : current);
  }

  async function handleSave() {
    if (!form) {
      return;
    }

    try {
      setSaving(true);
      const response = await updateFeatureSettings(form);
      setForm(response.features);
      setError("");
      await reload();
      showToast({ tone: "success", title: "settings.features.savedTitle", description: "settings.features.savedDescription" });
    } catch (saveError) {
      setError(saveError instanceof ApiError ? saveError.message : "settings.saveError");
    } finally {
      setSaving(false);
    }
  }

  return (
    <SettingsScaffold
      title="settings.features.title"
      description="settings.features.description"
      actions={<Button isLoading={saving} disabled={!form} onClick={handleSave}>common.save</Button>}
    >
      {loading ? (
        <Card padding="lg">
          <div className="hc-skeleton-stack">
            <SkeletonLoader variant="rect" height="4rem" />
            <SkeletonLoader variant="rect" height="4rem" />
            <SkeletonLoader variant="rect" height="4rem" />
          </div>
        </Card>
      ) : error && !form ? (
        <Card padding="lg">
          <EmptyState title="settings.errorTitle" description={error} />
        </Card>
      ) : form ? (
        <div className="hc-settings-feature-grid">
          {error ? <div className="hc-inline-error">{error}</div> : null}
          {featureGroups.map((group) => (
            <Card key={group.title} padding="lg">
              <div className="hc-settings-feature-card">
                <h2 className="hc-settings-feature-card__title">{t(group.title)}</h2>
                <p className="hc-settings-feature-card__description">{t(group.description)}</p>
                <div className="hc-settings-feature-list">
                  {group.features.map((feature) => {
                    const inventoryChild = isInventoryChild(feature.key);
                    const disabled = inventoryChild && !inventoryEnabled;
                    return (
                      <Checkbox
                        key={feature.key}
                        checked={form[feature.key]}
                        disabled={disabled}
                        label={feature.inactive ? `${feature.label}Inactive` : feature.label}
                        description={feature.description}
                        onChange={(event) => setFeature(feature.key, event.target.checked)}
                      />
                    );
                  })}
                </div>
              </div>
            </Card>
          ))}
        </div>
      ) : (
        <Card padding="lg">
          <EmptyState title="settings.features.emptyTitle" description="settings.features.emptyDescription" />
        </Card>
      )}
    </SettingsScaffold>
  );
}

function isInventoryChild(key: FeatureKey) {
  return key === "enableStockTransfers" ||
    key === "enableStockAdjustments" ||
    key === "enableInventoryCounts" ||
    key === "enableInventoryIssues" ||
    key === "enableLowStockAlerts";
}

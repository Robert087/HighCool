import { useEffect, useMemo, useState } from "react";
import type { FormEvent } from "react";
import { Link, useParams } from "react-router-dom";
import { DocumentPageLayout, DocumentSection } from "../components/patterns";
import { Badge, Button, Checkbox, EmptyState, Field, Input, SkeletonLoader, useI18n, useToast } from "../components/ui";
import { useAuth } from "../features/auth/AuthProvider";
import { ApiError } from "../services/api";
import {
  getReorderSettings,
  mapReorderSettingsToFormValues,
  updateReorderSettings,
  type ReorderSettings,
  type ReorderSettingsFormValues,
} from "../services/inventoryMonitoringApi";
import { Permissions } from "../services/permissions";

type ValidationErrors = Record<string, string[]>;

const INITIAL_VALUES: ReorderSettingsFormValues = {
  enableMonitoring: false,
  minimumStock: 0,
  reorderPoint: "",
  maximumStock: "",
  reorderQuantity: "",
  safetyStock: "",
  leadTimeDays: "",
};

export function validateReorderSettingsForm(values: ReorderSettingsFormValues): ValidationErrors {
  const errors: ValidationErrors = {};
  const minimumStock = values.minimumStock === "" ? Number.NaN : Number(values.minimumStock);
  const reorderPoint = values.reorderPoint === "" ? Number.NaN : Number(values.reorderPoint);
  const maximumStock = values.maximumStock === "" ? Number.NaN : Number(values.maximumStock);
  const reorderQuantity = values.reorderQuantity === "" ? Number.NaN : Number(values.reorderQuantity);
  const safetyStock = values.safetyStock === "" ? null : Number(values.safetyStock);
  const leadTimeDays = values.leadTimeDays === "" ? null : Number(values.leadTimeDays);

  if (!Number.isFinite(minimumStock) || minimumStock < 0) {
    errors.minimumStock = ["module.inventoryMonitoring.validation.minimumStock"];
  }

  if (!Number.isFinite(reorderPoint) || reorderPoint < minimumStock) {
    errors.reorderPoint = ["module.inventoryMonitoring.validation.reorderPoint"];
  }

  if (!Number.isFinite(maximumStock) || maximumStock <= 0 || maximumStock < reorderPoint) {
    errors.maximumStock = ["module.inventoryMonitoring.validation.maximumStock"];
  }

  if (!Number.isFinite(reorderQuantity) || reorderQuantity <= 0) {
    errors.reorderQuantity = ["module.inventoryMonitoring.validation.reorderQuantity"];
  }

  if (safetyStock !== null && (!Number.isFinite(safetyStock) || safetyStock < 0)) {
    errors.safetyStock = ["module.inventoryMonitoring.validation.safetyStock"];
  }

  if (leadTimeDays !== null && (!Number.isInteger(leadTimeDays) || leadTimeDays < 0)) {
    errors.leadTimeDays = ["module.inventoryMonitoring.validation.leadTimeDays"];
  }

  return errors;
}

export function InventoryReorderSettingsPage() {
  const { itemId = "" } = useParams();
  const { hasPermission } = useAuth();
  const { t } = useI18n();
  const { showToast } = useToast();
  const [settings, setSettings] = useState<ReorderSettings | null>(null);
  const [values, setValues] = useState<ReorderSettingsFormValues>(INITIAL_VALUES);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [formError, setFormError] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);
  const canManage = hasPermission(Permissions.InventoryMonitorManage);
  const formId = useMemo(() => `reorder-settings-${itemId || "new"}`, [itemId]);

  useEffect(() => {
    let active = true;

    async function load() {
      if (!itemId) {
        setLoading(false);
        setFormError(t("module.inventoryMonitoring.settings.missingItem"));
        return;
      }

      try {
        setLoading(true);
        setFormError("");
        const result = await getReorderSettings(itemId);

        if (active) {
          setSettings(result);
          setValues(mapReorderSettingsToFormValues(result));
        }
      } catch (loadError) {
        if (active) {
          setFormError(loadError instanceof ApiError ? loadError.message : t("module.inventoryMonitoring.settings.loadError"));
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
  }, [itemId, reloadKey, t]);

  function setValue<Key extends keyof ReorderSettingsFormValues>(key: Key, value: ReorderSettingsFormValues[Key]) {
    setValues((current) => ({ ...current, [key]: value }));
  }

  function displayError(key: string | undefined) {
    return key ? t(key) : "";
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!canManage || !itemId) {
      return;
    }

    const nextErrors = validateReorderSettingsForm(values);
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) {
      return;
    }

    try {
      setSaving(true);
      setFormError("");
      const result = await updateReorderSettings(itemId, values);
      setSettings(result);
      setValues(mapReorderSettingsToFormValues(result));
      showToast({
        tone: "success",
        title: t("module.inventoryMonitoring.settings.toast.savedTitle"),
        description: t("module.inventoryMonitoring.settings.toast.savedDescription", { value: result.itemCode }),
      });
    } catch (saveError) {
      setFormError(saveError instanceof ApiError ? saveError.message : t("module.inventoryMonitoring.settings.saveError"));
    } finally {
      setSaving(false);
    }
  }

  function renderActions() {
    return (
      <>
        <Link className="hc-button hc-button--secondary hc-button--md" to="/inventory-monitoring">{t("module.inventoryMonitoring.backToMonitoring")}</Link>
        {canManage ? <Button form={formId} isLoading={saving} type="submit">{t("module.inventoryMonitoring.settings.save")}</Button> : null}
      </>
    );
  }

  return (
    <DocumentPageLayout
      actions={renderActions()}
      description="module.inventoryMonitoring.settings.description"
      eyebrow="route.section.inventory"
      footer={renderActions()}
      status={<Badge tone={values.enableMonitoring ? "success" : "neutral"}>{values.enableMonitoring ? t("module.inventoryMonitoring.enabled") : t("module.inventoryMonitoring.disabled")}</Badge>}
      title="module.inventoryMonitoring.settings.title"
    >
      {loading ? (
        <div className="hc-document-section">
          <div className="hc-skeleton-stack">
            <SkeletonLoader height="2.75rem" variant="rect" />
            <SkeletonLoader height="2.75rem" variant="rect" />
            <SkeletonLoader height="2.75rem" variant="rect" />
          </div>
        </div>
      ) : null}

      {!loading && formError && !settings ? (
        <div className="hc-document-section">
          <EmptyState
            action={<Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>{t("common.retry")}</Button>}
            description={formError}
            title="module.inventoryMonitoring.settings.loadErrorTitle"
          />
        </div>
      ) : null}

      {!loading && settings ? (
        <form className="hc-document-form" id={formId} onSubmit={handleSubmit}>
          {formError ? <div className="hc-inline-error">{formError}</div> : null}

          <DocumentSection title="module.inventoryMonitoring.settings.itemSection" description="module.inventoryMonitoring.settings.itemDescription">
            <div className="hc-document-form-grid">
              <Field label={t("table.item")}>
                <Input disabled value={`${settings.itemCode} - ${settings.itemName}`} onChange={() => undefined} />
              </Field>
              <Field label={t("table.baseUom")}>
                <Input disabled value={settings.baseUomCode} onChange={() => undefined} />
              </Field>
              <Field className="hc-document-field--span-full" label={t("module.inventoryMonitoring.enableMonitoring")}>
                <Checkbox
                  checked={values.enableMonitoring}
                  disabled={!canManage || saving}
                  label={t("module.inventoryMonitoring.enableMonitoring")}
                  onChange={(event) => setValue("enableMonitoring", event.target.checked)}
                />
              </Field>
            </div>
          </DocumentSection>

          <DocumentSection title="module.inventoryMonitoring.settings.thresholdSection" description="module.inventoryMonitoring.settings.thresholdDescription">
            <div className="hc-document-form-grid">
              <Field label={t("module.inventoryMonitoring.minimumStock")} required>
                <Input disabled={!canManage || saving} min="0" step="0.000001" type="number" value={values.minimumStock} onChange={(event) => setValue("minimumStock", event.target.value === "" ? "" : Number(event.target.value))} />
                {errors.minimumStock ? <small className="hc-field-error">{displayError(errors.minimumStock[0])}</small> : null}
              </Field>
              <Field label={t("module.inventoryMonitoring.reorderPoint")} required>
                <Input disabled={!canManage || saving} min="0" step="0.000001" type="number" value={values.reorderPoint} onChange={(event) => setValue("reorderPoint", event.target.value === "" ? "" : Number(event.target.value))} />
                {errors.reorderPoint ? <small className="hc-field-error">{displayError(errors.reorderPoint[0])}</small> : null}
              </Field>
              <Field label={t("module.inventoryMonitoring.maximumStock")} required>
                <Input disabled={!canManage || saving} min="0.000001" step="0.000001" type="number" value={values.maximumStock} onChange={(event) => setValue("maximumStock", event.target.value === "" ? "" : Number(event.target.value))} />
                {errors.maximumStock ? <small className="hc-field-error">{displayError(errors.maximumStock[0])}</small> : null}
              </Field>
              <Field label={t("module.inventoryMonitoring.reorderQuantity")} required>
                <Input disabled={!canManage || saving} min="0.000001" step="0.000001" type="number" value={values.reorderQuantity} onChange={(event) => setValue("reorderQuantity", event.target.value === "" ? "" : Number(event.target.value))} />
                {errors.reorderQuantity ? <small className="hc-field-error">{displayError(errors.reorderQuantity[0])}</small> : null}
              </Field>
              <Field label={t("module.inventoryMonitoring.safetyStock")}>
                <Input disabled={!canManage || saving} min="0" step="0.000001" type="number" value={values.safetyStock} onChange={(event) => setValue("safetyStock", event.target.value === "" ? "" : Number(event.target.value))} />
                {errors.safetyStock ? <small className="hc-field-error">{displayError(errors.safetyStock[0])}</small> : null}
              </Field>
              <Field label={t("module.inventoryMonitoring.leadTimeDays")}>
                <Input disabled={!canManage || saving} min="0" step="1" type="number" value={values.leadTimeDays} onChange={(event) => setValue("leadTimeDays", event.target.value === "" ? "" : Number(event.target.value))} />
                {errors.leadTimeDays ? <small className="hc-field-error">{displayError(errors.leadTimeDays[0])}</small> : null}
              </Field>
            </div>
          </DocumentSection>
        </form>
      ) : null}
    </DocumentPageLayout>
  );
}

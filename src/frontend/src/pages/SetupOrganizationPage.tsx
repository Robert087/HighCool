import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Badge, Button, Card, Checkbox, EmptyState, Field, Input, PageHeader, Select, SkeletonLoader, useToast } from "../components/ui";
import { useAuth } from "../features/auth/AuthProvider";
import { useI18n } from "../i18n";
import { ApiError } from "../services/api";
import {
  completeOrganizationSetup,
  getOrganizationSetup,
  saveOrganizationSetup,
  type OrganizationSetup,
  type SaveOrganizationSetupRequest,
} from "../services/settingsApi";

const steps = [
  "setup.steps.companyProfile",
  "setup.steps.modules",
  "setup.steps.workflow",
  "setup.steps.stock",
  "setup.steps.review",
] as const;

const currencyOptions = ["EGP", "USD", "EUR", "SAR", "AED", "GBP"] as const;
const timezoneOptions = ["Africa/Cairo", "UTC", "Asia/Riyadh", "Asia/Dubai", "Europe/London", "Europe/Berlin", "America/New_York"] as const;
const monthOptions = [
  { value: 1, key: "setup.month.january" },
  { value: 2, key: "setup.month.february" },
  { value: 3, key: "setup.month.march" },
  { value: 4, key: "setup.month.april" },
  { value: 5, key: "setup.month.may" },
  { value: 6, key: "setup.month.june" },
  { value: 7, key: "setup.month.july" },
  { value: 8, key: "setup.month.august" },
  { value: 9, key: "setup.month.september" },
  { value: 10, key: "setup.month.october" },
  { value: 11, key: "setup.month.november" },
  { value: 12, key: "setup.month.december" },
] as const;

type ModuleKey = "procurement" | "inventory" | "suppliers" | "supplierFinancials" | "shortage" | "uom" | "uomConversion" | "warehouses" | "stockAdjustments";

function toRequestModel(setup: OrganizationSetup): SaveOrganizationSetupRequest {
  return {
    ...setup.organization,
    ...setup.features,
    ...setup.workflow,
    ...setup.stock,
    defaultWarehouseName: null,
    setupStep: setup.status.setupStep ?? "company-profile",
    setupVersion: setup.status.setupVersion ?? "v1",
  };
}

export function SetupOrganizationPage() {
  const navigate = useNavigate();
  const { logout, refreshWorkspace, workspace } = useAuth();
  const { t } = useI18n();
  const { showToast } = useToast();
  const [form, setForm] = useState<SaveOrganizationSetupRequest | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [stepIndex, setStepIndex] = useState(0);

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        const response = await getOrganizationSetup();
        if (!active) {
          return;
        }

        setForm(toRequestModel(response));
        setStepIndex(Math.max(0, stepKeyToIndex(response.status.setupStep)));
        setError("");
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : t("setup.loadError"));
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
  }, [t]);

  useEffect(() => {
    if (workspace?.setupCompleted) {
      navigate("/workspace", { replace: true });
    }
  }, [navigate, workspace?.setupCompleted]);

  const currentStep = steps[stepIndex] ?? steps[0];
  const canShowStock = form ? form.enableInventory : false;
  const stepProgress = Math.round(((stepIndex + 1) / steps.length) * 100);

  const reviewWarnings = useMemo(() => {
    if (!form) {
      return [];
    }

    const warnings: string[] = [];

    if (!form.enableShortageManagement) {
      warnings.push("setup.reviewWarnings.shortageDisabled");
    }

    if (!form.enableUomConversion) {
      warnings.push("setup.reviewWarnings.uomConversionDisabled");
    }

    if (!form.enableSupplierFinancials) {
      warnings.push("setup.reviewWarnings.supplierFinancialsDisabled");
    }

    return warnings;
  }, [form]);

  async function persistSetup(nextStepIndex?: number) {
    if (!form) {
      return;
    }

    try {
      setSaving(true);
      const response = await saveOrganizationSetup({
        ...form,
        setupStep: indexToStepKey(nextStepIndex ?? stepIndex),
      });
      setForm(toRequestModel(response));
      setError("");
      showToast({
        tone: "success",
        title: "setup.savedTitle",
        description: "setup.savedDescription",
      });
    } catch (saveError) {
      setError(saveError instanceof ApiError ? saveError.message : t("setup.saveError"));
      throw saveError;
    } finally {
      setSaving(false);
    }
  }

  async function handleNext() {
    if (!form) {
      return;
    }

    const profileError = currentStep === "setup.steps.companyProfile" ? validateCompanyProfile(form, t) : null;
    if (profileError) {
      setError(profileError);
      return;
    }

    const stockValidationError = currentStep === "setup.steps.stock" ? validateStockSetup(form, t) : null;
    if (stockValidationError) {
      setError(stockValidationError);
      return;
    }

    const next = Math.min(stepIndex + 1, steps.length - 1);
    await persistSetup(next);
    setStepIndex(next);
  }

  async function handleBack() {
    const previous = Math.max(stepIndex - 1, 0);
    await persistSetup(previous);
    setStepIndex(previous);
  }

  async function handleComplete() {
    if (!form) {
      return;
    }

    try {
      const profileError = validateCompanyProfile(form, t);
      if (profileError) {
        setError(profileError);
        setStepIndex(0);
        return;
      }

      const stockValidationError = validateStockSetup(form, t);
      if (stockValidationError) {
        setError(stockValidationError);
        setStepIndex(3);
        return;
      }

      await persistSetup(steps.length - 1);
      await completeOrganizationSetup();
      await refreshWorkspace();
      showToast({
        tone: "success",
        title: "setup.completedTitle",
        description: "setup.completedDescription",
      });
      navigate("/workspace", { replace: true });
    } catch {
      return;
    }
  }

  return (
    <section className="hc-setup-page">
      <PageHeader
        eyebrow="setup.eyebrow"
        title="setup.title"
        description="setup.description"
        actions={<Button variant="secondary" onClick={() => void logout(false)}>app.logout</Button>}
      />

      {loading ? (
        <Card padding="lg">
          <div className="hc-skeleton-stack">
            <SkeletonLoader variant="rect" height="4rem" />
            <SkeletonLoader variant="rect" height="18rem" />
          </div>
        </Card>
      ) : error && !form ? (
        <Card padding="lg">
          <EmptyState title="setup.errorTitle" description={error} />
        </Card>
      ) : form ? (
        <>
          <Card padding="md">
            <div className="hc-setup-steps">
              {steps.map((step, index) => (
                <div key={step} className={`hc-setup-step ${index === stepIndex ? "hc-setup-step--active" : index < stepIndex ? "hc-setup-step--done" : ""}`}>
                  <span className="hc-setup-step__index">{index + 1}</span>
                  <span className="hc-setup-step__label">{t(step)}</span>
                </div>
              ))}
            </div>
            <div className="hc-setup-progress">
              <div aria-hidden className="hc-setup-progress__bar">
                <span className="hc-setup-progress__fill" style={{ width: `${stepProgress}%` }} />
              </div>
              <p className="hc-setup-progress__label">{t("setup.progress").replace("{current}", String(stepIndex + 1)).replace("{total}", String(steps.length))}</p>
            </div>
          </Card>

          <Card padding="lg">
            {error ? <p className="auth-card__error">{error}</p> : null}

            {currentStep === "setup.steps.companyProfile" ? (
              <div className="hc-settings-form__grid">
                <Field label="auth.organizationName" required>
                  <Input value={form.name} onChange={(event) => setForm((current) => current ? { ...current, name: event.target.value } : current)} />
                </Field>
                <Field label="setup.fields.address">
                  <Input value={form.address ?? ""} onChange={(event) => setForm((current) => current ? { ...current, address: event.target.value || null } : current)} />
                </Field>
                <Field label="setup.fields.phone">
                  <Input value={form.phone ?? ""} onChange={(event) => setForm((current) => current ? { ...current, phone: event.target.value || null } : current)} />
                </Field>
                <Field label="setup.fields.taxId">
                  <Input value={form.taxId ?? ""} onChange={(event) => setForm((current) => current ? { ...current, taxId: event.target.value || null } : current)} />
                </Field>
                <Field label="setup.fields.commercialRegistry">
                  <Input value={form.commercialRegistry ?? ""} onChange={(event) => setForm((current) => current ? { ...current, commercialRegistry: event.target.value || null } : current)} />
                </Field>
                <Field label="setup.fields.currency" required>
                  <Select value={form.defaultCurrency} onChange={(event) => setForm((current) => current ? { ...current, defaultCurrency: event.target.value } : current)}>
                    <option value="">setup.selectCurrency</option>
                    {currencyOptions.map((currency) => <option key={currency} value={currency}>{currency}</option>)}
                  </Select>
                </Field>
                <Field label="setup.fields.timezone" required>
                  <Select value={form.timezone} onChange={(event) => setForm((current) => current ? { ...current, timezone: event.target.value } : current)}>
                    <option value="">setup.selectTimezone</option>
                    {timezoneOptions.map((timezone) => <option key={timezone} value={timezone}>{timezone}</option>)}
                  </Select>
                </Field>
                <Field label="setup.fields.defaultLanguage">
                  <Select value={form.defaultLanguage} onChange={(event) => setForm((current) => current ? { ...current, defaultLanguage: event.target.value } : current)}>
                    <option value="en">setup.languages.en</option>
                    <option value="ar">setup.languages.ar</option>
                  </Select>
                </Field>
                <Field label="setup.fields.fiscalYearStart" required>
                  <Select value={String(form.fiscalYearStartMonth)} onChange={(event) => setForm((current) => current ? { ...current, fiscalYearStartMonth: Number(event.target.value || 1) } : current)}>
                    {monthOptions.map((month) => <option key={month.value} value={month.value}>{t(month.key)}</option>)}
                  </Select>
                </Field>
                <div className="hc-settings-form__checks">
                  <Checkbox checked={form.rtlEnabled} label="setup.fields.rtlEnabled" onChange={(event) => setForm((current) => current ? { ...current, rtlEnabled: event.target.checked } : current)} />
                </div>
              </div>
            ) : null}

            {currentStep === "setup.steps.modules" ? (
              <div className="hc-setup-modules-grid">
                <ModuleCard badge="setup.badge.core" checked={form.enableProcurement} description="setup.module.procurement.description" onChange={(checked) => setForm((current) => current ? applyDependencyRules(current, "procurement", checked, t) : current)} title="setup.module.procurement.title" includes={["setup.features.purchaseOrders", "setup.features.purchaseReceipts", "setup.module.procurement.returns"]} />
                <ModuleCard badge="setup.badge.core" checked={form.enableInventory} description="setup.module.inventory.description" onChange={(checked) => setForm((current) => current ? applyDependencyRules(current, "inventory", checked, t) : current)} title="setup.module.inventory.title" includes={["setup.module.inventory.stockLedger", "setup.features.warehouses", "setup.stock.enableStockAdjustments"]} />
                <ModuleCard badge="setup.badge.core" checked={form.enableSupplierManagement} description="setup.module.suppliers.description" onChange={(checked) => setForm((current) => current ? applyDependencyRules(current, "suppliers", checked, t) : current)} title="setup.module.suppliers.title" includes={["setup.module.suppliers.masterData", "setup.module.suppliers.statements"]} />
                <ModuleCard badge="setup.badge.requires" checked={form.enableSupplierFinancials} description="setup.module.supplierFinancials.description" disabled={!form.enableSupplierManagement} disabledReason={!form.enableSupplierManagement ? "setup.dependency.suppliersRequired" : undefined} onChange={(checked) => setForm((current) => current ? applyDependencyRules(current, "supplierFinancials", checked, t) : current)} title="setup.module.supplierFinancials.title" includes={["setup.module.supplierFinancials.payables", "setup.module.supplierFinancials.payments", "setup.module.supplierFinancials.reversals"]} />
                <ModuleCard badge="setup.badge.requires" checked={form.enableShortageManagement} description="setup.module.shortage.description" disabled={!form.enableProcurement || !form.enablePurchaseReceipts} disabledReason={!form.enableProcurement || !form.enablePurchaseReceipts ? "setup.dependency.shortageRequiresProcurement" : undefined} onChange={(checked) => setForm((current) => current ? applyDependencyRules(current, "shortage", checked, t) : current)} title="setup.module.shortage.title" includes={["setup.module.shortage.detection", "setup.module.shortage.resolution", "setup.module.shortage.status"]} />
                <ModuleCard badge="setup.badge.optional" checked={form.enableUom} description="setup.module.uom.description" onChange={(checked) => setForm((current) => current ? applyDependencyRules(current, "uom", checked, t) : current)} title="setup.module.uom.title" includes={["setup.module.uom.base", "setup.module.uom.purchase", "setup.module.uom.conversions"]} />
                <ModuleCard badge="setup.badge.requires" checked={form.enableStockAdjustments} description="setup.module.stockAdjustments.description" disabled={!form.enableInventory} disabledReason={!form.enableInventory ? "setup.dependency.inventoryRequired" : undefined} onChange={(checked) => setForm((current) => current ? applyDependencyRules(current, "stockAdjustments", checked, t) : current)} title="setup.module.stockAdjustments.title" includes={["setup.stock.enableStockAdjustments"]} />
              </div>
            ) : null}

            {currentStep === "setup.steps.workflow" ? (
              <div className="hc-setup-workflow-list">
                {form.enableProcurement ? (
                  <>
                    <WorkflowRow checked={form.requirePoBeforeReceipt} description="setup.workflow.requirePoBeforeReceiptDescription" label="setup.workflow.requirePoBeforeReceipt" onChange={(checked) => setForm((current) => current ? { ...current, requirePoBeforeReceipt: checked } : current)} />
                    <WorkflowRow checked={form.allowPartialReceipt} description="setup.workflow.allowPartialReceiptDescription" label="setup.workflow.allowPartialReceipt" onChange={(checked) => setForm((current) => current ? { ...current, allowPartialReceipt: checked } : current)} />
                  </>
                ) : null}
                {form.enableSupplierFinancials ? <WorkflowRow checked={form.enablePostingWorkflow} description="setup.workflow.autoCreatePayableDescription" label="setup.workflow.autoCreatePayable" onChange={(checked) => setForm((current) => current ? { ...current, enablePostingWorkflow: checked } : current)} /> : null}
                {form.enableShortageManagement ? <WorkflowRow checked={form.enableShortageManagement} description="setup.workflow.autoDetectShortageDescription" label="setup.workflow.autoDetectShortage" onChange={(checked) => setForm((current) => current ? applyDependencyRules(current, "shortage", checked, t) : current)} /> : null}
                {form.enableUom ? <WorkflowRow checked={form.enableUomConversion} description="setup.workflow.uomConversionDescription" label="setup.workflow.enableUomConversion" onChange={(checked) => setForm((current) => current ? applyDependencyRules(current, "uomConversion", checked, t) : current)} /> : null}
                {!form.enableProcurement && !form.enableSupplierFinancials && !form.enableShortageManagement && !form.enableUom ? <EmptyState title="setup.workflow.emptyTitle" description="setup.workflow.emptyDescription" /> : null}
              </div>
            ) : null}

            {currentStep === "setup.steps.stock" ? (
              canShowStock ? (
                <>
                  <div className="hc-settings-form__grid">
                    <Field label="setup.stock.enableWarehouses">
                      <Checkbox checked={form.enableWarehouses} label="setup.stock.enableWarehousesDescription" onChange={(event) => setForm((current) => current ? applyDependencyRules(current, "warehouses", event.target.checked, t) : current)} />
                    </Field>
                    <Field label="setup.stock.defaultWarehouseName">
                      <Input disabled={!form.enableWarehouses} value={form.defaultWarehouseName ?? ""} onChange={(event) => setForm((current) => current ? { ...current, defaultWarehouseName: event.target.value || null } : current)} />
                    </Field>
                  </div>
                  <div className="hc-settings-form__checks">
                    <Checkbox checked={form.allowNegativeStock} label="setup.stock.allowNegativeStock" onChange={(event) => setForm((current) => current ? { ...current, allowNegativeStock: event.target.checked } : current)} />
                    <Checkbox checked={form.enableStockAdjustments} label="setup.stock.enableStockAdjustments" onChange={(event) => setForm((current) => current ? applyDependencyRules(current, "stockAdjustments", event.target.checked, t) : current)} />
                  </div>
                </>
              ) : (
                <EmptyState title="setup.stock.emptyTitle" description="setup.stock.emptyDescription" />
              )
            ) : null}

            {currentStep === "setup.steps.review" ? (
              <div className="hc-setup-review">
                <div className="hc-workspace-details">
                  <div>
                    <dt>{t("setup.review.enabledModules")}</dt>
                    <dd>{enabledModuleLabels(form, t).join(", ") || t("setup.review.none")}</dd>
                  </div>
                  <div>
                    <dt>{t("setup.review.disabledModules")}</dt>
                    <dd>{disabledModuleLabels(form, t).join(", ") || t("setup.review.none")}</dd>
                  </div>
                  <div>
                    <dt>{t("setup.review.workflowRules")}</dt>
                    <dd>{workflowSummary(form, t)}</dd>
                  </div>
                  <div>
                    <dt>{t("setup.steps.stock")}</dt>
                    <dd>{stockSummary(form, t)}</dd>
                  </div>
                  <div>
                    <dt>{t("setup.review.localizationDefaults")}</dt>
                    <dd>{`${form.defaultLanguage.toUpperCase()} · ${form.timezone} · ${form.defaultCurrency}`}</dd>
                  </div>
                </div>
                {reviewWarnings.length > 0 ? (
                  <div className="hc-inline-alert hc-inline-alert--danger">
                    <strong>{t("setup.review.warningsTitle")}</strong>
                    <ul className="hc-simple-list">
                      {reviewWarnings.map((warning) => (
                        <li key={warning}>{t(warning)}</li>
                      ))}
                    </ul>
                  </div>
                ) : null}
              </div>
            ) : null}

            <div className="hc-settings-form__actions">
              <Button disabled={stepIndex === 0 || saving} variant="secondary" onClick={() => void handleBack()}>
                setup.actions.back
              </Button>
              {stepIndex < steps.length - 1 ? (
                <Button isLoading={saving} onClick={() => void handleNext()}>
                  setup.actions.next
                </Button>
              ) : (
                <Button isLoading={saving} onClick={() => void handleComplete()}>
                  setup.actions.finish
                </Button>
              )}
            </div>
          </Card>
        </>
      ) : null}
    </section>
  );
}

function stepKeyToIndex(step: string | null | undefined) {
  switch (step) {
    case "modules-features":
      return 1;
    case "workflow-rules":
      return 2;
    case "stock-warehouse":
      return 3;
    case "review-finish":
    case "completed":
      return 4;
    default:
      return 0;
  }
}

function indexToStepKey(index: number) {
  switch (index) {
    case 1:
      return "modules-features";
    case 2:
      return "workflow-rules";
    case 3:
      return "stock-warehouse";
    case 4:
      return "review-finish";
    default:
      return "company-profile";
  }
}

function enabledModuleLabels(form: SaveOrganizationSetupRequest, t: (key: string) => string) {
  return featureEntries(form).filter((entry) => entry.enabled).map((entry) => t(entry.label));
}

function disabledModuleLabels(form: SaveOrganizationSetupRequest, t: (key: string) => string) {
  return featureEntries(form).filter((entry) => !entry.enabled).map((entry) => t(entry.label));
}

function workflowSummary(form: SaveOrganizationSetupRequest, t: (key: string) => string) {
  const values = [
    form.requirePoBeforeReceipt ? t("setup.workflow.requirePoBeforeReceipt") : null,
    form.allowPartialReceipt ? t("setup.workflow.allowPartialReceipt") : null,
    form.enablePostingWorkflow ? t("setup.workflow.autoCreatePayable") : null,
    form.enableShortageManagement ? t("setup.workflow.autoDetectShortage") : null,
    form.enableUomConversion ? t("setup.workflow.enableUomConversion") : null,
  ].filter(Boolean);

  return values.length > 0 ? values.join(", ") : t("setup.review.none");
}

function stockSummary(form: SaveOrganizationSetupRequest, t: (key: string) => string) {
  if (!form.enableInventory) {
    return t("setup.stock.emptyDescription");
  }

  const values = [
    form.enableWarehouses ? t("setup.stock.enableWarehousesDescription") : t("setup.stock.warehousesDisabled"),
    form.defaultWarehouseName ? `${t("setup.stock.defaultWarehouseName")}: ${form.defaultWarehouseName}` : null,
    form.allowNegativeStock ? t("setup.stock.allowNegativeStock") : null,
    form.enableStockAdjustments ? t("setup.stock.enableStockAdjustments") : null,
  ].filter(Boolean);

  return values.join(", ");
}

function featureEntries(form: SaveOrganizationSetupRequest) {
  return [
    { enabled: form.enableProcurement, label: "setup.features.procurement" },
    { enabled: form.enableInventory, label: "setup.features.inventory" },
    { enabled: form.enableSupplierManagement, label: "setup.features.supplierManagement" },
    { enabled: form.enableSupplierFinancials, label: "setup.features.supplierFinancials" },
    { enabled: form.enableShortageManagement, label: "setup.features.shortageManagement" },
    { enabled: form.enableUom, label: "setup.features.uom" },
    { enabled: form.enableUomConversion, label: "setup.features.uomConversion" },
    { enabled: form.enableStockAdjustments, label: "setup.module.stockAdjustments.title" },
  ];
}

function validateCompanyProfile(form: SaveOrganizationSetupRequest, t: (key: string) => string) {
  if (!form.name.trim()) {
    return t("setup.validation.organizationNameRequired");
  }

  if (!form.defaultCurrency.trim()) {
    return t("setup.validation.currencyRequired");
  }

  if (!form.timezone.trim()) {
    return t("setup.validation.timezoneRequired");
  }

  if (form.fiscalYearStartMonth < 1 || form.fiscalYearStartMonth > 12) {
    return t("setup.validation.fiscalYearStartRequired");
  }

  return null;
}

function validateStockSetup(form: SaveOrganizationSetupRequest, t: (key: string) => string) {
  if (!form.enableInventory) {
    return null;
  }

  if (form.enableWarehouses && !form.defaultWarehouseName?.trim() && !form.defaultWarehouseId) {
    return t("setup.validation.defaultWarehouseNameRequired");
  }

  return null;
}

function applyDependencyRules(form: SaveOrganizationSetupRequest, key: ModuleKey, enabled: boolean, t: (key: string) => string): SaveOrganizationSetupRequest {
  const next = { ...form };

  if (key === "procurement") {
    next.enableProcurement = enabled;
    next.enablePurchaseOrders = enabled;
    next.enablePurchaseReceipts = enabled;
    if (!enabled && next.enableShortageManagement) {
      if (window.confirm(t("setup.confirm.disableProcurement"))) {
        next.enableShortageManagement = false;
      }
      next.enableShortageManagement = false;
    }
  }

  if (key === "inventory") {
    next.enableInventory = enabled;
    if (!enabled) {
      if (next.enableStockAdjustments) {
        window.confirm(t("setup.confirm.disableInventory"));
      }
      next.enableStockAdjustments = false;
      next.enableWarehouses = false;
      next.defaultWarehouseId = null;
    }
  }

  if (key === "suppliers") {
    next.enableSupplierManagement = enabled;
    if (!enabled) {
      if (next.enableSupplierFinancials) {
        window.confirm(t("setup.confirm.disableSuppliers"));
      }
      next.enableSupplierFinancials = false;
    }
  }

  if (key === "supplierFinancials") {
    if (!next.enableSupplierManagement) {
      return next;
    }
    next.enableSupplierFinancials = enabled;
  }

  if (key === "shortage") {
    if (!next.enableProcurement || !next.enablePurchaseReceipts) {
      return next;
    }
    next.enableShortageManagement = enabled;
  }

  if (key === "uom") {
    next.enableUom = enabled;
    if (!enabled) {
      if (next.enableUomConversion) {
        window.confirm(t("setup.confirm.disableUom"));
      }
      next.enableUomConversion = false;
    }
  }

  if (key === "uomConversion") {
    if (!next.enableUom) {
      return next;
    }
    next.enableUomConversion = enabled;
  }

  if (key === "warehouses") {
    next.enableWarehouses = enabled;
    if (!enabled) {
      next.defaultWarehouseId = null;
      next.defaultWarehouseName = null;
    }
  }

  if (key === "stockAdjustments") {
    if (!next.enableInventory) {
      return next;
    }
    next.enableStockAdjustments = enabled;
  }

  next.enableComponentsBom = false;
  next.enableMultipleWarehouses = false;
  next.enableBatchTracking = false;
  next.enableSerialTracking = false;
  next.enableExpiryTracking = false;
  next.enableStockTransfers = false;
  next.allowOverReceipt = false;
  next.overReceiptTolerancePercent = 0;
  next.lockPostedDocuments = false;
  next.requireApprovalBeforePosting = false;
  next.requireReasonForCancelOrReversal = false;
  next.allowDirectPurchaseReceipt = !next.requirePoBeforeReceipt;
  next.enableReversals = next.enableSupplierFinancials;

  return next;
}

function ModuleCard(props: {
  badge: string;
  checked: boolean;
  description: string;
  disabled?: boolean;
  disabledReason?: string;
  includes: string[];
  onChange: (checked: boolean) => void;
  title: string;
}) {
  const { t } = useI18n();

  return (
    <article className={`hc-setup-module-card ${props.checked ? "hc-setup-module-card--enabled" : ""} ${props.disabled ? "hc-setup-module-card--disabled" : ""}`}>
      <div className="hc-setup-module-card__header">
        <h3>{t(props.title)}</h3>
        <Badge tone={props.disabled ? "warning" : props.checked ? "success" : "neutral"}>{t(props.badge)}</Badge>
      </div>
      <p>{t(props.description)}</p>
      <ul className="hc-simple-list">
        {props.includes.map((item) => <li key={item}>{t(item)}</li>)}
      </ul>
      {props.disabledReason ? <p className="hc-setup-module-card__reason">{t(props.disabledReason)}</p> : null}
      <Checkbox checked={props.checked} disabled={props.disabled} label={props.checked ? "setup.state.enabled" : "setup.state.disabled"} onChange={(event) => props.onChange(event.target.checked)} />
    </article>
  );
}

function WorkflowRow(props: { checked: boolean; description: string; label: string; onChange: (checked: boolean) => void }) {
  const { t } = useI18n();

  return (
    <div className="hc-setup-workflow-row">
      <div>
        <p className="hc-setup-workflow-row__title">{t(props.label)}</p>
        <p className="hc-setup-workflow-row__description">{t(props.description)}</p>
      </div>
      <Checkbox checked={props.checked} label={props.checked ? "setup.state.enabled" : "setup.state.disabled"} onChange={(event) => props.onChange(event.target.checked)} />
    </div>
  );
}

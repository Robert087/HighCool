import { useEffect, useMemo, useState, type FormEvent } from "react";
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
import { DocumentPageLayout, DocumentSection } from "../components/patterns";
import { Badge, Button, Checkbox, EmptyState, Field, Input, Select, SkeletonLoader, Textarea, useI18n, useToast } from "../components/ui";
import { useAuth } from "../features/auth/AuthProvider";
import { ApiError, type ValidationErrors } from "../services/api";
import { Permissions } from "../services/permissions";
import {
  createPriceList,
  getPriceList,
  mapPriceListToFormValues,
  updatePriceList,
  type PriceList,
  type PriceListFormValues,
} from "../services/pricingApi";

const INITIAL_VALUES: PriceListFormValues = {
  code: "",
  name: "",
  type: "Selling",
  currency: "",
  isDefault: false,
  isActive: true,
  description: "",
};

export function validatePriceListForm(values: PriceListFormValues): ValidationErrors {
  const errors: ValidationErrors = {};
  if (!values.code.trim()) errors.code = ["module.pricing.validation.codeRequired"];
  if (!values.name.trim()) errors.name = ["module.pricing.validation.nameRequired"];
  if (!/^[A-Za-z]{3}$/.test(values.currency.trim())) errors.currency = ["module.pricing.validation.currency"];
  if (values.isDefault && !values.isActive) errors.isDefault = ["module.pricing.validation.defaultActive"];
  return errors;
}

export function PriceListFormPage() {
  const { t } = useI18n();
  const { showToast } = useToast();
  const { hasPermission } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const { priceListId } = useParams();
  const isExisting = Boolean(priceListId);
  const isReadOnly = isExisting && !location.pathname.endsWith("/edit");
  const canManage = hasPermission(Permissions.PricingPriceListManage);
  const editable = canManage && !isReadOnly;
  const [priceList, setPriceList] = useState<PriceList | null>(null);
  const [values, setValues] = useState<PriceListFormValues>(INITIAL_VALUES);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [formError, setFormError] = useState("");
  const [loading, setLoading] = useState(isExisting);
  const [saving, setSaving] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);
  const formId = useMemo(() => `price-list-form-${priceListId ?? "new"}`, [priceListId]);

  useEffect(() => {
    if (!priceListId) return;
    const currentPriceListId = priceListId;
    let active = true;

    async function load() {
      try {
        setLoading(true);
        setFormError("");
        const result = await getPriceList(currentPriceListId);
        if (active) {
          setPriceList(result);
          setValues(mapPriceListToFormValues(result));
        }
      } catch (loadError) {
        if (active) setFormError(loadError instanceof ApiError ? loadError.message : t("module.pricing.priceLists.loadOneError"));
      } finally {
        if (active) setLoading(false);
      }
    }

    void load();
    return () => {
      active = false;
    };
  }, [priceListId, reloadKey, t]);

  function setValue<Key extends keyof PriceListFormValues>(key: Key, value: PriceListFormValues[Key]) {
    setValues((current) => ({ ...current, [key]: value }));
  }

  function errorText(key: keyof PriceListFormValues) {
    const first = errors[key]?.[0];
    return first ? t(first) : "";
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!editable) return;

    const nextErrors = validatePriceListForm(values);
    setErrors(nextErrors);
    setFormError("");
    if (Object.keys(nextErrors).length > 0) return;

    try {
      setSaving(true);
      if (priceListId) {
        await updatePriceList(priceListId, values);
        showToast({ tone: "success", title: t("module.pricing.priceLists.updatedTitle"), description: t("module.pricing.priceLists.updatedDescription") });
      } else {
        await createPriceList(values);
        showToast({ tone: "success", title: t("module.pricing.priceLists.createdTitle"), description: t("module.pricing.priceLists.createdDescription") });
      }
      navigate("/price-lists");
    } catch (submitError) {
      if (submitError instanceof ApiError) {
        setErrors(submitError.validationErrors ?? {});
        setFormError(submitError.message);
      } else {
        setFormError(t("module.pricing.priceLists.saveError"));
      }
    } finally {
      setSaving(false);
    }
  }

  function renderActions() {
    return (
      <>
        <Link className="hc-button hc-button--secondary hc-button--md" to="/price-lists">{t("module.pricing.priceLists.back")}</Link>
        {isReadOnly && canManage && priceListId ? <Link className="hc-button hc-button--primary hc-button--md" to={`/price-lists/${priceListId}/edit`}>{t("common.edit")}</Link> : null}
        {editable ? <Button form={formId} isLoading={saving} type="submit">{isExisting ? "module.pricing.priceLists.save" : "module.pricing.priceLists.create"}</Button> : null}
      </>
    );
  }

  return (
    <DocumentPageLayout
      actions={renderActions()}
      description={isReadOnly ? "module.pricing.priceLists.detailDescription" : "module.pricing.priceLists.formDescription"}
      eyebrow="route.section.inventory"
      footer={renderActions()}
      status={priceList ? <Badge tone={priceList.isActive ? "success" : "neutral"}>{priceList.isActive ? t("status.active") : t("status.inactive")}</Badge> : undefined}
      title={isReadOnly ? "module.pricing.priceLists.detailTitle" : isExisting ? "module.pricing.priceLists.editTitle" : "module.pricing.priceLists.createTitle"}
    >
      {loading ? <div className="hc-document-section"><div className="hc-skeleton-stack"><SkeletonLoader height="2.75rem" variant="rect" /><SkeletonLoader height="2.75rem" variant="rect" /><SkeletonLoader height="2.75rem" variant="rect" /></div></div> : null}
      {!loading && formError && isExisting && !priceList ? <div className="hc-document-section"><EmptyState title="module.pricing.priceLists.errorTitle" description={formError} action={<Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>common.retry</Button>} /></div> : null}
      {!loading && (!isExisting || priceList) ? (
        <form className="hc-document-form" id={formId} onSubmit={handleSubmit}>
          {formError ? <div className="hc-inline-error">{formError}</div> : null}
          <DocumentSection title="module.pricing.priceLists.identitySection" description="module.pricing.priceLists.identityDescription">
            <div className="hc-document-form-grid">
              <Field label={t("module.pricing.code")} required>
                <Input disabled={!editable || saving} value={values.code} onChange={(event) => setValue("code", event.target.value)} />
                {errors.code ? <small className="hc-field-error">{errorText("code")}</small> : null}
              </Field>
              <Field label={t("module.pricing.name")} required>
                <Input disabled={!editable || saving} value={values.name} onChange={(event) => setValue("name", event.target.value)} />
                {errors.name ? <small className="hc-field-error">{errorText("name")}</small> : null}
              </Field>
              <Field label={t("module.pricing.type")} required>
                <Select disabled={!editable || saving} value={values.type} onChange={(event) => setValue("type", event.target.value as PriceListFormValues["type"])}>
                  <option value="Selling">{t("module.pricing.type.Selling")}</option>
                  <option value="Buying">{t("module.pricing.type.Buying")}</option>
                </Select>
              </Field>
              <Field label={t("module.pricing.currency")} required>
                <Input disabled={!editable || saving} maxLength={3} value={values.currency} onChange={(event) => setValue("currency", event.target.value.toUpperCase())} />
                {errors.currency ? <small className="hc-field-error">{errorText("currency")}</small> : null}
              </Field>
            </div>
          </DocumentSection>
          <DocumentSection title="module.pricing.priceLists.settingsSection" description="module.pricing.priceLists.settingsDescription">
            <div className="hc-document-form-grid">
              <Field label={t("table.status")}>
                <Checkbox checked={values.isActive} disabled={!editable || saving} label={t("status.active")} onChange={(event) => setValue("isActive", event.target.checked)} />
              </Field>
              <Field label={t("module.pricing.default")}>
                <Checkbox checked={values.isDefault} disabled={!editable || saving} label={t("module.pricing.default")} onChange={(event) => setValue("isDefault", event.target.checked)} />
                {errors.isDefault ? <small className="hc-field-error">{errorText("isDefault")}</small> : null}
              </Field>
              <Field className="hc-document-field--span-full" label={t("module.pricing.description")}>
                <Textarea disabled={!editable || saving} value={values.description} onChange={(event) => setValue("description", event.target.value)} />
              </Field>
            </div>
          </DocumentSection>
        </form>
      ) : null}
    </DocumentPageLayout>
  );
}

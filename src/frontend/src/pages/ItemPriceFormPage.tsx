import { useEffect, useMemo, useState, type FormEvent } from "react";
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
import { DocumentPageLayout, DocumentSection } from "../components/patterns";
import { Badge, Button, Checkbox, EmptyState, Field, Input, Select, SkeletonLoader, Textarea, useI18n, useToast } from "../components/ui";
import { useAuth } from "../features/auth/AuthProvider";
import { ApiError, type ValidationErrors } from "../services/api";
import { Permissions } from "../services/permissions";
import {
  createItemPrice,
  getItemPrice,
  getItemPricingUomOptions,
  getPricingFilterOptions,
  mapItemPriceToFormValues,
  updateItemPrice,
  type ItemPrice,
  type ItemPriceFormValues,
  type PricingFilterOptions,
} from "../services/pricingApi";

const today = () => new Date().toISOString().slice(0, 10);

const INITIAL_VALUES: ItemPriceFormValues = {
  priceListId: "",
  itemId: "",
  uomId: "",
  currency: "",
  rate: "",
  minimumQuantity: 1,
  validFrom: today(),
  validTo: "",
  isActive: true,
  notes: "",
};

export function validateItemPriceForm(values: ItemPriceFormValues): ValidationErrors {
  const errors: ValidationErrors = {};
  const rate = values.rate === "" ? Number.NaN : Number(values.rate);
  const minimumQuantity = values.minimumQuantity === "" ? Number.NaN : Number(values.minimumQuantity);
  if (!values.priceListId) errors.priceListId = ["module.pricing.validation.priceListRequired"];
  if (!values.itemId) errors.itemId = ["module.pricing.validation.itemRequired"];
  if (!values.uomId) errors.uomId = ["module.pricing.validation.uomRequired"];
  if (!Number.isFinite(rate) || rate <= 0) errors.rate = ["module.pricing.validation.rate"];
  if (!Number.isFinite(minimumQuantity) || minimumQuantity <= 0) errors.minimumQuantity = ["module.pricing.validation.minimumQuantity"];
  if (!values.validFrom) errors.validFrom = ["module.pricing.validation.validFrom"];
  if (values.validTo && values.validFrom && values.validTo < values.validFrom) errors.validTo = ["module.pricing.validation.validTo"];
  return errors;
}

export function derivePriceListCurrency(priceLists: PricingFilterOptions["priceLists"], priceListId: string) {
  return priceLists.find((option) => option.id === priceListId)?.currency ?? "";
}

export function ItemPriceFormPage() {
  const { t } = useI18n();
  const { showToast } = useToast();
  const { hasPermission } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const { itemPriceId } = useParams();
  const isExisting = Boolean(itemPriceId);
  const isReadOnly = isExisting && !location.pathname.endsWith("/edit");
  const canManage = hasPermission(Permissions.PricingItemPriceManage);
  const editable = canManage && !isReadOnly;
  const [itemPrice, setItemPrice] = useState<ItemPrice | null>(null);
  const [options, setOptions] = useState<PricingFilterOptions>({ priceLists: [], items: [], uoms: [], categories: [], currencies: [] });
  const [uomOptions, setUomOptions] = useState<PricingFilterOptions["uoms"]>([]);
  const [values, setValues] = useState<ItemPriceFormValues>(INITIAL_VALUES);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [formError, setFormError] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);
  const formId = useMemo(() => `item-price-form-${itemPriceId ?? "new"}`, [itemPriceId]);
  const displayCurrency = values.currency || t("module.pricing.derivedOnSave");

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        setFormError("");
        const [filterOptions, existing] = await Promise.all([
          getPricingFilterOptions(),
          itemPriceId ? getItemPrice(itemPriceId) : Promise.resolve(null),
        ]);
        if (active) {
          setOptions(filterOptions);
          if (existing) {
            setItemPrice(existing);
            setValues(mapItemPriceToFormValues(existing));
          }
        }
      } catch (loadError) {
        if (active) setFormError(loadError instanceof ApiError ? loadError.message : t("module.pricing.itemPrices.loadOneError"));
      } finally {
        if (active) setLoading(false);
      }
    }

    void load();
    return () => {
      active = false;
    };
  }, [itemPriceId, reloadKey, t]);

  useEffect(() => {
    let active = true;

    async function loadItemUoms() {
      if (!values.itemId) {
        setUomOptions([]);
        return;
      }

      try {
        const result = await getItemPricingUomOptions(values.itemId);
        if (active) {
          setUomOptions(result.uoms);
          setValues((current) => current.uomId && !result.uoms.some((option) => option.id === current.uomId)
            ? { ...current, uomId: "" }
            : current);
        }
      } catch {
        if (active) {
          setUomOptions([]);
        }
      }
    }

    void loadItemUoms();
    return () => {
      active = false;
    };
  }, [values.itemId]);

  function setValue<Key extends keyof ItemPriceFormValues>(key: Key, value: ItemPriceFormValues[Key]) {
    setValues((current) => ({ ...current, [key]: value }));
  }

  function handlePriceListChange(priceListId: string) {
    setValues((current) => ({ ...current, priceListId, currency: derivePriceListCurrency(options.priceLists, priceListId) }));
  }

  function errorText(key: keyof ItemPriceFormValues) {
    const first = errors[key]?.[0];
    return first ? t(first) : "";
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!editable) return;

    const nextErrors = validateItemPriceForm(values);
    setErrors(nextErrors);
    setFormError("");
    if (Object.keys(nextErrors).length > 0) return;

    try {
      setSaving(true);
      if (itemPriceId) {
        await updateItemPrice(itemPriceId, values);
        showToast({ tone: "success", title: t("module.pricing.itemPrices.updatedTitle"), description: t("module.pricing.itemPrices.updatedDescription") });
      } else {
        await createItemPrice(values);
        showToast({ tone: "success", title: t("module.pricing.itemPrices.createdTitle"), description: t("module.pricing.itemPrices.createdDescription") });
      }
      navigate("/item-prices");
    } catch (submitError) {
      if (submitError instanceof ApiError) {
        setErrors(submitError.validationErrors ?? {});
        setFormError(submitError.message);
      } else {
        setFormError(t("module.pricing.itemPrices.saveError"));
      }
    } finally {
      setSaving(false);
    }
  }

  function renderActions() {
    return (
      <>
        <Link className="hc-button hc-button--secondary hc-button--md" to="/item-prices">{t("module.pricing.itemPrices.back")}</Link>
        {isReadOnly && canManage && itemPriceId ? <Link className="hc-button hc-button--primary hc-button--md" to={`/item-prices/${itemPriceId}/edit`}>{t("common.edit")}</Link> : null}
        {editable ? <Button form={formId} isLoading={saving} type="submit">{isExisting ? "module.pricing.itemPrices.save" : "module.pricing.itemPrices.create"}</Button> : null}
      </>
    );
  }

  return (
    <DocumentPageLayout
      actions={renderActions()}
      description={isReadOnly ? "module.pricing.itemPrices.detailDescription" : "module.pricing.itemPrices.formDescription"}
      eyebrow="route.section.inventory"
      footer={renderActions()}
      status={itemPrice ? <Badge tone={itemPrice.isActive ? "success" : "neutral"}>{itemPrice.isActive ? t("status.active") : t("status.inactive")}</Badge> : undefined}
      title={isReadOnly ? "module.pricing.itemPrices.detailTitle" : isExisting ? "module.pricing.itemPrices.editTitle" : "module.pricing.itemPrices.createTitle"}
    >
      {loading ? <div className="hc-document-section"><div className="hc-skeleton-stack"><SkeletonLoader height="2.75rem" variant="rect" /><SkeletonLoader height="2.75rem" variant="rect" /><SkeletonLoader height="2.75rem" variant="rect" /></div></div> : null}
      {!loading && formError && isExisting && !itemPrice ? <div className="hc-document-section"><EmptyState title="module.pricing.itemPrices.errorTitle" description={formError} action={<Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>common.retry</Button>} /></div> : null}
      {!loading && (!isExisting || itemPrice) ? (
        <form className="hc-document-form" id={formId} onSubmit={handleSubmit}>
          {formError ? <div className="hc-inline-error">{formError}</div> : null}
          <DocumentSection title="module.pricing.itemPrices.scopeSection" description="module.pricing.itemPrices.scopeDescription">
            <div className="hc-document-form-grid">
              <Field label={t("module.pricing.priceList")} required>
                <Select disabled={!editable || saving} value={values.priceListId} onChange={(event) => handlePriceListChange(event.target.value)}>
                  <option value="">{t("module.pricing.selectPriceList")}</option>
                  {options.priceLists.map((option) => <option key={option.id} value={option.id}>{option.code} - {option.name}</option>)}
                </Select>
                {errors.priceListId ? <small className="hc-field-error">{errorText("priceListId")}</small> : null}
              </Field>
              <Field label={t("table.item")} required>
                <Select disabled={!editable || saving} value={values.itemId} onChange={(event) => setValue("itemId", event.target.value)}>
                  <option value="">{t("module.pricing.selectItem")}</option>
                  {options.items.map((option) => <option key={option.id} value={option.id}>{option.code} - {option.name}</option>)}
                </Select>
                {errors.itemId ? <small className="hc-field-error">{errorText("itemId")}</small> : null}
              </Field>
              <Field label={t("table.uom")} required>
                <Select disabled={!editable || saving} value={values.uomId} onChange={(event) => setValue("uomId", event.target.value)}>
                  <option value="">{t("module.pricing.selectUom")}</option>
                  {uomOptions.map((option) => <option key={option.id} value={option.id}>{option.code} - {option.name}</option>)}
                </Select>
                {errors.uomId ? <small className="hc-field-error">{errorText("uomId")}</small> : null}
              </Field>
              <Field label={t("module.pricing.currency")}>
                <Input disabled value={displayCurrency} onChange={() => undefined} />
              </Field>
            </div>
          </DocumentSection>
          <DocumentSection title="module.pricing.itemPrices.rateSection" description="module.pricing.itemPrices.rateDescription">
            <div className="hc-document-form-grid">
              <Field label={t("module.pricing.rate")} required>
                <Input disabled={!editable || saving} min="0.000001" step="0.000001" type="number" value={values.rate} onChange={(event) => setValue("rate", event.target.value === "" ? "" : Number(event.target.value))} />
                {errors.rate ? <small className="hc-field-error">{errorText("rate")}</small> : null}
              </Field>
              <Field label={t("module.pricing.minimumQuantity")} required>
                <Input disabled={!editable || saving} min="0.000001" step="0.000001" type="number" value={values.minimumQuantity} onChange={(event) => setValue("minimumQuantity", event.target.value === "" ? "" : Number(event.target.value))} />
                {errors.minimumQuantity ? <small className="hc-field-error">{errorText("minimumQuantity")}</small> : null}
              </Field>
              <Field label={t("module.pricing.validFrom")} required>
                <Input disabled={!editable || saving} type="date" value={values.validFrom} onChange={(event) => setValue("validFrom", event.target.value)} />
                {errors.validFrom ? <small className="hc-field-error">{errorText("validFrom")}</small> : null}
              </Field>
              <Field label={t("module.pricing.validTo")}>
                <Input disabled={!editable || saving} type="date" value={values.validTo} onChange={(event) => setValue("validTo", event.target.value)} />
                {errors.validTo ? <small className="hc-field-error">{errorText("validTo")}</small> : null}
              </Field>
              <Field label={t("table.status")}>
                <Checkbox checked={values.isActive} disabled={!editable || saving} label={t("status.active")} onChange={(event) => setValue("isActive", event.target.checked)} />
              </Field>
              <Field className="hc-document-field--span-full" label={t("module.pricing.notes")}>
                <Textarea disabled={!editable || saving} value={values.notes} onChange={(event) => setValue("notes", event.target.value)} />
              </Field>
            </div>
          </DocumentSection>
        </form>
      ) : null}
    </DocumentPageLayout>
  );
}

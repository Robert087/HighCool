import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ApiError, type ValidationErrors } from "../services/api";
import { FormPageLayout, FormSection } from "../components/patterns";
import { Button, Checkbox, EmptyState, Field, Input, Select, SkeletonLoader, useToast } from "../components/ui";
import {
  createItemUomConversion,
  getItemUomConversion,
  listItems,
  listUoms,
  updateItemUomConversion,
  type Item,
  type ItemUomConversionFormValues,
  type RoundingMode,
  type Uom,
} from "../services/masterDataApi";

const initialValues: ItemUomConversionFormValues = {
  itemId: "",
  fromUomId: "",
  toUomId: "",
  factor: 1,
  roundingMode: "None",
  minFraction: 0,
  isActive: true,
};

const roundingModes: RoundingMode[] = ["None", "Round", "Floor", "Ceiling"];

export function ItemUomConversionFormPage() {
  const { showToast } = useToast();
  const navigate = useNavigate();
  const { itemUomConversionId } = useParams();
  const isEdit = Boolean(itemUomConversionId);
  const [values, setValues] = useState<ItemUomConversionFormValues>(initialValues);
  const [items, setItems] = useState<Item[]>([]);
  const [uoms, setUoms] = useState<Uom[]>([]);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [formError, setFormError] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        const [itemList, uomList, row] = await Promise.all([
          listItems("", "active"),
          listUoms("", "active"),
          itemUomConversionId ? getItemUomConversion(itemUomConversionId) : Promise.resolve(null),
        ]);

        if (active) {
          setItems(itemList);
          setUoms(uomList);
          if (row) {
            setValues({
              itemId: row.itemId,
              fromUomId: row.fromUomId,
              toUomId: row.toUomId,
              factor: row.factor,
              roundingMode: row.roundingMode,
              minFraction: row.minFraction,
              isActive: row.isActive,
            });
          }
        }
      } catch (loadError) {
        if (active) {
          setFormError(loadError instanceof ApiError ? loadError.message : "Failed to load item UOM conversion form.");
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
  }, [itemUomConversionId, reloadKey]);

  function validate(currentValues: ItemUomConversionFormValues): ValidationErrors {
    const nextErrors: ValidationErrors = {};
    if (!currentValues.itemId) {
      nextErrors.itemId = ["Item is required."];
    }
    if (!currentValues.fromUomId) {
      nextErrors.fromUomId = ["From UOM is required."];
    }
    if (!currentValues.toUomId) {
      nextErrors.toUomId = ["To UOM is required."];
    }
    if (currentValues.fromUomId && currentValues.toUomId && currentValues.fromUomId === currentValues.toUomId) {
      nextErrors.toUomId = ["From UOM and To UOM must be different."];
    }
    if (currentValues.factor <= 0) {
      nextErrors.factor = ["Factor must be positive."];
    }
    if (currentValues.minFraction < 0) {
      nextErrors.minFraction = ["Min fraction cannot be negative."];
    }
    return nextErrors;
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const currentConversionId = itemUomConversionId;
    const nextErrors = validate(values);
    setErrors(nextErrors);
    setFormError("");
    if (Object.keys(nextErrors).length > 0) {
      return;
    }

    try {
      setSaving(true);
      if (currentConversionId) {
        await updateItemUomConversion(currentConversionId, values);
        showToast({ tone: "success", title: "Conversion updated", description: "The conversion rule was saved successfully." });
      } else {
        await createItemUomConversion(values);
        showToast({ tone: "success", title: "Conversion created", description: "The new conversion rule is now available." });
      }
      navigate("/item-uom-conversions");
    } catch (submitError) {
      if (submitError instanceof ApiError) {
        setErrors(submitError.validationErrors ?? {});
        setFormError(submitError.message);
      } else {
        setFormError("Failed to save item UOM conversion.");
      }
    } finally {
      setSaving(false);
    }
  }

  function setValue<Key extends keyof ItemUomConversionFormValues>(key: Key, value: ItemUomConversionFormValues[Key]) {
    setValues((current) => ({ ...current, [key]: value }));
  }

  return (
    <FormPageLayout eyebrow="Master Data" title={isEdit ? "Edit item UOM conversion" : "Create item UOM conversion"} description="Set the conversion factor between two item UOMs." actions={<Link className="hc-button hc-button--secondary hc-button--md" to="/item-uom-conversions">Back to conversions</Link>}>
      {loading ? <div className="hc-card hc-card--md"><div className="hc-skeleton-stack"><SkeletonLoader height="2.75rem" variant="rect" /><SkeletonLoader height="2.75rem" variant="rect" /><SkeletonLoader height="2.75rem" variant="rect" /><SkeletonLoader height="2.75rem" variant="rect" /></div></div> : null}
      {!loading && formError && (items.length === 0 || uoms.length === 0) ? <div className="hc-card hc-card--md"><EmptyState title="Unable to load conversion form" description={formError} action={<Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>Retry</Button>} /></div> : null}
      {!loading && (!formError || (items.length > 0 && uoms.length > 0)) ? (
        <form className="hc-form-stack" onSubmit={handleSubmit}>
          {formError ? <div className="hc-inline-error">{formError}</div> : null}
          <FormSection title="Conversion definition" description="Item, UOM pair, factor, and rounding.">
            <div className="hc-form-grid">
              <Field label="Item" required>
                <Select value={values.itemId} onChange={(event) => setValue("itemId", event.target.value)}>
                  <option value="">Select item</option>
                  {items.map((item) => (
                    <option key={item.id} value={item.id}>
                      {item.code} - {item.name}
                    </option>
                  ))}
                </Select>
                {errors.itemId ? <small className="hc-field-error">{errors.itemId[0]}</small> : null}
              </Field>
              <Field label="Rounding mode">
                <Select value={values.roundingMode} onChange={(event) => setValue("roundingMode", event.target.value as RoundingMode)}>
                  {roundingModes.map((mode) => (
                    <option key={mode} value={mode}>{mode}</option>
                  ))}
                </Select>
              </Field>
            </div>
            <div className="hc-form-grid">
              <Field label="From UOM" required>
                <Select value={values.fromUomId} onChange={(event) => setValue("fromUomId", event.target.value)}>
                  <option value="">Select from UOM</option>
                  {uoms.map((uom) => (
                    <option key={uom.id} value={uom.id}>
                      {uom.code} - {uom.name}
                    </option>
                  ))}
                </Select>
                {errors.fromUomId ? <small className="hc-field-error">{errors.fromUomId[0]}</small> : null}
              </Field>
              <Field label="To UOM" required>
                <Select value={values.toUomId} onChange={(event) => setValue("toUomId", event.target.value)}>
                  <option value="">Select to UOM</option>
                  {uoms.map((uom) => (
                    <option key={uom.id} value={uom.id}>
                      {uom.code} - {uom.name}
                    </option>
                  ))}
                </Select>
                {errors.toUomId ? <small className="hc-field-error">{errors.toUomId[0]}</small> : null}
              </Field>
            </div>
            <div className="hc-form-grid">
              <Field label="Factor" required><Input min={0.000001} step="0.000001" type="number" value={values.factor} onChange={(event) => setValue("factor", Number(event.target.value))} />{errors.factor ? <small className="hc-field-error">{errors.factor[0]}</small> : null}</Field>
              <Field label="Min fraction"><Input min={0} step="0.000001" type="number" value={values.minFraction} onChange={(event) => setValue("minFraction", Number(event.target.value))} />{errors.minFraction ? <small className="hc-field-error">{errors.minFraction[0]}</small> : null}</Field>
            </div>
            <Checkbox checked={values.isActive} label="Active conversion rule" onChange={(event) => setValue("isActive", event.target.checked)} />
          </FormSection>
          <div className="hc-form-actions"><Link className="hc-button hc-button--ghost hc-button--md" to="/item-uom-conversions">Cancel</Link><Button isLoading={saving} type="submit">{isEdit ? "Save conversion" : "Create conversion"}</Button></div>
        </form>
      ) : null}
    </FormPageLayout>
  );
}

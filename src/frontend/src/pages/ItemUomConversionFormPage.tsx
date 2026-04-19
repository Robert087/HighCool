import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ApiError, type ValidationErrors } from "../services/api";
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
  }, [itemUomConversionId]);

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
      } else {
        await createItemUomConversion(values);
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
    <section className="page-section">
      <div className="page-header">
        <div>
          <p className="eyebrow">Master Data</p>
          <h2>{isEdit ? "Edit item UOM conversion" : "Create item UOM conversion"}</h2>
          <p className="page-copy">Define an item-specific conversion factor and rounding behavior between two UOMs.</p>
        </div>
        <Link className="secondary-link" to="/item-uom-conversions">
          Back to conversions
        </Link>
      </div>

      {loading ? <p className="feedback">Loading item UOM conversion form...</p> : null}

      {!loading ? (
        <form className="entity-form" onSubmit={handleSubmit}>
          {formError ? <p className="feedback error">{formError}</p> : null}

          <label className="form-field">
            <span>Item</span>
            <select className="select-input" value={values.itemId} onChange={(event) => setValue("itemId", event.target.value)}>
              <option value="">Select item</option>
              {items.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.code} - {item.name}
                </option>
              ))}
            </select>
            {errors.itemId ? <small className="field-error">{errors.itemId[0]}</small> : null}
          </label>

          <label className="form-field">
            <span>From UOM</span>
            <select className="select-input" value={values.fromUomId} onChange={(event) => setValue("fromUomId", event.target.value)}>
              <option value="">Select from UOM</option>
              {uoms.map((uom) => (
                <option key={uom.id} value={uom.id}>
                  {uom.code} - {uom.name}
                </option>
              ))}
            </select>
            {errors.fromUomId ? <small className="field-error">{errors.fromUomId[0]}</small> : null}
          </label>

          <label className="form-field">
            <span>To UOM</span>
            <select className="select-input" value={values.toUomId} onChange={(event) => setValue("toUomId", event.target.value)}>
              <option value="">Select to UOM</option>
              {uoms.map((uom) => (
                <option key={uom.id} value={uom.id}>
                  {uom.code} - {uom.name}
                </option>
              ))}
            </select>
            {errors.toUomId ? <small className="field-error">{errors.toUomId[0]}</small> : null}
          </label>

          <label className="form-field">
            <span>Factor</span>
            <input className="text-input" min={0.000001} step="0.000001" type="number" value={values.factor} onChange={(event) => setValue("factor", Number(event.target.value))} />
            {errors.factor ? <small className="field-error">{errors.factor[0]}</small> : null}
          </label>

          <label className="form-field">
            <span>Rounding mode</span>
            <select className="select-input" value={values.roundingMode} onChange={(event) => setValue("roundingMode", event.target.value as RoundingMode)}>
              {roundingModes.map((mode) => (
                <option key={mode} value={mode}>
                  {mode}
                </option>
              ))}
            </select>
          </label>

          <label className="form-field">
            <span>Min fraction</span>
            <input className="text-input" min={0} step="0.000001" type="number" value={values.minFraction} onChange={(event) => setValue("minFraction", Number(event.target.value))} />
            {errors.minFraction ? <small className="field-error">{errors.minFraction[0]}</small> : null}
          </label>

          <label className="checkbox-field">
            <input checked={values.isActive} onChange={(event) => setValue("isActive", event.target.checked)} type="checkbox" />
            <span>Active conversion rule</span>
          </label>

          <div className="form-actions">
            <Link className="secondary-link" to="/item-uom-conversions">
              Cancel
            </Link>
            <button className="primary-button" disabled={saving} type="submit">
              {saving ? "Saving..." : isEdit ? "Save conversion" : "Create conversion"}
            </button>
          </div>
        </form>
      ) : null}
    </section>
  );
}

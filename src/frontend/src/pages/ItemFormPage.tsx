import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ApiError, type ValidationErrors } from "../services/api";
import {
  createItem,
  getItem,
  listUoms,
  updateItem,
  type ItemFormValues,
  type Uom,
} from "../services/masterDataApi";

const initialValues: ItemFormValues = {
  code: "",
  name: "",
  baseUomId: "",
  isActive: true,
  isSellable: true,
  isComponent: false,
};

export function ItemFormPage() {
  const navigate = useNavigate();
  const { itemId } = useParams();
  const isEdit = Boolean(itemId);
  const [values, setValues] = useState<ItemFormValues>(initialValues);
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
        const [uomList, item] = await Promise.all([
          listUoms("", "active"),
          itemId ? getItem(itemId) : Promise.resolve(null),
        ]);

        if (active) {
          setUoms(uomList);
          if (item) {
            setValues({
              code: item.code,
              name: item.name,
              baseUomId: item.baseUomId,
              isActive: item.isActive,
              isSellable: item.isSellable,
              isComponent: item.isComponent,
            });
          } else if (uomList.length > 0) {
            setValues((current) => ({ ...current, baseUomId: current.baseUomId || uomList[0].id }));
          }
        }
      } catch (loadError) {
        if (active) {
          setFormError(loadError instanceof ApiError ? loadError.message : "Failed to load item form.");
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
  }, [itemId]);

  function validate(currentValues: ItemFormValues): ValidationErrors {
    const nextErrors: ValidationErrors = {};

    if (!currentValues.code.trim()) {
      nextErrors.code = ["Code is required."];
    }

    if (!currentValues.name.trim()) {
      nextErrors.name = ["Name is required."];
    }

    if (!currentValues.baseUomId) {
      nextErrors.baseUomId = ["Base UOM is required."];
    }

    if (!currentValues.isSellable && !currentValues.isComponent) {
      nextErrors.isSellable = ["Item must be sellable, component, or both."];
    }

    return nextErrors;
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const currentItemId = itemId;
    const nextErrors = validate(values);
    setErrors(nextErrors);
    setFormError("");

    if (Object.keys(nextErrors).length > 0) {
      return;
    }

    try {
      setSaving(true);

      if (currentItemId) {
        await updateItem(currentItemId, values);
      } else {
        await createItem(values);
      }

      navigate("/items");
    } catch (submitError) {
      if (submitError instanceof ApiError) {
        setErrors(submitError.validationErrors ?? {});
        setFormError(submitError.message);
      } else {
        setFormError("Failed to save item.");
      }
    } finally {
      setSaving(false);
    }
  }

  function setValue<Key extends keyof ItemFormValues>(key: Key, value: ItemFormValues[Key]) {
    setValues((current) => ({ ...current, [key]: value }));
  }

  return (
    <section className="page-section">
      <div className="page-header">
        <div>
          <p className="eyebrow">Master Data</p>
          <h2>{isEdit ? "Edit item" : "Create item"}</h2>
          <p className="page-copy">Define item identity, base UOM, and whether the item is sellable, component, or both.</p>
        </div>
        <Link className="secondary-link" to="/items">
          Back to items
        </Link>
      </div>

      {loading ? <p className="feedback">Loading item form...</p> : null}

      {!loading ? (
        <form className="entity-form" onSubmit={handleSubmit}>
          {formError ? <p className="feedback error">{formError}</p> : null}

          <label className="form-field">
            <span>Code</span>
            <input className="text-input" value={values.code} onChange={(event) => setValue("code", event.target.value)} />
            {errors.code ? <small className="field-error">{errors.code[0]}</small> : null}
          </label>

          <label className="form-field">
            <span>Name</span>
            <input className="text-input" value={values.name} onChange={(event) => setValue("name", event.target.value)} />
            {errors.name ? <small className="field-error">{errors.name[0]}</small> : null}
          </label>

          <label className="form-field">
            <span>Base UOM</span>
            <select className="select-input" value={values.baseUomId} onChange={(event) => setValue("baseUomId", event.target.value)}>
              <option value="">Select base UOM</option>
              {uoms.map((uom) => (
                <option key={uom.id} value={uom.id}>
                  {uom.code} - {uom.name}
                </option>
              ))}
            </select>
            {errors.baseUomId ? <small className="field-error">{errors.baseUomId[0]}</small> : null}
          </label>

          <label className="checkbox-field">
            <input checked={values.isSellable} onChange={(event) => setValue("isSellable", event.target.checked)} type="checkbox" />
            <span>Sellable item</span>
          </label>

          <label className="checkbox-field">
            <input checked={values.isComponent} onChange={(event) => setValue("isComponent", event.target.checked)} type="checkbox" />
            <span>Component item</span>
          </label>
          {errors.isSellable ? <small className="field-error">{errors.isSellable[0]}</small> : null}

          <label className="checkbox-field">
            <input checked={values.isActive} onChange={(event) => setValue("isActive", event.target.checked)} type="checkbox" />
            <span>Active item</span>
          </label>

          <div className="form-actions">
            <Link className="secondary-link" to="/items">
              Cancel
            </Link>
            <button className="primary-button" disabled={saving} type="submit">
              {saving ? "Saving..." : isEdit ? "Save item" : "Create item"}
            </button>
          </div>
        </form>
      ) : null}
    </section>
  );
}

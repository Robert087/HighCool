import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ApiError, type ValidationErrors } from "../services/api";
import {
  createItemComponent,
  getItemComponent,
  listItems,
  updateItemComponent,
  type Item,
  type ItemComponentFormValues,
} from "../services/masterDataApi";

const initialValues: ItemComponentFormValues = {
  parentItemId: "",
  componentItemId: "",
  quantity: 1,
};

export function ItemComponentFormPage() {
  const navigate = useNavigate();
  const { itemComponentId } = useParams();
  const isEdit = Boolean(itemComponentId);
  const [values, setValues] = useState<ItemComponentFormValues>(initialValues);
  const [items, setItems] = useState<Item[]>([]);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [formError, setFormError] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        const [itemList, row] = await Promise.all([
          listItems("", "active"),
          itemComponentId ? getItemComponent(itemComponentId) : Promise.resolve(null),
        ]);

        if (active) {
          setItems(itemList);
          if (row) {
            setValues({
              parentItemId: row.parentItemId,
              componentItemId: row.componentItemId,
              quantity: row.quantity,
            });
          }
        }
      } catch (loadError) {
        if (active) {
          setFormError(loadError instanceof ApiError ? loadError.message : "Failed to load item component form.");
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
  }, [itemComponentId]);

  function validate(currentValues: ItemComponentFormValues): ValidationErrors {
    const nextErrors: ValidationErrors = {};
    if (!currentValues.parentItemId) {
      nextErrors.parentItemId = ["Parent item is required."];
    }
    if (!currentValues.componentItemId) {
      nextErrors.componentItemId = ["Component item is required."];
    }
    if (currentValues.parentItemId && currentValues.componentItemId && currentValues.parentItemId === currentValues.componentItemId) {
      nextErrors.componentItemId = ["Parent item and component item must be different."];
    }
    if (currentValues.quantity <= 0) {
      nextErrors.quantity = ["Quantity must be positive."];
    }
    return nextErrors;
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const currentRowId = itemComponentId;
    const nextErrors = validate(values);
    setErrors(nextErrors);
    setFormError("");
    if (Object.keys(nextErrors).length > 0) {
      return;
    }

    try {
      setSaving(true);
      if (currentRowId) {
        await updateItemComponent(currentRowId, values);
      } else {
        await createItemComponent(values);
      }
      navigate("/item-components");
    } catch (submitError) {
      if (submitError instanceof ApiError) {
        setErrors(submitError.validationErrors ?? {});
        setFormError(submitError.message);
      } else {
        setFormError("Failed to save item component.");
      }
    } finally {
      setSaving(false);
    }
  }

  function setValue<Key extends keyof ItemComponentFormValues>(key: Key, value: ItemComponentFormValues[Key]) {
    setValues((current) => ({ ...current, [key]: value }));
  }

  const componentCandidates = items.filter((item) => item.isComponent);

  return (
    <section className="page-section">
      <div className="page-header">
        <div>
          <p className="eyebrow">Master Data</p>
          <h2>{isEdit ? "Edit item component" : "Create item component"}</h2>
          <p className="page-copy">Attach a component item and required quantity to a parent item.</p>
        </div>
        <Link className="secondary-link" to="/item-components">
          Back to item components
        </Link>
      </div>

      {loading ? <p className="feedback">Loading item component form...</p> : null}

      {!loading ? (
        <form className="entity-form" onSubmit={handleSubmit}>
          {formError ? <p className="feedback error">{formError}</p> : null}

          <label className="form-field">
            <span>Parent item</span>
            <select className="select-input" value={values.parentItemId} onChange={(event) => setValue("parentItemId", event.target.value)}>
              <option value="">Select parent item</option>
              {items.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.code} - {item.name}
                </option>
              ))}
            </select>
            {errors.parentItemId ? <small className="field-error">{errors.parentItemId[0]}</small> : null}
          </label>

          <label className="form-field">
            <span>Component item</span>
            <select className="select-input" value={values.componentItemId} onChange={(event) => setValue("componentItemId", event.target.value)}>
              <option value="">Select component item</option>
              {componentCandidates.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.code} - {item.name}
                </option>
              ))}
            </select>
            {errors.componentItemId ? <small className="field-error">{errors.componentItemId[0]}</small> : null}
          </label>

          <label className="form-field">
            <span>Quantity</span>
            <input
              className="text-input"
              min={0.000001}
              step="0.000001"
              type="number"
              value={values.quantity}
              onChange={(event) => setValue("quantity", Number(event.target.value))}
            />
            {errors.quantity ? <small className="field-error">{errors.quantity[0]}</small> : null}
          </label>

          <div className="form-actions">
            <Link className="secondary-link" to="/item-components">
              Cancel
            </Link>
            <button className="primary-button" disabled={saving} type="submit">
              {saving ? "Saving..." : isEdit ? "Save component row" : "Create component row"}
            </button>
          </div>
        </form>
      ) : null}
    </section>
  );
}

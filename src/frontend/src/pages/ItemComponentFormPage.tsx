import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ApiError, type ValidationErrors } from "../services/api";
import { FormPageLayout, FormSection } from "../components/patterns";
import { Button, EmptyState, Field, Input, Select, SkeletonLoader, useToast } from "../components/ui";
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
  const { showToast } = useToast();
  const navigate = useNavigate();
  const { itemComponentId } = useParams();
  const isEdit = Boolean(itemComponentId);
  const [values, setValues] = useState<ItemComponentFormValues>(initialValues);
  const [items, setItems] = useState<Item[]>([]);
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
  }, [itemComponentId, reloadKey]);

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
        showToast({ tone: "success", title: "Component row updated", description: "The component relationship was saved successfully." });
      } else {
        await createItemComponent(values);
        showToast({ tone: "success", title: "Component row created", description: "The new component relationship is now available." });
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
    <FormPageLayout eyebrow="Master Data" title={isEdit ? "Edit item component" : "Create item component"} description="Link a component item to a parent item." actions={<Link className="hc-button hc-button--secondary hc-button--md" to="/item-components">Back to item components</Link>}>
      {loading ? <div className="hc-card hc-card--md"><div className="hc-skeleton-stack"><SkeletonLoader height="2.75rem" variant="rect" /><SkeletonLoader height="2.75rem" variant="rect" /><SkeletonLoader height="2.75rem" variant="rect" /></div></div> : null}
      {!loading && formError && items.length === 0 ? <div className="hc-card hc-card--md"><EmptyState title="Unable to load item component form" description={formError} action={<Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>Retry</Button>} /></div> : null}
      {!loading && (!formError || items.length > 0) ? (
        <form className="hc-form-stack" onSubmit={handleSubmit}>
          {formError ? <div className="hc-inline-error">{formError}</div> : null}
          <FormSection title="Relationship definition" description="Parent item, component item, and quantity.">
            <div className="hc-form-grid">
              <Field label="Parent item" required>
                <Select value={values.parentItemId} onChange={(event) => setValue("parentItemId", event.target.value)}>
                  <option value="">Select parent item</option>
                  {items.map((item) => (
                    <option key={item.id} value={item.id}>
                      {item.code} - {item.name}
                    </option>
                  ))}
                </Select>
                {errors.parentItemId ? <small className="hc-field-error">{errors.parentItemId[0]}</small> : null}
              </Field>
              <Field label="Component item" required>
                <Select value={values.componentItemId} onChange={(event) => setValue("componentItemId", event.target.value)}>
                  <option value="">Select component item</option>
                  {componentCandidates.map((item) => (
                    <option key={item.id} value={item.id}>
                      {item.code} - {item.name}
                    </option>
                  ))}
                </Select>
                {errors.componentItemId ? <small className="hc-field-error">{errors.componentItemId[0]}</small> : null}
              </Field>
            </div>
            <Field label="Quantity" required hint="The quantity required per one parent item unit.">
              <Input min={0.000001} step="0.000001" type="number" value={values.quantity} onChange={(event) => setValue("quantity", Number(event.target.value))} />
              {errors.quantity ? <small className="hc-field-error">{errors.quantity[0]}</small> : null}
            </Field>
          </FormSection>
          <div className="hc-form-actions"><Link className="hc-button hc-button--ghost hc-button--md" to="/item-components">Cancel</Link><Button isLoading={saving} type="submit">{isEdit ? "Save component row" : "Create component row"}</Button></div>
        </form>
      ) : null}
    </FormPageLayout>
  );
}

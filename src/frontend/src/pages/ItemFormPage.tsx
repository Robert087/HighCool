import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ApiError, type ValidationErrors } from "../services/api";
import { FormPageLayout, FormSection } from "../components/patterns";
import { Button, Checkbox, EmptyState, Field, Input, Select, SkeletonLoader, useToast } from "../components/ui";
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
  const { showToast } = useToast();
  const navigate = useNavigate();
  const { itemId } = useParams();
  const isEdit = Boolean(itemId);
  const [values, setValues] = useState<ItemFormValues>(initialValues);
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
  }, [itemId, reloadKey]);

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
        showToast({ tone: "success", title: "Item updated", description: "The item changes were saved successfully." });
      } else {
        await createItem(values);
        showToast({ tone: "success", title: "Item created", description: "The new item is now available in the catalogue." });
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
    <FormPageLayout
      eyebrow="Master Data"
      title={isEdit ? "Edit item" : "Create item"}
      description="Set the core item details and roles."
      actions={<Link className="hc-button hc-button--secondary hc-button--md" to="/items">Back to items</Link>}
    >
      {loading ? (
        <div className="hc-card hc-card--md">
          <div className="hc-skeleton-stack">
            <SkeletonLoader height="2.75rem" variant="rect" />
            <SkeletonLoader height="2.75rem" variant="rect" />
            <SkeletonLoader height="8rem" variant="rect" />
          </div>
        </div>
      ) : null}

      {!loading && formError && uoms.length === 0 ? (
        <div className="hc-card hc-card--md">
          <EmptyState
            title="Unable to load item form"
            description={formError}
            action={<Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>Retry</Button>}
          />
        </div>
      ) : null}

      {!loading && (!formError || uoms.length > 0) ? (
        <form className="hc-form-stack" onSubmit={handleSubmit}>
          {formError ? <div className="hc-inline-error">{formError}</div> : null}

          <FormSection title="Core details" description="Name, code, and base UOM.">
            <div className="hc-form-grid">
              <Field label="Code" required>
                <Input value={values.code} onChange={(event) => setValue("code", event.target.value)} />
                {errors.code ? <small className="hc-field-error">{errors.code[0]}</small> : null}
              </Field>

              <Field label="Name" required>
                <Input value={values.name} onChange={(event) => setValue("name", event.target.value)} />
                {errors.name ? <small className="hc-field-error">{errors.name[0]}</small> : null}
              </Field>
            </div>

            <Field label="Base UOM" required hint="Used as the primary quantity unit.">
              <Select value={values.baseUomId} onChange={(event) => setValue("baseUomId", event.target.value)}>
                <option value="">Select base UOM</option>
                {uoms.map((uom) => (
                  <option key={uom.id} value={uom.id}>
                    {uom.code} - {uom.name}
                  </option>
                ))}
              </Select>
              {errors.baseUomId ? <small className="hc-field-error">{errors.baseUomId[0]}</small> : null}
            </Field>
          </FormSection>

          <FormSection title="Roles and status" description="Choose how the item is used.">
            <Checkbox checked={values.isSellable} label="Sellable item" onChange={(event) => setValue("isSellable", event.target.checked)} />
            <Checkbox checked={values.isComponent} label="Component item" onChange={(event) => setValue("isComponent", event.target.checked)} />
            {errors.isSellable ? <small className="hc-field-error">{errors.isSellable[0]}</small> : null}
            <Checkbox checked={values.isActive} label="Active item" onChange={(event) => setValue("isActive", event.target.checked)} />
          </FormSection>

          <div className="hc-form-actions">
            <Link className="hc-button hc-button--ghost hc-button--md" to="/items">Cancel</Link>
            <Button isLoading={saving} type="submit">{isEdit ? "Save item" : "Create item"}</Button>
          </div>
        </form>
      ) : null}
    </FormPageLayout>
  );
}

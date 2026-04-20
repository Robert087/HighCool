import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ApiError, type ValidationErrors } from "../services/api";
import { FormPageLayout, FormSection } from "../components/patterns";
import { Button, Checkbox, EmptyState, Field, Input, SkeletonLoader, useToast } from "../components/ui";
import {
  createWarehouse,
  getWarehouse,
  updateWarehouse,
  type WarehouseFormValues,
} from "../services/masterDataApi";

const initialValues: WarehouseFormValues = {
  code: "",
  name: "",
  location: "",
  isActive: true,
};

export function WarehouseFormPage() {
  const { showToast } = useToast();
  const navigate = useNavigate();
  const { warehouseId } = useParams();
  const isEdit = Boolean(warehouseId);
  const [values, setValues] = useState<WarehouseFormValues>(initialValues);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [formError, setFormError] = useState("");
  const [loading, setLoading] = useState(isEdit);
  const [saving, setSaving] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    if (!warehouseId) {
      return;
    }

    const currentWarehouseId: string = warehouseId;

    let active = true;

    async function load() {
      try {
        setLoading(true);
        const warehouse = await getWarehouse(currentWarehouseId);

        if (active) {
          setValues({
            code: warehouse.code,
            name: warehouse.name,
            location: warehouse.location ?? "",
            isActive: warehouse.isActive,
          });
        }
      } catch (loadError) {
        if (active) {
          setFormError(loadError instanceof ApiError ? loadError.message : "Failed to load warehouse.");
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
  }, [warehouseId, reloadKey]);

  function validate(currentValues: WarehouseFormValues): ValidationErrors {
    const nextErrors: ValidationErrors = {};

    if (!currentValues.code.trim()) {
      nextErrors.code = ["Code is required."];
    }

    if (!currentValues.name.trim()) {
      nextErrors.name = ["Name is required."];
    }

    return nextErrors;
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const currentWarehouseId = warehouseId;
    const nextErrors = validate(values);
    setErrors(nextErrors);
    setFormError("");

    if (Object.keys(nextErrors).length > 0) {
      return;
    }

    try {
      setSaving(true);

      if (currentWarehouseId) {
        await updateWarehouse(currentWarehouseId, values);
        showToast({ tone: "success", title: "Warehouse updated", description: "The warehouse changes were saved successfully." });
      } else {
        await createWarehouse(values);
        showToast({ tone: "success", title: "Warehouse created", description: "The new warehouse is now available in the register." });
      }

      navigate("/warehouses");
    } catch (submitError) {
      if (submitError instanceof ApiError) {
        setErrors(submitError.validationErrors ?? {});
        setFormError(submitError.message);
      } else {
        setFormError("Failed to save warehouse.");
      }
    } finally {
      setSaving(false);
    }
  }

  function setValue<Key extends keyof WarehouseFormValues>(key: Key, value: WarehouseFormValues[Key]) {
    setValues((current) => ({ ...current, [key]: value }));
  }

  return (
    <FormPageLayout eyebrow="Master Data" title={isEdit ? "Edit warehouse" : "Create warehouse"} description="Set the warehouse identity and location." actions={<Link className="hc-button hc-button--secondary hc-button--md" to="/warehouses">Back to warehouses</Link>}>
      {loading ? <div className="hc-card hc-card--md"><div className="hc-skeleton-stack"><SkeletonLoader height="2.75rem" variant="rect" /><SkeletonLoader height="2.75rem" variant="rect" /><SkeletonLoader height="2.75rem" variant="rect" /></div></div> : null}
      {!loading && formError && isEdit ? <div className="hc-card hc-card--md"><EmptyState title="Unable to load warehouse" description={formError} action={<Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>Retry</Button>} /></div> : null}
      {!loading && (!formError || !isEdit) ? (
        <form className="hc-form-stack" onSubmit={handleSubmit}>
          {formError ? <div className="hc-inline-error">{formError}</div> : null}
          <FormSection title="Warehouse identity" description="Code, name, and optional location.">
            <div className="hc-form-grid">
              <Field label="Code" required><Input value={values.code} onChange={(event) => setValue("code", event.target.value)} />{errors.code ? <small className="hc-field-error">{errors.code[0]}</small> : null}</Field>
              <Field label="Name" required><Input value={values.name} onChange={(event) => setValue("name", event.target.value)} />{errors.name ? <small className="hc-field-error">{errors.name[0]}</small> : null}</Field>
            </div>
            <Field label="Location" hint="Optional branch, zone, or operational reference."><Input value={values.location} onChange={(event) => setValue("location", event.target.value)} />{errors.location ? <small className="hc-field-error">{errors.location[0]}</small> : null}</Field>
            <Checkbox checked={values.isActive} label="Active warehouse" onChange={(event) => setValue("isActive", event.target.checked)} />
          </FormSection>
          <div className="hc-form-actions"><Link className="hc-button hc-button--ghost hc-button--md" to="/warehouses">Cancel</Link><Button isLoading={saving} type="submit">{isEdit ? "Save warehouse" : "Create warehouse"}</Button></div>
        </form>
      ) : null}
    </FormPageLayout>
  );
}

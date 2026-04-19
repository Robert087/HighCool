import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ApiError, type ValidationErrors } from "../services/api";
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
  const navigate = useNavigate();
  const { warehouseId } = useParams();
  const isEdit = Boolean(warehouseId);
  const [values, setValues] = useState<WarehouseFormValues>(initialValues);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [formError, setFormError] = useState("");
  const [loading, setLoading] = useState(isEdit);
  const [saving, setSaving] = useState(false);

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
  }, [warehouseId]);

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
      } else {
        await createWarehouse(values);
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
    <section className="page-section">
      <div className="page-header">
        <div>
          <p className="eyebrow">Master Data</p>
          <h2>{isEdit ? "Edit warehouse" : "Create warehouse"}</h2>
          <p className="page-copy">Define warehouses that can later participate in stock and receipt operations.</p>
        </div>
        <Link className="secondary-link" to="/warehouses">
          Back to warehouses
        </Link>
      </div>

      {loading ? <p className="feedback">Loading warehouse...</p> : null}

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
            <span>Location</span>
            <input className="text-input" value={values.location} onChange={(event) => setValue("location", event.target.value)} />
            {errors.location ? <small className="field-error">{errors.location[0]}</small> : null}
          </label>

          <label className="checkbox-field">
            <input
              checked={values.isActive}
              onChange={(event) => setValue("isActive", event.target.checked)}
              type="checkbox"
            />
            <span>Active warehouse</span>
          </label>

          <div className="form-actions">
            <Link className="secondary-link" to="/warehouses">
              Cancel
            </Link>
            <button className="primary-button" disabled={saving} type="submit">
              {saving ? "Saving..." : isEdit ? "Save warehouse" : "Create warehouse"}
            </button>
          </div>
        </form>
      ) : null}
    </section>
  );
}

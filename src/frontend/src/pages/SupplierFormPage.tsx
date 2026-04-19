import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ApiError, type ValidationErrors } from "../services/api";
import {
  createSupplier,
  getSupplier,
  updateSupplier,
  type SupplierFormValues,
} from "../services/masterDataApi";

const initialValues: SupplierFormValues = {
  code: "",
  name: "",
  statementName: "",
  phone: "",
  email: "",
  isActive: true,
};

export function SupplierFormPage() {
  const navigate = useNavigate();
  const { supplierId } = useParams();
  const isEdit = Boolean(supplierId);
  const [values, setValues] = useState<SupplierFormValues>(initialValues);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [formError, setFormError] = useState("");
  const [loading, setLoading] = useState(isEdit);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!supplierId) {
      return;
    }

    const currentSupplierId: string = supplierId;

    let active = true;

    async function load() {
      try {
        setLoading(true);
        const supplier = await getSupplier(currentSupplierId);

        if (active) {
          setValues({
            code: supplier.code,
            name: supplier.name,
            statementName: supplier.statementName,
            phone: supplier.phone ?? "",
            email: supplier.email ?? "",
            isActive: supplier.isActive,
          });
        }
      } catch (loadError) {
        if (active) {
          setFormError(loadError instanceof ApiError ? loadError.message : "Failed to load supplier.");
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
  }, [supplierId]);

  function validate(currentValues: SupplierFormValues): ValidationErrors {
    const nextErrors: ValidationErrors = {};

    if (!currentValues.code.trim()) {
      nextErrors.code = ["Code is required."];
    }

    if (!currentValues.name.trim()) {
      nextErrors.name = ["Name is required."];
    }

    if (!currentValues.statementName.trim()) {
      nextErrors.statementName = ["Statement name is required."];
    }

    if (currentValues.email && !/^\S+@\S+\.\S+$/.test(currentValues.email)) {
      nextErrors.email = ["Email format is invalid."];
    }

    return nextErrors;
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const currentSupplierId = supplierId;
    const nextErrors = validate(values);
    setErrors(nextErrors);
    setFormError("");

    if (Object.keys(nextErrors).length > 0) {
      return;
    }

    try {
      setSaving(true);

      if (currentSupplierId) {
        await updateSupplier(currentSupplierId, values);
      } else {
        await createSupplier(values);
      }

      navigate("/suppliers");
    } catch (submitError) {
      if (submitError instanceof ApiError) {
        setErrors(submitError.validationErrors ?? {});
        setFormError(submitError.message);
      } else {
        setFormError("Failed to save supplier.");
      }
    } finally {
      setSaving(false);
    }
  }

  function setValue<Key extends keyof SupplierFormValues>(key: Key, value: SupplierFormValues[Key]) {
    setValues((current) => ({ ...current, [key]: value }));
  }

  return (
    <section className="page-section">
      <div className="page-header">
        <div>
          <p className="eyebrow">Master Data</p>
          <h2>{isEdit ? "Edit supplier" : "Create supplier"}</h2>
          <p className="page-copy">Capture the supplier identity fields needed for future statement linkage.</p>
        </div>
        <Link className="secondary-link" to="/suppliers">
          Back to suppliers
        </Link>
      </div>

      {loading ? <p className="feedback">Loading supplier...</p> : null}

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
            <span>Statement Name</span>
            <input
              className="text-input"
              value={values.statementName}
              onChange={(event) => setValue("statementName", event.target.value)}
            />
            {errors.statementName ? <small className="field-error">{errors.statementName[0]}</small> : null}
          </label>

          <label className="form-field">
            <span>Phone</span>
            <input className="text-input" value={values.phone} onChange={(event) => setValue("phone", event.target.value)} />
            {errors.phone ? <small className="field-error">{errors.phone[0]}</small> : null}
          </label>

          <label className="form-field">
            <span>Email</span>
            <input className="text-input" value={values.email} onChange={(event) => setValue("email", event.target.value)} />
            {errors.email ? <small className="field-error">{errors.email[0]}</small> : null}
          </label>

          <label className="checkbox-field">
            <input
              checked={values.isActive}
              onChange={(event) => setValue("isActive", event.target.checked)}
              type="checkbox"
            />
            <span>Active supplier</span>
          </label>

          <div className="form-actions">
            <Link className="secondary-link" to="/suppliers">
              Cancel
            </Link>
            <button className="primary-button" disabled={saving} type="submit">
              {saving ? "Saving..." : isEdit ? "Save supplier" : "Create supplier"}
            </button>
          </div>
        </form>
      ) : null}
    </section>
  );
}

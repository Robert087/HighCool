import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ApiError, type ValidationErrors } from "../services/api";
import {
  createUom,
  getUom,
  updateUom,
  type UomFormValues,
} from "../services/masterDataApi";

const initialValues: UomFormValues = {
  code: "",
  name: "",
  precision: 0,
  allowsFraction: false,
  isActive: true,
};

export function UomFormPage() {
  const navigate = useNavigate();
  const { uomId } = useParams();
  const isEdit = Boolean(uomId);
  const [values, setValues] = useState<UomFormValues>(initialValues);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [formError, setFormError] = useState("");
  const [loading, setLoading] = useState(isEdit);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!uomId) {
      return;
    }

    const currentUomId: string = uomId;

    let active = true;

    async function load() {
      try {
        setLoading(true);
        const uom = await getUom(currentUomId);

        if (active) {
          setValues({
            code: uom.code,
            name: uom.name,
            precision: uom.precision,
            allowsFraction: uom.allowsFraction,
            isActive: uom.isActive,
          });
        }
      } catch (loadError) {
        if (active) {
          setFormError(loadError instanceof ApiError ? loadError.message : "Failed to load UOM.");
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
  }, [uomId]);

  function validate(currentValues: UomFormValues): ValidationErrors {
    const nextErrors: ValidationErrors = {};

    if (!currentValues.code.trim()) {
      nextErrors.code = ["Code is required."];
    }

    if (!currentValues.name.trim()) {
      nextErrors.name = ["Name is required."];
    }

    if (currentValues.precision < 0 || currentValues.precision > 6) {
      nextErrors.precision = ["Precision must be between 0 and 6."];
    }

    return nextErrors;
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const currentUomId = uomId;
    const nextErrors = validate(values);
    setErrors(nextErrors);
    setFormError("");

    if (Object.keys(nextErrors).length > 0) {
      return;
    }

    try {
      setSaving(true);

      if (currentUomId) {
        await updateUom(currentUomId, values);
      } else {
        await createUom(values);
      }

      navigate("/uoms");
    } catch (submitError) {
      if (submitError instanceof ApiError) {
        setErrors(submitError.validationErrors ?? {});
        setFormError(submitError.message);
      } else {
        setFormError("Failed to save UOM.");
      }
    } finally {
      setSaving(false);
    }
  }

  function setValue<Key extends keyof UomFormValues>(key: Key, value: UomFormValues[Key]) {
    setValues((current) => ({ ...current, [key]: value }));
  }

  return (
    <section className="page-section">
      <div className="page-header">
        <div>
          <p className="eyebrow">Master Data</p>
          <h2>{isEdit ? "Edit UOM" : "Create UOM"}</h2>
          <p className="page-copy">Define the code, precision, and fraction behavior for future inventory usage.</p>
        </div>
        <Link className="secondary-link" to="/uoms">
          Back to UOMs
        </Link>
      </div>

      {loading ? <p className="feedback">Loading UOM...</p> : null}

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
            <span>Precision</span>
            <input
              className="text-input"
              max={6}
              min={0}
              type="number"
              value={values.precision}
              onChange={(event) => setValue("precision", Number(event.target.value))}
            />
            {errors.precision ? <small className="field-error">{errors.precision[0]}</small> : null}
          </label>

          <label className="checkbox-field">
            <input
              checked={values.allowsFraction}
              onChange={(event) => setValue("allowsFraction", event.target.checked)}
              type="checkbox"
            />
            <span>Allows fractional quantities</span>
          </label>

          <label className="checkbox-field">
            <input
              checked={values.isActive}
              onChange={(event) => setValue("isActive", event.target.checked)}
              type="checkbox"
            />
            <span>Active UOM</span>
          </label>

          <div className="form-actions">
            <Link className="secondary-link" to="/uoms">
              Cancel
            </Link>
            <button className="primary-button" disabled={saving} type="submit">
              {saving ? "Saving..." : isEdit ? "Save UOM" : "Create UOM"}
            </button>
          </div>
        </form>
      ) : null}
    </section>
  );
}

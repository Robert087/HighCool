import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ApiError, type ValidationErrors } from "../services/api";
import { FormPageLayout, FormSection } from "../components/patterns";
import { Button, Checkbox, EmptyState, Field, Input, SkeletonLoader, useToast } from "../components/ui";
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
  const { showToast } = useToast();
  const navigate = useNavigate();
  const { uomId } = useParams();
  const isEdit = Boolean(uomId);
  const [values, setValues] = useState<UomFormValues>(initialValues);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [formError, setFormError] = useState("");
  const [loading, setLoading] = useState(isEdit);
  const [saving, setSaving] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);

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
  }, [uomId, reloadKey]);

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
        showToast({ tone: "success", title: "UOM updated", description: "The UOM changes were saved successfully." });
      } else {
        await createUom(values);
        showToast({ tone: "success", title: "UOM created", description: "The new UOM is now available in the catalogue." });
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
    <FormPageLayout eyebrow="Master Data" title={isEdit ? "Edit UOM" : "Create UOM"} description="Set the measurement code and precision rules." actions={<Link className="hc-button hc-button--secondary hc-button--md" to="/uoms">Back to UOMs</Link>}>
      {loading ? <div className="hc-card hc-card--md"><div className="hc-skeleton-stack"><SkeletonLoader height="2.75rem" variant="rect" /><SkeletonLoader height="2.75rem" variant="rect" /><SkeletonLoader height="2.75rem" variant="rect" /></div></div> : null}
      {!loading && formError && isEdit ? <div className="hc-card hc-card--md"><EmptyState title="Unable to load UOM" description={formError} action={<Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>Retry</Button>} /></div> : null}
      {!loading && (!formError || !isEdit) ? (
        <form className="hc-form-stack" onSubmit={handleSubmit}>
          {formError ? <div className="hc-inline-error">{formError}</div> : null}
          <FormSection title="Measurement definition" description="Code, name, and fraction rules.">
            <div className="hc-form-grid">
              <Field label="Code" required><Input value={values.code} onChange={(event) => setValue("code", event.target.value)} />{errors.code ? <small className="hc-field-error">{errors.code[0]}</small> : null}</Field>
              <Field label="Name" required><Input value={values.name} onChange={(event) => setValue("name", event.target.value)} />{errors.name ? <small className="hc-field-error">{errors.name[0]}</small> : null}</Field>
            </div>
            <Field label="Precision" required hint="Allowed range is 0 to 6 decimal places."><Input max={6} min={0} type="number" value={values.precision} onChange={(event) => setValue("precision", Number(event.target.value))} />{errors.precision ? <small className="hc-field-error">{errors.precision[0]}</small> : null}</Field>
            <Checkbox checked={values.allowsFraction} label="Allows fractional quantities" onChange={(event) => setValue("allowsFraction", event.target.checked)} />
            <Checkbox checked={values.isActive} label="Active UOM" onChange={(event) => setValue("isActive", event.target.checked)} />
          </FormSection>
          <div className="hc-form-actions"><Link className="hc-button hc-button--ghost hc-button--md" to="/uoms">Cancel</Link><Button isLoading={saving} type="submit">{isEdit ? "Save UOM" : "Create UOM"}</Button></div>
        </form>
      ) : null}
    </FormPageLayout>
  );
}

import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ApiError, type ValidationErrors } from "../services/api";
import { DocumentPageLayout, DocumentSection } from "../components/patterns";
import { Button, Checkbox, EmptyState, Field, Input, Select, SkeletonLoader, useToast } from "../components/ui";
import {
  createUomConversion,
  getActiveUomsCached,
  getUomConversion,
  updateUomConversion,
  type RoundingMode,
  type Uom,
  type UomConversionFormValues,
} from "../services/masterDataApi";

const initialValues: UomConversionFormValues = {
  fromUomId: "",
  toUomId: "",
  factor: 1,
  roundingMode: "None",
  isActive: true,
};

const roundingModes: RoundingMode[] = ["None", "Round", "Floor", "Ceiling"];

export function UomConversionFormPage() {
  const { showToast } = useToast();
  const navigate = useNavigate();
  const { uomConversionId } = useParams();
  const isEdit = Boolean(uomConversionId);
  const [values, setValues] = useState<UomConversionFormValues>(initialValues);
  const [uoms, setUoms] = useState<Uom[]>([]);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [formError, setFormError] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);
  const formId = "uom-conversion-form";

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        const [uomList, row] = await Promise.all([
          getActiveUomsCached(),
          uomConversionId ? getUomConversion(uomConversionId) : Promise.resolve(null),
        ]);

        if (active) {
          setUoms(uomList);
          if (row) {
            setValues({
              fromUomId: row.fromUomId,
              toUomId: row.toUomId,
              factor: row.factor,
              roundingMode: row.roundingMode,
              isActive: row.isActive,
            });
          }
        }
      } catch (loadError) {
        if (active) {
          setFormError(loadError instanceof ApiError ? loadError.message : "Failed to load UOM conversion form.");
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
  }, [reloadKey, uomConversionId]);

  function validate(currentValues: UomConversionFormValues): ValidationErrors {
    const nextErrors: ValidationErrors = {};
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
      nextErrors.factor = ["Factor must be greater than zero."];
    }
    return nextErrors;
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const currentId = uomConversionId;
    const nextErrors = validate(values);
    setErrors(nextErrors);
    setFormError("");

    if (Object.keys(nextErrors).length > 0) {
      return;
    }

    try {
      setSaving(true);
      if (currentId) {
        await updateUomConversion(currentId, values);
        showToast({ tone: "success", title: "Conversion updated", description: "The global UOM conversion was saved successfully." });
      } else {
        await createUomConversion(values);
        showToast({ tone: "success", title: "Conversion created", description: "The global UOM conversion is now available." });
      }

      navigate("/uom-conversions");
    } catch (submitError) {
      if (submitError instanceof ApiError) {
        setErrors(submitError.validationErrors ?? {});
        setFormError(submitError.message);
      } else {
        setFormError("Failed to save UOM conversion.");
      }
    } finally {
      setSaving(false);
    }
  }

  function setValue<Key extends keyof UomConversionFormValues>(key: Key, value: UomConversionFormValues[Key]) {
    setValues((current) => ({ ...current, [key]: value }));
  }

  function renderActionBar() {
    return (
      <>
        <Link className="hc-button hc-button--secondary hc-button--md" to="/uom-conversions">Close</Link>
        <Button form={formId} isLoading={saving} type="submit">{isEdit ? "Save conversion" : "Create conversion"}</Button>
      </>
    );
  }

  return (
    <DocumentPageLayout
      eyebrow="Master Data"
      title={isEdit ? "Edit UOM Conversion" : "Create UOM Conversion"}
      description="Define global measurement rules with the same structured ERP form pattern used across the workspace."
      actions={renderActionBar()}
      footer={renderActionBar()}
    >
      {loading ? <div className="hc-document-section"><div className="hc-skeleton-stack"><SkeletonLoader height="2.75rem" variant="rect" /><SkeletonLoader height="2.75rem" variant="rect" /><SkeletonLoader height="2.75rem" variant="rect" /></div></div> : null}
      {!loading && formError && uoms.length === 0 ? <div className="hc-document-section"><EmptyState title="Unable to load UOM conversion form" description={formError} action={<Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>Retry</Button>} /></div> : null}
      {!loading && (!formError || uoms.length > 0) ? (
        <form className="hc-document-form" id={formId} onSubmit={handleSubmit}>
          {formError ? <div className="hc-inline-error">{formError}</div> : null}
          <DocumentSection title="Form Header" description="Keep conversion pairs and numeric rules aligned in a predictable two-column layout.">
            <div className="hc-document-form-grid">
              <Field label="From UOM" required>
                <Select value={values.fromUomId} onChange={(event) => setValue("fromUomId", event.target.value)}>
                  <option value="">Select from UOM</option>
                  {uoms.map((uom) => (
                    <option key={uom.id} value={uom.id}>{uom.code} - {uom.name}</option>
                  ))}
                </Select>
                {errors.fromUomId ? <small className="hc-field-error">{errors.fromUomId[0]}</small> : null}
              </Field>
              <Field label="To UOM" required>
                <Select value={values.toUomId} onChange={(event) => setValue("toUomId", event.target.value)}>
                  <option value="">Select to UOM</option>
                  {uoms.map((uom) => (
                    <option key={uom.id} value={uom.id}>{uom.code} - {uom.name}</option>
                  ))}
                </Select>
                {errors.toUomId ? <small className="hc-field-error">{errors.toUomId[0]}</small> : null}
              </Field>
              <Field label="Factor" required>
                <Input min={0.000001} step="0.000001" type="number" value={values.factor} onChange={(event) => setValue("factor", Number(event.target.value))} />
                {errors.factor ? <small className="hc-field-error">{errors.factor[0]}</small> : null}
              </Field>
              <Field label="Rounding mode">
                <Select value={values.roundingMode} onChange={(event) => setValue("roundingMode", event.target.value as RoundingMode)}>
                  {roundingModes.map((mode) => (
                    <option key={mode} value={mode}>{mode}</option>
                  ))}
                </Select>
              </Field>
              <Field className="hc-document-field--span-full" label="Status">
                <Checkbox checked={values.isActive} label="Active conversion rule" onChange={(event) => setValue("isActive", event.target.checked)} />
              </Field>
            </div>
          </DocumentSection>
        </form>
      ) : null}
    </DocumentPageLayout>
  );
}

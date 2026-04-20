import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ApiError, type ValidationErrors } from "../services/api";
import { FormPageLayout, FormSection } from "../components/patterns";
import { Button, Checkbox, EmptyState, Field, Input, SkeletonLoader, useToast } from "../components/ui";
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
  const { showToast } = useToast();
  const navigate = useNavigate();
  const { supplierId } = useParams();
  const isEdit = Boolean(supplierId);
  const [values, setValues] = useState<SupplierFormValues>(initialValues);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [formError, setFormError] = useState("");
  const [loading, setLoading] = useState(isEdit);
  const [saving, setSaving] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);

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
  }, [supplierId, reloadKey]);

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
        showToast({ tone: "success", title: "Supplier updated", description: "The supplier changes were saved successfully." });
      } else {
        await createSupplier(values);
        showToast({ tone: "success", title: "Supplier created", description: "The new supplier is now available in the register." });
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
    <FormPageLayout eyebrow="Master Data" title={isEdit ? "Edit supplier" : "Create supplier"} description="Set the supplier record and contact details." actions={<Link className="hc-button hc-button--secondary hc-button--md" to="/suppliers">Back to suppliers</Link>}>
      {loading ? <div className="hc-card hc-card--md"><div className="hc-skeleton-stack"><SkeletonLoader height="2.75rem" variant="rect" /><SkeletonLoader height="2.75rem" variant="rect" /><SkeletonLoader height="2.75rem" variant="rect" /></div></div> : null}
      {!loading && formError && isEdit ? <div className="hc-card hc-card--md"><EmptyState title="Unable to load supplier" description={formError} action={<Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>Retry</Button>} /></div> : null}
      {!loading && (!formError || !isEdit) ? (
        <form className="hc-form-stack" onSubmit={handleSubmit}>
          {formError ? <div className="hc-inline-error">{formError}</div> : null}
          <FormSection title="Supplier identity" description="Core naming and reference fields.">
            <div className="hc-form-grid">
              <Field label="Code" required><Input value={values.code} onChange={(event) => setValue("code", event.target.value)} />{errors.code ? <small className="hc-field-error">{errors.code[0]}</small> : null}</Field>
              <Field label="Name" required><Input value={values.name} onChange={(event) => setValue("name", event.target.value)} />{errors.name ? <small className="hc-field-error">{errors.name[0]}</small> : null}</Field>
            </div>
            <Field label="Statement name" required hint="Shown on supplier statements."><Input value={values.statementName} onChange={(event) => setValue("statementName", event.target.value)} />{errors.statementName ? <small className="hc-field-error">{errors.statementName[0]}</small> : null}</Field>
          </FormSection>
          <FormSection title="Contact and status" description="Keep the record reachable and current.">
            <div className="hc-form-grid">
              <Field label="Phone"><Input value={values.phone} onChange={(event) => setValue("phone", event.target.value)} />{errors.phone ? <small className="hc-field-error">{errors.phone[0]}</small> : null}</Field>
              <Field label="Email"><Input value={values.email} onChange={(event) => setValue("email", event.target.value)} />{errors.email ? <small className="hc-field-error">{errors.email[0]}</small> : null}</Field>
            </div>
            <Checkbox checked={values.isActive} label="Active supplier" onChange={(event) => setValue("isActive", event.target.checked)} />
          </FormSection>
          <div className="hc-form-actions"><Link className="hc-button hc-button--ghost hc-button--md" to="/suppliers">Cancel</Link><Button isLoading={saving} type="submit">{isEdit ? "Save supplier" : "Create supplier"}</Button></div>
        </form>
      ) : null}
    </FormPageLayout>
  );
}

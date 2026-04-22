import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ApiError, type ValidationErrors } from "../services/api";
import { DocumentPageLayout, DocumentSection } from "../components/patterns";
import { Button, Checkbox, EmptyState, Field, Input, SkeletonLoader, Textarea, useToast } from "../components/ui";
import {
  createCustomer,
  getCustomer,
  updateCustomer,
  type CustomerFormValues,
} from "../services/masterDataApi";

const initialValues: CustomerFormValues = {
  code: "",
  name: "",
  phone: "",
  email: "",
  taxNumber: "",
  address: "",
  city: "",
  area: "",
  creditLimit: 0,
  paymentTerms: "",
  notes: "",
  isActive: true,
};

export function CustomerFormPage() {
  const { showToast } = useToast();
  const navigate = useNavigate();
  const { customerId } = useParams();
  const isEdit = Boolean(customerId);
  const [values, setValues] = useState<CustomerFormValues>(initialValues);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [formError, setFormError] = useState("");
  const [loading, setLoading] = useState(isEdit);
  const [saving, setSaving] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);
  const formId = "customer-form";

  useEffect(() => {
    if (!customerId) {
      return;
    }

    const currentCustomerId: string = customerId;

    let active = true;

    async function load() {
      try {
        setLoading(true);
        const customer = await getCustomer(currentCustomerId);

        if (active) {
          setValues({
            code: customer.code,
            name: customer.name,
            phone: customer.phone ?? "",
            email: customer.email ?? "",
            taxNumber: customer.taxNumber ?? "",
            address: customer.address ?? "",
            city: customer.city ?? "",
            area: customer.area ?? "",
            creditLimit: customer.creditLimit,
            paymentTerms: customer.paymentTerms ?? "",
            notes: customer.notes ?? "",
            isActive: customer.isActive,
          });
        }
      } catch (loadError) {
        if (active) {
          setFormError(loadError instanceof ApiError ? loadError.message : "Failed to load customer.");
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
  }, [customerId, reloadKey]);

  function validate(currentValues: CustomerFormValues): ValidationErrors {
    const nextErrors: ValidationErrors = {};

    if (!currentValues.code.trim()) {
      nextErrors.code = ["Code is required."];
    }

    if (!currentValues.name.trim()) {
      nextErrors.name = ["Name is required."];
    }

    if (currentValues.email && !/^\S+@\S+\.\S+$/.test(currentValues.email)) {
      nextErrors.email = ["Email format is invalid."];
    }

    if (currentValues.creditLimit < 0) {
      nextErrors.creditLimit = ["Credit limit cannot be negative."];
    }

    return nextErrors;
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const currentCustomerId = customerId;
    const nextErrors = validate(values);
    setErrors(nextErrors);
    setFormError("");

    if (Object.keys(nextErrors).length > 0) {
      return;
    }

    try {
      setSaving(true);

      if (currentCustomerId) {
        await updateCustomer(currentCustomerId, values);
        showToast({ tone: "success", title: "Customer updated", description: "The customer changes were saved successfully." });
      } else {
        await createCustomer(values);
        showToast({ tone: "success", title: "Customer created", description: "The new customer is now available in the register." });
      }

      navigate("/customers");
    } catch (submitError) {
      if (submitError instanceof ApiError) {
        setErrors(submitError.validationErrors ?? {});
        setFormError(submitError.message);
      } else {
        setFormError("Failed to save customer.");
      }
    } finally {
      setSaving(false);
    }
  }

  function setValue<Key extends keyof CustomerFormValues>(key: Key, value: CustomerFormValues[Key]) {
    setValues((current) => ({ ...current, [key]: value }));
  }

  function renderActionBar() {
    return (
      <>
        <Link className="hc-button hc-button--secondary hc-button--md" to="/customers">Close</Link>
        <Button form={formId} isLoading={saving} type="submit">{isEdit ? "Save customer" : "Create customer"}</Button>
      </>
    );
  }

  return (
    <DocumentPageLayout
      eyebrow="Master Data"
      title={isEdit ? "Edit customer" : "Create customer"}
      description="Maintain customer identity, contact details, and commercial controls in the same ERP document layout."
      actions={renderActionBar()}
      footer={renderActionBar()}
    >
      {loading ? <div className="hc-document-section"><div className="hc-skeleton-stack"><SkeletonLoader height="2.75rem" variant="rect" /><SkeletonLoader height="2.75rem" variant="rect" /><SkeletonLoader height="2.75rem" variant="rect" /></div></div> : null}
      {!loading && formError && isEdit ? <div className="hc-document-section"><EmptyState title="Unable to load customer" description={formError} action={<Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>Retry</Button>} /></div> : null}
      {!loading && (!formError || !isEdit) ? (
        <form className="hc-document-form" id={formId} onSubmit={handleSubmit}>
          {formError ? <div className="hc-inline-error">{formError}</div> : null}
          <DocumentSection title="Customer identity" description="Capture customer naming, core tax fields, and account identity.">
            <div className="hc-document-form-grid">
              <Field label="Code" required><Input value={values.code} onChange={(event) => setValue("code", event.target.value)} />{errors.code ? <small className="hc-field-error">{errors.code[0]}</small> : null}</Field>
              <Field label="Name" required><Input value={values.name} onChange={(event) => setValue("name", event.target.value)} />{errors.name ? <small className="hc-field-error">{errors.name[0]}</small> : null}</Field>
              <Field label="Tax number"><Input value={values.taxNumber} onChange={(event) => setValue("taxNumber", event.target.value)} />{errors.taxNumber ? <small className="hc-field-error">{errors.taxNumber[0]}</small> : null}</Field>
              <Field label="Payment terms"><Input value={values.paymentTerms} onChange={(event) => setValue("paymentTerms", event.target.value)} />{errors.paymentTerms ? <small className="hc-field-error">{errors.paymentTerms[0]}</small> : null}</Field>
            </div>
          </DocumentSection>
          <DocumentSection title="Contact and location" description="Keep the account reachable and geographically clear.">
            <div className="hc-document-form-grid">
              <Field label="Phone"><Input value={values.phone} onChange={(event) => setValue("phone", event.target.value)} />{errors.phone ? <small className="hc-field-error">{errors.phone[0]}</small> : null}</Field>
              <Field label="Email"><Input value={values.email} onChange={(event) => setValue("email", event.target.value)} />{errors.email ? <small className="hc-field-error">{errors.email[0]}</small> : null}</Field>
              <Field className="hc-document-field--span-full" label="Address"><Textarea value={values.address} onChange={(event) => setValue("address", event.target.value)} />{errors.address ? <small className="hc-field-error">{errors.address[0]}</small> : null}</Field>
              <Field label="City"><Input value={values.city} onChange={(event) => setValue("city", event.target.value)} />{errors.city ? <small className="hc-field-error">{errors.city[0]}</small> : null}</Field>
              <Field label="Area"><Input value={values.area} onChange={(event) => setValue("area", event.target.value)} />{errors.area ? <small className="hc-field-error">{errors.area[0]}</small> : null}</Field>
            </div>
          </DocumentSection>
          <DocumentSection title="Commercial controls" description="Manage credit exposure, notes, and account status.">
            <div className="hc-document-form-grid">
              <Field label="Credit limit" required hint="Stored on the customer master record."><Input min={0} step="0.01" type="number" value={values.creditLimit} onChange={(event) => setValue("creditLimit", event.target.value === "" ? 0 : Number(event.target.value))} />{errors.creditLimit ? <small className="hc-field-error">{errors.creditLimit[0]}</small> : null}</Field>
              <Field className="hc-document-field--span-full" label="Notes"><Textarea value={values.notes} onChange={(event) => setValue("notes", event.target.value)} />{errors.notes ? <small className="hc-field-error">{errors.notes[0]}</small> : null}</Field>
              <Field className="hc-document-field--span-full" label="Status">
                <Checkbox checked={values.isActive} label="Active customer" onChange={(event) => setValue("isActive", event.target.checked)} />
              </Field>
            </div>
          </DocumentSection>
        </form>
      ) : null}
    </DocumentPageLayout>
  );
}

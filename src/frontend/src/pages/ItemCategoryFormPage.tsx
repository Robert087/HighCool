import { useEffect, useState, type FormEvent } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { DocumentPageLayout, DocumentSection } from "../components/patterns";
import { Button, Checkbox, EmptyState, Field, Input, SkeletonLoader, Textarea, useToast } from "../components/ui";
import { useI18n } from "../i18n";
import { ApiError, type ValidationErrors } from "../services/api";
import { createItemCategory, getItemCategory, updateItemCategory, type ItemCategoryFormValues } from "../services/masterDataApi";

const initialValues: ItemCategoryFormValues = {
  code: "",
  name: "",
  description: "",
  isActive: true,
};

export function ItemCategoryFormPage() {
  const { showToast } = useToast();
  const { t } = useI18n();
  const navigate = useNavigate();
  const { categoryId } = useParams();
  const isEdit = Boolean(categoryId);
  const [values, setValues] = useState<ItemCategoryFormValues>(initialValues);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [formError, setFormError] = useState("");
  const [loading, setLoading] = useState(isEdit);
  const [saving, setSaving] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);
  const formId = "item-category-form";

  useEffect(() => {
    if (!categoryId) {
      return;
    }

    const currentCategoryId = categoryId;
    let active = true;
    async function load() {
      try {
        setLoading(true);
        setFormError("");
        const category = await getItemCategory(currentCategoryId);
        if (active) {
          setValues({
            code: category.code,
            name: category.name,
            description: category.description ?? "",
            isActive: category.isActive,
          });
        }
      } catch (loadError) {
        if (active) {
          setFormError(loadError instanceof ApiError ? loadError.message : "module.itemCategories.formLoadError");
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
  }, [categoryId, reloadKey]);

  function validate(currentValues: ItemCategoryFormValues): ValidationErrors {
    const nextErrors: ValidationErrors = {};
    if (!currentValues.code.trim()) {
      nextErrors.code = ["module.itemCategories.validation.codeRequired"];
    }
    if (!currentValues.name.trim()) {
      nextErrors.name = ["module.itemCategories.validation.nameRequired"];
    }
    return nextErrors;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const nextErrors = validate(values);
    setErrors(nextErrors);
    setFormError("");

    if (Object.keys(nextErrors).length > 0) {
      return;
    }

    try {
      setSaving(true);
      if (categoryId) {
        await updateItemCategory(categoryId, values);
        showToast({ tone: "success", title: "module.itemCategories.updated", description: "module.itemCategories.updatedDescription" });
      } else {
        await createItemCategory(values);
        showToast({ tone: "success", title: "module.itemCategories.created", description: "module.itemCategories.createdDescription" });
      }
      navigate("/item-categories");
    } catch (submitError) {
      if (submitError instanceof ApiError) {
        setErrors(submitError.validationErrors ?? {});
        setFormError(submitError.message);
      } else {
        setFormError("module.itemCategories.saveError");
      }
    } finally {
      setSaving(false);
    }
  }

  function setValue<Key extends keyof ItemCategoryFormValues>(key: Key, value: ItemCategoryFormValues[Key]) {
    setValues((current) => ({ ...current, [key]: value }));
  }

  function renderActionBar() {
    return (
      <>
        <Link className="hc-button hc-button--secondary hc-button--md" to="/item-categories">common.close</Link>
        <Button form={formId} isLoading={saving} type="submit">{isEdit ? "module.itemCategories.save" : "module.itemCategories.create"}</Button>
      </>
    );
  }

  return (
    <DocumentPageLayout
      eyebrow="module.inventory.eyebrow"
      title={isEdit ? "module.itemCategories.editTitle" : "module.itemCategories.createTitle"}
      description="module.itemCategories.formDescription"
      actions={renderActionBar()}
      footer={renderActionBar()}
    >
      {loading ? (
        <div className="hc-document-section">
          <div className="hc-skeleton-stack">
            <SkeletonLoader height="2.75rem" variant="rect" />
            <SkeletonLoader height="2.75rem" variant="rect" />
          </div>
        </div>
      ) : null}

      {!loading && formError && !values.code ? (
        <div className="hc-document-section">
          <EmptyState title="module.itemCategories.formLoadErrorTitle" description={formError} action={<Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>common.retry</Button>} />
        </div>
      ) : null}

      {!loading && (!formError || values.code) ? (
        <form className="hc-document-form" id={formId} onSubmit={handleSubmit}>
          {formError ? <div className="hc-inline-error">{formError}</div> : null}
          <DocumentSection title="module.itemCategories.formSection" description="module.itemCategories.formSectionDescription">
            <div className="hc-document-form-grid">
              <Field label="module.itemCategories.fields.code" required>
                <Input value={values.code} onChange={(event) => setValue("code", event.target.value)} />
                {errors.code ? <small className="hc-field-error">{t(errors.code[0])}</small> : null}
              </Field>
              <Field label="module.itemCategories.fields.name" required>
                <Input value={values.name} onChange={(event) => setValue("name", event.target.value)} />
                {errors.name ? <small className="hc-field-error">{t(errors.name[0])}</small> : null}
              </Field>
              <Field label="module.itemCategories.fields.description">
                <Textarea value={values.description} onChange={(event) => setValue("description", event.target.value)} />
                {errors.description ? <small className="hc-field-error">{t(errors.description[0])}</small> : null}
              </Field>
              <Field label="common.status">
                <Checkbox checked={values.isActive} label="status.active" onChange={(event) => setValue("isActive", event.target.checked)} />
              </Field>
            </div>
          </DocumentSection>
        </form>
      ) : null}
    </DocumentPageLayout>
  );
}

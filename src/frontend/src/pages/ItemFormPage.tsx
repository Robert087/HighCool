import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ApiError, type ValidationErrors } from "../services/api";
import { DocumentPageLayout, DocumentSection } from "../components/patterns";
import { Button, Checkbox, EmptyState, Field, Input, Select, SkeletonLoader, useToast } from "../components/ui";
import {
  createItem,
  getActiveItemsCached,
  getActiveUomsCached,
  getItem,
  updateItem,
  type Item,
  type ItemComponentFormValues,
  type ItemFormValues,
  type Uom,
} from "../services/masterDataApi";

const initialValues: ItemFormValues = {
  code: "",
  name: "",
  baseUomId: "",
  isActive: true,
  isSellable: true,
  hasComponents: false,
  components: [],
};

export function ItemFormPage() {
  const { showToast } = useToast();
  const navigate = useNavigate();
  const { itemId } = useParams();
  const isEdit = Boolean(itemId);
  const [values, setValues] = useState<ItemFormValues>(initialValues);
  const [items, setItems] = useState<Item[]>([]);
  const [uoms, setUoms] = useState<Uom[]>([]);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [formError, setFormError] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);
  const formId = "item-form";

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        const [uomList, itemList, item] = await Promise.all([
          getActiveUomsCached(),
          getActiveItemsCached(),
          itemId ? getItem(itemId) : Promise.resolve(null),
        ]);

        if (active) {
          setUoms(uomList);
          setItems(itemList);
          if (item) {
            setValues({
              code: item.code,
              name: item.name,
              baseUomId: item.baseUomId,
              isActive: item.isActive,
              isSellable: item.isSellable,
              hasComponents: item.hasComponents,
              components: item.components.map((component) => ({
                componentItemId: component.componentItemId,
                uomId: component.uomId,
                quantity: component.quantity,
              })),
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

    if (currentValues.hasComponents && currentValues.components.length === 0) {
      nextErrors.components = ["Add at least one component row when the item is marked as having components."];
    }

    if (!currentValues.hasComponents && currentValues.components.length > 0) {
      nextErrors.components = ["Clear component rows or mark the item as having components."];
    }

    currentValues.components.forEach((component, index) => {
      if (!component.componentItemId) {
        nextErrors[`components.${index}.componentItemId`] = ["Component item is required."];
      }

      if (!component.uomId) {
        nextErrors[`components.${index}.uomId`] = ["UOM is required."];
      }

      if (component.quantity <= 0) {
        nextErrors[`components.${index}.quantity`] = ["Quantity must be greater than zero."];
      }

      if (itemId && component.componentItemId === itemId) {
        nextErrors[`components.${index}.componentItemId`] = ["Item components cannot reference the same item as both parent and component."];
      }
    });

    const duplicateIds = currentValues.components
      .map((component) => component.componentItemId)
      .filter(Boolean)
      .filter((componentId, index, allIds) => allIds.indexOf(componentId) !== index);

    if (duplicateIds.length > 0) {
      nextErrors.components = ["Duplicate component rows are not allowed."];
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

  function addComponentRow() {
    setValues((current) => ({
      ...current,
      hasComponents: true,
      components: [
        ...current.components,
        {
          componentItemId: "",
          uomId: current.baseUomId,
          quantity: 1,
        },
      ],
    }));
  }

  function updateComponentRow<Key extends keyof ItemComponentFormValues>(
    index: number,
    key: Key,
    value: ItemComponentFormValues[Key],
  ) {
    setValues((current) => ({
      ...current,
      components: current.components.map((component, componentIndex) =>
        componentIndex === index ? { ...component, [key]: value } : component),
    }));
  }

  function removeComponentRow(index: number) {
    setValues((current) => ({
      ...current,
      components: current.components.filter((_, componentIndex) => componentIndex !== index),
    }));
  }

  const componentCandidates = items.filter((item) => item.id !== itemId);

  function renderActionBar() {
    return (
      <>
        <Link className="hc-button hc-button--secondary hc-button--md" to="/items">Close</Link>
        <Button form={formId} isLoading={saving} type="submit">{isEdit ? "Save item" : "Create item"}</Button>
      </>
    );
  }

  return (
    <DocumentPageLayout
      eyebrow="Master Data"
      title={isEdit ? "Edit Item" : "Create Item"}
      description="Maintain item identity, usage flags, and BOM component rows in a structured ERP editing screen."
      actions={renderActionBar()}
      footer={renderActionBar()}
    >
      {loading ? (
        <div className="hc-document-section">
          <div className="hc-skeleton-stack">
            <SkeletonLoader height="2.75rem" variant="rect" />
            <SkeletonLoader height="2.75rem" variant="rect" />
            <SkeletonLoader height="8rem" variant="rect" />
          </div>
        </div>
      ) : null}

      {!loading && formError && uoms.length === 0 ? (
        <div className="hc-document-section">
          <EmptyState
            title="Unable to load item form"
            description={formError}
            action={<Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>Retry</Button>}
          />
        </div>
      ) : null}

      {!loading && (!formError || uoms.length > 0) ? (
        <form className="hc-document-form" id={formId} onSubmit={handleSubmit}>
          {formError ? <div className="hc-inline-error">{formError}</div> : null}

          <DocumentSection title="Form Header" description="Keep item identity and usage settings aligned in one predictable workspace.">
            <div className="hc-document-form-grid">
              <Field label="Code" required>
                <Input value={values.code} onChange={(event) => setValue("code", event.target.value)} />
                {errors.code ? <small className="hc-field-error">{errors.code[0]}</small> : null}
              </Field>

              <Field label="Name" required>
                <Input value={values.name} onChange={(event) => setValue("name", event.target.value)} />
                {errors.name ? <small className="hc-field-error">{errors.name[0]}</small> : null}
              </Field>
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
              <Field label="Usage">
                <div className="hc-document-form">
                  <Checkbox checked={values.isSellable} label="Sellable item" onChange={(event) => setValue("isSellable", event.target.checked)} />
                  <Checkbox checked={values.hasComponents} label="Has components" onChange={(event) => setValue("hasComponents", event.target.checked)} />
                  <Checkbox checked={values.isActive} label="Active item" onChange={(event) => setValue("isActive", event.target.checked)} />
                </div>
              </Field>
            </div>
          </DocumentSection>

          {values.hasComponents ? (
            <DocumentSection
              title="Components"
              description="Manage child component rows in a compact table-style editor."
              actions={<Button type="button" variant="secondary" onClick={addComponentRow}>Add component row</Button>}
            >
              {errors.components ? <div className="hc-inline-error">{errors.components[0]}</div> : null}
              <div className="hc-document-table-wrap">
                <table className="hc-table hc-table--compact">
                  <thead>
                    <tr>
                      <th>Component item</th>
                      <th>UOM</th>
                      <th>Quantity</th>
                      <th className="hc-table__head-actions" />
                    </tr>
                  </thead>
                  <tbody>
                    {values.components.length > 0 ? (
                      values.components.map((component, index) => (
                        <tr key={`${index}-${component.componentItemId}`}>
                          <td>
                            <Select value={component.componentItemId} onChange={(event) => updateComponentRow(index, "componentItemId", event.target.value)}>
                              <option value="">Select component item</option>
                              {componentCandidates.map((item) => (
                                <option key={item.id} value={item.id}>
                                  {item.code} - {item.name}
                                </option>
                              ))}
                            </Select>
                            {errors[`components.${index}.componentItemId`] ? <small className="hc-field-error">{errors[`components.${index}.componentItemId`][0]}</small> : null}
                          </td>
                          <td>
                            <Select value={component.uomId} onChange={(event) => updateComponentRow(index, "uomId", event.target.value)}>
                              <option value="">Select UOM</option>
                              {uoms.map((uom) => (
                                <option key={uom.id} value={uom.id}>
                                  {uom.code} - {uom.name}
                                </option>
                              ))}
                            </Select>
                            {errors[`components.${index}.uomId`] ? <small className="hc-field-error">{errors[`components.${index}.uomId`][0]}</small> : null}
                          </td>
                          <td>
                            <Input min={0.000001} step="0.000001" type="number" value={component.quantity} onChange={(event) => updateComponentRow(index, "quantity", Number(event.target.value))} />
                            {errors[`components.${index}.quantity`] ? <small className="hc-field-error">{errors[`components.${index}.quantity`][0]}</small> : null}
                          </td>
                          <td className="hc-table__cell-actions">
                            <Button type="button" variant="ghost" onClick={() => removeComponentRow(index)}>Remove row</Button>
                          </td>
                        </tr>
                      ))
                    ) : (
                      <tr>
                        <td colSpan={4}>
                          <div className="hc-table__empty">No component rows added yet.</div>
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </DocumentSection>
          ) : null}
        </form>
      ) : null}
    </DocumentPageLayout>
  );
}

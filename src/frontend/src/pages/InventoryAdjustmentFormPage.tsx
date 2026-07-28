import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { DocumentPageLayout, DocumentSection } from "../components/patterns";
import { Badge, Button, EmptyState, Field, Input, Select, SkeletonLoader, Textarea, useConfirmationDialog, useI18n, useToast } from "../components/ui";
import { useAuth } from "../features/auth/AuthProvider";
import { ApiError, type ValidationErrors } from "../services/api";
import {
  cancelInventoryAdjustment,
  createInventoryAdjustment,
  deleteInventoryAdjustmentDraft,
  getInventoryAdjustment,
  mapInventoryAdjustmentToFormValues,
  postInventoryAdjustment,
  updateInventoryAdjustment,
  type InventoryAdjustment,
  type InventoryAdjustmentFormValues,
  type InventoryAdjustmentLineFormValues,
} from "../services/inventoryAdjustmentsApi";
import { getActiveItemsCached, getActiveUomsCached, getActiveWarehousesCached, type Item, type Uom, type Warehouse } from "../services/masterDataApi";
import { Permissions } from "../services/permissions";

const INITIAL_VALUES: InventoryAdjustmentFormValues = {
  adjustmentNo: "",
  adjustmentDate: new Date().toISOString().slice(0, 10),
  warehouseId: "",
  reason: "",
  notes: "",
  lines: [
    {
      lineNo: 1,
      itemId: "",
      uomId: "",
      quantity: "",
      adjustmentType: "Increase",
      baseQty: 0,
      notes: "",
    },
  ],
};

function statusTone(status: InventoryAdjustment["status"]) {
  return status === "Posted" ? "success" : status === "Canceled" ? "neutral" : "warning";
}

export function getInventoryAdjustmentFormCapabilities(
  status: InventoryAdjustment["status"],
  isEdit: boolean,
  hasPermission: (permission: string) => boolean,
) {
  const isEditable = status === "Draft";
  const canSaveDraft = isEditable && hasPermission(Permissions.InventoryAdjustmentCreate);
  const canPostAdjustment = isEditable && hasPermission(Permissions.InventoryAdjustmentPost) && (canSaveDraft || isEdit);
  const canCancelAdjustment = status === "Posted" && isEdit && hasPermission(Permissions.InventoryAdjustmentPost);

  return {
    canCancelAdjustment,
    canPostAdjustment,
    canSaveDraft,
  };
}

function toNumber(value: number | "") {
  return value === "" ? 0 : Number(value);
}

export function InventoryAdjustmentFormPage() {
  const { confirm, dialog } = useConfirmationDialog();
  const { formatDate, formatQuantity, t } = useI18n();
  const { showToast } = useToast();
  const { hasPermission } = useAuth();
  const navigate = useNavigate();
  const { inventoryAdjustmentId } = useParams();
  const isEdit = Boolean(inventoryAdjustmentId);
  const [values, setValues] = useState<InventoryAdjustmentFormValues>(INITIAL_VALUES);
  const [items, setItems] = useState<Item[]>([]);
  const [uoms, setUoms] = useState<Uom[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [status, setStatus] = useState<InventoryAdjustment["status"]>("Draft");
  const [documentMeta, setDocumentMeta] = useState<InventoryAdjustment | null>(null);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [formError, setFormError] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [posting, setPosting] = useState(false);
  const [canceling, setCanceling] = useState(false);
  const [deleting, setDeleting] = useState(false);

  const { canCancelAdjustment, canPostAdjustment, canSaveDraft } = getInventoryAdjustmentFormCapabilities(status, isEdit, hasPermission);
  const itemLookup = useMemo(() => new Map(items.map((item) => [item.id, item])), [items]);
  const busy = saving || posting || canceling || deleting;

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        setFormError("");

        const [itemRows, uomRows, warehouseRows, existingDocument] = await Promise.all([
          getActiveItemsCached(),
          getActiveUomsCached(),
          getActiveWarehousesCached(),
          inventoryAdjustmentId ? getInventoryAdjustment(inventoryAdjustmentId) : Promise.resolve(null),
        ]);

        if (!active) {
          return;
        }

        setItems(itemRows);
        setUoms(uomRows);
        setWarehouses(warehouseRows);

        if (existingDocument) {
          setValues(mapInventoryAdjustmentToFormValues(existingDocument));
          setStatus(existingDocument.status);
          setDocumentMeta(existingDocument);
        } else {
          setValues((current) => ({
            ...current,
            warehouseId: current.warehouseId || warehouseRows[0]?.id || "",
          }));
          setStatus("Draft");
          setDocumentMeta(null);
        }
      } catch (loadError) {
        if (active) {
          setFormError(loadError instanceof ApiError ? loadError.message : t("module.inventoryAdjustments.form.loadError"));
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
  }, [inventoryAdjustmentId, t]);

  function setValue<K extends keyof InventoryAdjustmentFormValues>(key: K, value: InventoryAdjustmentFormValues[K]) {
    setValues((current) => ({ ...current, [key]: value }));
  }

  function setLineValue<K extends keyof InventoryAdjustmentLineFormValues>(index: number, key: K, value: InventoryAdjustmentLineFormValues[K]) {
    setValues((current) => ({
      ...current,
      lines: current.lines.map((line, lineIndex) => {
        if (lineIndex !== index) {
          return line;
        }

        const nextLine = { ...line, [key]: value };
        if (key === "itemId" && typeof value === "string") {
          const selectedItem = itemLookup.get(value);
          return {
            ...nextLine,
            uomId: selectedItem?.baseUomId ?? nextLine.uomId,
            baseQty: 0,
          };
        }

        return nextLine;
      }),
    }));
  }

  function addLine() {
    setValues((current) => ({
      ...current,
      lines: [
        ...current.lines,
        {
          lineNo: current.lines.length + 1,
          itemId: "",
          uomId: "",
          quantity: "",
          adjustmentType: "Increase",
          baseQty: 0,
          notes: "",
        },
      ],
    }));
  }

  function removeLine(index: number) {
    setValues((current) => ({
      ...current,
      lines: current.lines
        .filter((_, lineIndex) => lineIndex !== index)
        .map((line, lineIndex) => ({ ...line, lineNo: lineIndex + 1 })),
    }));
  }

  function validate(currentValues: InventoryAdjustmentFormValues): ValidationErrors {
    const nextErrors: ValidationErrors = {};

    if (!currentValues.adjustmentDate) {
      nextErrors.adjustmentDate = ["module.inventoryAdjustments.validation.dateRequired"];
    }

    if (!currentValues.warehouseId) {
      nextErrors.warehouseId = ["module.inventoryAdjustments.validation.warehouseRequired"];
    }

    if (!currentValues.reason.trim()) {
      nextErrors.reason = ["module.inventoryAdjustments.validation.reasonRequired"];
    }

    if (currentValues.lines.length === 0) {
      nextErrors.lines = ["module.inventoryAdjustments.validation.linesRequired"];
    }

    currentValues.lines.forEach((line, index) => {
      if (!line.itemId) {
        nextErrors[`lines.${index}.itemId`] = ["module.inventoryAdjustments.validation.itemRequired"];
      }

      if (!line.uomId) {
        nextErrors[`lines.${index}.uomId`] = ["module.inventoryAdjustments.validation.uomRequired"];
      }

      if (line.quantity === "" || Number(line.quantity) <= 0) {
        nextErrors[`lines.${index}.quantity`] = ["module.inventoryAdjustments.validation.quantityRequired"];
      }
    });

    return nextErrors;
  }

  function displayError(key: string | undefined) {
    if (!key) {
      return "";
    }

    return key.startsWith("module.inventoryAdjustments.") ? t(key) : key;
  }

  function estimatedBaseQty(line: InventoryAdjustmentLineFormValues) {
    const selectedItem = itemLookup.get(line.itemId);
    if (selectedItem?.baseUomId === line.uomId && line.quantity !== "") {
      return toNumber(line.quantity);
    }

    return line.baseQty;
  }

  async function submit(shouldPost: boolean) {
    const nextErrors = validate(values);
    setErrors(nextErrors);
    setFormError("");

    if (Object.keys(nextErrors).length > 0) {
      return;
    }

    try {
      if (shouldPost) {
        setPosting(true);
      } else {
        setSaving(true);
      }

      const saved = isEdit && inventoryAdjustmentId
        ? await updateInventoryAdjustment(inventoryAdjustmentId, values)
        : await createInventoryAdjustment(values);

      setValues(mapInventoryAdjustmentToFormValues(saved));
      setStatus(saved.status);
      setDocumentMeta(saved);

      if (!isEdit) {
        navigate(`/inventory-adjustments/${saved.id}/edit`, { replace: true });
      }

      if (shouldPost) {
        const posted = await postInventoryAdjustment(saved.id);
        setValues(mapInventoryAdjustmentToFormValues(posted));
        setStatus(posted.status);
        setDocumentMeta(posted);
        showToast({ tone: "success", title: t("module.inventoryAdjustments.toast.postedTitle"), description: t("module.inventoryAdjustments.toast.postedDescription", { value: posted.adjustmentNo }) });
      } else {
        showToast({ tone: "success", title: t("module.inventoryAdjustments.toast.savedTitle"), description: t("module.inventoryAdjustments.toast.savedDescription", { value: saved.adjustmentNo }) });
      }
    } catch (error) {
      if (error instanceof ApiError) {
        setErrors(error.validationErrors ?? {});
        setFormError(error.message);
      } else {
        setFormError(t("module.inventoryAdjustments.form.saveError"));
      }
    } finally {
      setSaving(false);
      setPosting(false);
    }
  }

  async function handlePostExisting() {
    if (!inventoryAdjustmentId) {
      return;
    }

    try {
      setPosting(true);
      setFormError("");
      const posted = await postInventoryAdjustment(inventoryAdjustmentId);
      setValues(mapInventoryAdjustmentToFormValues(posted));
      setStatus(posted.status);
      setDocumentMeta(posted);
      showToast({ tone: "success", title: t("module.inventoryAdjustments.toast.postedTitle"), description: t("module.inventoryAdjustments.toast.postedDescription", { value: posted.adjustmentNo }) });
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : t("module.inventoryAdjustments.form.saveError"));
    } finally {
      setPosting(false);
    }
  }

  async function handleCancel() {
    if (!inventoryAdjustmentId) {
      return;
    }

    const confirmed = await confirm({
      title: "module.inventoryAdjustments.confirm.cancelTitle",
      description: "module.inventoryAdjustments.confirm.cancelDescription",
      confirmLabel: "module.inventoryAdjustments.cancelAdjustment",
      tone: "warning",
    });

    if (!confirmed) {
      return;
    }

    try {
      setCanceling(true);
      setFormError("");
      const canceled = await cancelInventoryAdjustment(inventoryAdjustmentId);
      setValues(mapInventoryAdjustmentToFormValues(canceled));
      setStatus(canceled.status);
      setDocumentMeta(canceled);
      showToast({ tone: "success", title: t("module.inventoryAdjustments.toast.canceledTitle"), description: t("module.inventoryAdjustments.toast.canceledDescription", { value: canceled.adjustmentNo }) });
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : t("module.inventoryAdjustments.form.cancelError"));
    } finally {
      setCanceling(false);
    }
  }

  async function handleDelete() {
    if (!inventoryAdjustmentId) {
      return;
    }

    const confirmed = await confirm({
      title: "module.inventoryAdjustments.confirm.deleteTitle",
      description: "module.inventoryAdjustments.confirm.deleteDescription",
      confirmLabel: "module.inventoryAdjustments.deleteDraft",
      tone: "danger",
    });

    if (!confirmed) {
      return;
    }

    try {
      setDeleting(true);
      setFormError("");
      await deleteInventoryAdjustmentDraft(inventoryAdjustmentId);
      showToast({ tone: "success", title: t("module.inventoryAdjustments.toast.deletedTitle"), description: t("module.inventoryAdjustments.toast.deletedDescription") });
      navigate("/inventory-adjustments", { replace: true });
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : t("module.inventoryAdjustments.form.deleteError"));
    } finally {
      setDeleting(false);
    }
  }

  if (loading) {
    return (
      <div className="hc-list-card">
        <SkeletonLoader />
        <SkeletonLoader />
        <SkeletonLoader />
      </div>
    );
  }

  if (formError && warehouses.length === 0) {
    return <EmptyState title="module.inventoryAdjustments.form.loadErrorTitle" description={formError} />;
  }

  return (
    <DocumentPageLayout
      eyebrow="route.section.inventory"
      title={isEdit ? "module.inventoryAdjustments.form.editTitle" : "module.inventoryAdjustments.form.newTitle"}
      description="module.inventoryAdjustments.form.description"
      status={(
        <div className="hc-inline-cluster">
          <Badge tone={statusTone(status)}>{t(`document.status.${status}`)}</Badge>
          {documentMeta?.postedAt ? <Badge tone="neutral">{t("module.inventoryAdjustments.postedOn", { date: formatDate(documentMeta.postedAt) })}</Badge> : null}
          {documentMeta?.canceledAt ? <Badge tone="neutral">{t("module.inventoryAdjustments.canceledOn", { date: formatDate(documentMeta.canceledAt) })}</Badge> : null}
        </div>
      )}
      actions={(
        <div className="hc-document-actions">
          <Link className="hc-button hc-button--ghost hc-button--md" to="/inventory-adjustments">{t("module.inventoryAdjustments.backToList")}</Link>
          {canSaveDraft || canPostAdjustment ? (
            <>
              {isEdit && canSaveDraft ? <Button disabled={busy} variant="danger" onClick={() => void handleDelete()}>{deleting ? t("common.deleting") : t("module.inventoryAdjustments.deleteDraft")}</Button> : null}
              {canSaveDraft ? <Button disabled={busy} variant="secondary" onClick={() => void submit(false)}>{saving ? t("common.saving") : t("common.saveDraft")}</Button> : null}
              {canPostAdjustment ? <Button disabled={busy} onClick={() => void (canSaveDraft ? submit(true) : handlePostExisting())}>{posting ? t("module.inventoryAdjustments.posting") : t("module.inventoryAdjustments.postAdjustment")}</Button> : null}
            </>
          ) : null}
          {canCancelAdjustment ? <Button disabled={busy} variant="secondary" onClick={() => void handleCancel()}>{canceling ? t("module.inventoryAdjustments.canceling") : t("module.inventoryAdjustments.cancelAdjustment")}</Button> : null}
        </div>
      )}
    >
      {dialog}
      {formError ? <div className="hc-inline-error">{formError}</div> : null}

      <DocumentSection title="module.inventoryAdjustments.form.headerSection" description="module.inventoryAdjustments.form.headerDescription">
        <div className="hc-document-form-grid">
          <Field label={t("module.inventoryAdjustments.adjustmentNo")}>
            <Input disabled value={values.adjustmentNo} placeholder={t("module.inventoryAdjustments.autoNumber")} onChange={(event) => setValue("adjustmentNo", event.target.value)} />
          </Field>
          <Field label={t("module.inventoryAdjustments.adjustmentDate")} required>
            <Input disabled={!canSaveDraft || busy} type="date" value={values.adjustmentDate} onChange={(event) => setValue("adjustmentDate", event.target.value)} />
            {errors.adjustmentDate ? <small className="hc-field-error">{displayError(errors.adjustmentDate[0])}</small> : null}
          </Field>
          <Field label={t("table.warehouse")} required>
            <Select disabled={!canSaveDraft || busy} value={values.warehouseId} onChange={(event) => setValue("warehouseId", event.target.value)}>
              <option value="">{t("module.inventoryAdjustments.selectWarehouse")}</option>
              {warehouses.map((warehouse) => (
                <option key={warehouse.id} value={warehouse.id}>{warehouse.code} - {warehouse.name}</option>
              ))}
            </Select>
            {errors.warehouseId ? <small className="hc-field-error">{displayError(errors.warehouseId[0])}</small> : null}
          </Field>
          <Field label={t("module.inventoryAdjustments.reason")} required>
            <Input disabled={!canSaveDraft || busy} value={values.reason} onChange={(event) => setValue("reason", event.target.value)} />
            {errors.reason ? <small className="hc-field-error">{displayError(errors.reason[0])}</small> : null}
          </Field>
        </div>
        <Field label={t("common.notes")}>
          <Textarea disabled={!canSaveDraft || busy} value={values.notes} onChange={(event) => setValue("notes", event.target.value)} />
        </Field>
      </DocumentSection>

      <DocumentSection title="module.inventoryAdjustments.form.linesSection" description="module.inventoryAdjustments.form.linesDescription">
        {errors.lines ? <div className="hc-inline-error">{displayError(errors.lines[0])}</div> : null}
        <div className="hc-table-wrap">
          <table className="hc-table hc-table--compact">
            <thead>
              <tr>
                <th>{t("table.line")}</th>
                <th>{t("table.item")}</th>
                <th>{t("module.inventoryAdjustments.adjustmentType")}</th>
                <th>{t("module.inventoryAdjustments.quantity")}</th>
                <th>{t("table.uom")}</th>
                <th>{t("module.inventoryAdjustments.baseQuantity")}</th>
                <th>{t("common.notes")}</th>
                {canSaveDraft ? <th /> : null}
              </tr>
            </thead>
            <tbody>
              {values.lines.map((line, index) => (
                <tr key={`${line.lineNo}-${index}`}>
                  <td>{line.lineNo}</td>
                  <td>
                    <Select disabled={!canSaveDraft || busy} value={line.itemId} onChange={(event) => setLineValue(index, "itemId", event.target.value)}>
                      <option value="">{t("module.inventoryAdjustments.selectItem")}</option>
                      {items.map((item) => (
                        <option key={item.id} value={item.id}>{item.code} - {item.name}</option>
                      ))}
                    </Select>
                    {errors[`lines.${index}.itemId`] ? <small className="hc-field-error">{displayError(errors[`lines.${index}.itemId`]?.[0])}</small> : null}
                  </td>
                  <td>
                    <Select disabled={!canSaveDraft || busy} value={line.adjustmentType} onChange={(event) => setLineValue(index, "adjustmentType", event.target.value as InventoryAdjustmentLineFormValues["adjustmentType"])}>
                      <option value="Increase">{t("module.inventoryAdjustments.type.Increase")}</option>
                      <option value="Decrease">{t("module.inventoryAdjustments.type.Decrease")}</option>
                    </Select>
                  </td>
                  <td>
                    <Input disabled={!canSaveDraft || busy} type="number" min="0" step="0.000001" value={line.quantity} onChange={(event) => setLineValue(index, "quantity", event.target.value === "" ? "" : Number(event.target.value))} />
                    {errors[`lines.${index}.quantity`] ? <small className="hc-field-error">{displayError(errors[`lines.${index}.quantity`]?.[0])}</small> : null}
                  </td>
                  <td>
                    <Select disabled={!canSaveDraft || busy} value={line.uomId} onChange={(event) => setLineValue(index, "uomId", event.target.value)}>
                      <option value="">{t("module.inventoryAdjustments.selectUom")}</option>
                      {uoms.map((uom) => (
                        <option key={uom.id} value={uom.id}>{uom.code}</option>
                      ))}
                    </Select>
                    {errors[`lines.${index}.uomId`] ? <small className="hc-field-error">{displayError(errors[`lines.${index}.uomId`]?.[0])}</small> : null}
                  </td>
                  <td>{formatQuantity(estimatedBaseQty(line))}</td>
                  <td>
                    <Input disabled={!canSaveDraft || busy} value={line.notes} onChange={(event) => setLineValue(index, "notes", event.target.value)} />
                  </td>
                  {canSaveDraft ? (
                    <td className="hc-table__actions-cell">
                      <Button disabled={busy || values.lines.length <= 1} type="button" variant="ghost" onClick={() => removeLine(index)}>{t("common.remove")}</Button>
                    </td>
                  ) : null}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {canSaveDraft ? <Button disabled={busy} type="button" variant="secondary" onClick={addLine}>{t("module.inventoryAdjustments.addLine")}</Button> : null}
      </DocumentSection>
    </DocumentPageLayout>
  );
}

import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { DocumentPageLayout, DocumentSection } from "../components/patterns";
import { Badge, Button, EmptyState, Field, Input, Select, SkeletonLoader, Textarea, useConfirmationDialog, useI18n, useToast } from "../components/ui";
import { useAuth } from "../features/auth/AuthProvider";
import { ApiError, type ValidationErrors } from "../services/api";
import {
  cancelInventoryTransfer,
  createInventoryTransfer,
  deleteInventoryTransferDraft,
  getInventoryTransfer,
  mapInventoryTransferToFormValues,
  postInventoryTransfer,
  updateInventoryTransfer,
  type InventoryTransfer,
  type InventoryTransferFormValues,
  type InventoryTransferLineFormValues,
} from "../services/inventoryTransfersApi";
import { getActiveItemsCached, getActiveUomsCached, getActiveWarehousesCached, type Item, type Uom, type Warehouse } from "../services/masterDataApi";
import { Permissions } from "../services/permissions";

const INITIAL_VALUES: InventoryTransferFormValues = {
  transferNo: "",
  transferDate: new Date().toISOString().slice(0, 10),
  sourceWarehouseId: "",
  destinationWarehouseId: "",
  notes: "",
  lines: [
    {
      lineNo: 1,
      itemId: "",
      uomId: "",
      quantity: "",
      baseQty: 0,
      notes: "",
    },
  ],
};

function statusTone(status: InventoryTransfer["status"]) {
  return status === "Posted" ? "success" : status === "Canceled" ? "neutral" : "warning";
}

export function getInventoryTransferFormCapabilities(
  status: InventoryTransfer["status"],
  isEdit: boolean,
  hasPermission: (permission: string) => boolean,
) {
  const isEditable = status === "Draft";
  const canSaveDraft = isEditable && hasPermission(Permissions.InventoryTransferCreate);
  const canPostTransfer = isEditable && hasPermission(Permissions.InventoryTransferPost) && (canSaveDraft || isEdit);
  const canCancelTransfer = status === "Posted" && isEdit && hasPermission(Permissions.InventoryTransferPost);

  return {
    canCancelTransfer,
    canPostTransfer,
    canSaveDraft,
  };
}

function toNumber(value: number | "") {
  return value === "" ? 0 : Number(value);
}

export function InventoryTransferFormPage() {
  const { confirm, dialog } = useConfirmationDialog();
  const { formatDate, formatQuantity, t } = useI18n();
  const { showToast } = useToast();
  const { hasPermission } = useAuth();
  const navigate = useNavigate();
  const { inventoryTransferId } = useParams();
  const isEdit = Boolean(inventoryTransferId);
  const [values, setValues] = useState<InventoryTransferFormValues>(INITIAL_VALUES);
  const [items, setItems] = useState<Item[]>([]);
  const [uoms, setUoms] = useState<Uom[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [status, setStatus] = useState<InventoryTransfer["status"]>("Draft");
  const [documentMeta, setDocumentMeta] = useState<InventoryTransfer | null>(null);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [formError, setFormError] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [posting, setPosting] = useState(false);
  const [canceling, setCanceling] = useState(false);
  const [deleting, setDeleting] = useState(false);

  const { canCancelTransfer, canPostTransfer, canSaveDraft } = getInventoryTransferFormCapabilities(status, isEdit, hasPermission);
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
          inventoryTransferId ? getInventoryTransfer(inventoryTransferId) : Promise.resolve(null),
        ]);

        if (!active) {
          return;
        }

        setItems(itemRows);
        setUoms(uomRows);
        setWarehouses(warehouseRows);

        if (existingDocument) {
          setValues(mapInventoryTransferToFormValues(existingDocument));
          setStatus(existingDocument.status);
          setDocumentMeta(existingDocument);
        } else {
          setValues((current) => ({
            ...current,
            sourceWarehouseId: current.sourceWarehouseId || warehouseRows[0]?.id || "",
            destinationWarehouseId: current.destinationWarehouseId || warehouseRows.find((row) => row.id !== warehouseRows[0]?.id)?.id || "",
          }));
          setStatus("Draft");
          setDocumentMeta(null);
        }
      } catch (loadError) {
        if (active) {
          setFormError(loadError instanceof ApiError ? loadError.message : t("module.inventoryTransfers.form.loadError"));
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
  }, [inventoryTransferId, t]);

  function setValue<K extends keyof InventoryTransferFormValues>(key: K, value: InventoryTransferFormValues[K]) {
    setValues((current) => ({ ...current, [key]: value }));
  }

  function setLineValue<K extends keyof InventoryTransferLineFormValues>(index: number, key: K, value: InventoryTransferLineFormValues[K]) {
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

  function validate(currentValues: InventoryTransferFormValues): ValidationErrors {
    const nextErrors: ValidationErrors = {};

    if (!currentValues.transferDate) {
      nextErrors.transferDate = ["module.inventoryTransfers.validation.dateRequired"];
    }

    if (!currentValues.sourceWarehouseId) {
      nextErrors.sourceWarehouseId = ["module.inventoryTransfers.validation.sourceWarehouseRequired"];
    }

    if (!currentValues.destinationWarehouseId) {
      nextErrors.destinationWarehouseId = ["module.inventoryTransfers.validation.destinationWarehouseRequired"];
    }

    if (currentValues.sourceWarehouseId && currentValues.sourceWarehouseId === currentValues.destinationWarehouseId) {
      nextErrors.destinationWarehouseId = ["module.inventoryTransfers.validation.warehouseDifferent"];
    }

    if (currentValues.lines.length === 0) {
      nextErrors.lines = ["module.inventoryTransfers.validation.linesRequired"];
    }

    currentValues.lines.forEach((line, index) => {
      if (!line.itemId) {
        nextErrors[`lines.${index}.itemId`] = ["module.inventoryTransfers.validation.itemRequired"];
      }

      if (!line.uomId) {
        nextErrors[`lines.${index}.uomId`] = ["module.inventoryTransfers.validation.uomRequired"];
      }

      if (line.quantity === "" || Number(line.quantity) <= 0) {
        nextErrors[`lines.${index}.quantity`] = ["module.inventoryTransfers.validation.quantityRequired"];
      }
    });

    return nextErrors;
  }

  function displayError(key: string | undefined) {
    if (!key) {
      return "";
    }

    return key.startsWith("module.inventoryTransfers.") ? t(key) : key;
  }

  function estimatedBaseQty(line: InventoryTransferLineFormValues) {
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

      const saved = isEdit && inventoryTransferId
        ? await updateInventoryTransfer(inventoryTransferId, values)
        : await createInventoryTransfer(values);

      setValues(mapInventoryTransferToFormValues(saved));
      setStatus(saved.status);
      setDocumentMeta(saved);

      if (!isEdit) {
        navigate(`/inventory-transfers/${saved.id}/edit`, { replace: true });
      }

      if (shouldPost) {
        const posted = await postInventoryTransfer(saved.id);
        setValues(mapInventoryTransferToFormValues(posted));
        setStatus(posted.status);
        setDocumentMeta(posted);
        showToast({ tone: "success", title: t("module.inventoryTransfers.toast.postedTitle"), description: t("module.inventoryTransfers.toast.postedDescription", { value: posted.transferNo }) });
      } else {
        showToast({ tone: "success", title: t("module.inventoryTransfers.toast.savedTitle"), description: t("module.inventoryTransfers.toast.savedDescription", { value: saved.transferNo }) });
      }
    } catch (error) {
      if (error instanceof ApiError) {
        setErrors(error.validationErrors ?? {});
        setFormError(error.message);
      } else {
        setFormError(t("module.inventoryTransfers.form.saveError"));
      }
    } finally {
      setSaving(false);
      setPosting(false);
    }
  }

  async function handlePostExisting() {
    if (!inventoryTransferId) {
      return;
    }

    try {
      setPosting(true);
      setFormError("");
      const posted = await postInventoryTransfer(inventoryTransferId);
      setValues(mapInventoryTransferToFormValues(posted));
      setStatus(posted.status);
      setDocumentMeta(posted);
      showToast({ tone: "success", title: t("module.inventoryTransfers.toast.postedTitle"), description: t("module.inventoryTransfers.toast.postedDescription", { value: posted.transferNo }) });
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : t("module.inventoryTransfers.form.saveError"));
    } finally {
      setPosting(false);
    }
  }

  async function handleCancel() {
    if (!inventoryTransferId) {
      return;
    }

    const confirmed = await confirm({
      title: "module.inventoryTransfers.confirm.cancelTitle",
      description: "module.inventoryTransfers.confirm.cancelDescription",
      confirmLabel: "module.inventoryTransfers.cancelTransfer",
      tone: "warning",
    });

    if (!confirmed) {
      return;
    }

    try {
      setCanceling(true);
      setFormError("");
      const canceled = await cancelInventoryTransfer(inventoryTransferId);
      setValues(mapInventoryTransferToFormValues(canceled));
      setStatus(canceled.status);
      setDocumentMeta(canceled);
      showToast({ tone: "success", title: t("module.inventoryTransfers.toast.canceledTitle"), description: t("module.inventoryTransfers.toast.canceledDescription", { value: canceled.transferNo }) });
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : t("module.inventoryTransfers.form.cancelError"));
    } finally {
      setCanceling(false);
    }
  }

  async function handleDelete() {
    if (!inventoryTransferId) {
      return;
    }

    const confirmed = await confirm({
      title: "module.inventoryTransfers.confirm.deleteTitle",
      description: "module.inventoryTransfers.confirm.deleteDescription",
      confirmLabel: "module.inventoryTransfers.deleteDraft",
      tone: "danger",
    });

    if (!confirmed) {
      return;
    }

    try {
      setDeleting(true);
      setFormError("");
      await deleteInventoryTransferDraft(inventoryTransferId);
      showToast({ tone: "success", title: t("module.inventoryTransfers.toast.deletedTitle"), description: t("module.inventoryTransfers.toast.deletedDescription") });
      navigate("/inventory-transfers", { replace: true });
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : t("module.inventoryTransfers.form.deleteError"));
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
    return <EmptyState title="module.inventoryTransfers.form.loadErrorTitle" description={formError} />;
  }

  return (
    <DocumentPageLayout
      eyebrow="route.section.inventory"
      title={isEdit ? "module.inventoryTransfers.form.editTitle" : "module.inventoryTransfers.form.newTitle"}
      description="module.inventoryTransfers.form.description"
      status={(
        <div className="hc-inline-cluster">
          <Badge tone={statusTone(status)}>{t(`document.status.${status}`)}</Badge>
          {documentMeta?.postedAt ? <Badge tone="neutral">{t("module.inventoryTransfers.postedOn", { date: formatDate(documentMeta.postedAt) })}</Badge> : null}
          {documentMeta?.canceledAt ? <Badge tone="neutral">{t("module.inventoryTransfers.canceledOn", { date: formatDate(documentMeta.canceledAt) })}</Badge> : null}
        </div>
      )}
      actions={(
        <div className="hc-document-actions">
          <Link className="hc-button hc-button--ghost hc-button--md" to="/inventory-transfers">{t("module.inventoryTransfers.backToList")}</Link>
          {canSaveDraft || canPostTransfer ? (
            <>
              {isEdit && canSaveDraft ? <Button disabled={busy} variant="danger" onClick={() => void handleDelete()}>{deleting ? t("common.deleting") : t("module.inventoryTransfers.deleteDraft")}</Button> : null}
              {canSaveDraft ? <Button disabled={busy} variant="secondary" onClick={() => void submit(false)}>{saving ? t("common.saving") : t("common.saveDraft")}</Button> : null}
              {canPostTransfer ? <Button disabled={busy} onClick={() => void (canSaveDraft ? submit(true) : handlePostExisting())}>{posting ? t("module.inventoryTransfers.posting") : t("module.inventoryTransfers.postTransfer")}</Button> : null}
            </>
          ) : null}
          {canCancelTransfer ? <Button disabled={busy} variant="secondary" onClick={() => void handleCancel()}>{canceling ? t("module.inventoryTransfers.canceling") : t("module.inventoryTransfers.cancelTransfer")}</Button> : null}
        </div>
      )}
    >
      {dialog}
      {formError ? <div className="hc-inline-error">{formError}</div> : null}

      <DocumentSection title="module.inventoryTransfers.form.headerSection" description="module.inventoryTransfers.form.headerDescription">
        <div className="hc-document-form-grid">
          <Field label={t("module.inventoryTransfers.transferNo")}>
            <Input disabled value={values.transferNo} placeholder={t("module.inventoryTransfers.autoNumber")} onChange={(event) => setValue("transferNo", event.target.value)} />
          </Field>
          <Field label={t("module.inventoryTransfers.transferDate")} required>
            <Input disabled={!canSaveDraft || busy} type="date" value={values.transferDate} onChange={(event) => setValue("transferDate", event.target.value)} />
            {errors.transferDate ? <small className="hc-field-error">{displayError(errors.transferDate[0])}</small> : null}
          </Field>
          <Field label={t("module.inventoryTransfers.sourceWarehouse")} required>
            <Select disabled={!canSaveDraft || busy} value={values.sourceWarehouseId} onChange={(event) => setValue("sourceWarehouseId", event.target.value)}>
              <option value="">{t("module.inventoryTransfers.selectSourceWarehouse")}</option>
              {warehouses.map((warehouse) => (
                <option key={warehouse.id} value={warehouse.id}>{warehouse.code} - {warehouse.name}</option>
              ))}
            </Select>
            {errors.sourceWarehouseId ? <small className="hc-field-error">{displayError(errors.sourceWarehouseId[0])}</small> : null}
          </Field>
          <Field label={t("module.inventoryTransfers.destinationWarehouse")} required>
            <Select disabled={!canSaveDraft || busy} value={values.destinationWarehouseId} onChange={(event) => setValue("destinationWarehouseId", event.target.value)}>
              <option value="">{t("module.inventoryTransfers.selectDestinationWarehouse")}</option>
              {warehouses.map((warehouse) => (
                <option key={warehouse.id} value={warehouse.id}>{warehouse.code} - {warehouse.name}</option>
              ))}
            </Select>
            {errors.destinationWarehouseId ? <small className="hc-field-error">{displayError(errors.destinationWarehouseId[0])}</small> : null}
          </Field>
        </div>
        <Field label={t("common.notes")}>
          <Textarea disabled={!canSaveDraft || busy} value={values.notes} onChange={(event) => setValue("notes", event.target.value)} />
        </Field>
      </DocumentSection>

      <DocumentSection title="module.inventoryTransfers.form.linesSection" description="module.inventoryTransfers.form.linesDescription">
        {errors.lines ? <div className="hc-inline-error">{displayError(errors.lines[0])}</div> : null}
        <div className="hc-table-wrap">
          <table className="hc-table hc-table--compact">
            <thead>
              <tr>
                <th>{t("table.line")}</th>
                <th>{t("table.item")}</th>
                <th>{t("module.inventoryTransfers.quantity")}</th>
                <th>{t("table.uom")}</th>
                <th>{t("module.inventoryTransfers.baseQuantity")}</th>
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
                      <option value="">{t("module.inventoryTransfers.selectItem")}</option>
                      {items.map((item) => (
                        <option key={item.id} value={item.id}>{item.code} - {item.name}</option>
                      ))}
                    </Select>
                    {errors[`lines.${index}.itemId`] ? <small className="hc-field-error">{displayError(errors[`lines.${index}.itemId`]?.[0])}</small> : null}
                  </td>
                  <td>
                    <Input disabled={!canSaveDraft || busy} type="number" min="0" step="0.000001" value={line.quantity} onChange={(event) => setLineValue(index, "quantity", event.target.value === "" ? "" : Number(event.target.value))} />
                    {errors[`lines.${index}.quantity`] ? <small className="hc-field-error">{displayError(errors[`lines.${index}.quantity`]?.[0])}</small> : null}
                  </td>
                  <td>
                    <Select disabled={!canSaveDraft || busy} value={line.uomId} onChange={(event) => setLineValue(index, "uomId", event.target.value)}>
                      <option value="">{t("module.inventoryTransfers.selectUom")}</option>
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
        {canSaveDraft ? <Button disabled={busy} type="button" variant="secondary" onClick={addLine}>{t("module.inventoryTransfers.addLine")}</Button> : null}
      </DocumentSection>
    </DocumentPageLayout>
  );
}

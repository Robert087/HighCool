import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { DocumentPageLayout, DocumentSection } from "../components/patterns";
import { Badge, Button, EmptyState, Field, Input, Select, SkeletonLoader, Textarea, useConfirmationDialog, useI18n, useToast } from "../components/ui";
import { useAuth } from "../features/auth/AuthProvider";
import { ApiError, type ValidationErrors } from "../services/api";
import {
  cancelInventoryCount,
  createInventoryCount,
  deleteInventoryCountDraft,
  getInventoryCount,
  mapInventoryCountToFormValues,
  postInventoryCount,
  refreshInventoryCountSystemQuantities,
  updateInventoryCount,
  type InventoryCount,
  type InventoryCountFormValues,
  type InventoryCountLineFormValues,
} from "../services/inventoryCountsApi";
import { getActiveItemsCached, getActiveUomsCached, getActiveWarehousesCached, type Item, type Uom, type Warehouse } from "../services/masterDataApi";
import { Permissions } from "../services/permissions";

const INITIAL_VALUES: InventoryCountFormValues = {
  countNo: "",
  countDate: new Date().toISOString().slice(0, 10),
  snapshotAt: null,
  warehouseId: "",
  notes: "",
  lines: [
    {
      lineNo: 1,
      itemId: "",
      uomId: "",
      systemQty: 0,
      countedQty: "",
      varianceQty: 0,
      baseSystemQty: 0,
      baseCountedQty: 0,
      baseVarianceQty: 0,
      notes: "",
    },
  ],
};

function statusTone(status: InventoryCount["status"]) {
  return status === "Posted" ? "success" : status === "Canceled" ? "neutral" : "warning";
}

export function getInventoryCountFormCapabilities(
  status: InventoryCount["status"],
  isEdit: boolean,
  hasPermission: (permission: string) => boolean,
) {
  const isEditable = status === "Draft";
  const canSaveDraft = isEditable && hasPermission(Permissions.InventoryCountCreate);
  const canPostCount = isEditable && hasPermission(Permissions.InventoryCountPost) && (canSaveDraft || isEdit);
  const canCancelCount = status === "Posted" && isEdit && hasPermission(Permissions.InventoryCountPost);

  return {
    canCancelCount,
    canPostCount,
    canSaveDraft,
  };
}

function toNumber(value: number | "") {
  return value === "" ? 0 : Number(value);
}

export function InventoryCountFormPage() {
  const { confirm, dialog } = useConfirmationDialog();
  const { formatDate, formatQuantity, t } = useI18n();
  const { showToast } = useToast();
  const { hasPermission } = useAuth();
  const navigate = useNavigate();
  const { inventoryCountId } = useParams();
  const isEdit = Boolean(inventoryCountId);
  const [values, setValues] = useState<InventoryCountFormValues>(INITIAL_VALUES);
  const [items, setItems] = useState<Item[]>([]);
  const [uoms, setUoms] = useState<Uom[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [status, setStatus] = useState<InventoryCount["status"]>("Draft");
  const [documentMeta, setDocumentMeta] = useState<InventoryCount | null>(null);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [formError, setFormError] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [posting, setPosting] = useState(false);
  const [canceling, setCanceling] = useState(false);
  const [deleting, setDeleting] = useState(false);

  const { canCancelCount, canPostCount, canSaveDraft } = getInventoryCountFormCapabilities(status, isEdit, hasPermission);
  const itemLookup = useMemo(() => new Map(items.map((item) => [item.id, item])), [items]);
  const busy = saving || refreshing || posting || canceling || deleting;

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
          inventoryCountId ? getInventoryCount(inventoryCountId) : Promise.resolve(null),
        ]);

        if (!active) {
          return;
        }

        setItems(itemRows);
        setUoms(uomRows);
        setWarehouses(warehouseRows);

        if (existingDocument) {
          setValues(mapInventoryCountToFormValues(existingDocument));
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
          setFormError(loadError instanceof ApiError ? loadError.message : t("module.inventoryCounts.form.loadError"));
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
  }, [inventoryCountId, t]);

  function setValue<K extends keyof InventoryCountFormValues>(key: K, value: InventoryCountFormValues[K]) {
    setValues((current) => ({ ...current, [key]: value }));
  }

  function setLineValue<K extends keyof InventoryCountLineFormValues>(index: number, key: K, value: InventoryCountLineFormValues[K]) {
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
          };
        }

        if (key === "countedQty") {
          return {
            ...nextLine,
            varianceQty: toNumber(value as number | "") - nextLine.systemQty,
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
          systemQty: 0,
          countedQty: "",
          varianceQty: 0,
          baseSystemQty: 0,
          baseCountedQty: 0,
          baseVarianceQty: 0,
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

  function validate(currentValues: InventoryCountFormValues): ValidationErrors {
    const nextErrors: ValidationErrors = {};

    if (!currentValues.countDate) {
      nextErrors.countDate = ["module.inventoryCounts.validation.dateRequired"];
    }

    if (!currentValues.warehouseId) {
      nextErrors.warehouseId = ["module.inventoryCounts.validation.warehouseRequired"];
    }

    if (currentValues.lines.length === 0) {
      nextErrors.lines = ["module.inventoryCounts.validation.linesRequired"];
    }

    const seenItems = new Set<string>();
    currentValues.lines.forEach((line, index) => {
      if (!line.itemId) {
        nextErrors[`lines.${index}.itemId`] = ["module.inventoryCounts.validation.itemRequired"];
      } else if (seenItems.has(line.itemId)) {
        nextErrors[`lines.${index}.itemId`] = ["module.inventoryCounts.validation.duplicateItem"];
      } else {
        seenItems.add(line.itemId);
      }

      if (!line.uomId) {
        nextErrors[`lines.${index}.uomId`] = ["module.inventoryCounts.validation.uomRequired"];
      }

      if (line.countedQty === "" || Number(line.countedQty) < 0) {
        nextErrors[`lines.${index}.countedQty`] = ["module.inventoryCounts.validation.countedQtyRequired"];
      }
    });

    return nextErrors;
  }

  function displayError(key: string | undefined) {
    if (!key) {
      return "";
    }

    return key.startsWith("module.inventoryCounts.") ? t(key) : key;
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

      const saved = isEdit && inventoryCountId
        ? await updateInventoryCount(inventoryCountId, values)
        : await createInventoryCount(values);

      setValues(mapInventoryCountToFormValues(saved));
      setStatus(saved.status);
      setDocumentMeta(saved);

      if (!isEdit) {
        navigate(`/inventory-counts/${saved.id}/edit`, { replace: true });
      }

      if (shouldPost) {
        const posted = await postInventoryCount(saved.id);
        setValues(mapInventoryCountToFormValues(posted));
        setStatus(posted.status);
        setDocumentMeta(posted);
        showToast({ tone: "success", title: t("module.inventoryCounts.toast.postedTitle"), description: t("module.inventoryCounts.toast.postedDescription", { value: posted.countNo }) });
      } else {
        showToast({ tone: "success", title: t("module.inventoryCounts.toast.savedTitle"), description: t("module.inventoryCounts.toast.savedDescription", { value: saved.countNo }) });
      }
    } catch (error) {
      if (error instanceof ApiError) {
        setErrors(error.validationErrors ?? {});
        setFormError(error.message);
      } else {
        setFormError(t("module.inventoryCounts.form.saveError"));
      }
    } finally {
      setSaving(false);
      setPosting(false);
    }
  }

  async function handleRefresh() {
    if (!inventoryCountId) {
      await submit(false);
      return;
    }

    try {
      setRefreshing(true);
      setFormError("");
      const refreshed = await refreshInventoryCountSystemQuantities(inventoryCountId);
      setValues(mapInventoryCountToFormValues(refreshed));
      setStatus(refreshed.status);
      setDocumentMeta(refreshed);
      showToast({ tone: "success", title: t("module.inventoryCounts.toast.refreshedTitle"), description: t("module.inventoryCounts.toast.refreshedDescription", { value: refreshed.countNo }) });
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : t("module.inventoryCounts.form.refreshError"));
    } finally {
      setRefreshing(false);
    }
  }

  async function handlePostExisting() {
    if (!inventoryCountId) {
      return;
    }

    try {
      setPosting(true);
      setFormError("");
      const posted = await postInventoryCount(inventoryCountId);
      setValues(mapInventoryCountToFormValues(posted));
      setStatus(posted.status);
      setDocumentMeta(posted);
      showToast({ tone: "success", title: t("module.inventoryCounts.toast.postedTitle"), description: t("module.inventoryCounts.toast.postedDescription", { value: posted.countNo }) });
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : t("module.inventoryCounts.form.saveError"));
    } finally {
      setPosting(false);
    }
  }

  async function handleCancel() {
    if (!inventoryCountId) {
      return;
    }

    const confirmed = await confirm({
      title: "module.inventoryCounts.confirm.cancelTitle",
      description: "module.inventoryCounts.confirm.cancelDescription",
      confirmLabel: "module.inventoryCounts.cancelCount",
      tone: "warning",
    });

    if (!confirmed) {
      return;
    }

    try {
      setCanceling(true);
      setFormError("");
      const canceled = await cancelInventoryCount(inventoryCountId);
      setValues(mapInventoryCountToFormValues(canceled));
      setStatus(canceled.status);
      setDocumentMeta(canceled);
      showToast({ tone: "success", title: t("module.inventoryCounts.toast.canceledTitle"), description: t("module.inventoryCounts.toast.canceledDescription", { value: canceled.countNo }) });
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : t("module.inventoryCounts.form.cancelError"));
    } finally {
      setCanceling(false);
    }
  }

  async function handleDelete() {
    if (!inventoryCountId) {
      return;
    }

    const confirmed = await confirm({
      title: "module.inventoryCounts.confirm.deleteTitle",
      description: "module.inventoryCounts.confirm.deleteDescription",
      confirmLabel: "module.inventoryCounts.deleteDraft",
      tone: "danger",
    });

    if (!confirmed) {
      return;
    }

    try {
      setDeleting(true);
      setFormError("");
      await deleteInventoryCountDraft(inventoryCountId);
      showToast({ tone: "success", title: t("module.inventoryCounts.toast.deletedTitle"), description: t("module.inventoryCounts.toast.deletedDescription") });
      navigate("/inventory-counts", { replace: true });
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : t("module.inventoryCounts.form.deleteError"));
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
    return <EmptyState title="module.inventoryCounts.form.loadErrorTitle" description={formError} />;
  }

  return (
    <DocumentPageLayout
      eyebrow="route.section.inventory"
      title={isEdit ? "module.inventoryCounts.form.editTitle" : "module.inventoryCounts.form.newTitle"}
      description="module.inventoryCounts.form.description"
      status={(
        <div className="hc-inline-cluster">
          <Badge tone={statusTone(status)}>{t(`document.status.${status}`)}</Badge>
          {values.snapshotAt ? <Badge tone="neutral">{t("module.inventoryCounts.snapshotAt", { date: formatDate(values.snapshotAt) })}</Badge> : null}
          {documentMeta?.postedAt ? <Badge tone="neutral">{t("module.inventoryCounts.postedOn", { date: formatDate(documentMeta.postedAt) })}</Badge> : null}
          {documentMeta?.canceledAt ? <Badge tone="neutral">{t("module.inventoryCounts.canceledOn", { date: formatDate(documentMeta.canceledAt) })}</Badge> : null}
        </div>
      )}
      actions={(
        <div className="hc-document-actions">
          <Link className="hc-button hc-button--ghost hc-button--md" to="/inventory-counts">{t("module.inventoryCounts.backToList")}</Link>
          {canSaveDraft || canPostCount ? (
            <>
              {isEdit && canSaveDraft ? <Button disabled={busy} variant="danger" onClick={() => void handleDelete()}>{deleting ? t("common.deleting") : t("module.inventoryCounts.deleteDraft")}</Button> : null}
              {canSaveDraft && isEdit ? <Button disabled={busy} variant="secondary" onClick={() => void handleRefresh()}>{refreshing ? t("module.inventoryCounts.refreshing") : t("module.inventoryCounts.refreshQuantities")}</Button> : null}
              {canSaveDraft ? <Button disabled={busy} variant="secondary" onClick={() => void submit(false)}>{saving ? t("common.saving") : t("common.saveDraft")}</Button> : null}
              {canPostCount ? <Button disabled={busy} onClick={() => void (canSaveDraft ? submit(true) : handlePostExisting())}>{posting ? t("module.inventoryCounts.posting") : t("module.inventoryCounts.postCount")}</Button> : null}
            </>
          ) : null}
          {canCancelCount ? <Button disabled={busy} variant="secondary" onClick={() => void handleCancel()}>{canceling ? t("module.inventoryCounts.canceling") : t("module.inventoryCounts.cancelCount")}</Button> : null}
        </div>
      )}
    >
      {dialog}
      {formError ? <div className="hc-inline-error">{formError}</div> : null}

      <DocumentSection title="module.inventoryCounts.form.headerSection" description="module.inventoryCounts.form.headerDescription">
        <div className="hc-document-form-grid">
          <Field label={t("module.inventoryCounts.countNo")}>
            <Input disabled value={values.countNo} placeholder={t("module.inventoryCounts.autoNumber")} onChange={(event) => setValue("countNo", event.target.value)} />
          </Field>
          <Field label={t("module.inventoryCounts.countDate")} required>
            <Input disabled={!canSaveDraft || busy} type="date" value={values.countDate} onChange={(event) => setValue("countDate", event.target.value)} />
            {errors.countDate ? <small className="hc-field-error">{displayError(errors.countDate[0])}</small> : null}
          </Field>
          <Field label={t("module.inventoryCounts.warehouse")} required>
            <Select disabled={!canSaveDraft || busy} value={values.warehouseId} onChange={(event) => setValue("warehouseId", event.target.value)}>
              <option value="">{t("module.inventoryCounts.selectWarehouse")}</option>
              {warehouses.map((warehouse) => (
                <option key={warehouse.id} value={warehouse.id}>{warehouse.code} - {warehouse.name}</option>
              ))}
            </Select>
            {errors.warehouseId ? <small className="hc-field-error">{displayError(errors.warehouseId[0])}</small> : null}
          </Field>
        </div>
        <Field label={t("common.notes")}>
          <Textarea disabled={!canSaveDraft || busy} value={values.notes} onChange={(event) => setValue("notes", event.target.value)} />
        </Field>
      </DocumentSection>

      <DocumentSection title="module.inventoryCounts.form.linesSection" description="module.inventoryCounts.form.linesDescription">
        {errors.lines ? <div className="hc-inline-error">{displayError(errors.lines[0])}</div> : null}
        <div className="hc-table-wrap">
          <table className="hc-table hc-table--compact">
            <thead>
              <tr>
                <th>{t("table.line")}</th>
                <th>{t("table.item")}</th>
                <th>{t("table.uom")}</th>
                <th>{t("module.inventoryCounts.systemQty")}</th>
                <th>{t("module.inventoryCounts.countedQty")}</th>
                <th>{t("module.inventoryCounts.varianceQty")}</th>
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
                      <option value="">{t("module.inventoryCounts.selectItem")}</option>
                      {items.map((item) => (
                        <option key={item.id} value={item.id}>{item.code} - {item.name}</option>
                      ))}
                    </Select>
                    {errors[`lines.${index}.itemId`] ? <small className="hc-field-error">{displayError(errors[`lines.${index}.itemId`]?.[0])}</small> : null}
                  </td>
                  <td>
                    <Select disabled={!canSaveDraft || busy} value={line.uomId} onChange={(event) => setLineValue(index, "uomId", event.target.value)}>
                      <option value="">{t("module.inventoryCounts.selectUom")}</option>
                      {uoms.map((uom) => (
                        <option key={uom.id} value={uom.id}>{uom.code}</option>
                      ))}
                    </Select>
                    {errors[`lines.${index}.uomId`] ? <small className="hc-field-error">{displayError(errors[`lines.${index}.uomId`]?.[0])}</small> : null}
                  </td>
                  <td>{formatQuantity(line.systemQty)}</td>
                  <td>
                    <Input disabled={!canSaveDraft || busy} type="number" min="0" step="0.000001" value={line.countedQty} onChange={(event) => setLineValue(index, "countedQty", event.target.value === "" ? "" : Number(event.target.value))} />
                    {errors[`lines.${index}.countedQty`] ? <small className="hc-field-error">{displayError(errors[`lines.${index}.countedQty`]?.[0])}</small> : null}
                  </td>
                  <td>{formatQuantity(line.varianceQty)}</td>
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
        {canSaveDraft ? <Button disabled={busy} type="button" variant="secondary" onClick={addLine}>{t("module.inventoryCounts.addLine")}</Button> : null}
      </DocumentSection>
    </DocumentPageLayout>
  );
}

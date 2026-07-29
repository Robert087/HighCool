import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { DocumentPageLayout, DocumentSection } from "../components/patterns";
import { Badge, Button, EmptyState, Field, Input, Select, SkeletonLoader, Textarea, useConfirmationDialog, useI18n, useToast } from "../components/ui";
import { useAuth } from "../features/auth/AuthProvider";
import { ApiError, type ValidationErrors } from "../services/api";
import {
  cancelInventoryIssue,
  createInventoryIssue,
  deleteInventoryIssueDraft,
  getInventoryIssue,
  INVENTORY_ISSUE_REASONS,
  mapInventoryIssueToFormValues,
  postInventoryIssue,
  updateInventoryIssue,
  type InventoryIssue,
  type InventoryIssueFormValues,
  type InventoryIssueLineFormValues,
} from "../services/inventoryIssuesApi";
import { getActiveItemsCached, getActiveUomsCached, getActiveWarehousesCached, type Item, type Uom, type Warehouse } from "../services/masterDataApi";
import { Permissions } from "../services/permissions";

const INITIAL_VALUES: InventoryIssueFormValues = {
  issueNo: "",
  issueDate: new Date().toISOString().slice(0, 10),
  warehouseId: "",
  reason: "InternalConsumption",
  referenceNo: "",
  requestedBy: "",
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

function statusTone(status: InventoryIssue["status"]) {
  return status === "Posted" ? "success" : status === "Canceled" ? "neutral" : "warning";
}

export function getInventoryIssueFormCapabilities(
  status: InventoryIssue["status"],
  isEdit: boolean,
  hasPermission: (permission: string) => boolean,
) {
  const isEditable = status === "Draft";
  const canSaveDraft = isEditable && hasPermission(Permissions.InventoryIssueCreate);
  const canPostIssue = isEditable && hasPermission(Permissions.InventoryIssuePost) && (canSaveDraft || isEdit);
  const canCancelIssue = status === "Posted" && isEdit && hasPermission(Permissions.InventoryIssuePost);

  return {
    canCancelIssue,
    canPostIssue,
    canSaveDraft,
  };
}

export function validateInventoryIssueForm(currentValues: InventoryIssueFormValues): ValidationErrors {
  const nextErrors: ValidationErrors = {};

  if (!currentValues.issueDate) {
    nextErrors.issueDate = ["module.inventoryIssues.validation.dateRequired"];
  }

  if (!currentValues.warehouseId) {
    nextErrors.warehouseId = ["module.inventoryIssues.validation.warehouseRequired"];
  }

  if (!currentValues.reason) {
    nextErrors.reason = ["module.inventoryIssues.validation.reasonRequired"];
  }

  if (currentValues.lines.length === 0) {
    nextErrors.lines = ["module.inventoryIssues.validation.linesRequired"];
  }

  const seenItems = new Set<string>();
  currentValues.lines.forEach((line, index) => {
    if (!line.itemId) {
      nextErrors[`lines.${index}.itemId`] = ["module.inventoryIssues.validation.itemRequired"];
    } else if (seenItems.has(line.itemId)) {
      nextErrors[`lines.${index}.itemId`] = ["module.inventoryIssues.validation.duplicateItem"];
    } else {
      seenItems.add(line.itemId);
    }

    if (!line.uomId) {
      nextErrors[`lines.${index}.uomId`] = ["module.inventoryIssues.validation.uomRequired"];
    }

    if (line.quantity === "" || Number(line.quantity) <= 0) {
      nextErrors[`lines.${index}.quantity`] = ["module.inventoryIssues.validation.quantityRequired"];
    }
  });

  return nextErrors;
}

export function InventoryIssueFormPage() {
  const { confirm, dialog } = useConfirmationDialog();
  const { formatDate, formatQuantity, t } = useI18n();
  const { showToast } = useToast();
  const { hasPermission } = useAuth();
  const navigate = useNavigate();
  const { inventoryIssueId } = useParams();
  const isEdit = Boolean(inventoryIssueId);
  const [values, setValues] = useState<InventoryIssueFormValues>(INITIAL_VALUES);
  const [items, setItems] = useState<Item[]>([]);
  const [uoms, setUoms] = useState<Uom[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [status, setStatus] = useState<InventoryIssue["status"]>("Draft");
  const [documentMeta, setDocumentMeta] = useState<InventoryIssue | null>(null);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [formError, setFormError] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [posting, setPosting] = useState(false);
  const [canceling, setCanceling] = useState(false);
  const [deleting, setDeleting] = useState(false);

  const { canCancelIssue, canPostIssue, canSaveDraft } = getInventoryIssueFormCapabilities(status, isEdit, hasPermission);
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
          inventoryIssueId ? getInventoryIssue(inventoryIssueId) : Promise.resolve(null),
        ]);

        if (!active) {
          return;
        }

        setItems(itemRows);
        setUoms(uomRows);
        setWarehouses(warehouseRows);

        if (existingDocument) {
          setValues(mapInventoryIssueToFormValues(existingDocument));
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
          setFormError(loadError instanceof ApiError ? loadError.message : t("module.inventoryIssues.form.loadError"));
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
  }, [inventoryIssueId, t]);

  function setValue<K extends keyof InventoryIssueFormValues>(key: K, value: InventoryIssueFormValues[K]) {
    setValues((current) => ({ ...current, [key]: value }));
  }

  function setLineValue<K extends keyof InventoryIssueLineFormValues>(index: number, key: K, value: InventoryIssueLineFormValues[K]) {
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

        if (key === "quantity") {
          return {
            ...nextLine,
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

  function displayError(key: string | undefined) {
    if (!key) {
      return "";
    }

    return key.startsWith("module.inventoryIssues.") ? t(key) : key;
  }

  function findDocumentLine(line: InventoryIssueLineFormValues) {
    return documentMeta?.lines.find((documentLine) => documentLine.lineNo === line.lineNo && documentLine.itemId === line.itemId);
  }

  function formatDocumentItem(line: InventoryIssueLineFormValues) {
    const documentLine = findDocumentLine(line);
    return documentLine ? `${documentLine.itemCode} - ${documentLine.itemName}` : t("common.notAvailable");
  }

  function formatDocumentUom(line: InventoryIssueLineFormValues) {
    const documentLine = documentMeta?.lines.find((candidate) => candidate.lineNo === line.lineNo && candidate.uomId === line.uomId);
    return documentLine?.uomCode ?? t("common.notAvailable");
  }

  async function submit(shouldPost: boolean) {
    const nextErrors = validateInventoryIssueForm(values);
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

      const saved = isEdit && inventoryIssueId
        ? await updateInventoryIssue(inventoryIssueId, values)
        : await createInventoryIssue(values);

      setValues(mapInventoryIssueToFormValues(saved));
      setStatus(saved.status);
      setDocumentMeta(saved);

      if (!isEdit) {
        navigate(`/inventory-issues/${saved.id}/edit`, { replace: true });
      }

      if (shouldPost) {
        const posted = await postInventoryIssue(saved.id);
        setValues(mapInventoryIssueToFormValues(posted));
        setStatus(posted.status);
        setDocumentMeta(posted);
        showToast({ tone: "success", title: t("module.inventoryIssues.toast.postedTitle"), description: t("module.inventoryIssues.toast.postedDescription", { value: posted.issueNo }) });
      } else {
        showToast({ tone: "success", title: t("module.inventoryIssues.toast.savedTitle"), description: t("module.inventoryIssues.toast.savedDescription", { value: saved.issueNo }) });
      }
    } catch (error) {
      if (error instanceof ApiError) {
        setErrors(error.validationErrors ?? {});
        setFormError(error.message);
      } else {
        setFormError(t("module.inventoryIssues.form.saveError"));
      }
    } finally {
      setSaving(false);
      setPosting(false);
    }
  }

  async function handlePostExisting() {
    if (!inventoryIssueId) {
      return;
    }

    try {
      setPosting(true);
      setFormError("");
      const posted = await postInventoryIssue(inventoryIssueId);
      setValues(mapInventoryIssueToFormValues(posted));
      setStatus(posted.status);
      setDocumentMeta(posted);
      showToast({ tone: "success", title: t("module.inventoryIssues.toast.postedTitle"), description: t("module.inventoryIssues.toast.postedDescription", { value: posted.issueNo }) });
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : t("module.inventoryIssues.form.saveError"));
    } finally {
      setPosting(false);
    }
  }

  async function handleCancel() {
    if (!inventoryIssueId) {
      return;
    }

    const confirmed = await confirm({
      title: "module.inventoryIssues.confirm.cancelTitle",
      description: "module.inventoryIssues.confirm.cancelDescription",
      confirmLabel: "module.inventoryIssues.cancelIssue",
      tone: "warning",
    });

    if (!confirmed) {
      return;
    }

    try {
      setCanceling(true);
      setFormError("");
      const canceled = await cancelInventoryIssue(inventoryIssueId);
      setValues(mapInventoryIssueToFormValues(canceled));
      setStatus(canceled.status);
      setDocumentMeta(canceled);
      showToast({ tone: "success", title: t("module.inventoryIssues.toast.canceledTitle"), description: t("module.inventoryIssues.toast.canceledDescription", { value: canceled.issueNo }) });
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : t("module.inventoryIssues.form.cancelError"));
    } finally {
      setCanceling(false);
    }
  }

  async function handleDelete() {
    if (!inventoryIssueId) {
      return;
    }

    const confirmed = await confirm({
      title: "module.inventoryIssues.confirm.deleteTitle",
      description: "module.inventoryIssues.confirm.deleteDescription",
      confirmLabel: "module.inventoryIssues.deleteDraft",
      tone: "danger",
    });

    if (!confirmed) {
      return;
    }

    try {
      setDeleting(true);
      setFormError("");
      await deleteInventoryIssueDraft(inventoryIssueId);
      showToast({ tone: "success", title: t("module.inventoryIssues.toast.deletedTitle"), description: t("module.inventoryIssues.toast.deletedDescription") });
      navigate("/inventory-issues", { replace: true });
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : t("module.inventoryIssues.form.deleteError"));
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
    return <EmptyState title="module.inventoryIssues.form.loadErrorTitle" description={formError} />;
  }

  return (
    <DocumentPageLayout
      eyebrow="route.section.inventory"
      title={isEdit ? "module.inventoryIssues.form.editTitle" : "module.inventoryIssues.form.newTitle"}
      description="module.inventoryIssues.form.description"
      status={(
        <div className="hc-inline-cluster">
          <Badge tone={statusTone(status)}>{t(`document.status.${status}`)}</Badge>
          {documentMeta?.postedAt ? <Badge tone="neutral">{t("module.inventoryIssues.postedOn", { date: formatDate(documentMeta.postedAt) })}</Badge> : null}
          {documentMeta?.canceledAt ? <Badge tone="neutral">{t("module.inventoryIssues.canceledOn", { date: formatDate(documentMeta.canceledAt) })}</Badge> : null}
        </div>
      )}
      actions={(
        <div className="hc-document-actions">
          <Link className="hc-button hc-button--ghost hc-button--md" to="/inventory-issues">{t("module.inventoryIssues.backToList")}</Link>
          {canSaveDraft || canPostIssue ? (
            <>
              {isEdit && canSaveDraft ? <Button disabled={busy} variant="danger" onClick={() => void handleDelete()}>{deleting ? t("common.deleting") : t("module.inventoryIssues.deleteDraft")}</Button> : null}
              {canSaveDraft ? <Button disabled={busy} variant="secondary" onClick={() => void submit(false)}>{saving ? t("common.saving") : t("common.saveDraft")}</Button> : null}
              {canPostIssue ? <Button disabled={busy} onClick={() => void (canSaveDraft ? submit(true) : handlePostExisting())}>{posting ? t("module.inventoryIssues.posting") : t("module.inventoryIssues.postIssue")}</Button> : null}
            </>
          ) : null}
          {canCancelIssue ? <Button disabled={busy} variant="secondary" onClick={() => void handleCancel()}>{canceling ? t("module.inventoryIssues.canceling") : t("module.inventoryIssues.cancelIssue")}</Button> : null}
        </div>
      )}
    >
      {dialog}
      {formError ? <div className="hc-inline-error">{formError}</div> : null}

      <DocumentSection title="module.inventoryIssues.form.headerSection" description="module.inventoryIssues.form.headerDescription">
        <div className="hc-document-form-grid">
          <Field label={t("module.inventoryIssues.issueNo")}>
            <Input disabled value={values.issueNo} placeholder={t("module.inventoryIssues.autoNumber")} onChange={(event) => setValue("issueNo", event.target.value)} />
          </Field>
          <Field label={t("module.inventoryIssues.issueDate")} required>
            <Input disabled={!canSaveDraft || busy} type="date" value={values.issueDate} onChange={(event) => setValue("issueDate", event.target.value)} />
            {errors.issueDate ? <small className="hc-field-error">{displayError(errors.issueDate[0])}</small> : null}
          </Field>
          <Field label={t("module.inventoryIssues.warehouse")} required>
            {canSaveDraft ? (
              <Select disabled={busy} value={values.warehouseId} onChange={(event) => setValue("warehouseId", event.target.value)}>
                <option value="">{t("module.inventoryIssues.selectWarehouse")}</option>
                {warehouses.map((warehouse) => (
                  <option key={warehouse.id} value={warehouse.id}>{warehouse.code} - {warehouse.name}</option>
                ))}
              </Select>
            ) : (
              <div className="hc-document-readonly">
                <strong>{documentMeta ? `${documentMeta.warehouseCode} - ${documentMeta.warehouseName}` : t("common.notAvailable")}</strong>
              </div>
            )}
            {errors.warehouseId ? <small className="hc-field-error">{displayError(errors.warehouseId[0])}</small> : null}
          </Field>
          <Field label={t("module.inventoryIssues.reason")} required>
            <Select disabled={!canSaveDraft || busy} value={values.reason} onChange={(event) => setValue("reason", event.target.value as InventoryIssueFormValues["reason"])}>
              <option value="">{t("module.inventoryIssues.selectReason")}</option>
              {INVENTORY_ISSUE_REASONS.map((reason) => (
                <option key={reason} value={reason}>{t(`inventory.issueReason.${reason}`)}</option>
              ))}
            </Select>
            {errors.reason ? <small className="hc-field-error">{displayError(errors.reason[0])}</small> : null}
          </Field>
          <Field label={t("module.inventoryIssues.referenceNo")}>
            <Input disabled={!canSaveDraft || busy} value={values.referenceNo} onChange={(event) => setValue("referenceNo", event.target.value)} />
          </Field>
          <Field label={t("module.inventoryIssues.requestedBy")}>
            <Input disabled={!canSaveDraft || busy} value={values.requestedBy} onChange={(event) => setValue("requestedBy", event.target.value)} />
          </Field>
        </div>
        <Field label={t("common.notes")}>
          <Textarea disabled={!canSaveDraft || busy} value={values.notes} onChange={(event) => setValue("notes", event.target.value)} />
        </Field>
      </DocumentSection>

      <DocumentSection title="module.inventoryIssues.form.linesSection" description="module.inventoryIssues.form.linesDescription">
        {errors.lines ? <div className="hc-inline-error">{displayError(errors.lines[0])}</div> : null}
        <div className="hc-table-wrap">
          <table className="hc-table hc-table--compact">
            <thead>
              <tr>
                <th>{t("table.line")}</th>
                <th>{t("table.item")}</th>
                <th>{t("table.uom")}</th>
                <th>{t("module.inventoryIssues.quantity")}</th>
                <th>{t("module.inventoryIssues.baseQty")}</th>
                <th>{t("common.notes")}</th>
                {canSaveDraft ? <th /> : null}
              </tr>
            </thead>
            <tbody>
              {values.lines.map((line, index) => (
                <tr key={`${line.lineNo}-${index}`}>
                  <td>{line.lineNo}</td>
                  <td>
                    {canSaveDraft ? (
                      <Select disabled={busy} value={line.itemId} onChange={(event) => setLineValue(index, "itemId", event.target.value)}>
                        <option value="">{t("module.inventoryIssues.selectItem")}</option>
                        {items.map((item) => (
                          <option key={item.id} value={item.id}>{item.code} - {item.name}</option>
                        ))}
                      </Select>
                    ) : (
                      <span className="hc-table__subtitle">
                        {formatDocumentItem(line)}
                      </span>
                    )}
                    {errors[`lines.${index}.itemId`] ? <small className="hc-field-error">{displayError(errors[`lines.${index}.itemId`]?.[0])}</small> : null}
                  </td>
                  <td>
                    {canSaveDraft ? (
                      <Select disabled={busy} value={line.uomId} onChange={(event) => setLineValue(index, "uomId", event.target.value)}>
                        <option value="">{t("module.inventoryIssues.selectUom")}</option>
                        {uoms.map((uom) => (
                          <option key={uom.id} value={uom.id}>{uom.code}</option>
                        ))}
                      </Select>
                    ) : (
                      <span className="hc-table__subtitle">
                        {formatDocumentUom(line)}
                      </span>
                    )}
                    {errors[`lines.${index}.uomId`] ? <small className="hc-field-error">{displayError(errors[`lines.${index}.uomId`]?.[0])}</small> : null}
                  </td>
                  <td>
                    <Input disabled={!canSaveDraft || busy} type="number" min="0.000001" step="0.000001" value={line.quantity} onChange={(event) => setLineValue(index, "quantity", event.target.value === "" ? "" : Number(event.target.value))} />
                    {errors[`lines.${index}.quantity`] ? <small className="hc-field-error">{displayError(errors[`lines.${index}.quantity`]?.[0])}</small> : null}
                  </td>
                  <td>{formatQuantity(line.baseQty)}</td>
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
        {canSaveDraft ? <Button disabled={busy} type="button" variant="secondary" onClick={addLine}>{t("module.inventoryIssues.addLine")}</Button> : null}
      </DocumentSection>
    </DocumentPageLayout>
  );
}

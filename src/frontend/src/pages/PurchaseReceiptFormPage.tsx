import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { DocumentPageLayout, DocumentSection } from "../components/patterns";
import { Badge, Button, EmptyState, Field, Input, ReversalDialog, Select, SkeletonLoader, Textarea, useConfirmationDialog, useToast } from "../components/ui";
import { useI18n } from "../i18n";
import { ApiError, type ValidationErrors } from "../services/api";
import {
  listItems,
  listSuppliers,
  listUomConversions,
  listUoms,
  listWarehouses,
  type Item,
  type Supplier,
  type Uom,
  type UomConversion,
  type Warehouse,
} from "../services/masterDataApi";
import {
  listAvailablePurchaseOrderLines,
  listPurchaseOrders,
  type PurchaseOrderListItem,
} from "../services/purchaseOrdersApi";
import {
  createPurchaseReceiptDraft,
  getPurchaseReceiptDraft,
  postPurchaseReceipt,
  updatePurchaseReceiptDraft,
  type DocumentStatus,
  type PurchaseReceipt,
  type PurchaseReceiptFormValues,
  type PurchaseReceiptLineComponentFormValues,
  type PurchaseReceiptLineFormValues,
} from "../services/purchaseReceiptsApi";
import { reversePurchaseReceipt } from "../services/reversalsApi";

const initialValues: PurchaseReceiptFormValues = {
  receiptNo: "",
  supplierId: "",
  warehouseId: "",
  purchaseOrderId: "",
  receiptDate: new Date().toISOString().slice(0, 10),
  supplierPayableAmount: 0,
  notes: "",
  reversalDocumentId: null,
  lines: [],
};

const initialLine = (lineNo: number): PurchaseReceiptLineFormValues => ({
  lineNo,
  purchaseOrderLineId: "",
  itemId: "",
  orderedQtySnapshot: "",
  receivedQty: "",
  uomId: "",
  notes: "",
  components: [],
});

function roundQuantity(value: number): number {
  return Math.round(value * 1_000_000) / 1_000_000;
}

function toNumber(value: number | "" | null | undefined): number {
  if (value === "" || value === null || value === undefined || Number.isNaN(Number(value))) {
    return 0;
  }

  return Number(value);
}

function nearlyEqual(left: number, right: number): boolean {
  return Math.abs(left - right) < 0.000001;
}

function buildConversionMap(conversions: UomConversion[]) {
  return new Map(conversions.map((conversion) => [`${conversion.fromUomId}:${conversion.toUomId}`, conversion.factor]));
}

function convertQuantity(
  quantity: number,
  fromUomId: string,
  toUomId: string,
  conversionMap: Map<string, number>,
): number | null {
  if (!fromUomId || !toUomId) {
    return null;
  }

  if (fromUomId === toUomId) {
    return roundQuantity(quantity);
  }

  const factor = conversionMap.get(`${fromUomId}:${toUomId}`);
  if (factor === undefined) {
    return null;
  }

  return roundQuantity(quantity * factor);
}

function calculateShortageQty(component: PurchaseReceiptLineComponentFormValues): number {
  return roundQuantity(component.expectedQty - component.actualReceivedQty);
}

function syncLineComponents(
  line: PurchaseReceiptLineFormValues,
  itemsById: Map<string, Item>,
  conversionMap: Map<string, number>,
): PurchaseReceiptLineFormValues {
  const item = itemsById.get(line.itemId);
  if (!item || item.components.length === 0) {
    return { ...line, components: [] };
  }

  const receivedQty = toNumber(line.receivedQty);
  const receivedBaseQty = convertQuantity(receivedQty, line.uomId, item.baseUomId, conversionMap) ?? 0;

  const components = item.components
    .slice()
    .sort((left, right) => left.componentItemCode.localeCompare(right.componentItemCode))
    .map((definition) => {
      const existing = line.components.find((component) => component.componentItemId === definition.componentItemId);
      const expectedQty = roundQuantity(receivedBaseQty * definition.quantity);
      const shouldResetActual = !existing || nearlyEqual(existing.actualReceivedQty, existing.expectedQty);

      return {
        componentItemId: definition.componentItemId,
        expectedQty,
        actualReceivedQty: shouldResetActual ? expectedQty : existing.actualReceivedQty,
        uomId: definition.uomId,
        shortageReasonCodeId: existing?.shortageReasonCodeId ?? "",
        notes: existing?.notes ?? "",
      };
    });

  return { ...line, components };
}

function mapReceiptToFormValues(receipt: PurchaseReceipt): PurchaseReceiptFormValues {
  return {
    receiptNo: receipt.receiptNo,
    supplierId: receipt.supplierId,
    warehouseId: receipt.warehouseId,
    purchaseOrderId: receipt.purchaseOrderId ?? "",
    receiptDate: receipt.receiptDate.slice(0, 10),
    supplierPayableAmount: receipt.supplierPayableAmount,
    notes: receipt.notes ?? "",
    reversalDocumentId: receipt.reversalDocumentId,
    lines: receipt.lines.map((line) => ({
      lineNo: line.lineNo,
      purchaseOrderLineId: line.purchaseOrderLineId ?? "",
      itemId: line.itemId,
      orderedQtySnapshot: line.orderedQtySnapshot ?? "",
      receivedQty: line.receivedQty,
      uomId: line.uomId,
      notes: line.notes ?? "",
      components: line.components.map((component) => ({
        componentItemId: component.componentItemId,
        expectedQty: component.expectedQty,
        actualReceivedQty: component.actualReceivedQty,
        uomId: component.uomId,
        shortageReasonCodeId: component.shortageReasonCodeId ?? "",
        notes: component.notes ?? "",
      })),
    })),
  };
}

function buildMissingConversionHint(
  formError: string,
  values: PurchaseReceiptFormValues,
  itemsById: Map<string, Item>,
  uomsById: Map<string, Uom>,
  conversionMap: Map<string, number>,
): string | null {
  if (!formError.toLowerCase().includes("global uom conversion")) {
    return null;
  }

  for (const line of values.lines) {
    const item = itemsById.get(line.itemId);
    if (!item || !line.uomId) {
      continue;
    }

    if (line.uomId !== item.baseUomId && !conversionMap.has(`${line.uomId}:${item.baseUomId}`)) {
      const fromUom = uomsById.get(line.uomId)?.code ?? "selected receipt UOM";
      const toUom = item.baseUomCode ?? uomsById.get(item.baseUomId)?.code ?? "item base UOM";
      return `Add an active UOM conversion from ${fromUom} to ${toUom} for line ${line.lineNo} item ${item.code}. This screen only supports stock posting through globally defined conversions.`;
    }

    for (const component of line.components) {
      const definition = item.components.find((row) => row.componentItemId === component.componentItemId);
      if (!definition || !component.uomId) {
        continue;
      }

      if (component.uomId !== definition.componentBaseUomId && !conversionMap.has(`${component.uomId}:${definition.componentBaseUomId}`)) {
        const fromUom = uomsById.get(component.uomId)?.code ?? definition.uomCode;
        const toUom = definition.componentBaseUomCode;
        return `Add an active UOM conversion from ${fromUom} to ${toUom} for component ${definition.componentItemCode} on line ${line.lineNo}. Receipt components must convert cleanly into the component base UOM.`;
      }
    }
  }

  return "Add the missing active global UOM conversion in UOM Conversions, then try saving or posting the receipt again.";
}

export function PurchaseReceiptFormPage() {
  const { showToast } = useToast();
  const { confirm, dialog } = useConfirmationDialog();
  const { t } = useI18n();
  const navigate = useNavigate();
  const { purchaseReceiptId } = useParams();
  const [searchParams] = useSearchParams();
  const requestedPurchaseOrderId = searchParams.get("purchaseOrderId") ?? "";
  const isEdit = Boolean(purchaseReceiptId);
  const [values, setValues] = useState<PurchaseReceiptFormValues>(initialValues);
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [items, setItems] = useState<Item[]>([]);
  const [uoms, setUoms] = useState<Uom[]>([]);
  const [uomConversions, setUomConversions] = useState<UomConversion[]>([]);
  const [purchaseOrders, setPurchaseOrders] = useState<PurchaseOrderListItem[]>([]);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [formError, setFormError] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [posting, setPosting] = useState(false);
  const [reversing, setReversing] = useState(false);
  const [showReversalDialog, setShowReversalDialog] = useState(false);
  const [loadingPurchaseOrderLines, setLoadingPurchaseOrderLines] = useState(false);
  const [status, setStatus] = useState<DocumentStatus>("Draft");
  const [reloadKey, setReloadKey] = useState(0);

  const itemLookup = useMemo(() => new Map(items.map((item) => [item.id, item])), [items]);
  const uomLookup = useMemo(() => new Map(uoms.map((uom) => [uom.id, uom])), [uoms]);
  const conversionMap = useMemo(() => buildConversionMap(uomConversions), [uomConversions]);
  const selectedPurchaseOrder = purchaseOrders.find((purchaseOrder) => purchaseOrder.id === values.purchaseOrderId) ?? null;
  const selectablePurchaseOrders = useMemo(
    () => purchaseOrders.filter((purchaseOrder) =>
      purchaseOrder.status === "Posted" &&
      (purchaseOrder.receiptProgressStatus !== "FullyReceived" || purchaseOrder.id === values.purchaseOrderId)),
    [purchaseOrders, values.purchaseOrderId],
  );
  const isPosted = status === "Posted";
  const isEditable = status === "Draft";
  const isReversed = isPosted && Boolean(values.reversalDocumentId);
  const isPurchaseOrderLinked = Boolean(values.purchaseOrderId);
  const totalComponentRows = values.lines.reduce((sum, line) => sum + line.components.length, 0);
  const formId = "purchase-receipt-form";
  const missingConversionHint = useMemo(
    () => buildMissingConversionHint(formError, values, itemLookup, uomLookup, conversionMap),
    [conversionMap, formError, itemLookup, uomLookup, values],
  );

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        setFormError("");
        const [supplierRows, warehouseRows, itemRows, uomRows, conversionRows, purchaseOrderRows, receipt] = await Promise.all([
          listSuppliers("", "active"),
          listWarehouses("", "active"),
          listItems("", "active"),
          listUoms("", "active"),
          listUomConversions("", "active"),
          listPurchaseOrders({
            search: "",
            status: "",
            receiptProgress: "",
            fromDate: "",
            toDate: "",
            page: 1,
            pageSize: 100,
            sortBy: "orderDate",
            sortDirection: "Desc",
          }).then((result) => result.items),
          purchaseReceiptId ? getPurchaseReceiptDraft(purchaseReceiptId) : Promise.resolve(null),
        ]);

        if (!active) {
          return;
        }

        setSuppliers(supplierRows);
        setWarehouses(warehouseRows);
        setItems(itemRows);
        setUoms(uomRows);
        setUomConversions(conversionRows);
        setPurchaseOrders(purchaseOrderRows);

        if (receipt) {
          setStatus(receipt.status);
          setValues(mapReceiptToFormValues(receipt));
        } else {
          setStatus("Draft");
          setValues((current) => ({
            ...current,
            supplierId: current.supplierId || supplierRows[0]?.id || "",
            warehouseId: current.warehouseId || warehouseRows[0]?.id || "",
            purchaseOrderId: requestedPurchaseOrderId || current.purchaseOrderId,
            lines: current.lines.length > 0 ? current.lines : [initialLine(1)],
          }));
        }
      } catch (loadError) {
        if (active) {
          setFormError(loadError instanceof ApiError ? loadError.message : "Failed to load purchase receipt form.");
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
  }, [purchaseReceiptId, reloadKey, requestedPurchaseOrderId]);

  useEffect(() => {
    if (isEdit || !requestedPurchaseOrderId || loading || values.purchaseOrderId === requestedPurchaseOrderId) {
      return;
    }

    void handlePurchaseOrderSelection(requestedPurchaseOrderId);
  }, [conversionMap, isEdit, loading, requestedPurchaseOrderId, values.purchaseOrderId]);

  function validate(currentValues: PurchaseReceiptFormValues): ValidationErrors {
    const nextErrors: ValidationErrors = {};

    if (!currentValues.supplierId) {
      nextErrors.supplierId = ["Supplier is required."];
    }

    if (!currentValues.warehouseId) {
      nextErrors.warehouseId = ["Warehouse is required."];
    }

    if (!currentValues.receiptDate) {
      nextErrors.receiptDate = ["Receipt date is required."];
    }

    if (currentValues.supplierPayableAmount === "" || Number(currentValues.supplierPayableAmount) < 0) {
      nextErrors.supplierPayableAmount = ["Supplier payable amount cannot be negative."];
    }

    if (currentValues.lines.length === 0) {
      nextErrors.lines = ["At least one line is required."];
    }

    const lineNumbers = currentValues.lines.map((line) => line.lineNo);
    if (new Set(lineNumbers).size !== lineNumbers.length) {
      nextErrors.lines = ["Line numbers must be unique inside the document."];
    }

    currentValues.lines.forEach((line, lineIndex) => {
      const item = itemLookup.get(line.itemId);

      if (!line.itemId) {
        nextErrors[`lines.${lineIndex}.itemId`] = ["Item is required."];
      }

      if (isPurchaseOrderLinked && !line.purchaseOrderLineId) {
        nextErrors[`lines.${lineIndex}.purchaseOrderLineId`] = ["Purchase order line is required for linked receipts."];
      }

      if (isPurchaseOrderLinked && (line.orderedQtySnapshot === "" || toNumber(line.orderedQtySnapshot) <= 0)) {
        nextErrors[`lines.${lineIndex}.orderedQtySnapshot`] = ["Ordered quantity snapshot is required for linked receipts."];
      }

      if (line.receivedQty === "" || toNumber(line.receivedQty) <= 0) {
        nextErrors[`lines.${lineIndex}.receivedQty`] = ["Received quantity must be greater than zero."];
      }

      if (!line.uomId) {
        nextErrors[`lines.${lineIndex}.uomId`] = ["UOM is required."];
      }

      const componentIds = line.components.map((component) => component.componentItemId).filter(Boolean);
      if (new Set(componentIds).size !== componentIds.length) {
        nextErrors[`lines.${lineIndex}.components`] = ["Duplicate component rows are not allowed inside the same line."];
      }

      if (item?.components.length) {
        line.components.forEach((component, componentIndex) => {
          if (component.actualReceivedQty < 0) {
            nextErrors[`lines.${lineIndex}.components.${componentIndex}.actualReceivedQty`] = ["Actual received quantity must be zero or greater."];
          }

        });
      }
    });

    return nextErrors;
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const currentId = purchaseReceiptId;
    const nextErrors = validate(values);
    setErrors(nextErrors);
    setFormError("");

    if (Object.keys(nextErrors).length > 0 || !isEditable) {
      return;
    }

    try {
      setSaving(true);
      const saved = currentId
        ? await updatePurchaseReceiptDraft(currentId, values)
        : await createPurchaseReceiptDraft(values);

      setStatus(saved.status);
      setValues(mapReceiptToFormValues(saved));

      showToast({
        tone: "success",
        title: currentId ? "Purchase receipt updated" : "Purchase receipt created",
        description: `Purchase receipt ${saved.receiptNo} was saved successfully.`,
      });

      navigate(`/purchase-receipts/${saved.id}/edit`);
    } catch (submitError) {
      if (submitError instanceof ApiError) {
        setErrors(submitError.validationErrors ?? {});
        setFormError(submitError.message);
      } else {
        setFormError("Failed to save purchase receipt.");
      }
    } finally {
      setSaving(false);
    }
  }

  async function handlePost() {
    if (!purchaseReceiptId || isPosted) {
      return;
    }

    const confirmed = await confirm({
      title: "Post purchase receipt",
      description: "Posting will write stock ledger entries and shortage ledger entries. After posting, this receipt becomes read-only.",
      confirmLabel: "Post receipt",
      cancelLabel: "Keep as draft",
      tone: "warning",
    });

    if (!confirmed) {
      return;
    }

    try {
      setPosting(true);
      const posted = await postPurchaseReceipt(purchaseReceiptId);
      setValues(mapReceiptToFormValues(posted));
      setStatus(posted.status);
      showToast({ tone: "success", title: "Purchase receipt posted", description: `${posted.receiptNo} was posted successfully.` });
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : "Failed to post purchase receipt.");
    } finally {
      setPosting(false);
    }
  }

  async function handlePurchaseOrderSelection(purchaseOrderId: string) {
    if (!isEditable) {
      return;
    }

    try {
      setLoadingPurchaseOrderLines(true);
      setFormError("");

      if (!purchaseOrderId) {
        setValues((current) => ({
          ...current,
          purchaseOrderId: "",
          lines: current.lines.length > 0
            ? current.lines.map((line, index) => {
                const nextLine: PurchaseReceiptLineFormValues = {
                  ...line,
                  lineNo: index + 1,
                  purchaseOrderLineId: "",
                  orderedQtySnapshot: "",
                };
                return syncLineComponents(nextLine, itemLookup, conversionMap);
              })
            : [initialLine(1)],
        }));
        return;
      }

      const availableLines = await listAvailablePurchaseOrderLines(purchaseOrderId);
      const purchaseOrder = purchaseOrders.find((row) => row.id === purchaseOrderId);

      setValues((current) => ({
        ...current,
        purchaseOrderId,
        supplierId: purchaseOrder?.supplierId ?? current.supplierId,
        lines: availableLines.length > 0
          ? availableLines.map((line, index) =>
              syncLineComponents(
                {
                  lineNo: index + 1,
                  purchaseOrderLineId: line.purchaseOrderLineId,
                  itemId: line.itemId,
                  orderedQtySnapshot: line.orderedQty,
                  receivedQty: line.remainingQty,
                  uomId: line.uomId,
                  notes: line.notes ?? "",
                  components: [],
                },
                itemLookup,
                conversionMap))
          : [initialLine(1)],
      }));

      if (availableLines.length === 0) {
        setFormError("The selected purchase order has no remaining lines available for receipt.");
      }
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : "Failed to load purchase order lines.");
    } finally {
      setLoadingPurchaseOrderLines(false);
    }
  }

  function setValue<Key extends keyof PurchaseReceiptFormValues>(key: Key, value: PurchaseReceiptFormValues[Key]) {
    setValues((current) => ({ ...current, [key]: value }));
  }

  function updateLine(index: number, recipe: (line: PurchaseReceiptLineFormValues) => PurchaseReceiptLineFormValues) {
    setValues((current) => ({
      ...current,
      lines: current.lines.map((line, lineIndex) => {
        if (lineIndex !== index) {
          return line;
        }

        return recipe(line);
      }),
    }));
  }

  function setLineValue<Key extends keyof PurchaseReceiptLineFormValues>(index: number, key: Key, value: PurchaseReceiptLineFormValues[Key]) {
    updateLine(index, (line) => {
      const nextLine = { ...line, [key]: value };

      if (key === "itemId") {
        const item = itemLookup.get(String(value));
        if (item && !isPurchaseOrderLinked) {
          nextLine.uomId = item.baseUomId;
        }
      }

      if (key === "itemId" || key === "receivedQty" || key === "uomId") {
        return syncLineComponents(nextLine, itemLookup, conversionMap);
      }

      return nextLine;
    });
  }

  function setComponentValue<Key extends keyof PurchaseReceiptLineComponentFormValues>(
    lineIndex: number,
    componentIndex: number,
    key: Key,
    value: PurchaseReceiptLineComponentFormValues[Key],
  ) {
    updateLine(lineIndex, (line) => ({
      ...line,
      components: line.components.map((component, nestedIndex) =>
        nestedIndex === componentIndex ? { ...component, [key]: value } : component),
    }));
  }

  function addLine() {
    if (isPurchaseOrderLinked) {
      return;
    }

    setValues((current) => ({ ...current, lines: [...current.lines, initialLine(current.lines.length + 1)] }));
  }

  function removeLine(index: number) {
    setValues((current) => ({
      ...current,
      lines: current.lines.filter((_, lineIndex) => lineIndex !== index).map((line, lineIndex) => ({ ...line, lineNo: lineIndex + 1 })),
    }));
  }

  const documentStatus = (
    <>
      <Badge tone={status === "Posted" ? "success" : status === "Canceled" ? "neutral" : "warning"}>{status}</Badge>
      {isReversed ? <Badge tone="neutral">{t("status.reversed")}</Badge> : null}
      {selectedPurchaseOrder ? <Badge tone="neutral">{selectedPurchaseOrder.poNo}</Badge> : <Badge tone="neutral">{t("Manual receipt")}</Badge>}
    </>
  );

  function renderActionBar() {
    return (
      <>
        <Link className="hc-button hc-button--secondary hc-button--md" to="/purchase-receipts">Close</Link>
        {purchaseReceiptId && status === "Draft" ? <Button type="button" isLoading={posting} onClick={handlePost}>Post receipt</Button> : null}
        {purchaseReceiptId && status === "Posted" && !isReversed ? <Button type="button" variant="secondary" isLoading={reversing} onClick={() => setShowReversalDialog(true)}>Reverse</Button> : null}
        <Button disabled={!isEditable || posting || loadingPurchaseOrderLines} form={formId} isLoading={saving} type="submit">Save draft</Button>
      </>
    );
  }

  async function handleReverse(reason: string) {
    if (!purchaseReceiptId) {
      return;
    }

    try {
      setReversing(true);
      setFormError("");
      await reversePurchaseReceipt(purchaseReceiptId, reason);
      const refreshed = await getPurchaseReceiptDraft(purchaseReceiptId);
      setValues(mapReceiptToFormValues(refreshed));
      setStatus(refreshed.status);
      showToast({
        tone: "success",
        title: "Purchase receipt reversed",
        description: `${refreshed.receiptNo} was reversed successfully.`,
      });
      setShowReversalDialog(false);
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : "Failed to reverse purchase receipt.");
    } finally {
      setReversing(false);
    }
  }

  return (
    <DocumentPageLayout
      eyebrow="Purchasing"
      title={isEdit ? "Purchase Receipt" : "Create Purchase Receipt"}
      description="Capture actual delivered quantities with traceable purchase order context and nested component details."
      status={documentStatus}
      actions={renderActionBar()}
      footer={renderActionBar()}
    >
      <ReversalDialog
        description="This reversal will write opposite stock and supplier statement effects and cancel unresolved shortages for the receipt."
        impactSummary="Reversal is blocked when active purchase returns, supplier payment allocations, or shortage resolutions already depend on this receipt."
        isLoading={reversing}
        onCancel={() => setShowReversalDialog(false)}
        onConfirm={(reason) => void handleReverse(reason)}
        open={showReversalDialog}
        title="Reverse purchase receipt"
      />
      {loading ? (
        <div className="hc-document-section">
          <div className="hc-skeleton-stack">
            <SkeletonLoader height="2.75rem" variant="rect" />
            <SkeletonLoader height="10rem" variant="rect" />
            <SkeletonLoader height="12rem" variant="rect" />
          </div>
        </div>
      ) : null}

      {!loading && formError && suppliers.length === 0 ? (
        <div className="hc-document-section">
          <EmptyState title="Unable to load purchase receipt" description={formError} action={<Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>Retry</Button>} />
        </div>
      ) : null}

      {!loading && (!formError || suppliers.length > 0) ? (
        <form className="hc-document-form" id={formId} onSubmit={handleSubmit}>
          {formError ? <div className="hc-inline-error">{formError}</div> : null}
          {missingConversionHint ? (
            <div className="hc-inline-help">
              <span>{missingConversionHint}</span>
              <Link className="hc-inline-help__link" to="/uom-conversions">
                Open UOM conversions
              </Link>
            </div>
          ) : null}

          <DocumentSection title="Form Header" description="Arrange receiving, supplier, and traceability fields in one structured grid.">
            <div className="hc-document-form-grid">
              <Field label="Receipt No">
                <Input disabled value={values.receiptNo} placeholder="Auto-generated on save" />
              </Field>
              <Field label="Purchase order">
                <Select
                  disabled={!isEditable || loadingPurchaseOrderLines}
                  value={values.purchaseOrderId}
                  onChange={async (event) => {
                    const nextPurchaseOrderId = event.target.value;
                    if (nextPurchaseOrderId === values.purchaseOrderId) {
                      return;
                    }

                    if (values.lines.length > 0 && nextPurchaseOrderId) {
                      const confirmed = await confirm({
                        title: "Change purchase order",
                        description: "Changing the purchase order will replace the current receipt lines with lines from the new order.",
                        confirmLabel: "Change purchase order",
                        cancelLabel: "Keep current order",
                        tone: "warning",
                      });

                      if (!confirmed) {
                        return;
                      }
                    }

                    if (values.lines.length > 0 && !nextPurchaseOrderId && values.purchaseOrderId) {
                      const confirmed = await confirm({
                        title: "Switch to manual receipt",
                        description: "This will clear the current purchase-order-linked lines so you can enter receipt lines manually.",
                        confirmLabel: "Switch to manual",
                        cancelLabel: "Keep linked order",
                        tone: "warning",
                      });

                      if (!confirmed) {
                        return;
                      }
                    }

                    await handlePurchaseOrderSelection(nextPurchaseOrderId);
                  }}
                >
                  <option value="">Manual receipt</option>
                  {selectablePurchaseOrders.map((purchaseOrder) => (
                    <option key={purchaseOrder.id} value={purchaseOrder.id}>
                      {purchaseOrder.poNo} - {purchaseOrder.supplierName}
                    </option>
                  ))}
                </Select>
              </Field>
              <Field label="Supplier" required>
                <Select disabled={!isEditable || isPurchaseOrderLinked} value={values.supplierId} onChange={(event) => setValue("supplierId", event.target.value)}>
                  <option value="">Select supplier</option>
                  {suppliers.map((supplier) => (
                    <option key={supplier.id} value={supplier.id}>{supplier.code} - {supplier.name}</option>
                  ))}
                </Select>
                {errors.supplierId ? <small className="hc-field-error">{errors.supplierId[0]}</small> : null}
              </Field>
              <Field label="Warehouse" required>
                <Select disabled={!isEditable} value={values.warehouseId} onChange={(event) => setValue("warehouseId", event.target.value)}>
                  <option value="">Select warehouse</option>
                  {warehouses.map((warehouse) => (
                    <option key={warehouse.id} value={warehouse.id}>{warehouse.code} - {warehouse.name}</option>
                  ))}
                </Select>
                {errors.warehouseId ? <small className="hc-field-error">{errors.warehouseId[0]}</small> : null}
              </Field>
              <Field label="Receipt date" required>
                <Input disabled={!isEditable} type="date" value={values.receiptDate} onChange={(event) => setValue("receiptDate", event.target.value)} />
                {errors.receiptDate ? <small className="hc-field-error">{errors.receiptDate[0]}</small> : null}
              </Field>
              <Field label="Supplier payable amount" required>
                <Input
                  disabled={!isEditable}
                  min={0}
                  step="0.000001"
                  type="number"
                  value={values.supplierPayableAmount}
                  onChange={(event) => setValue("supplierPayableAmount", event.target.value === "" ? "" : Number(event.target.value))}
                />
                <div className="hc-field__hint">Used for supplier statement and payment allocation until receipt pricing is modeled per line.</div>
                {errors.supplierPayableAmount ? <small className="hc-field-error">{errors.supplierPayableAmount[0]}</small> : null}
              </Field>
              <Field className="hc-document-field--summary" label="Receipt mode">
                <div className="hc-document-readonly">
                  <strong>{isPurchaseOrderLinked ? "Purchase order linked" : "Manual receipt"}</strong>
                  <div className="hc-field__hint">{selectedPurchaseOrder ? selectedPurchaseOrder.supplierName : "No purchase order linked."}</div>
                </div>
              </Field>
              <Field className="hc-document-field--summary" label="Document status">
                <div className="hc-document-readonly">
                  <strong>{status}</strong>
                  <div className="hc-field__hint">{selectedPurchaseOrder ? `Source order ${selectedPurchaseOrder.poNo}` : "Standalone receiving workflow."}</div>
                </div>
              </Field>
              <Field className="hc-document-field--span-full" label="Notes">
              <Textarea disabled={!isEditable} value={values.notes} onChange={(event) => setValue("notes", event.target.value)} />
              </Field>
            </div>
          </DocumentSection>

          <DocumentSection
            className="hc-allocation-stage hc-allocation-stage--first hc-allocation-stage--selected"
            title="Lines Grid"
            description={isPurchaseOrderLinked ? "PO-linked lines keep ordered snapshots from the source purchase order." : "Selecting an item auto-loads component expectations from its BOM."}
            actions={(
              <div className="hc-document-toolbar">
                <div className="hc-document-toolbar__meta">
                  {isPurchaseOrderLinked && selectedPurchaseOrder ? <Badge tone="primary">{t("Linked to {value}", { value: selectedPurchaseOrder.poNo })}</Badge> : null}
                  <Badge tone="neutral">{values.lines.length === 1 ? t("{count} line", { count: values.lines.length }) : t("{count} lines", { count: values.lines.length })}</Badge>
                  <Badge tone="neutral">{totalComponentRows === 1 ? t("{count} component row", { count: totalComponentRows }) : t("{count} component rows", { count: totalComponentRows })}</Badge>
                </div>
                <Button disabled={!isEditable || isPurchaseOrderLinked} type="button" onClick={addLine}>Add line</Button>
              </div>
            )}
          >
            {errors.lines ? <div className="hc-inline-error">{errors.lines[0]}</div> : null}
            <div className="hc-document-table-wrap hc-document-table-wrap--task hc-document-table-wrap--selected">
              <table className="hc-table hc-table--compact">
                <thead>
                  <tr>
                    <th>Line</th>
                    <th>Item</th>
                    <th className="hc-table__numeric">Ordered Snapshot</th>
                    <th className="hc-table__numeric">Received Qty</th>
                    <th>UOM</th>
                    <th>Notes</th>
                    <th className="hc-table__head-actions" />
                  </tr>
                </thead>
                <tbody>
                  {values.lines.map((line, lineIndex) => {
                    const item = itemLookup.get(line.itemId);

                    return [
                      <tr key={`${line.lineNo}-${lineIndex}`}>
                        <td className="hc-table__numeric">{line.lineNo}</td>
                        <td>
                          <div className="hc-table__cell-strong">
                            <Select disabled={!isEditable || isPurchaseOrderLinked} value={line.itemId} onChange={(event) => setLineValue(lineIndex, "itemId", event.target.value)}>
                              <option value="">Select item</option>
                              {items.map((row) => (
                                <option key={row.id} value={row.id}>{row.code} - {row.name}</option>
                              ))}
                            </Select>
                            {item ? <span className="hc-table__subtitle">{item.code} · {item.name}</span> : null}
                            {errors[`lines.${lineIndex}.itemId`] ? <small className="hc-field-error">{errors[`lines.${lineIndex}.itemId`][0]}</small> : null}
                          </div>
                        </td>
                        <td className="hc-table__numeric">
                          <Input disabled type="number" value={line.orderedQtySnapshot} placeholder={isPurchaseOrderLinked ? "From PO" : "Optional"} />
                          {errors[`lines.${lineIndex}.orderedQtySnapshot`] ? <small className="hc-field-error">{errors[`lines.${lineIndex}.orderedQtySnapshot`][0]}</small> : null}
                        </td>
                        <td className="hc-table__numeric">
                          <Input
                            disabled={!isEditable}
                            type="number"
                            min={0.000001}
                            step="0.000001"
                            value={line.receivedQty}
                            onChange={(event) => setLineValue(lineIndex, "receivedQty", event.target.value === "" ? "" : Number(event.target.value))}
                          />
                          {errors[`lines.${lineIndex}.receivedQty`] ? <small className="hc-field-error">{errors[`lines.${lineIndex}.receivedQty`][0]}</small> : null}
                        </td>
                        <td>
                          <div className="hc-table__cell-strong">
                            <Select disabled={!isEditable || isPurchaseOrderLinked} value={line.uomId} onChange={(event) => setLineValue(lineIndex, "uomId", event.target.value)}>
                              <option value="">Select UOM</option>
                              {uoms.map((uom) => (
                                <option key={uom.id} value={uom.id}>{uom.code}</option>
                              ))}
                            </Select>
                            {line.uomId ? <span className="hc-table__subtitle">{uomLookup.get(line.uomId)?.name ?? "Selected UOM"}</span> : null}
                            {errors[`lines.${lineIndex}.uomId`] ? <small className="hc-field-error">{errors[`lines.${lineIndex}.uomId`][0]}</small> : null}
                          </div>
                        </td>
                        <td>
                          <Input disabled={!isEditable} value={line.notes} onChange={(event) => setLineValue(lineIndex, "notes", event.target.value)} placeholder={item?.components.length ? "Components auto-filled below" : ""} />
                        </td>
                        <td className="hc-table__cell-actions">
                          <Button disabled={!isEditable} type="button" variant="ghost" onClick={() => removeLine(lineIndex)}>Remove</Button>
                        </td>
                      </tr>,
                      <tr key={`components-${line.lineNo}-${lineIndex}`} className="hc-table__detail-row">
                        <td colSpan={7}>
                          <div className="hc-line-components hc-line-components--panel">
                            <div className="hc-line-components__header">
                              <div className="po-form-toolbar">
                                <Badge tone="neutral">{item?.code ?? t("No item selected")}</Badge>
                                <Badge tone="neutral">{line.components.length === 1 ? t("{count} component", { count: line.components.length }) : t("{count} components", { count: line.components.length })}</Badge>
                              </div>
                              <p className="hc-line-components__description">
                                {item?.components.length
                                  ? "Expected quantities are system-derived from the item BOM and receipt quantity. Actual quantities remain editable."
                                  : "The selected item has no BOM components."}
                              </p>
                            </div>

                            {errors[`lines.${lineIndex}.components`] ? <div className="hc-inline-error">{errors[`lines.${lineIndex}.components`][0]}</div> : null}

                            <div className="hc-document-table-wrap hc-document-table-wrap--task hc-document-table-wrap--available">
                              <table className="hc-table hc-table--compact">
                                <thead>
                                  <tr>
                                    <th>Component Item</th>
                                    <th className="hc-table__numeric">Expected Qty</th>
                                    <th className="hc-table__numeric">Actual Qty</th>
                                    <th>Shortage</th>
                                    <th>Notes</th>
                                  </tr>
                                </thead>
                                <tbody>
                                  {line.components.length > 0 ? (
                                    line.components.map((component, componentIndex) => {
                                      const componentDefinition = item?.components.find((definition) => definition.componentItemId === component.componentItemId);
                                      const shortageQty = calculateShortageQty(component);
                                      const hasShortage = shortageQty > 0;
                                      const shortageError = errors[`lines.${lineIndex}.components.${componentIndex}.shortageReasonCodeId`];

                                      return (
                                        <tr key={`${component.componentItemId}-${componentIndex}`}>
                                          <td>
                                            <div className="hc-table__cell-strong">
                                              <span className="hc-table__title">{componentDefinition?.componentItemName ?? component.componentItemId}</span>
                                              <span className="hc-table__subtitle">{componentDefinition?.componentItemCode ?? component.uomId}</span>
                                            </div>
                                          </td>
                                          <td className="hc-table__numeric">
                                            <div className="hc-table__cell-strong hc-table__cell-strong--numeric">
                                              <span className="hc-table__title">{component.expectedQty.toLocaleString()}</span>
                                              <span className="hc-table__subtitle">{componentDefinition?.uomCode ?? ""}</span>
                                            </div>
                                          </td>
                                          <td className="hc-table__numeric">
                                            <Input
                                              disabled={!isEditable}
                                              type="number"
                                              min={0}
                                              step="0.000001"
                                              value={component.actualReceivedQty}
                                              onChange={(event) => setComponentValue(lineIndex, componentIndex, "actualReceivedQty", Number(event.target.value))}
                                            />
                                            {errors[`lines.${lineIndex}.components.${componentIndex}.actualReceivedQty`]
                                              ? <small className="hc-field-error">{errors[`lines.${lineIndex}.components.${componentIndex}.actualReceivedQty`][0]}</small>
                                              : null}
                                          </td>
                                          <td>
                                            {hasShortage
                                              ? <Badge tone="warning">Short {shortageQty.toLocaleString()}</Badge>
                                              : <Badge tone="success">No shortage</Badge>}
                                          </td>
                                          <td>
                                            <Input disabled={!isEditable} value={component.notes} onChange={(event) => setComponentValue(lineIndex, componentIndex, "notes", event.target.value)} />
                                            {shortageError ? <small className="hc-field-error">{shortageError[0]}</small> : null}
                                          </td>
                                        </tr>
                                      );
                                    })
                                  ) : (
                                    <tr>
                                      <td colSpan={5}>
                                        <div className="hc-table__empty">No components defined for this item.</div>
                                      </td>
                                    </tr>
                                  )}
                                </tbody>
                              </table>
                            </div>
                          </div>
                        </td>
                      </tr>
                    ];
                  })}
                </tbody>
              </table>
            </div>
          </DocumentSection>
        </form>
      ) : null}
      {dialog}
    </DocumentPageLayout>
  );
}

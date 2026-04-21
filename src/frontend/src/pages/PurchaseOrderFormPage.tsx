import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { FormPageLayout, FormSection } from "../components/patterns";
import { Badge, Button, EmptyState, Field, Input, Select, SkeletonLoader, Textarea, useToast } from "../components/ui";
import { ApiError, type ValidationErrors } from "../services/api";
import { listItems, listSuppliers, listUoms, type Item, type Supplier, type Uom } from "../services/masterDataApi";
import {
  cancelPurchaseOrder,
  createPurchaseOrder,
  getPurchaseOrder,
  postPurchaseOrder,
  updatePurchaseOrder,
  type PurchaseOrderFormValues,
  type PurchaseOrderLineFormValues,
} from "../services/purchaseOrdersApi";

const initialValues: PurchaseOrderFormValues = {
  poNo: "",
  supplierId: "",
  orderDate: new Date().toISOString().slice(0, 10),
  expectedDate: "",
  notes: "",
  lines: [],
};

const initialLine = (lineNo: number): PurchaseOrderLineFormValues => ({
  lineNo,
  itemId: "",
  orderedQty: "",
  uomId: "",
  notes: "",
});

export function PurchaseOrderFormPage() {
  const { showToast } = useToast();
  const navigate = useNavigate();
  const { purchaseOrderId } = useParams();
  const isEdit = Boolean(purchaseOrderId);
  const [values, setValues] = useState<PurchaseOrderFormValues>(initialValues);
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [items, setItems] = useState<Item[]>([]);
  const [uoms, setUoms] = useState<Uom[]>([]);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [formError, setFormError] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [posting, setPosting] = useState(false);
  const [canceling, setCanceling] = useState(false);
  const [status, setStatus] = useState<"Draft" | "Posted" | "Canceled">("Draft");
  const [receiptProgressStatus, setReceiptProgressStatus] = useState<"NotReceived" | "PartiallyReceived" | "FullyReceived">("NotReceived");
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        const [supplierRows, itemRows, uomRows, purchaseOrder] = await Promise.all([
          listSuppliers("", "active"),
          listItems("", "active"),
          listUoms("", "active"),
          purchaseOrderId ? getPurchaseOrder(purchaseOrderId) : Promise.resolve(null),
        ]);

        if (!active) {
          return;
        }

        setSuppliers(supplierRows);
        setItems(itemRows);
        setUoms(uomRows);

        if (purchaseOrder) {
          setStatus(purchaseOrder.status);
          setReceiptProgressStatus(purchaseOrder.receiptProgressStatus);
          setValues({
            poNo: purchaseOrder.poNo,
            supplierId: purchaseOrder.supplierId,
            orderDate: purchaseOrder.orderDate.slice(0, 10),
            expectedDate: purchaseOrder.expectedDate?.slice(0, 10) ?? "",
            notes: purchaseOrder.notes ?? "",
            lines: purchaseOrder.lines.map((line) => ({
              lineNo: line.lineNo,
              itemId: line.itemId,
              orderedQty: line.orderedQty,
              uomId: line.uomId,
              notes: line.notes ?? "",
            })),
          });
        } else {
          setStatus("Draft");
          setReceiptProgressStatus("NotReceived");
          setValues((current) => ({
            ...current,
            supplierId: current.supplierId || supplierRows[0]?.id || "",
            lines: current.lines.length > 0 ? current.lines : [initialLine(1)],
          }));
        }
      } catch (loadError) {
        if (active) {
          setFormError(loadError instanceof ApiError ? loadError.message : "Failed to load purchase order form.");
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
  }, [purchaseOrderId, reloadKey]);

  const isEditable = status === "Draft";
  const supplierLookup = useMemo(() => new Map(suppliers.map((supplier) => [supplier.id, supplier])), [suppliers]);
  const selectedSupplier = values.supplierId ? supplierLookup.get(values.supplierId) : null;
  const totalOrderedQty = values.lines.reduce((sum, line) => sum + (line.orderedQty === "" ? 0 : Number(line.orderedQty)), 0);

  function validate(currentValues: PurchaseOrderFormValues): ValidationErrors {
    const nextErrors: ValidationErrors = {};

    if (!currentValues.supplierId) {
      nextErrors.supplierId = ["Supplier is required."];
    }

    if (!currentValues.orderDate) {
      nextErrors.orderDate = ["Order date is required."];
    }

    if (currentValues.lines.length === 0) {
      nextErrors.lines = ["At least one line is required."];
    }

    const lineNumbers = currentValues.lines.map((line) => line.lineNo);
    if (new Set(lineNumbers).size !== lineNumbers.length) {
      nextErrors.lines = ["Line numbers must be unique inside the document."];
    }

    currentValues.lines.forEach((line, index) => {
      if (!line.itemId) {
        nextErrors[`lines.${index}.itemId`] = ["Item is required."];
      }

      if (line.orderedQty === "" || Number(line.orderedQty) <= 0) {
        nextErrors[`lines.${index}.orderedQty`] = ["Ordered quantity must be greater than zero."];
      }

      if (!line.uomId) {
        nextErrors[`lines.${index}.uomId`] = ["UOM is required."];
      }
    });

    return nextErrors;
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const currentId = purchaseOrderId;
    const nextErrors = validate(values);
    setErrors(nextErrors);
    setFormError("");

    if (Object.keys(nextErrors).length > 0 || !isEditable) {
      return;
    }

    try {
      setSaving(true);
      const saved = currentId
        ? await updatePurchaseOrder(currentId, values)
        : await createPurchaseOrder(values);

      setStatus(saved.status);
      setReceiptProgressStatus(saved.receiptProgressStatus);
      showToast({
        tone: "success",
        title: currentId ? "Purchase order updated" : "Purchase order created",
        description: `Purchase order ${saved.poNo} was saved successfully.`,
      });

      navigate(`/purchase-orders/${saved.id}/edit`);
    } catch (submitError) {
      if (submitError instanceof ApiError) {
        setErrors(submitError.validationErrors ?? {});
        setFormError(submitError.message);
      } else {
        setFormError("Failed to save purchase order.");
      }
    } finally {
      setSaving(false);
    }
  }

  async function handlePost() {
    if (!purchaseOrderId || status !== "Draft") {
      return;
    }

    if (!window.confirm("Post this purchase order? Posted orders become immutable except through cancel.")) {
      return;
    }

    try {
      setPosting(true);
      const posted = await postPurchaseOrder(purchaseOrderId);
      setStatus(posted.status);
      setReceiptProgressStatus(posted.receiptProgressStatus);
      showToast({ tone: "success", title: "Purchase order posted", description: `${posted.poNo} is now ready for receipt capture.` });
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : "Failed to post purchase order.");
    } finally {
      setPosting(false);
    }
  }

  async function handleCancel() {
    if (!purchaseOrderId || status !== "Posted") {
      return;
    }

    if (!window.confirm("Cancel this purchase order?")) {
      return;
    }

    try {
      setCanceling(true);
      const canceled = await cancelPurchaseOrder(purchaseOrderId);
      setStatus(canceled.status);
      setReceiptProgressStatus(canceled.receiptProgressStatus);
      showToast({ tone: "success", title: "Purchase order canceled", description: `${canceled.poNo} was canceled.` });
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : "Failed to cancel purchase order.");
    } finally {
      setCanceling(false);
    }
  }

  function setValue<Key extends keyof PurchaseOrderFormValues>(key: Key, value: PurchaseOrderFormValues[Key]) {
    setValues((current) => ({ ...current, [key]: value }));
  }

  function setLineValue<Key extends keyof PurchaseOrderLineFormValues>(index: number, key: Key, value: PurchaseOrderLineFormValues[Key]) {
    setValues((current) => ({
      ...current,
      lines: current.lines.map((line, lineIndex) => (lineIndex === index ? { ...line, [key]: value } : line)),
    }));
  }

  function addLine() {
    setValues((current) => ({ ...current, lines: [...current.lines, initialLine(current.lines.length + 1)] }));
  }

  function removeLine(index: number) {
    setValues((current) => ({
      ...current,
      lines: current.lines.filter((_, lineIndex) => lineIndex !== index).map((line, lineIndex) => ({ ...line, lineNo: lineIndex + 1 })),
    }));
  }

  return (
    <FormPageLayout width="wide">
      {loading ? (
        <div className="hc-card hc-card--md">
          <div className="hc-skeleton-stack">
            <SkeletonLoader height="2.75rem" variant="rect" />
            <SkeletonLoader height="10rem" variant="rect" />
            <SkeletonLoader height="12rem" variant="rect" />
          </div>
        </div>
      ) : null}

      {!loading && formError && suppliers.length === 0 ? (
        <div className="hc-card hc-card--md">
          <EmptyState title="Unable to load purchase order" description={formError} action={<Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>Retry</Button>} />
        </div>
      ) : null}

      {!loading && (!formError || suppliers.length > 0) ? (
        <form className="hc-form-stack" onSubmit={handleSubmit}>
          {formError ? <div className="hc-inline-error">{formError}</div> : null}

          <FormSection title="Header" description="Supplier, dates, and purchase order status.">
            <div className="hc-form-grid">
              <Field label="PO No">
                <Input disabled value={values.poNo} placeholder="Auto-generated on save" />
              </Field>
              <Field label="Supplier" required>
                <Select disabled={!isEditable} value={values.supplierId} onChange={(event) => setValue("supplierId", event.target.value)}>
                  <option value="">Select supplier</option>
                  {suppliers.map((supplier) => (
                    <option key={supplier.id} value={supplier.id}>{supplier.code} - {supplier.name}</option>
                  ))}
                </Select>
                {errors.supplierId ? <small className="hc-field-error">{errors.supplierId[0]}</small> : null}
              </Field>
              <Field label="Order date" required>
                <Input disabled={!isEditable} type="date" value={values.orderDate} onChange={(event) => setValue("orderDate", event.target.value)} />
                {errors.orderDate ? <small className="hc-field-error">{errors.orderDate[0]}</small> : null}
              </Field>
              <Field label="Expected date">
                <Input disabled={!isEditable} type="date" value={values.expectedDate} onChange={(event) => setValue("expectedDate", event.target.value)} />
              </Field>
              <Field label="Document status">
                <div className="po-form-toolbar">
                  <Badge tone={status === "Posted" ? "success" : status === "Canceled" ? "neutral" : "warning"}>{status}</Badge>
                  <Badge tone={receiptProgressStatus === "FullyReceived" ? "success" : receiptProgressStatus === "PartiallyReceived" ? "warning" : "neutral"}>
                    {receiptProgressStatus}
                  </Badge>
                  {values.poNo ? <Badge tone="primary">{values.poNo}</Badge> : <Badge tone="neutral">New Draft</Badge>}
                </div>
              </Field>
              <Field label="Supplier summary">
                <div className="po-summary-card">
                  <strong>{selectedSupplier ? selectedSupplier.name : "No supplier selected"}</strong>
                  <div className="hc-field__hint">{selectedSupplier ? selectedSupplier.code : "Select a supplier to continue."}</div>
                </div>
              </Field>
            </div>

            <Field label="Notes">
              <Textarea disabled={!isEditable} value={values.notes} onChange={(event) => setValue("notes", event.target.value)} />
            </Field>
          </FormSection>

          <FormSection title="Lines" description="Ordered items and quantities. Keep this grid aligned to how receipts will be captured later.">
            {errors.lines ? <div className="hc-inline-error">{errors.lines[0]}</div> : null}
            <div className="po-form-toolbar po-form-toolbar--split">
              <div className="po-form-toolbar">
                <Badge tone="neutral">{values.lines.length} {values.lines.length === 1 ? "line" : "lines"}</Badge>
                <Badge tone="neutral">Ordered qty {totalOrderedQty.toLocaleString()}</Badge>
              </div>
              <Button disabled={!isEditable} type="button" onClick={addLine}>Add line</Button>
            </div>
            <div className="hc-card hc-card--muted hc-card--sm">
              <table className="hc-table">
                <thead>
                  <tr>
                    <th>Line</th>
                    <th>Item</th>
                    <th>Ordered Qty</th>
                    <th>UOM</th>
                    <th>Notes</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {values.lines.map((line, index) => (
                    <tr key={`${line.lineNo}-${index}`}>
                      <td>{line.lineNo}</td>
                      <td>
                        <Select disabled={!isEditable} value={line.itemId} onChange={(event) => setLineValue(index, "itemId", event.target.value)}>
                          <option value="">Select item</option>
                          {items.map((item) => (
                            <option key={item.id} value={item.id}>{item.code} - {item.name}</option>
                          ))}
                        </Select>
                        {errors[`lines.${index}.itemId`] ? <small className="hc-field-error">{errors[`lines.${index}.itemId`][0]}</small> : null}
                      </td>
                      <td>
                        <Input disabled={!isEditable} type="number" min={0.000001} step="0.000001" value={line.orderedQty} onChange={(event) => setLineValue(index, "orderedQty", event.target.value === "" ? "" : Number(event.target.value))} />
                        {errors[`lines.${index}.orderedQty`] ? <small className="hc-field-error">{errors[`lines.${index}.orderedQty`][0]}</small> : null}
                      </td>
                      <td>
                        <Select disabled={!isEditable} value={line.uomId} onChange={(event) => setLineValue(index, "uomId", event.target.value)}>
                          <option value="">Select UOM</option>
                          {uoms.map((uom) => (
                            <option key={uom.id} value={uom.id}>{uom.code}</option>
                          ))}
                        </Select>
                        {errors[`lines.${index}.uomId`] ? <small className="hc-field-error">{errors[`lines.${index}.uomId`][0]}</small> : null}
                      </td>
                      <td>
                        <Input disabled={!isEditable} value={line.notes} onChange={(event) => setLineValue(index, "notes", event.target.value)} />
                      </td>
                      <td>
                        <Button disabled={!isEditable} type="button" variant="ghost" onClick={() => removeLine(index)}>Remove</Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </FormSection>

          <div className="hc-form-actions">
            {purchaseOrderId && status === "Draft" ? <Button type="button" isLoading={posting} onClick={handlePost}>Post purchase order</Button> : null}
            {purchaseOrderId && status === "Posted" ? <Button type="button" variant="secondary" isLoading={canceling} onClick={handleCancel}>Cancel purchase order</Button> : null}
            <Link className="hc-button hc-button--secondary hc-button--md" to="/purchase-orders">Close</Link>
            <Button disabled={!isEditable || posting || canceling} isLoading={saving} type="submit">Save draft</Button>
          </div>
        </form>
      ) : null}
    </FormPageLayout>
  );
}

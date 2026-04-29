import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { DocumentPageLayout, DocumentSection } from "../components/patterns";
import { Badge, Button, EmptyState, Field, Input, Select, SkeletonLoader, Textarea, useToast } from "../components/ui";
import { ApiError, type ValidationErrors } from "../services/api";
import {
  getActiveItemsCached,
  getActiveSuppliersCached,
  getActiveUomsCached,
  getActiveWarehousesCached,
  type Item,
  type Supplier,
  type Uom,
  type Warehouse,
} from "../services/masterDataApi";
import {
  getPurchaseReceiptDraft,
  listPurchaseReceiptDrafts,
  type PurchaseReceipt,
  type PurchaseReceiptLine,
  type PurchaseReceiptListItem,
} from "../services/purchaseReceiptsApi";
import {
  buildReceiptLineLookup,
  createPurchaseReturn,
  getPurchaseReturn,
  mapPurchaseReturnToFormValues,
  postPurchaseReturn,
  updatePurchaseReturn,
  type PurchaseReturnFormValues,
  type PurchaseReturnLineFormValues,
} from "../services/purchaseReturnsApi";

const INITIAL_VALUES: PurchaseReturnFormValues = {
  returnNo: "",
  supplierId: "",
  referenceReceiptId: "",
  returnDate: new Date().toISOString().slice(0, 10),
  notes: "",
  lines: [
    {
      lineNo: 1,
      itemId: "",
      componentId: "",
      warehouseId: "",
      returnQty: "",
      remainingReturnableQty: 0,
      uomId: "",
      referenceReceiptLineId: "",
    },
  ],
};

function toNumber(value: number | "") {
  return value === "" ? 0 : Number(value);
}

function roundQty(value: number) {
  return Number.isFinite(value) ? Math.round(value * 1_000_000) / 1_000_000 : 0;
}

function normalizeQty(value: number | null | undefined) {
  return typeof value === "number" && Number.isFinite(value) ? value : 0;
}

function statusTone(status: "Draft" | "Posted" | "Canceled") {
  return status === "Posted" ? "success" : status === "Canceled" ? "neutral" : "warning";
}

export function PurchaseReturnFormPage() {
  const { showToast } = useToast();
  const navigate = useNavigate();
  const { purchaseReturnId } = useParams();
  const isEdit = Boolean(purchaseReturnId);
  const [values, setValues] = useState<PurchaseReturnFormValues>(INITIAL_VALUES);
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [items, setItems] = useState<Item[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [uoms, setUoms] = useState<Uom[]>([]);
  const [receipts, setReceipts] = useState<PurchaseReceiptListItem[]>([]);
  const [referenceReceipt, setReferenceReceipt] = useState<PurchaseReceipt | null>(null);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [formError, setFormError] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [posting, setPosting] = useState(false);
  const [status, setStatus] = useState<"Draft" | "Posted" | "Canceled">("Draft");

  const isEditable = status === "Draft";
  const receiptLineLookup = useMemo(() => buildReceiptLineLookup(referenceReceipt), [referenceReceipt]);

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        setFormError("");

        const [supplierRows, itemRows, warehouseRows, uomRows, receiptRows, existingReturn] = await Promise.all([
          getActiveSuppliersCached(),
          getActiveItemsCached(),
          getActiveWarehousesCached(),
          getActiveUomsCached(),
          listPurchaseReceiptDrafts({
            search: "",
            status: "Posted",
            source: "",
            fromDate: "",
            toDate: "",
            page: 1,
            pageSize: 100,
            sortBy: "receiptDate",
            sortDirection: "Desc",
          }).then((result) => result.items),
          purchaseReturnId ? getPurchaseReturn(purchaseReturnId) : Promise.resolve(null),
        ]);

        if (!active) {
          return;
        }

        setSuppliers(supplierRows);
        setItems(itemRows);
        setWarehouses(warehouseRows);
        setUoms(uomRows);
        setReceipts(receiptRows.filter((row) => row.status === "Posted"));

        if (existingReturn) {
          setValues(mapPurchaseReturnToFormValues(existingReturn));
          setStatus(existingReturn.status);
          if (existingReturn.referenceReceiptId) {
            const matchedReceipt = await getPurchaseReceiptDraft(existingReturn.referenceReceiptId);
            if (active) {
              setReferenceReceipt(matchedReceipt);
            }
          }
        } else {
          setValues((current) => ({
            ...current,
            supplierId: current.supplierId || supplierRows[0]?.id || "",
            lines: current.lines.length > 0 ? current.lines : INITIAL_VALUES.lines,
          }));
          setStatus("Draft");
        }
      } catch (loadError) {
        if (active) {
          setFormError(loadError instanceof ApiError ? loadError.message : "Failed to load purchase return.");
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
  }, [purchaseReturnId]);

  async function handleReferenceReceiptChange(receiptId: string) {
    setValue("referenceReceiptId", receiptId);

    if (!receiptId) {
      setReferenceReceipt(null);
      return;
    }

    try {
      const receipt = referenceReceipt?.id === receiptId ? referenceReceipt : await getPurchaseReceiptDraft(receiptId);
      setReferenceReceipt(receipt);
      setValues((current) => ({
        ...current,
        supplierId: receipt.supplierId,
        lines: current.lines.map((line) => ({
          ...line,
          warehouseId: line.warehouseId || receipt.warehouseId,
        })),
      }));
    } catch (error) {
      setFormError(error instanceof ApiError ? error.message : "Failed to load reference receipt.");
    }
  }

  function setValue<K extends keyof PurchaseReturnFormValues>(key: K, value: PurchaseReturnFormValues[K]) {
    setValues((current) => ({ ...current, [key]: value }));
  }

  function setLineValue<K extends keyof PurchaseReturnLineFormValues>(index: number, key: K, value: PurchaseReturnLineFormValues[K]) {
    setValues((current) => ({
      ...current,
      lines: current.lines.map((line, lineIndex) => {
        if (lineIndex !== index) {
          return line;
        }

        const nextLine = { ...line, [key]: value };

        if (key === "referenceReceiptLineId" && typeof value === "string" && value) {
          const referenceLine = receiptLineLookup.get(value);
          if (referenceLine) {
            return {
              ...nextLine,
              itemId: referenceLine.itemId,
              warehouseId: referenceReceipt?.warehouseId ?? nextLine.warehouseId,
              remainingReturnableQty: normalizeQty(referenceLine.remainingReturnableQty),
              uomId: referenceLine.uomId,
            };
          }
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
          componentId: "",
          warehouseId: referenceReceipt?.warehouseId ?? "",
          returnQty: "",
          remainingReturnableQty: 0,
          uomId: "",
          referenceReceiptLineId: "",
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

  function validate(currentValues: PurchaseReturnFormValues): ValidationErrors {
    const nextErrors: ValidationErrors = {};

    if (!currentValues.supplierId) {
      nextErrors.supplierId = ["Supplier is required."];
    }

    if (!currentValues.returnDate) {
      nextErrors.returnDate = ["Return date is required."];
    }

    if (currentValues.lines.length === 0) {
      nextErrors.lines = ["At least one purchase return line is required."];
    }

    currentValues.lines.forEach((line, index) => {
      if (!line.itemId) {
        nextErrors[`lines.${index}.itemId`] = ["Item is required."];
      }

      if (!line.warehouseId) {
        nextErrors[`lines.${index}.warehouseId`] = ["Warehouse is required."];
      }

      if (!line.uomId) {
        nextErrors[`lines.${index}.uomId`] = ["UOM is required."];
      }

      if (line.returnQty === "" || Number(line.returnQty) <= 0) {
        nextErrors[`lines.${index}.returnQty`] = ["Return quantity must be greater than zero."];
      } else if (line.referenceReceiptLineId && Number(line.returnQty) > line.remainingReturnableQty) {
        nextErrors[`lines.${index}.returnQty`] = ["Return quantity cannot exceed the remaining returnable quantity."];
      }
    });

    const selectedReferenceLines = currentValues.lines
      .filter((line) => line.referenceReceiptLineId)
      .map((line) => line.referenceReceiptLineId);

    if (new Set(selectedReferenceLines).size !== selectedReferenceLines.length) {
      nextErrors.lines = ["The same reference receipt line cannot be added more than once."];
    }

    return nextErrors;
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

      const saved = isEdit && purchaseReturnId
        ? await updatePurchaseReturn(purchaseReturnId, values)
        : await createPurchaseReturn(values);

      setValues(mapPurchaseReturnToFormValues(saved));
      setStatus(saved.status);

      if (!isEdit) {
        navigate(`/purchase-returns/${saved.id}`, { replace: true });
      }

      if (saved.referenceReceiptId) {
        await handleReferenceReceiptChange(saved.referenceReceiptId);
      }

      if (shouldPost) {
        const posted = await postPurchaseReturn(saved.id);
        setValues(mapPurchaseReturnToFormValues(posted));
        setStatus(posted.status);
        showToast({ tone: "success", title: "Purchase return posted", description: `${posted.returnNo} is now posted.` });
      } else {
        showToast({ tone: "success", title: "Purchase return saved", description: `${saved.returnNo} draft was saved.` });
      }
    } catch (error) {
      if (error instanceof ApiError) {
        setErrors(error.validationErrors ?? {});
        setFormError(error.message);
      } else {
        setFormError("Failed to save purchase return.");
      }
    } finally {
      setSaving(false);
      setPosting(false);
    }
  }

  function getReferenceLine(lineId: string): PurchaseReceiptLine | undefined {
    return lineId ? receiptLineLookup.get(lineId) : undefined;
  }

  function getRemainingReturnableQty(line: PurchaseReturnLineFormValues): number {
    if (line.referenceReceiptLineId) {
      return normalizeQty(line.remainingReturnableQty);
    }

    return 0;
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

  if (formError && suppliers.length === 0) {
    return <EmptyState title="Unable to load purchase return" description={formError} />;
  }

  return (
    <DocumentPageLayout
      eyebrow="Purchasing"
      title={isEdit ? "Purchase Return" : "New purchase return"}
      description="Return received items through a controlled document that reverses stock and supplier effects without editing posted receipts."
      status={<div className="hc-inline-cluster"><Badge tone={statusTone(status)}>{status}</Badge>{values.referenceReceiptId ? <Badge tone="neutral">Receipt linked</Badge> : <Badge tone="neutral">Manual return</Badge>}</div>}
      actions={(
        <div className="hc-document-actions">
          <Link className="hc-button hc-button--ghost hc-button--md" to="/purchase-returns">Back to returns</Link>
          {isEditable ? (
            <>
              <Button disabled={saving || posting} variant="secondary" onClick={() => void submit(false)}>Save draft</Button>
              <Button disabled={saving || posting} onClick={() => void submit(true)}>{posting ? "Posting..." : "Post return"}</Button>
            </>
          ) : null}
        </div>
      )}
    >
      {formError ? <div className="hc-inline-error">{formError}</div> : null}

      <DocumentSection title="Form Header" description="Keep supplier, return date, and receipt reference aligned in one structured header.">
        <div className="hc-document-form-grid">
          <Field label="Return no">
            <Input disabled value={values.returnNo} placeholder="Auto-generated on save" />
          </Field>
          <Field label="Supplier" required>
            <Select disabled={!isEditable || Boolean(values.referenceReceiptId)} value={values.supplierId} onChange={(event) => setValue("supplierId", event.target.value)}>
              <option value="">Select supplier</option>
              {suppliers.map((supplier) => (
                <option key={supplier.id} value={supplier.id}>{supplier.code} - {supplier.name}</option>
              ))}
            </Select>
            {errors.supplierId ? <small className="hc-field-error">{errors.supplierId[0]}</small> : null}
          </Field>
              <Field label="Reference receipt">
                <Select disabled={!isEditable} value={values.referenceReceiptId} onChange={(event) => void handleReferenceReceiptChange(event.target.value)}>
                  <option value="">Manual return</option>
                  {receipts.map((receipt) => (
                    <option key={receipt.id} value={receipt.id}>{receipt.receiptNo} - {receipt.supplierCode}</option>
                  ))}
                </Select>
          </Field>
          <Field label="Return date" required>
            <Input disabled={!isEditable} type="date" value={values.returnDate} onChange={(event) => setValue("returnDate", event.target.value)} />
            {errors.returnDate ? <small className="hc-field-error">{errors.returnDate[0]}</small> : null}
          </Field>
        </div>
        <Field label="Notes">
          <Textarea disabled={!isEditable} value={values.notes} onChange={(event) => setValue("notes", event.target.value)} />
        </Field>
      </DocumentSection>

      <DocumentSection title="Return Lines" description="Choose receipt lines when available, then enter the quantity that must be returned.">
        {errors.lines ? <div className="hc-inline-error">{errors.lines[0]}</div> : null}
        <div className="hc-table-wrap">
          <table className="hc-table hc-table--compact">
            <thead>
              <tr>
                <th>Line</th>
                <th>Reference receipt line</th>
                <th>Item</th>
                <th>Warehouse</th>
                <th>Remaining returnable</th>
                <th>Return qty</th>
                <th>UOM</th>
                {isEditable ? <th /> : null}
              </tr>
            </thead>
            <tbody>
              {values.lines.map((line, index) => {
                const referenceLine = getReferenceLine(line.referenceReceiptLineId);
                const remainingReturnableQty = getRemainingReturnableQty(line);
                const selectedReferenceLineIds = new Set(
                  values.lines
                    .filter((_, lineIndex) => lineIndex !== index)
                    .map((row) => row.referenceReceiptLineId)
                    .filter(Boolean),
                );

                return (
                  <tr key={`${line.lineNo}-${index}`}>
                    <td>{line.lineNo}</td>
                    <td>
                      <Select disabled={!isEditable || !referenceReceipt} value={line.referenceReceiptLineId} onChange={(event) => setLineValue(index, "referenceReceiptLineId", event.target.value)}>
                        <option value="">None</option>
                        {(referenceReceipt?.lines ?? [])
                          .filter((receiptLine) => normalizeQty(receiptLine.remainingReturnableQty) > 0 || receiptLine.id === line.referenceReceiptLineId)
                          .map((receiptLine) => (
                          <option key={receiptLine.id} value={receiptLine.id} disabled={selectedReferenceLineIds.has(receiptLine.id)}>
                            Line {receiptLine.lineNo} - {receiptLine.itemCode} ({roundQty(normalizeQty(receiptLine.remainingReturnableQty)).toLocaleString()} returnable / {roundQty(normalizeQty(receiptLine.returnedQty)).toLocaleString()} returned)
                          </option>
                        ))}
                      </Select>
                    </td>
                    <td>
                      <Select disabled={!isEditable || Boolean(referenceLine)} value={line.itemId} onChange={(event) => setLineValue(index, "itemId", event.target.value)}>
                        <option value="">Select item</option>
                        {items.map((item) => (
                          <option key={item.id} value={item.id}>{item.code} - {item.name}</option>
                        ))}
                      </Select>
                      {errors[`lines.${index}.itemId`] ? <small className="hc-field-error">{errors[`lines.${index}.itemId`]?.[0]}</small> : null}
                    </td>
                    <td>
                      <Select disabled={!isEditable || Boolean(referenceReceipt)} value={line.warehouseId} onChange={(event) => setLineValue(index, "warehouseId", event.target.value)}>
                        <option value="">Select warehouse</option>
                        {warehouses.map((warehouse) => (
                          <option key={warehouse.id} value={warehouse.id}>{warehouse.code} - {warehouse.name}</option>
                        ))}
                      </Select>
                      {errors[`lines.${index}.warehouseId`] ? <small className="hc-field-error">{errors[`lines.${index}.warehouseId`]?.[0]}</small> : null}
                    </td>
                    <td>
                      {line.referenceReceiptLineId ? (
                        <div className="hc-table__cell-strong hc-table__cell-strong--numeric">
                          <span className="hc-table__title">{roundQty(remainingReturnableQty).toLocaleString()}</span>
                          <span className="hc-table__subtitle">{referenceLine?.uomCode ?? "base"} still available</span>
                        </div>
                      ) : (
                        <span className="hc-table__subtitle">Reference required for tracked balance</span>
                      )}
                    </td>
                    <td>
                      <Input disabled={!isEditable} type="number" min="0" step="0.000001" value={line.returnQty} onChange={(event) => setLineValue(index, "returnQty", event.target.value === "" ? "" : Number(event.target.value))} />
                      {line.referenceReceiptLineId && toNumber(line.returnQty) > remainingReturnableQty ? (
                        <small className="hc-field-error">Cannot exceed {roundQty(remainingReturnableQty).toLocaleString()}.</small>
                      ) : null}
                      {errors[`lines.${index}.returnQty`] ? <small className="hc-field-error">{errors[`lines.${index}.returnQty`]?.[0]}</small> : null}
                    </td>
                    <td>
                      <Select disabled={!isEditable || Boolean(referenceLine)} value={line.uomId} onChange={(event) => setLineValue(index, "uomId", event.target.value)}>
                        <option value="">Select UOM</option>
                        {uoms.map((uom) => (
                          <option key={uom.id} value={uom.id}>{uom.code}</option>
                        ))}
                      </Select>
                      {referenceLine ? <small className="hc-field-hint">Receipt line {referenceLine.lineNo} uses {referenceLine.uomCode}.</small> : null}
                      {errors[`lines.${index}.uomId`] ? <small className="hc-field-error">{errors[`lines.${index}.uomId`]?.[0]}</small> : null}
                    </td>
                    {isEditable ? (
                      <td className="hc-table__actions-cell">
                        <Button type="button" variant="ghost" onClick={() => removeLine(index)}>Remove</Button>
                      </td>
                    ) : null}
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
        {isEditable ? <Button type="button" variant="secondary" onClick={addLine}>Add line</Button> : null}
      </DocumentSection>
    </DocumentPageLayout>
  );
}

import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { DocumentPageLayout, DocumentSection } from "../components/patterns";
import { Badge, Button, Card, EmptyState, Field, Input, ReversalDialog, Select, SkeletonLoader, Textarea, useToast } from "../components/ui";
import { ApiError, type ValidationErrors } from "../services/api";
import { listSuppliers, type Supplier } from "../services/masterDataApi";
import {
  createPayment,
  getPayment,
  listSupplierOpenBalances,
  postPayment,
  updatePayment,
  type DocumentStatus,
  type Payment,
  type PaymentAllocationFormValues,
  type PaymentDirection,
  type PaymentFormValues,
  type PaymentMethod,
  type SupplierOpenBalance,
} from "../services/paymentsApi";
import { reversePayment } from "../services/reversalsApi";

const INITIAL_VALUES: PaymentFormValues = {
  paymentNo: "",
  partyType: "Supplier",
  partyId: "",
  direction: "OutboundToParty",
  amount: "",
  paymentDate: new Date().toISOString().slice(0, 10),
  currency: "EGP",
  exchangeRate: "",
  paymentMethod: "BankTransfer",
  referenceNote: "",
  notes: "",
  allocations: [],
};

function toNumber(value: number | "") {
  return value === "" ? 0 : Number(value);
}

function roundAmount(value: number) {
  return Math.round(value * 1_000_000) / 1_000_000;
}

function formatAmount(value: number, currency?: string | null) {
  const label = value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  return currency ? `${label} ${currency}` : label;
}

function formatDirection(direction: PaymentDirection) {
  return direction === "OutboundToParty" ? "Paid to supplier" : "Received from supplier";
}

function formatTargetDocumentLabel(targetDocType: PaymentAllocationFormValues["targetDocType"]) {
  return targetDocType === "PurchaseReceipt" ? "Purchase receipt" : "Shortage financial resolution";
}

function formatTargetStatus(status: string) {
  switch (status) {
    case "PartiallySettled":
      return "Partially settled";
    case "Settled":
      return "Settled";
    case "Reversed":
      return "Reversed";
    default:
      return "Open";
  }
}

function buildAllocationFromTarget(target: SupplierOpenBalance, allocationOrder: number): PaymentAllocationFormValues {
  return {
    targetDocType: target.targetDocType,
    targetDocId: target.targetDocId,
    targetLineId: "",
    allocatedAmount: target.openAmount,
    allocationOrder,
  };
}

function mapPaymentToFormValues(payment: Payment): PaymentFormValues {
  return {
    paymentNo: payment.paymentNo,
    partyType: payment.partyType,
    partyId: payment.partyId,
    direction: payment.direction,
    amount: payment.amount,
    paymentDate: payment.paymentDate.slice(0, 10),
    currency: payment.currency ?? "",
    exchangeRate: payment.exchangeRate ?? "",
    paymentMethod: payment.paymentMethod,
    referenceNote: payment.referenceNote ?? "",
    notes: payment.notes ?? "",
    allocations: payment.allocations.map((allocation) => ({
      targetDocType: allocation.targetDocType,
      targetDocId: allocation.targetDocId,
      targetLineId: allocation.targetLineId ?? "",
      allocatedAmount: allocation.allocatedAmount,
      allocationOrder: allocation.allocationOrder,
    })),
  };
}

function statusTone(status: DocumentStatus) {
  return status === "Posted" ? "success" : status === "Canceled" ? "danger" : "neutral";
}

export function PaymentFormPage() {
  const { showToast } = useToast();
  const navigate = useNavigate();
  const { paymentId } = useParams();
  const isEdit = Boolean(paymentId);
  const [values, setValues] = useState<PaymentFormValues>(INITIAL_VALUES);
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [openBalances, setOpenBalances] = useState<SupplierOpenBalance[]>([]);
  const [openBalanceSearch, setOpenBalanceSearch] = useState("");
  const [payment, setPayment] = useState<Payment | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadingTargets, setLoadingTargets] = useState(false);
  const [saving, setSaving] = useState(false);
  const [posting, setPosting] = useState(false);
  const [reversing, setReversing] = useState(false);
  const [showReversalDialog, setShowReversalDialog] = useState(false);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [formError, setFormError] = useState("");

  const status = payment?.status ?? "Draft";
  const isEditable = status === "Draft";
  const isReversed = status === "Posted" && Boolean(payment?.reversalDocumentId);
  const selectedSupplier = suppliers.find((supplier) => supplier.id === values.partyId) ?? null;
  const targetLookup = useMemo(() => new Map(openBalances.map((target) => [`${target.targetDocType}:${target.targetDocId}`, target])), [openBalances]);
  const savedAllocationLookup = useMemo(
    () => new Map(
      (payment?.allocations ?? []).map((allocation) => [
        `${allocation.targetDocType}:${allocation.targetDocId}`,
        allocation,
      ]),
    ),
    [payment],
  );
  const selectedTargetKeys = useMemo(
    () => new Set(values.allocations.map((allocation) => `${allocation.targetDocType}:${allocation.targetDocId}`)),
    [values.allocations],
  );
  const allocationAmountByTargetKey = useMemo(
    () => new Map(
      values.allocations.map((allocation) => [
        `${allocation.targetDocType}:${allocation.targetDocId}`,
        toNumber(allocation.allocatedAmount),
      ]),
    ),
    [values.allocations],
  );
  const availableTargets = useMemo(
    () => openBalances.filter((target) => !selectedTargetKeys.has(`${target.targetDocType}:${target.targetDocId}`)),
    [openBalances, selectedTargetKeys],
  );
  const paymentAmount = roundAmount(toNumber(values.amount));
  const totalAllocated = useMemo(
    () => roundAmount(values.allocations.reduce((sum, allocation) => sum + toNumber(allocation.allocatedAmount), 0)),
    [values.allocations],
  );
  const unallocatedAmount = roundAmount(Math.max(paymentAmount - totalAllocated, 0));

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        setFormError("");

        const [supplierRows, existingPayment] = await Promise.all([
          listSuppliers("", "active"),
          paymentId ? getPayment(paymentId) : Promise.resolve(null),
        ]);

        if (!active) {
          return;
        }

        setSuppliers(supplierRows);
        setPayment(existingPayment);

        if (existingPayment) {
          setValues(mapPaymentToFormValues(existingPayment));
        } else {
          setValues((current) => ({
            ...current,
            partyId: current.partyId || supplierRows[0]?.id || "",
          }));
        }
      } catch (loadError) {
        if (active) {
          setFormError(loadError instanceof ApiError ? loadError.message : "Failed to load supplier payment.");
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
  }, [paymentId]);

  useEffect(() => {
    if (!values.partyId) {
      setOpenBalances([]);
      return;
    }

    let active = true;

    async function loadOpenBalances() {
      try {
        setLoadingTargets(true);
        const result = await listSupplierOpenBalances(values.partyId, values.direction, openBalanceSearch);
        if (active) {
          setOpenBalances(result);
        }
      } catch (loadError) {
        if (active) {
          setOpenBalances([]);
          setFormError(loadError instanceof ApiError ? loadError.message : "Failed to load supplier open balances.");
        }
      } finally {
        if (active) {
          setLoadingTargets(false);
        }
      }
    }

    void loadOpenBalances();
    return () => {
      active = false;
    };
  }, [openBalanceSearch, values.direction, values.partyId]);

  function setValue<K extends keyof PaymentFormValues>(key: K, value: PaymentFormValues[K]) {
    setValues((current) => ({ ...current, [key]: value }));
  }

  function getSelectedAllocationDisplay(allocation: PaymentFormValues["allocations"][number]) {
    const key = `${allocation.targetDocType}:${allocation.targetDocId}`;
    const liveTarget = targetLookup.get(key);
    if (liveTarget) {
      const currentAllocateAmount = toNumber(allocation.allocatedAmount);
      return {
        targetDocumentNo: liveTarget.targetDocumentNo,
        targetDocumentDate: liveTarget.targetDocumentDate,
        originalAmount: liveTarget.originalAmount,
        adjustedAmount: liveTarget.adjustedAmount,
        netAmount: liveTarget.netAmount,
        alreadyAllocatedAmount: liveTarget.allocatedAmount,
        openAmountBeforeAllocation: liveTarget.openAmount,
        openAmountAfterAllocation: roundAmount(Math.max(liveTarget.openAmount - currentAllocateAmount, 0)),
        status: liveTarget.status,
      };
    }

    const savedAllocation = savedAllocationLookup.get(key);
    if (savedAllocation) {
      return {
        targetDocumentNo: savedAllocation.targetDocumentNo,
        targetDocumentDate: savedAllocation.targetDocumentDate,
        originalAmount: savedAllocation.originalAmount,
        adjustedAmount: savedAllocation.adjustedAmount,
        netAmount: savedAllocation.netAmount,
        alreadyAllocatedAmount: savedAllocation.alreadyAllocatedAmount,
        openAmountBeforeAllocation: roundAmount(savedAllocation.openAmount + savedAllocation.allocatedAmount),
        openAmountAfterAllocation: savedAllocation.openAmount,
        status: savedAllocation.status,
      };
    }

    return {
      targetDocumentNo: allocation.targetDocId,
      targetDocumentDate: "",
      originalAmount: 0,
      adjustedAmount: 0,
      netAmount: 0,
      alreadyAllocatedAmount: 0,
      openAmountBeforeAllocation: 0,
      openAmountAfterAllocation: 0,
      status: "Open",
    };
  }

  function validate(currentValues: PaymentFormValues, requireFullAllocation: boolean): ValidationErrors {
    const nextErrors: ValidationErrors = {};

    if (!currentValues.partyId) {
      nextErrors.partyId = ["Supplier is required."];
    }

    if (!currentValues.paymentDate) {
      nextErrors.paymentDate = ["Payment date is required."];
    }

    if (currentValues.amount === "" || Number(currentValues.amount) <= 0) {
      nextErrors.amount = ["Payment amount must be greater than zero."];
    }

    const allocatedTotal = roundAmount(currentValues.allocations.reduce((sum, allocation) => sum + toNumber(allocation.allocatedAmount), 0));
    if (allocatedTotal > paymentAmount) {
      nextErrors.allocations = ["Allocated total cannot exceed the payment amount."];
    }

    if (requireFullAllocation) {
      if (currentValues.allocations.length === 0) {
        nextErrors.allocations = ["At least one allocation is required before posting."];
      } else if (allocatedTotal !== paymentAmount) {
        nextErrors.allocations = ["Posted supplier payments must be fully allocated. Payment amount must equal the allocated total."];
      }
    }

    const targetKeys = currentValues.allocations.map((allocation) => `${allocation.targetDocType}:${allocation.targetDocId}`);
    if (new Set(targetKeys).size !== targetKeys.length) {
      nextErrors.allocations = ["The same target document cannot be added more than once."];
    }

    currentValues.allocations.forEach((allocation, index) => {
      if (allocation.allocatedAmount === "" || Number(allocation.allocatedAmount) <= 0) {
        nextErrors[`allocations.${index}.allocatedAmount`] = ["Allocate amount must be greater than zero."];
      }

      const target = targetLookup.get(`${allocation.targetDocType}:${allocation.targetDocId}`);
      if (target && Number(allocation.allocatedAmount) > target.openAmount) {
        nextErrors[`allocations.${index}.allocatedAmount`] = [`Allocate amount cannot exceed the open amount for ${target.targetDocumentNo}.`];
      }
    });

    return nextErrors;
  }

  function handleDirectionChange(direction: PaymentDirection) {
    setValues((current) => ({
      ...current,
      direction,
      allocations: [],
    }));
  }

  function addTarget(target: SupplierOpenBalance) {
    setValues((current) => ({
      ...current,
      allocations: current.allocations.some((allocation) =>
        allocation.targetDocType === target.targetDocType && allocation.targetDocId === target.targetDocId)
        ? current.allocations
        : [
            ...current.allocations,
            buildAllocationFromTarget(target, current.allocations.length + 1),
          ],
    }));
  }

  function removeAllocation(targetDocType: PaymentAllocationFormValues["targetDocType"], targetDocId: string) {
    setValues((current) => ({
      ...current,
      allocations: current.allocations
        .filter((allocation) => !(allocation.targetDocType === targetDocType && allocation.targetDocId === targetDocId))
        .map((allocation, index) => ({ ...allocation, allocationOrder: index + 1 })),
    }));
  }

  function updateAllocation(targetDocType: PaymentAllocationFormValues["targetDocType"], targetDocId: string, allocatedAmount: number | "") {
    setValues((current) => ({
      ...current,
      allocations: current.allocations.map((allocation) =>
        allocation.targetDocType === targetDocType && allocation.targetDocId === targetDocId
          ? { ...allocation, allocatedAmount }
          : allocation),
    }));
  }

  function autoAllocateFifo() {
    if (paymentAmount <= 0) {
      return;
    }

    let remainder = paymentAmount;
    const nextAllocations: PaymentAllocationFormValues[] = [];

    availableTargets
      .slice()
      .sort((left, right) => new Date(left.targetDocumentDate).getTime() - new Date(right.targetDocumentDate).getTime())
      .forEach((target, index) => {
        if (remainder <= 0) {
          return;
        }

        const allocate = roundAmount(Math.min(target.openAmount, remainder));
        if (allocate <= 0) {
          return;
        }

        nextAllocations.push({
          targetDocType: target.targetDocType,
          targetDocId: target.targetDocId,
          targetLineId: "",
          allocatedAmount: allocate,
          allocationOrder: index + 1,
        });

        remainder = roundAmount(remainder - allocate);
      });

    setValues((current) => ({
      ...current,
      allocations: nextAllocations,
    }));
  }

  async function submit(requirePosting: boolean) {
    const validationErrors = validate(values, requirePosting);
    setErrors(validationErrors);

    if (Object.keys(validationErrors).length > 0) {
      setFormError(requirePosting ? "Resolve the payment validation errors before posting." : "Resolve the payment validation errors before saving.");
      return;
    }

    try {
      setFormError("");

      if (requirePosting) {
        setPosting(true);
      } else {
        setSaving(true);
      }

      const saved = isEdit && paymentId
        ? await updatePayment(paymentId, values)
        : await createPayment(values);

      setPayment(saved);
      setValues(mapPaymentToFormValues(saved));
      setErrors({});

      if (requirePosting) {
        const posted = await postPayment(saved.id);
        setPayment(posted);
        setValues(mapPaymentToFormValues(posted));
        showToast({ tone: "success", title: "Supplier payment posted", description: "The payment and its allocations were posted successfully." });
      } else {
        showToast({
          tone: "success",
          title: isEdit ? "Supplier payment updated" : "Supplier payment draft created",
          description: isEdit ? "The draft changes were saved successfully." : "The payment draft is ready for allocation and posting.",
        });
      }

      if (!isEdit) {
        navigate(`/payments/${saved.id}`, { replace: true });
      }
    } catch (submitError) {
      if (submitError instanceof ApiError) {
        setErrors(submitError.validationErrors ?? {});
        setFormError(submitError.message);
      } else {
        setFormError(requirePosting ? "Failed to post supplier payment." : "Failed to save supplier payment.");
      }
    } finally {
      setSaving(false);
      setPosting(false);
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

  return (
    <DocumentPageLayout
      eyebrow="Procurement"
      title={isEdit ? (status === "Posted" ? "Supplier payment details" : "Edit supplier payment") : "New supplier payment"}
      description="Allocate this supplier payment against the right open documents in a clear, step-by-step flow."
      status={<Badge tone={statusTone(status)}>{status}</Badge>}
      actions={(
        <div className="hc-document-actions">
          <Link className="hc-button hc-button--ghost hc-button--md" to="/payments">Back to payments</Link>
          {isEditable ? (
            <>
              <Button disabled={saving || posting} variant="secondary" onClick={() => void submit(false)}>
                Save draft
              </Button>
              <Button disabled={saving || posting} onClick={() => void submit(true)}>
                Post payment
              </Button>
            </>
          ) : paymentId && !isReversed ? (
            <Button disabled={reversing} isLoading={reversing} variant="secondary" onClick={() => setShowReversalDialog(true)}>
              Reverse
            </Button>
          ) : null}
        </div>
      )}
    >
      <ReversalDialog
        description="This reversal will restore supplier open balances and create opposite supplier statement entries without deleting the original allocation trail."
        impactSummary="Use reversal instead of edit or delete whenever a posted supplier payment must be corrected."
        isLoading={reversing}
        onCancel={() => setShowReversalDialog(false)}
        onConfirm={async (reason) => {
          if (!paymentId) {
            return;
          }

          try {
            setReversing(true);
            setFormError("");
            await reversePayment(paymentId, reason);
            const refreshed = await getPayment(paymentId);
            setPayment(refreshed);
            setValues(mapPaymentToFormValues(refreshed));
            showToast({ tone: "success", title: "Payment reversed", description: `${refreshed.paymentNo} was reversed successfully.` });
            setShowReversalDialog(false);
          } catch (error) {
            setFormError(error instanceof ApiError ? error.message : "Failed to reverse payment.");
          } finally {
            setReversing(false);
          }
        }}
        open={showReversalDialog}
        title="Reverse supplier payment"
      />
      {formError ? <div className="hc-inline-error">{formError}</div> : null}
      {isReversed ? <div className="hc-inline-help">This posted payment has already been reversed and no longer consumes supplier open balances.</div> : null}

      <DocumentSection title="Form Header" description="Keep supplier, direction, payment, and status context aligned in one ERP header grid.">
        <div className="hc-document-form-grid">
          <Field label="Payment no">
            <Input disabled value={values.paymentNo} placeholder="Auto-generated on save" />
          </Field>
          <Field label="Supplier" required>
            <Select disabled={!isEditable} value={values.partyId} onChange={(event) => setValue("partyId", event.target.value)}>
              <option value="">Select supplier</option>
              {suppliers.map((supplier) => (
                <option key={supplier.id} value={supplier.id}>
                  {supplier.code} - {supplier.name}
                </option>
              ))}
            </Select>
            {errors.partyId ? <small className="hc-field-error">{errors.partyId[0]}</small> : null}
          </Field>
          <Field label="Direction" required>
            <Select disabled={!isEditable} value={values.direction} onChange={(event) => handleDirectionChange(event.target.value as PaymentDirection)}>
              <option value="OutboundToParty">Paid to supplier</option>
              <option value="InboundFromParty">Received from supplier</option>
            </Select>
          </Field>
          <Field label="Payment date" required>
            <Input disabled={!isEditable} type="date" value={values.paymentDate} onChange={(event) => setValue("paymentDate", event.target.value)} />
            {errors.paymentDate ? <small className="hc-field-error">{errors.paymentDate[0]}</small> : null}
          </Field>
          <Field label="Payment amount" required>
            <Input
              disabled={!isEditable}
              min={0}
              step="0.000001"
              type="number"
              value={values.amount}
              onChange={(event) => setValue("amount", event.target.value === "" ? "" : Number(event.target.value))}
            />
            {errors.amount ? <small className="hc-field-error">{errors.amount[0]}</small> : null}
          </Field>
          <Field label="Payment method" required>
            <Select disabled={!isEditable} value={values.paymentMethod} onChange={(event) => setValue("paymentMethod", event.target.value as PaymentMethod)}>
              <option value="BankTransfer">Bank transfer</option>
              <option value="Cash">Cash</option>
              <option value="Cheque">Cheque</option>
              <option value="Other">Other</option>
            </Select>
          </Field>
          <Field className="hc-document-field--summary" label="Settlement mode">
            <div className="hc-document-readonly">
              <strong>{formatDirection(values.direction)}</strong>
              <div className="hc-field__hint">{selectedSupplier ? `${selectedSupplier.code} - ${selectedSupplier.name}` : "Select a supplier first."}</div>
            </div>
          </Field>
          <Field className="hc-document-field--summary" label="Allocation totals">
            <div className="hc-document-readonly">
              <strong>{formatAmount(totalAllocated, values.currency || null)} allocated</strong>
              <div className="hc-field__hint">{formatAmount(unallocatedAmount, values.currency || null)} remaining to allocate</div>
            </div>
          </Field>
          <Field className="hc-document-field--span-full" label="Notes">
            <Textarea disabled={!isEditable} value={values.notes} onChange={(event) => setValue("notes", event.target.value)} />
          </Field>
        </div>
      </DocumentSection>

      <Card className="hc-task-summary-panel hc-payment-allocation-summary" padding="md">
        <div className="hc-task-summary-panel__header">
          <div>
            <h2 className="hc-task-summary-panel__title">Allocation Summary</h2>
            <p className="hc-task-summary-panel__description">
              Review the totals here first, then fill the selected targets section, then add more documents from the available targets section.
            </p>
          </div>
          {isEditable ? (
            <div className="hc-task-summary-panel__actions">
              <Button disabled={paymentAmount <= 0 || availableTargets.length === 0} variant="secondary" onClick={autoAllocateFifo} type="button">
                Auto-fill FIFO
              </Button>
            </div>
          ) : null}
        </div>

        <div className="hc-task-summary-grid">
          <div className="hc-task-summary-metric">
            <span className="hc-task-summary-metric__label">Selected targets</span>
            <strong className="hc-task-summary-metric__value">
              {values.allocations.length} {values.allocations.length === 1 ? "document" : "documents"}
            </strong>
            <span className="hc-task-summary-metric__caption">Documents already added to this payment.</span>
          </div>
          <div className="hc-task-summary-metric">
            <span className="hc-task-summary-metric__label">Allocated amount</span>
            <strong className="hc-task-summary-metric__value">{formatAmount(totalAllocated, values.currency || null)}</strong>
            <span className="hc-task-summary-metric__caption">Amount currently assigned to selected targets.</span>
          </div>
          <div className="hc-task-summary-metric">
            <span className="hc-task-summary-metric__label">Still to allocate</span>
            <strong className="hc-task-summary-metric__value">{formatAmount(unallocatedAmount, values.currency || null)}</strong>
            <span className="hc-task-summary-metric__caption">
              {unallocatedAmount === 0
                ? "This payment is fully allocated."
                : "Add more documents below or adjust the selected rows."}
            </span>
          </div>
        </div>
      </Card>

      <DocumentSection
        className="hc-task-stage hc-task-stage--selected"
        title="Selected Targets"
        description={isEditable
          ? "Step 1: review the documents already chosen for this payment and enter the amount to apply to each one."
          : "These are the documents that were settled by this posted payment."}
      >
        {errors.allocations ? <div className="hc-inline-error">{errors.allocations[0]}</div> : null}
        {values.allocations.length === 0 ? (
          <div className="hc-task-empty-state">
            <EmptyState
              title="No targets added yet"
              description="Search and add one or more documents from the section below."
            />
          </div>
        ) : (
          <div className="hc-document-table-wrap hc-document-table-wrap--task hc-document-table-wrap--selected hc-task-table-wrap">
            <table className="hc-table hc-table--compact">
              <thead>
                <tr>
                  <th>Document</th>
                  <th className="hc-table__numeric">Net target</th>
                  <th className="hc-table__numeric">Already paid</th>
                  <th className="hc-table__numeric">Open before this payment</th>
                  <th className="hc-table__numeric">Amount to apply</th>
                  <th className="hc-table__numeric">Order</th>
                  {isEditable ? <th className="hc-table__head-actions" /> : null}
                </tr>
              </thead>
              <tbody>
                {values.allocations.map((allocation, index) => {
                  const display = getSelectedAllocationDisplay(allocation);
                  const currentAllocateAmount = toNumber(allocation.allocatedAmount);

                  return (
                    <tr key={`${allocation.targetDocType}-${allocation.targetDocId}`}>
                      <td className="hc-table__primary-cell">
                        <div className="hc-table__cell-strong">
                          <span className="hc-table__title">{display.targetDocumentNo}</span>
                          <span className="hc-table__subtitle">
                            {formatTargetDocumentLabel(allocation.targetDocType)} dated{" "}
                            {display.targetDocumentDate ? new Date(display.targetDocumentDate).toLocaleDateString() : "Unknown date"}
                            {" • "}
                            {formatTargetStatus(display.status)}
                          </span>
                        </div>
                      </td>
                      <td className="hc-table__numeric">
                        <div className="hc-table__cell-strong hc-table__cell-strong--numeric">
                          <span className="hc-table__title">{formatAmount(display.netAmount, values.currency || null)}</span>
                          <span className="hc-table__subtitle">
                            Base {formatAmount(display.originalAmount, values.currency || null)}
                            {display.adjustedAmount > 0 ? ` • Adjusted ${formatAmount(display.adjustedAmount, values.currency || null)}` : ""}
                          </span>
                        </div>
                      </td>
                      <td className="hc-table__numeric">{formatAmount(display.alreadyAllocatedAmount, values.currency || null)}</td>
                      <td className="hc-table__numeric">
                        <div className="hc-table__cell-strong hc-table__cell-strong--numeric">
                          <span className="hc-table__title">{formatAmount(display.openAmountBeforeAllocation, values.currency || null)}</span>
                          <span className="hc-table__subtitle">After this payment: {formatAmount(display.openAmountAfterAllocation, values.currency || null)}</span>
                        </div>
                      </td>
                      <td className="hc-table__numeric">
                        {isEditable ? (
                          <div className="hc-allocation-amount-field">
                            <Input
                              min={0}
                              step="0.000001"
                              type="number"
                              value={allocation.allocatedAmount}
                              onChange={(event) => updateAllocation(
                                allocation.targetDocType,
                                allocation.targetDocId,
                                event.target.value === "" ? "" : Number(event.target.value),
                              )}
                            />
                            {errors[`allocations.${index}.allocatedAmount`] ? (
                              <small className="hc-field-error">{errors[`allocations.${index}.allocatedAmount`][0]}</small>
                            ) : null}
                          </div>
                      ) : (
                          formatAmount(currentAllocateAmount, values.currency || null)
                        )}
                      </td>
                      <td className="hc-table__numeric">{allocation.allocationOrder}</td>
                      {isEditable ? (
                        <td className="hc-table__cell-actions">
                          <div className="hc-table__actions">
                            <Button
                              variant="ghost"
                              onClick={() => removeAllocation(allocation.targetDocType, allocation.targetDocId)}
                              type="button"
                            >
                              Remove
                            </Button>
                          </div>
                        </td>
                      ) : null}
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </DocumentSection>

      {isEditable ? (
        <DocumentSection
          className="hc-task-stage hc-task-stage--available"
          title={values.direction === "OutboundToParty" ? "Available Supplier Documents" : "Available Supplier Recovery Documents"}
          description="Step 2: search the open documents for this supplier, then use Add to move the right ones into the selected targets section above."
          actions={(
            <Field className="hc-task-search" label="Search available documents">
              <Input
                placeholder="Search by document number or notes"
                value={openBalanceSearch}
                onChange={(event) => setOpenBalanceSearch(event.target.value)}
              />
            </Field>
          )}
        >
          {!values.partyId ? (
            <div className="hc-task-empty-state">
              <EmptyState title="Select a supplier first" description="Open documents will appear here after you choose the supplier and payment direction above." />
            </div>
          ) : loadingTargets ? (
            <div className="hc-list-card">
              <SkeletonLoader />
              <SkeletonLoader />
            </div>
          ) : availableTargets.length === 0 ? (
            <div className="hc-task-empty-state">
              <EmptyState title="No documents available to add" description="There are no open documents for this supplier and payment direction right now." />
            </div>
          ) : (
            <div className="hc-document-table-wrap hc-document-table-wrap--task hc-document-table-wrap--available hc-task-table-wrap">
              <table className="hc-table hc-table--compact">
                <thead>
                  <tr>
                    <th>Document</th>
                    <th className="hc-table__numeric">Net target</th>
                    <th className="hc-table__numeric">Already paid</th>
                    <th className="hc-table__numeric">Still open</th>
                    <th className="hc-table__numeric">Open after adding</th>
                    <th className="hc-table__head-actions" />
                  </tr>
                </thead>
                <tbody>
                  {availableTargets.map((target) => {
                    const targetKey = `${target.targetDocType}:${target.targetDocId}`;
                    const allocatedInDraft = allocationAmountByTargetKey.get(targetKey) ?? 0;
                    const draftRemainingAmount = roundAmount(Math.max(target.openAmount - allocatedInDraft, 0));

                    return (
                      <tr className="hc-table__row--actionable" key={`${target.targetDocType}-${target.targetDocId}`}>
                        <td className="hc-table__primary-cell">
                          <div className="hc-table__cell-strong hc-task-table__document">
                            <span className="hc-table__title">{target.targetDocumentNo}</span>
                            <span className="hc-table__subtitle">
                              {formatTargetDocumentLabel(target.targetDocType)} dated {new Date(target.targetDocumentDate).toLocaleDateString()}
                              {" • "}
                              {formatTargetStatus(target.status)}
                            </span>
                          </div>
                        </td>
                        <td className="hc-table__numeric">
                          <div className="hc-table__cell-strong hc-table__cell-strong--numeric">
                            <span className="hc-table__title">{formatAmount(target.netAmount, target.currency)}</span>
                            <span className="hc-table__subtitle">
                              Base {formatAmount(target.originalAmount, target.currency)}
                              {target.adjustedAmount > 0 ? ` • Adjusted ${formatAmount(target.adjustedAmount, target.currency)}` : ""}
                            </span>
                          </div>
                        </td>
                        <td className="hc-table__numeric">{formatAmount(target.allocatedAmount, target.currency)}</td>
                        <td className="hc-table__numeric">{formatAmount(target.openAmount, target.currency)}</td>
                        <td className="hc-table__numeric">{formatAmount(draftRemainingAmount, target.currency)}</td>
                        <td className="hc-table__cell-actions">
                          <div className="hc-table__actions">
                            <Button className="hc-task-table__add-action" onClick={() => addTarget(target)} size="sm" type="button">Add</Button>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </DocumentSection>
      ) : null}
    </DocumentPageLayout>
  );
}

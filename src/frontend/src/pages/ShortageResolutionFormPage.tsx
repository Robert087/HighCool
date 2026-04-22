import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { DocumentPageLayout, DocumentSection } from "../components/patterns";
import {
  Badge,
  Button,
  Field,
  Input,
  Select,
  SkeletonLoader,
  Textarea,
  useToast,
} from "../components/ui";
import { ApiError, type ValidationErrors } from "../services/api";
import { listItems, listSuppliers, type Item, type Supplier } from "../services/masterDataApi";
import {
  createShortageResolution,
  getShortageResolution,
  listOpenShortages,
  postShortageResolution,
  suggestShortageAllocations,
  updateShortageResolution,
  type OpenShortage,
  type OpenShortageFilters,
  type ShortageResolution,
  type ShortageResolutionAllocationFormValues,
  type ShortageResolutionFormValues,
  type ShortageResolutionType,
} from "../services/shortageResolutionsApi";

const INITIAL_VALUES: ShortageResolutionFormValues = {
  resolutionNo: "",
  supplierId: "",
  resolutionType: "Physical",
  resolutionDate: new Date().toISOString().slice(0, 10),
  currency: "EGP",
  notes: "",
  allocations: [],
};

const INITIAL_OPEN_SHORTAGE_FILTERS: OpenShortageFilters = {
  search: "",
  supplierId: "",
  itemId: "",
  componentItemId: "",
  status: "",
  affectsSupplierBalance: "",
  fromDate: "",
  toDate: "",
};

function toNumber(value: number | "") {
  return value === "" ? 0 : Number(value);
}

function roundQuantity(value: number) {
  return Math.round(value * 1_000_000) / 1_000_000;
}

function calculateAmount(qty: number | "", rate: number | "") {
  const normalizedQty = toNumber(qty);
  const normalizedRate = toNumber(rate);

  if (normalizedQty <= 0 || normalizedRate <= 0) {
    return 0;
  }

  return roundQuantity(normalizedQty * normalizedRate);
}

function impliedRate(shortage: Pick<OpenShortage, "shortageValue" | "shortageQty">) {
  if (shortage.shortageValue === null || shortage.shortageQty <= 0) {
    return null;
  }

  return roundQuantity(shortage.shortageValue / shortage.shortageQty);
}

function buildAllocationFromShortage(
  shortage: OpenShortage,
  resolutionType: ShortageResolutionType,
  sequenceNo: number,
): ShortageResolutionAllocationFormValues {
  return {
    shortageLedgerId: shortage.id,
    allocatedQty: shortage.openQty,
    allocatedAmount: resolutionType === "Financial" ? calculateAmount(shortage.openQty, impliedRate(shortage) ?? "") : "",
    valuationRate: resolutionType === "Financial" ? (impliedRate(shortage) ?? "") : "",
    allocationMethod: "Manual",
    sequenceNo,
  };
}

function mapResolutionToFormValues(resolution: ShortageResolution): ShortageResolutionFormValues {
  return {
    resolutionNo: resolution.resolutionNo,
    supplierId: resolution.supplierId,
    resolutionType: resolution.resolutionType,
    resolutionDate: resolution.resolutionDate.slice(0, 10),
    currency: resolution.currency ?? "",
    notes: resolution.notes ?? "",
    allocations: resolution.allocations.map((allocation) => ({
      shortageLedgerId: allocation.shortageLedgerId,
      allocatedQty: allocation.allocatedQty ?? "",
      allocatedAmount: allocation.allocatedAmount ?? "",
      valuationRate: allocation.valuationRate ?? "",
      allocationMethod: allocation.allocationMethod,
      sequenceNo: allocation.sequenceNo,
    })),
  };
}

function buildPersistedShortageLookup(resolution: ShortageResolution | null) {
  if (!resolution) {
    return new Map<string, OpenShortage>();
  }

  return new Map(
    resolution.allocations.map((allocation) => [
      allocation.shortageLedgerId,
      {
        id: allocation.shortageLedgerId,
        supplierId: allocation.supplierId,
        supplierCode: allocation.supplierCode,
        supplierName: allocation.supplierName,
        purchaseReceiptId: "",
        purchaseReceiptNo: allocation.purchaseReceiptNo,
        receiptDate: allocation.receiptDate,
        purchaseReceiptLineId: "",
        purchaseOrderId: null,
        purchaseOrderNo: null,
        itemId: allocation.itemId,
        itemCode: allocation.itemCode,
        itemName: allocation.itemName,
        componentItemId: allocation.componentItemId,
        componentItemCode: allocation.componentItemCode,
        componentItemName: allocation.componentItemName,
        shortageQty: allocation.shortageQty,
        resolvedPhysicalQty: allocation.resolvedPhysicalQty,
        resolvedFinancialQtyEquivalent: allocation.resolvedFinancialQtyEquivalent,
        resolvedQtyEquivalent: allocation.resolvedQtyEquivalent,
        openQty: allocation.openQty,
        shortageValue: null,
        resolvedAmount: allocation.allocatedAmount ?? 0,
        openAmount: allocation.openAmount,
        status: allocation.status as OpenShortage["status"],
        affectsSupplierBalance: allocation.affectsSupplierBalance,
        shortageReasonCodeId: null,
        shortageReasonCode: null,
        shortageReasonName: null,
        approvalStatus: "",
        createdAt: allocation.createdAt,
        updatedAt: null,
      },
    ]),
  );
}

export function ShortageResolutionFormPage() {
  const { showToast } = useToast();
  const navigate = useNavigate();
  const { shortageResolutionId } = useParams();
  const isEdit = Boolean(shortageResolutionId);
  const [values, setValues] = useState<ShortageResolutionFormValues>(INITIAL_VALUES);
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [items, setItems] = useState<Item[]>([]);
  const [openShortages, setOpenShortages] = useState<OpenShortage[]>([]);
  const [openShortageFilters, setOpenShortageFilters] = useState<OpenShortageFilters>(INITIAL_OPEN_SHORTAGE_FILTERS);
  const [resolution, setResolution] = useState<ShortageResolution | null>(null);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [formError, setFormError] = useState("");
  const [loading, setLoading] = useState(true);
  const [loadingShortages, setLoadingShortages] = useState(false);
  const [saving, setSaving] = useState(false);
  const [posting, setPosting] = useState(false);

  const liveShortageLookup = useMemo(() => new Map(openShortages.map((row) => [row.id, row])), [openShortages]);
  const persistedShortageLookup = useMemo(() => buildPersistedShortageLookup(resolution), [resolution]);
  const status = resolution?.status ?? "Draft";
  const isEditable = status === "Draft";
  const totalQty = useMemo(
    () => roundQuantity(values.allocations.reduce((sum, allocation) => sum + toNumber(allocation.allocatedQty), 0)),
    [values.allocations],
  );
  const totalAmount = useMemo(
    () => roundQuantity(values.allocations.reduce((sum, allocation) =>
      sum + (values.resolutionType === "Financial"
        ? calculateAmount(allocation.allocatedQty, allocation.valuationRate)
        : 0), 0)),
    [values.allocations, values.resolutionType],
  );

  const unselectedOpenShortages = useMemo(() => {
    const selectedIds = new Set(values.allocations.map((allocation) => allocation.shortageLedgerId));
    return openShortages.filter((row) => !selectedIds.has(row.id));
  }, [openShortages, values.allocations]);

  function renderEmptyGridRow(message: string, colSpan: number) {
    return (
      <div className="hc-resolution-grid">
        <table className="hc-resolution-grid__table">
          <tbody>
            <tr>
              <td className="hc-resolution-grid__empty" colSpan={colSpan}>{message}</td>
            </tr>
          </tbody>
        </table>
      </div>
    );
  }

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        setFormError("");

        const [supplierRows, itemRows, existingResolution] = await Promise.all([
          listSuppliers("", "active"),
          listItems("", "active"),
          shortageResolutionId ? getShortageResolution(shortageResolutionId) : Promise.resolve(null),
        ]);

        if (!active) {
          return;
        }

        setSuppliers(supplierRows);
        setItems(itemRows);
        setResolution(existingResolution);

        if (existingResolution) {
          setValues(mapResolutionToFormValues(existingResolution));
          setOpenShortageFilters((current) => ({
            ...current,
            supplierId: existingResolution.supplierId,
          }));
        } else {
          setValues(INITIAL_VALUES);
        }
      } catch (loadError) {
        if (active) {
          setFormError(loadError instanceof ApiError ? loadError.message : "Failed to load shortage resolution.");
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
  }, [shortageResolutionId]);

  useEffect(() => {
    if (!values.supplierId) {
      setOpenShortages([]);
      return;
    }

    let active = true;

    async function loadShortages() {
      try {
        setLoadingShortages(true);
        setFormError("");

        const result = await listOpenShortages({
          ...openShortageFilters,
          supplierId: values.supplierId,
        });

        if (active) {
          setOpenShortages(result);
        }
      } catch (loadError) {
        if (active) {
          setFormError(loadError instanceof ApiError ? loadError.message : "Failed to load open shortages.");
        }
      } finally {
        if (active) {
          setLoadingShortages(false);
        }
      }
    }

    void loadShortages();
    return () => {
      active = false;
    };
  }, [openShortageFilters, values.supplierId, values.resolutionType]);

  function setValue<K extends keyof ShortageResolutionFormValues>(key: K, value: ShortageResolutionFormValues[K]) {
    setValues((current) => ({ ...current, [key]: value }));
  }

  function setOpenShortageFilter<K extends keyof OpenShortageFilters>(key: K, value: OpenShortageFilters[K]) {
    setOpenShortageFilters((current) => ({ ...current, [key]: value }));
  }

  function handleSupplierChange(supplierId: string) {
    setValues((current) => ({
      ...current,
      supplierId,
      allocations: [],
    }));
  }

  function handleResolutionTypeChange(resolutionType: ShortageResolutionType) {
    setValues((current) => ({
      ...current,
      resolutionType,
      allocations: current.allocations.map((allocation) => ({
        ...allocation,
        allocatedQty: allocation.allocatedQty,
        allocatedAmount: resolutionType === "Financial"
          ? calculateAmount(allocation.allocatedQty, allocation.valuationRate)
          : "",
        valuationRate: resolutionType === "Physical" ? "" : allocation.valuationRate,
      })),
    }));
  }

  function addShortage(shortage: OpenShortage) {
    setValues((current) => ({
      ...current,
      allocations: [
        ...current.allocations,
        buildAllocationFromShortage(shortage, current.resolutionType, current.allocations.length + 1),
      ],
    }));
  }

  function removeAllocation(shortageLedgerId: string) {
    setValues((current) => ({
      ...current,
      allocations: current.allocations
        .filter((allocation) => allocation.shortageLedgerId !== shortageLedgerId)
        .map((allocation, index) => ({ ...allocation, sequenceNo: index + 1 })),
    }));
  }

  function updateAllocation(shortageLedgerId: string, patch: Partial<ShortageResolutionAllocationFormValues>) {
    setValues((current) => ({
      ...current,
      allocations: current.allocations.map((allocation) =>
        allocation.shortageLedgerId === shortageLedgerId
          ? {
              ...allocation,
              ...patch,
              allocatedAmount: current.resolutionType === "Financial"
                ? calculateAmount(
                    patch.allocatedQty ?? allocation.allocatedQty,
                    patch.valuationRate ?? allocation.valuationRate,
                  )
                : "",
            }
          : allocation,
      ),
    }));
  }

  function validateFinancialAllocations() {
    if (values.resolutionType !== "Financial") {
      return null;
    }

    for (const allocation of values.allocations) {
      const shortage = liveShortageLookup.get(allocation.shortageLedgerId) ?? persistedShortageLookup.get(allocation.shortageLedgerId);
      const qty = toNumber(allocation.allocatedQty);
      const rate = toNumber(allocation.valuationRate);
      const amount = calculateAmount(allocation.allocatedQty, allocation.valuationRate);

      if (qty <= 0) {
        return "Resolved quantity must be greater than zero for every financial allocation.";
      }

      if (rate <= 0) {
        return "Valuation rate must be greater than zero for every financial allocation.";
      }

      if (shortage?.openAmount != null && amount > shortage.openAmount) {
        return "Calculated amount cannot exceed the open shortage value.";
      }

      if (shortage && qty > shortage.openQty) {
        return "Resolved quantity cannot exceed the open shortage quantity.";
      }
    }

    return null;
  }

  function validatePhysicalAllocations() {
    if (values.resolutionType !== "Physical") {
      return null;
    }

    for (const allocation of values.allocations) {
      const shortage = liveShortageLookup.get(allocation.shortageLedgerId) ?? persistedShortageLookup.get(allocation.shortageLedgerId);
      const qty = toNumber(allocation.allocatedQty);

      if (qty <= 0) {
        return "Resolved quantity must be greater than zero for every physical allocation.";
      }

      if (toNumber(allocation.valuationRate) > 0) {
        return "Physical resolution does not require a valuation rate.";
      }

      if (shortage && qty > shortage.openQty) {
        return "Resolved quantity cannot exceed the open shortage quantity.";
      }
    }

    return null;
  }

  async function handleAutoFillFifo() {
    if (!values.supplierId) {
      setFormError("Select a supplier before requesting FIFO allocation suggestions.");
      return;
    }

    try {
      const suggestions = await suggestShortageAllocations(
        values.supplierId,
        values.resolutionType,
        totalQty || null,
        null,
      );

      setValues((current) => ({
        ...current,
        allocations: suggestions.map((suggestion, index) => ({
          shortageLedgerId: suggestion.shortageLedgerId,
          allocatedQty: suggestion.allocatedQty ?? "",
          allocatedAmount: current.resolutionType === "Financial"
            ? calculateAmount(suggestion.allocatedQty ?? "", suggestion.valuationRate ?? "")
            : "",
          valuationRate: suggestion.valuationRate ?? "",
          allocationMethod: suggestion.allocationMethod,
          sequenceNo: index + 1,
        })),
      }));
    } catch (loadError) {
      setFormError(loadError instanceof ApiError ? loadError.message : "Failed to suggest FIFO allocations.");
    }
  }

  async function handleSave() {
    const physicalValidationError = validatePhysicalAllocations();
    if (physicalValidationError) {
      setFormError(physicalValidationError);
      return;
    }

    const financialValidationError = validateFinancialAllocations();
    if (financialValidationError) {
      setFormError(financialValidationError);
      return;
    }

    try {
      setSaving(true);
      setFormError("");
      setErrors({});

      const saved = isEdit && shortageResolutionId
        ? await updateShortageResolution(shortageResolutionId, values)
        : await createShortageResolution(values);

      setResolution(saved);
      setValues(mapResolutionToFormValues(saved));
      showToast({
        tone: "success",
        title: "Shortage resolution saved",
        description: `${saved.resolutionNo} is available as a draft.`,
      });

      if (!isEdit) {
        navigate(`/shortage-resolutions/${saved.id}/edit`, { replace: true });
      }
    } catch (saveError) {
      if (saveError instanceof ApiError) {
        setFormError(saveError.message);
        setErrors(saveError.validationErrors ?? {});
      } else {
        setFormError("Failed to save shortage resolution.");
      }
    } finally {
      setSaving(false);
    }
  }

  async function handlePost() {
    if (!shortageResolutionId) {
      setFormError("Save the draft before posting the shortage resolution.");
      return;
    }

    const physicalValidationError = validatePhysicalAllocations();
    if (physicalValidationError) {
      setFormError(physicalValidationError);
      return;
    }

    const financialValidationError = validateFinancialAllocations();
    if (financialValidationError) {
      setFormError(financialValidationError);
      return;
    }

    try {
      setPosting(true);
      setFormError("");
      const posted = await postShortageResolution(shortageResolutionId);
      setResolution(posted);
      setValues(mapResolutionToFormValues(posted));
      showToast({
        tone: "success",
        title: "Shortage resolution posted",
        description: `${posted.resolutionNo} applied its settlement effects successfully.`,
      });
    } catch (postError) {
      setFormError(postError instanceof ApiError ? postError.message : "Failed to post shortage resolution.");
    } finally {
      setPosting(false);
    }
  }

  if (loading) {
    return (
      <div className="hc-document-page">
        <div className="hc-document-page__surface">
          <div className="hc-card hc-card--md">
            <div className="hc-skeleton-stack">
              <SkeletonLoader height="3rem" variant="rect" />
              <SkeletonLoader height="12rem" variant="rect" />
              <SkeletonLoader height="18rem" variant="rect" />
            </div>
          </div>
        </div>
      </div>
    );
  }

  return (
    <DocumentPageLayout
      title={values.resolutionNo || (isEdit ? "Shortage resolution" : "New shortage resolution")}
      eyebrow="Shortage resolution"
      description="Settle one or more shortage rows through physical replacement or financial compensation."
      status={<Badge tone={status === "Posted" ? "success" : "warning"}>{status}</Badge>}
      actions={
        <>
          <Link className="hc-button hc-button--ghost hc-button--md" to="/shortage-resolutions">
            Back to resolutions
          </Link>
          {isEditable ? (
            <>
              <Button variant="secondary" size="md" onClick={handleSave} disabled={saving || posting}>
                {saving ? "Saving..." : "Save draft"}
              </Button>
              <Button variant="primary" size="md" onClick={handlePost} disabled={posting || saving || !isEdit}>
                {posting ? "Posting..." : "Post resolution"}
              </Button>
            </>
          ) : null}
        </>
      }
    >
      <DocumentSection title="Header">
        <div className="hc-form-grid">
          <Field label="Resolution no" hint={errors.resolutionNo?.[0]}>
            <Input value={values.resolutionNo} onChange={(event) => setValue("resolutionNo", event.target.value)} disabled={!isEditable} />
          </Field>

          <Field label="Supplier" hint={errors.supplierId?.[0]}>
            <Select value={values.supplierId} onChange={(event) => handleSupplierChange(event.target.value)} disabled={!isEditable}>
              <option value="">Select supplier</option>
              {suppliers.map((supplier) => (
                <option key={supplier.id} value={supplier.id}>
                  {supplier.code} - {supplier.name}
                </option>
              ))}
            </Select>
          </Field>

          <Field label="Resolution type" hint={errors.resolutionType?.[0]}>
            <Select value={values.resolutionType} onChange={(event) => handleResolutionTypeChange(event.target.value as ShortageResolutionType)} disabled={!isEditable}>
              <option value="Physical">Physical</option>
              <option value="Financial">Financial</option>
            </Select>
          </Field>

          <Field label="Resolution date" hint={errors.resolutionDate?.[0]}>
            <Input type="date" value={values.resolutionDate} onChange={(event) => setValue("resolutionDate", event.target.value)} disabled={!isEditable} />
          </Field>

          <Field label="Currency">
            <Input value={values.currency} onChange={(event) => setValue("currency", event.target.value)} disabled={!isEditable} />
          </Field>

          <Field label="Totals">
            <div className="hc-shortage-resolution__totals">
              {values.resolutionType === "Physical" ? (
                <span>{totalQty.toLocaleString()} qty</span>
              ) : (
                <>
                  <span>{totalQty.toLocaleString()} qty</span>
                  <span>{totalAmount.toLocaleString()} {values.currency || ""}</span>
                </>
              )}
              <span>{values.allocations.length} allocations</span>
            </div>
          </Field>
        </div>

        <Field label="Notes" hint={errors.notes?.[0]}>
          <Textarea value={values.notes} onChange={(event) => setValue("notes", event.target.value)} rows={4} disabled={!isEditable} />
        </Field>

        {formError ? (
          <div className="hc-inline-alert hc-inline-alert--danger">
            <strong>Resolution issue</strong>
            <span>{formError}</span>
          </div>
        ) : null}
      </DocumentSection>

      <DocumentSection title="Allocation Grid" actions={isEditable ? <Button size="sm" variant="ghost" onClick={handleAutoFillFifo}>Auto-fill FIFO</Button> : null}>
        {values.allocations.length === 0 ? renderEmptyGridRow("No allocations.", values.resolutionType === "Financial" ? 8 : 6) : (
          <div className="hc-resolution-grid">
            <table className="hc-resolution-grid__table">
              <thead>
                <tr>
                  <th>Shortage row</th>
                  <th>Open qty</th>
                  <th>Resolved qty</th>
                  {values.resolutionType === "Financial" ? <th>Valuation rate</th> : null}
                  {values.resolutionType === "Financial" ? <th>Calculated amount</th> : null}
                  <th>Remaining after post</th>
                  <th>Sequence</th>
                  <th aria-label="Actions" />
                </tr>
              </thead>
              <tbody>
                {values.allocations.map((allocation) => {
                  const shortage = liveShortageLookup.get(allocation.shortageLedgerId) ?? persistedShortageLookup.get(allocation.shortageLedgerId);

                  return (
                    <tr key={allocation.shortageLedgerId}>
                      <td>
                        <div className="hc-table__cell-strong">
                          <span className="hc-table__title">{shortage?.purchaseReceiptNo ?? allocation.shortageLedgerId}</span>
                          <span className="hc-table__subtitle">{shortage?.itemCode ?? "Item"} / {shortage?.componentItemCode ?? "Component"}</span>
                        </div>
                      </td>
                      <td>{shortage?.openQty?.toLocaleString() ?? "-"}</td>
                      <td>
                        <Input
                          type="number"
                          min="0"
                          step="0.000001"
                          value={allocation.allocatedQty}
                          onChange={(event) => updateAllocation(allocation.shortageLedgerId, { allocatedQty: event.target.value === "" ? "" : Number(event.target.value) })}
                          disabled={!isEditable}
                        />
                      </td>
                      {values.resolutionType === "Financial" ? (
                        <td>
                          <Input
                            type="number"
                            min="0"
                            step="0.000001"
                            value={allocation.valuationRate}
                            onChange={(event) => updateAllocation(allocation.shortageLedgerId, { valuationRate: event.target.value === "" ? "" : Number(event.target.value) })}
                            disabled={!isEditable}
                          />
                          {toNumber(allocation.valuationRate) <= 0 ? (
                            <div className="hc-table__subtitle">Required</div>
                          ) : null}
                        </td>
                      ) : null}
                      {values.resolutionType === "Financial" ? (
                        <td>{calculateAmount(allocation.allocatedQty, allocation.valuationRate).toLocaleString()}</td>
                      ) : null}
                      <td>
                        {Math.max(roundQuantity((shortage?.openQty ?? 0) - toNumber(allocation.allocatedQty)), 0).toLocaleString()}
                      </td>
                      <td>{allocation.sequenceNo}</td>
                      <td className="hc-resolution-grid__actions">
                        {isEditable ? (
                          <Button size="sm" variant="ghost" onClick={() => removeAllocation(allocation.shortageLedgerId)}>
                            Remove
                          </Button>
                        ) : null}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </DocumentSection>

      <DocumentSection title="Available Open Shortages">
        <div className="hc-form-grid hc-form-grid--single hc-shortage-resolution__filter-grid">
          <Field label="Search shortage rows">
            <Input
              placeholder="Search receipt, item, component"
              value={openShortageFilters.search}
              onChange={(event) => setOpenShortageFilter("search", event.target.value)}
            />
          </Field>
        </div>

        {!values.supplierId ? (
          renderEmptyGridRow("Select supplier.", 9)
        ) : loadingShortages ? (
          <div className="hc-skeleton-stack">
            <SkeletonLoader height="3rem" variant="rect" />
            <SkeletonLoader height="3rem" variant="rect" />
          </div>
        ) : unselectedOpenShortages.length === 0 ? (
          renderEmptyGridRow("No open shortages.", 9)
        ) : (
          <div className="hc-resolution-grid">
            <table className="hc-resolution-grid__table">
              <thead>
                <tr>
                  <th>Supplier</th>
                  <th>Receipt</th>
                  <th>Item</th>
                  <th>Component</th>
                  <th>Resolved</th>
                  <th>Open qty</th>
                  <th>Open amount</th>
                  <th>Status</th>
                  <th aria-label="Actions" />
                </tr>
              </thead>
              <tbody>
                {unselectedOpenShortages.map((shortage) => (
                  <tr key={shortage.id}>
                    <td>
                      <div className="hc-table__cell-strong">
                        <span className="hc-table__title">{shortage.supplierName}</span>
                        <span className="hc-table__subtitle">{shortage.supplierCode}</span>
                      </div>
                    </td>
                    <td>
                      <div className="hc-table__cell-strong">
                        <span className="hc-table__title">{shortage.purchaseReceiptNo}</span>
                        <span className="hc-table__subtitle">{new Date(shortage.receiptDate).toLocaleDateString()}</span>
                      </div>
                    </td>
                    <td>
                      <div className="hc-table__cell-strong">
                        <span className="hc-table__title">{shortage.itemName}</span>
                        <span className="hc-table__subtitle">{shortage.itemCode}</span>
                      </div>
                    </td>
                    <td>
                      <div className="hc-table__cell-strong">
                        <span className="hc-table__title">{shortage.componentItemName}</span>
                        <span className="hc-table__subtitle">{shortage.componentItemCode}</span>
                      </div>
                    </td>
                    <td>
                      <div className="hc-table__cell-strong">
                        <span className="hc-table__title">{shortage.resolvedQtyEquivalent.toLocaleString()}</span>
                        <span className="hc-table__subtitle">
                          P {shortage.resolvedPhysicalQty.toLocaleString()} / F {shortage.resolvedFinancialQtyEquivalent.toLocaleString()}
                        </span>
                      </div>
                    </td>
                    <td>{shortage.openQty.toLocaleString()}</td>
                    <td>{shortage.openAmount?.toLocaleString() ?? "Pending value"}</td>
                    <td><Badge tone={shortage.status === "PartiallyResolved" ? "warning" : "neutral"}>{shortage.status}</Badge></td>
                    <td className="hc-resolution-grid__actions">
                      {isEditable ? (
                        <Button size="sm" variant="secondary" onClick={() => addShortage(shortage)}>
                          Add
                        </Button>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </DocumentSection>
    </DocumentPageLayout>
  );
}

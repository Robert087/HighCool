# API Spec v1 — Procurement and Inventory

## Customers

### `GET /api/customers`

Lists customers.

Optional query parameters:

* `search`
* `isActive`

Behavior:

* search matches `code`, `name`, and `phone`

### `GET /api/customers/{id}`

Returns one customer master-data record.

### `POST /api/customers`

Creates a customer record.

### `PUT /api/customers/{id}`

Updates a customer record.

### `POST /api/customers/{id}/activate`

Marks a customer as active.

### `POST /api/customers/{id}/deactivate`

Marks a customer as inactive.

## Purchase Orders

### `GET /api/purchase-orders`

Lists purchase orders.

Optional query parameters:

* `search`

### `GET /api/purchase-orders/{id}`

Returns one purchase order with nested lines and computed receipt progress.

### `GET /api/purchase-orders/{id}/available-lines-for-receipt`

Returns posted PO lines with remaining receivable quantity greater than zero.

### `POST /api/purchase-orders`

Creates a purchase order draft.

Request body:

```json
{
  "poNo": "PO-20260420-0001",
  "supplierId": "guid",
  "orderDate": "2026-04-20T00:00:00.000Z",
  "expectedDate": "2026-04-25T00:00:00.000Z",
  "notes": "Expected supplier delivery",
  "lines": [
    {
      "lineNo": 1,
      "itemId": "guid",
      "orderedQty": 10.0,
      "uomId": "guid",
      "notes": "Main ordered item"
    }
  ]
}
```

### `PUT /api/purchase-orders/{id}`

Updates a purchase order draft.

### `POST /api/purchase-orders/{id}/post`

Posts a draft purchase order.

### `POST /api/purchase-orders/{id}/cancel`

Cancels a posted purchase order when no posted receipts already exist.

## Purchase Receipts

### `GET /api/purchase-receipts`

Lists purchase receipts.

Optional query parameters:

* `search`

### `GET /api/purchase-receipts/{id}`

Returns one purchase receipt with nested lines and auto-filled component rows. Each component row includes system-derived `expectedQty` plus editable `actualReceivedQty`.

### `POST /api/purchase-receipts`

Creates a purchase receipt draft.

Request body:

```json
{
  "receiptNo": "PR-20260420-0001",
  "supplierId": "guid",
  "warehouseId": "guid",
  "purchaseOrderId": "guid",
  "receiptDate": "2026-04-20T00:00:00.000Z",
  "supplierPayableAmount": 1250.0,
  "notes": "Receipt capture",
  "lines": [
    {
      "lineNo": 1,
      "purchaseOrderLineId": "guid",
      "itemId": "guid",
      "orderedQtySnapshot": 10.0,
      "receivedQty": 6.0,
      "uomId": "guid",
      "notes": "Partial receipt",
      "components": [
        {
          "componentItemId": "guid",
          "expectedQty": 12.0,
          "actualReceivedQty": 11.0,
          "uomId": "guid",
          "shortageReasonCodeId": "guid",
          "notes": "Short on one component"
        }
      ]
    }
  ]
}
```

### `PUT /api/purchase-receipts/{id}`

Updates a draft purchase receipt.

### `POST /api/purchase-receipts/{id}/post`

Posts a draft purchase receipt.

Behavior:

* receipt line components are derived from the selected item's BOM components
* expected component quantities are calculated as `receivedQty x item component quantity`
* actual component quantities default to expected quantities and remain editable
* shortage reason is optional when actual component quantity is below expected quantity
* only `Draft` receipts can be posted
* posting is idempotent
* linked PO quantities cannot exceed remaining posted PO quantity
* posting creates stock ledger entries
* posting creates supplier statement rows from the current receipt financial basis
* posting creates shortage ledger entries when actual components are below expected BOM quantities
* `supplierPayableAmount` is the current explicit receipt financial basis until receipt line pricing exists

## Shortage Reason Codes

### `GET /api/shortage-reason-codes`

Lists active shortage reason codes for purchase receipt shortage capture.

## Open Shortages

### `GET /api/shortages/open`

Lists shortage rows that still have open quantity.

Optional query parameters:

* `search`
* `supplierId`
* `itemId`
* `componentItemId`
* `affectsSupplierBalance`
* `status`
* `fromDate`
* `toDate`

### `GET /api/shortages/{id}`

Returns one shortage row with current open balance, physical resolved quantity, financial resolved quantity-equivalent, and monetary balances.

## Shortage Resolutions

### `GET /api/shortage-resolutions`

Lists shortage resolution documents.

Optional query parameters:

* `search`
* `supplierId`
* `resolutionType`
* `status`
* `fromDate`
* `toDate`

### `GET /api/shortage-resolutions/{id}`

Returns one shortage resolution with nested allocation rows.

### `GET /api/shortage-resolutions/{id}/allocations`

Returns allocation rows for one shortage resolution.

### `POST /api/shortage-resolutions`

Creates a shortage resolution draft.

Request body:

```json
{
  "resolutionNo": "SR-20260421-0001",
  "supplierId": "guid",
  "resolutionType": "Physical",
  "resolutionDate": "2026-04-21T00:00:00.000Z",
  "currency": "EGP",
  "notes": "Supplier replacement shipment",
  "allocations": [
    {
      "shortageLedgerId": "guid",
      "allocatedQty": 4.0,
      "valuationRate": null,
      "allocationMethod": "Manual",
      "sequenceNo": 1
    }
  ]
}
```

### `PUT /api/shortage-resolutions/{id}`

Updates a shortage resolution draft.

### `POST /api/shortage-resolutions/{id}/post`

Posts a draft shortage resolution.

Behavior:

* physical resolution creates stock ledger `IN` entries with transaction type `ShortagePhysicalResolution`
* financial resolution creates supplier statement entries with effect type `ShortageFinancialResolution`
* allocation rows are mandatory and keep source shortage traceability
* one resolution may settle multiple shortage rows
* one shortage row may be settled across multiple resolutions over time
* one shortage row may be settled by both physical and financial resolutions over time
* any shortage row with `open_qty > 0` may be settled in either physical or financial mode
* physical posting requires `allocated_qty` only
* financial posting requires `allocated_qty` plus `valuation_rate`
* financial posting calculates and stores `allocated_amount = allocated_qty x valuation_rate`

## Supplier Statements

### `GET /api/supplier-statements`

Lists supplier statement rows across suppliers.

Optional query parameters:

* `search`
* `supplierId`
* `effectType`
* `sourceDocType`
* `fromDate`
* `toDate`

### `GET /api/suppliers/{supplierId}/statement`

Lists supplier statement rows for one supplier.

Optional query parameters:

* `search`
* `effectType`
* `sourceDocType`
* `fromDate`
* `toDate`

### `GET /api/suppliers/{supplierId}/statement/summary`

Returns supplier statement summary values for one supplier.

Optional query parameters:

* `effectType`
* `sourceDocType`
* `fromDate`
* `toDate`

Behavior:

* supplier statements are generated from posted business documents only
* purchase receipt posting creates supplier statement rows
* financial shortage resolution posting creates supplier statement rows
* physical shortage resolution posting does not create supplier statement rows
* no manual create, update, or delete supplier statement endpoint exists
* purchase receipt statement amount currently comes from the posted receipt header payable amount
* financial posting stores `financial_qty_equivalent = allocated_qty`
* shortage status stays `PartiallyResolved` until `open_qty` reaches `0`
* posting is idempotent

## Supplier Payments

### `GET /api/payments`

Lists supplier payments.

Optional query parameters:

* `search`
* `supplierId`
* `direction`
* `status`
* `paymentMethod`
* `fromDate`
* `toDate`

### `GET /api/payments/{id}`

Returns one supplier payment with nested allocations.

### `GET /api/payments/{id}/allocations`

Returns allocation rows for one payment.

### `POST /api/payments`

Creates a supplier payment draft.

Request body:

```json
{
  "paymentNo": "PAY-20260422-0001",
  "partyType": "Supplier",
  "partyId": "guid",
  "direction": "OutboundToParty",
  "amount": 1250.0,
  "paymentDate": "2026-04-22T00:00:00.000Z",
  "currency": "EGP",
  "exchangeRate": null,
  "paymentMethod": "BankTransfer",
  "referenceNote": "BANK-TRX-001",
  "notes": "Supplier settlement",
  "allocations": [
    {
      "targetDocType": "PurchaseReceipt",
      "targetDocId": "guid",
      "targetLineId": null,
      "allocatedAmount": 1250.0,
      "allocationOrder": 1
    }
  ]
}
```

### `PUT /api/payments/{id}`

Updates a draft supplier payment.

### `POST /api/payments/{id}/post`

Posts a draft supplier payment.

Behavior:

* supplier payments are generated and posted through document workflows only
* payment allocation is mandatory before posting
* current supplier payment directions are:
  * `OutboundToParty` for company payment to supplier against purchase receipt payables
  * `InboundFromParty` for money received from supplier against financial shortage resolution receivables
* posted payment amount must equal total allocated amount
* no manual supplier statement entry endpoint exists
* payment posting creates supplier statement rows with effect type `Payment`

## Supplier Open Balances

### `GET /api/suppliers/{supplierId}/open-balances`

Lists currently open supplier-side targets that can be allocated from a payment.

Required query parameters:

* `direction`

Optional query parameters:

* `search`
* `fromDate`
* `toDate`

Behavior:

* `direction = OutboundToParty` returns open posted purchase receipts
* `direction = InboundFromParty` returns open posted financial shortage resolutions
* open amount is derived from source document amount minus posted payment allocations

### `POST /api/shortage-resolutions/suggest-allocations`

Returns FIFO allocation suggestions for the selected supplier and resolution type.

## Stock Ledger

### `GET /api/stock-ledger`

Lists stock movement rows from the append-only stock ledger.

Optional query parameters:

* `search`
* `itemId`
* `warehouseId`
* `transactionType`
* `fromDate`
* `toDate`

### `GET /api/stock-ledger/item/{itemId}`

Lists stock movement rows for one item with the same optional filters except `itemId`.

Behavior:

* stock movement history is read-only
* source document references are returned for traceability
* running balances come from ledger posting logic and are never edited directly
* shortage physical resolution rows appear here alongside purchase receipt stock rows

## Stock Balance

### `GET /api/stock-balance`

Lists stock balances grouped by item and warehouse.

Optional query parameters:

* `search`
* `itemId`
* `warehouseId`
* `transactionType`
* `fromDate`
* `toDate`

### `GET /api/stock-balance/item/{itemId}`

Lists stock balances for one item with the same optional filters except `itemId`.

Behavior:

* balances are derived from stock ledger rows only
* no direct stock quantity edit API exists
* filtered balances are computed from the filtered ledger slice

## Validation Responses

The API returns:

* `400` for business rule violations
* `409` for duplicate document numbers or duplicate entity constraints
* `404` when the target record does not exist

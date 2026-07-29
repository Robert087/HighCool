# API Spec v1 — Procurement and Inventory

## Standard List Contract

All operational list endpoints must use server-side pagination.

Standard query parameters:

* `page`
* `pageSize`
* `sortBy`
* `sortDirection`
* module-specific filters

Standard list response shape:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 0,
  "totalPages": 0,
  "appliedFilters": {},
  "sort": {
    "sortBy": "entryDate",
    "direction": "Desc"
  }
}
```

Rules:

* no future list endpoint may return an unbounded operational result set
* sorting and filtering must be applied on the server
* list endpoints must return lightweight summary DTOs
* detail endpoints remain separate from list endpoints
* clients are responsible for locale-aware display formatting; APIs should return stable raw values, document numbers, and status codes

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
* `status`
* `receiptProgressStatus`
* `fromDate`
* `toDate`
* `page`
* `pageSize`
* `sortBy`
* `sortDirection`

### `GET /api/purchase-orders/{id}`

Returns one purchase order with nested lines and computed receipt progress.

### `GET /api/purchase-orders/{id}/available-lines-for-receipt`

Returns posted PO lines with remaining receivable quantity greater than zero.

Behavior:

* fully received rows are excluded from this actionable endpoint
* remaining receivable quantity uses posted, non-reversed receipts only
* each row includes `unitPrice` from the linked PO line for receipt payable calculation

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
      "unitPrice": 125.0,
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
* `status`
* `linkedToPurchaseOrder`
* `fromDate`
* `toDate`
* `page`
* `pageSize`
* `sortBy`
* `sortDirection`

### `GET /api/purchase-receipts/{id}`

Returns one purchase receipt with nested lines and auto-filled component rows. Each line includes `remainingReturnableQty`; PO-linked lines also include `unitPrice` and `lineAmount`. Each component row includes system-derived `expectedQty` plus editable `actualReceivedQty`.

Behavior:

* `remainingReturnableQty` is returned in the receipt line UOM
* fully returned lines remain visible on the document detail view but are excluded from active return candidate lists

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
* duplicate component rows inside the same receipt line are rejected
* posting creates stock ledger entries
* posting creates supplier statement rows from the current receipt financial basis only when that basis is positive
* posting creates shortage ledger entries when actual components are below expected BOM quantities
* PO-linked `supplierPayableAmount` is server-calculated from `receivedQty x purchaseOrderLine.unitPrice`
* manual `supplierPayableAmount` remains the explicit receipt financial basis until manual receipt line pricing exists
* if `supplierPayableAmount <= 0`, posting still succeeds but no zero-value supplier statement row is created

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
* `page`
* `pageSize`
* `sortBy`
* `sortDirection`

### `GET /api/shortages/{id}`

Returns one shortage row with expected quantity, initial actual quantity, physical resolved quantity, final physical quantity, financial resolved quantity-equivalent, open quantity, and monetary balances.

Behavior:

* resolved rows do not appear in `GET /api/shortages/open`
* final physical quantity is `initial actual quantity + physical resolved quantity`

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
* `page`
* `pageSize`
* `sortBy`
* `sortDirection`

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
* duplicate shortage targets inside one draft resolution are rejected
* physical posting requires `allocated_qty` only
* financial posting requires `allocated_qty` plus `valuation_rate`
* financial posting calculates and stores `allocated_amount = allocated_qty x valuation_rate`

## Supplier Statements

### `GET /api/supplier-statements`

Supports paginated statement entry queries and must not return unbounded statement history.

Lists supplier statement rows across suppliers.

Optional query parameters:

* `search`
* `supplierId`
* `effectType`
* `sourceDocType`
* `fromDate`
* `toDate`

### `GET /api/suppliers/{supplierId}/statement`

Supports the same pagination, filter, and sort contract as `GET /api/supplier-statements`.

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
* purchase return posting creates supplier statement rows only when a valid referenced receipt financial basis produces a positive return amount
* financial shortage resolution posting creates supplier statement rows
* physical shortage resolution posting does not create supplier statement rows
* purchase receipt reversal rows use source type `PurchaseReceiptReversal` and effect type `PurchaseReceiptReversal`
* supplier payment reversal rows use source type `PaymentReversal` and effect type `PaymentReversal`
* shortage financial resolution reversal rows use source type `ShortageResolutionReversal` and effect type `ShortageResolutionReversal`
* financial supplier statement rows with both `debit = 0` and `credit = 0` are not returned in the supplier statement view
* no manual create, update, or delete supplier statement endpoint exists
* purchase receipt statement amount currently comes from the posted receipt header payable amount
* financial posting stores `financial_qty_equivalent = allocated_qty`
* shortage status stays `PartiallyResolved` until `open_qty` reaches `0`
* posting is idempotent
* current effect and source document values include:
  * `PurchaseReceipt`
  * `PurchaseReturn`
  * `Payment`
  * `PurchaseReceiptReversal`
  * `PaymentReversal`
  * `ShortageFinancialResolution`
  * `ShortageResolutionReversal`

## Supplier Payments

### `GET /api/payments`

Supports paginated payment list queries with server-side search, filters, and sorting.

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

Supports paginated open-target queries for payment allocation candidates.

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
* purchase receipt target state includes `originalAmount`, `adjustedAmount`, `netAmount`, `allocatedAmount`, `openAmount`, and `status`
* purchase receipt `adjustedAmount` is reduced by posted, non-reversed purchase returns linked to that receipt
* open amount is derived from active source document amount minus posted payment allocations

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
* shortage physical resolution, inventory adjustment, and inventory transfer rows appear here alongside purchase receipt stock rows

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

## Purchase Return Addendum

### `GET /api/purchase-returns`

Lists purchase return documents.

### `GET /api/purchase-returns/{id}`

Returns one purchase return with nested lines.

### `POST /api/purchase-returns`

Creates a purchase return draft.

### `PUT /api/purchase-returns/{id}`

Updates a purchase return draft.

### `POST /api/purchase-returns/{id}/post`

Posts a draft purchase return and creates stock ledger plus supplier statement effects when a valid return financial basis exists.

Behavior:

* linked rows validate against `remainingReturnableQty`
* return quantity greater than remaining returnable quantity is rejected
* the same receipt line cannot appear twice inside one return
* fully returned receipt lines must not appear in active return candidate lists
* purchase return supplier statement rows use source type `PurchaseReturn` and effect type `PurchaseReturn`
* if no valid referenced receipt financial basis exists, posting still succeeds but no zero-value supplier statement row is created

## Inventory Transfer Addendum

### `GET /api/inventory-transfers`

Lists inventory transfers.

Required permission: `inventory.stock_ledger.view`.

Optional query parameters:

* `search`
* `transferNo`
* `sourceWarehouseId`
* `destinationWarehouseId`
* `status`
* `fromDate`
* `toDate`
* `page`
* `pageSize`
* `sortBy`
* `sortDirection`

### `GET /api/inventory-transfers/{id}`

Returns one inventory transfer with nested lines.

Required permission: `inventory.stock_ledger.view`.

### `POST /api/inventory-transfers`

Creates an inventory transfer draft. The transfer number is generated server-side.

Required permission: `inventory.transfer.create`.

Request body:

```json
{
  "transferDate": "2026-07-28T00:00:00.000Z",
  "sourceWarehouseId": "guid",
  "destinationWarehouseId": "guid",
  "notes": "Move stock to branch warehouse",
  "lines": [
    {
      "lineNo": 1,
      "itemId": "guid",
      "uomId": "guid",
      "quantity": 5.0,
      "notes": "Transfer quantity"
    }
  ]
}
```

### `PUT /api/inventory-transfers/{id}`

Updates an inventory transfer draft. The transfer number remains immutable.

Required permission: `inventory.transfer.create`.

### `DELETE /api/inventory-transfers/{id}`

Deletes an inventory transfer draft.

Required permission: `inventory.transfer.create`.

### `POST /api/inventory-transfers/{id}/post`

Posts a draft transfer and creates one source warehouse `OUT` stock ledger row plus one destination warehouse `IN` stock ledger row per line.

Required permission: `inventory.transfer.post`.

### `POST /api/inventory-transfers/{id}/cancel`

Cancels a posted transfer and creates reversing stock ledger rows.

Required permission: `inventory.transfer.post`.

Behavior:

* source and destination warehouses must be active and different
* line `baseQty` is calculated server-side from the item base UOM conversion
* posting validates source warehouse stock and aggregates duplicate item/source lines in base UOM
* destination warehouse availability is not validated during posting
* cancellation validates destination warehouse stock and aggregates duplicate item/destination lines in base UOM
* posted and canceled transfers are read-only
* post and cancel are idempotent for already-final documents
* all routes require the `inventory` and `inventory_transfers` feature gates

## Reversal Addendum

## Inventory Count Addendum

### `GET /api/inventory-counts`

Lists inventory counts.

Required permission: `inventory.count.view`.

Optional query parameters:

* `search`
* `countNo`
* `warehouseId`
* `status`
* `fromDate`
* `toDate`
* `page`
* `pageSize`
* `sortBy`
* `sortDirection`

### `GET /api/inventory-counts/{id}`

Returns one inventory count with nested lines.

Required permission: `inventory.count.view`.

### `POST /api/inventory-counts`

Creates an inventory count draft. The count number is generated server-side.

Required permission: `inventory.count.create`.

Request body:

```json
{
  "countDate": "2026-07-28T00:00:00.000Z",
  "warehouseId": "guid",
  "notes": "Cycle count",
  "lines": [
    {
      "lineNo": 1,
      "itemId": "guid",
      "uomId": "guid",
      "countedQty": 12.0,
      "notes": "Shelf A"
    }
  ]
}
```

### `PUT /api/inventory-counts/{id}`

Updates an inventory count draft. The count number remains immutable.

Required permission: `inventory.count.create`.

### `DELETE /api/inventory-counts/{id}`

Deletes an inventory count draft.

Required permission: `inventory.count.create`.

### `POST /api/inventory-counts/{id}/refresh-system-quantities`

Refreshes draft system quantities from current stock ledger balances.

Required permission: `inventory.count.create`.

### `POST /api/inventory-counts/{id}/post`

Posts a draft count. The server re-reads current ledger stock inside the posting transaction, persists the system/count/variance values used for audit, and creates stock ledger rows only for non-zero variance lines.

The posting transaction uses the latest committed stock visible at posting time but does not freeze unrelated stock postings for the same item and warehouse. A stricter physical-count cutoff requires a future stock-freeze or reservation flow.

Required permission: `inventory.count.post`.

### `POST /api/inventory-counts/{id}/cancel`

Cancels a posted count and creates reversing stock ledger rows only for rows created by count posting.

Required permission: `inventory.count.post`.

Behavior:

* one count applies to one active warehouse
* the same item cannot appear more than once in one count document
* counted quantity must be zero or positive
* system quantity and base quantity fields are server-calculated and must not be trusted from the client
* positive variance writes `InventoryCountIncrease`
* negative variance writes `InventoryCountDecrease`
* zero variance writes no stock ledger row
* posting a legitimate decrease is allowed because the final stock equals the non-negative counted quantity
* cancellation of a count increase validates current stock when negative stock is disabled
* post and cancel are idempotent for already-final documents
* all routes require the `inventory` and `inventory_counts` feature gates

## Inventory Issue Addendum

### `GET /api/inventory-issues`

Lists inventory issues.

Required permission: `inventory.issue.view`.

Optional query parameters:

* `search`
* `issueNo`
* `warehouseId`
* `reason`
* `status`
* `fromDate`
* `toDate`
* `page`
* `pageSize`
* `sortBy`
* `sortDirection`

### `GET /api/inventory-issues/{id}`

Returns one inventory issue with nested lines.

Required permission: `inventory.issue.view`.

### `POST /api/inventory-issues`

Creates an inventory issue draft. The issue number is generated server-side.

Required permission: `inventory.issue.create`.

Request body:

```json
{
  "issueDate": "2026-07-29T00:00:00.000Z",
  "warehouseId": "guid",
  "reason": "InternalConsumption",
  "referenceNo": "REQ-1001",
  "requestedBy": "Maintenance Team",
  "notes": "Internal consumption issue",
  "lines": [
    {
      "lineNo": 1,
      "itemId": "guid",
      "uomId": "guid",
      "quantity": 5.0,
      "notes": "Consumed by production support"
    }
  ]
}
```

### `PUT /api/inventory-issues/{id}`

Updates an inventory issue draft. The issue number remains immutable.

Required permission: `inventory.issue.create`.

### `DELETE /api/inventory-issues/{id}`

Deletes an inventory issue draft.

Required permission: `inventory.issue.create`.

### `POST /api/inventory-issues/{id}/post`

Posts a draft issue and creates one warehouse `OUT` stock ledger row per line.

Required permission: `inventory.issue.post`.

### `POST /api/inventory-issues/{id}/cancel`

Cancels a posted issue and creates reversing warehouse `IN` stock ledger rows.

Required permission: `inventory.issue.post`.

Behavior:

* one issue applies to one active warehouse
* `reason` must be one of `InternalConsumption`, `Damage`, `Scrap`, `Sample`, `Maintenance`, `BranchUse`, or `Other`
* the same item cannot appear more than once in one issue document
* issue quantity must be positive
* line `baseQty` is calculated server-side from the item base UOM conversion
* posting validates warehouse stock and aggregates issue lines by item and warehouse in base UOM
* `baseQty` and document number fields must not be trusted from the client
* posting writes `InventoryIssue` transaction/source document rows
* cancellation writes `InventoryIssueCancellation` transaction/source document rows
* posted and canceled issues are read-only
* post and cancel are idempotent for already-final documents
* all routes require the `inventory` and `inventory_issues` feature gates

### `POST /api/purchase-receipts/{id}/reverse`

Reverses a posted purchase receipt.

### `POST /api/payments/{id}/reverse`

Reverses a posted supplier payment.

### `POST /api/shortage-resolutions/{id}/reverse`

Reverses a posted shortage resolution.

All reversal endpoints accept:

```json
{
  "reversalDate": "2026-04-23T00:00:00.000Z",
  "reversalReason": "Explain why the posted document must be reversed"
}
```

Behavior:

* duplicate reversal is rejected
* receipt reversal is blocked by active posted returns, payment allocations, or shortage resolutions that depend on that receipt
* shortage resolution reversal is blocked by active posted payment allocations that depend on that resolution
* supplier statement reversal rows always invert the original financial supplier statement rows instead of generating unrelated generic payment effects

## Local Backup & Restore Addendum

Local database endpoints are authenticated and permission-gated. Backups are selected by server-issued backup ID only; clients must not send arbitrary filesystem paths.

### `POST /api/local-database/backups`

Creates a manual encrypted local SQLite backup.

Permission:

* `settings.database_backup.create`

Behavior:

* rejects client-provided path fields
* creates a manifest-linked encrypted backup through the backend service
* returns backup ID, safe file names, size, checksum, reason, status, and message

### `GET /api/local-database/backups/summary`

Returns safe Backup Center summary data.

Permission:

* `settings.database_diagnostics.read`

Includes:

* health status and reasons
* last successful backup time
* last integrity verification time
* safe database file name and size
* available backup count and storage used
* encryption, retention, application version, and schema version

### `GET /api/local-database/backups`

Lists managed local backups newest first.

Permission:

* `settings.database_diagnostics.read`

Behavior:

* returns safe metadata only
* omits raw filesystem paths
* omits invalid manifests from the user-facing catalog while retention continues preserving them safely

### `GET /api/local-database/backups/{backupId}`

Returns details for one managed backup.

Permission:

* `settings.database_diagnostics.read`

Includes:

* manifest metadata
* encrypted and plain SHA-256 values
* encryption algorithm
* sizes
* persisted integrity status
* restore preflight compatibility when available

### `POST /api/local-database/backups/{backupId}/verify`

Verifies one managed backup.

Permission:

* `settings.database_backup.create`

Behavior:

* decrypts to a temporary backend-managed file
* runs SQLite integrity verification
* persists a safe verification result sidecar
* never marks a backup verified without backend confirmation

### `GET /api/local-database/backup-retention`

Returns effective local backup retention settings.

Permission:

* `settings.database_diagnostics.read`

### `PUT /api/local-database/backup-retention`

Saves local backup retention settings.

Permission:

* `settings.database_backup.create`

Behavior:

* persists settings in backend-managed local storage
* clamps counts and age limits to safe ranges
* does not allow arbitrary backup-root changes

### `POST /api/local-database/restore/validate`

Runs restore preflight for a backup ID and returns a server-issued restore operation ID when the selected backup is compatible.

Permission:

* `settings.database_restore.validate`

Request:

```json
{
  "backupId": "server-issued-backup-id"
}
```

Successful response includes safe preflight details plus:

```json
{
  "operationId": "server-issued-expiring-operation-id",
  "operationExpiresAtUtc": "2026-07-25T12:00:00Z"
}
```

The operation ID is bound to the selected backup, authenticated user, current installation metadata, and preflight compatibility metadata. It is single-use and expires.

### `POST /api/local-database/restore`

Executes restore after explicit backend confirmation.

Permission:

* `settings.database_restore.execute`

Request:

```json
{
  "backupId": "server-issued-backup-id",
  "operationId": "server-issued-expiring-operation-id",
  "confirmation": "RESTORE_LOCAL_DATABASE"
}
```

Behavior:

* requires the server-issued preflight operation ID and typed confirmation phrase
* reruns preflight
* rejects missing, expired, replayed, wrong-user, wrong-backup, or compatibility-mismatched operation IDs
* creates a pre-restore safety backup
* replaces the local database only through backend restore service logic
* records restore journal state
* returns selected backup ID and safety backup ID when available

All `/api/local-database/*` endpoints are available only in the Desktop host environment. Automated tests may enable the explicit Testing-only `LocalDatabase:EnableEndpointCapability` flag to exercise these endpoints without running the desktop host.

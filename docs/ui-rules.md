# UI Rules

## List View Rules

All list pages must use one shared list view pattern. Do not redesign row structure, action placement, density, or badge behavior per module unless the exception is documented in the page spec and approved during implementation.

### Standard Row Hierarchy

Every list row must follow this order of importance:

1. Primary identifier
2. Main business context
3. Grouped operational metadata
4. Primary status
5. Optional secondary status or progress
6. Actions

Rules:

* Column 1 must be the row anchor and must contain the primary identifier.
* Primary text must use semibold or bold styling.
* Optional secondary text must appear directly under the primary text in muted styling.
* Supporting metadata must not visually compete with the primary identifier.
* Rows must not give every column equal visual weight.

### Primary vs Secondary Data

Primary data is the value a user scans for first.

Examples:

* PO number
* receipt number
* customer name
* supplier name
* item code
* employee name

Secondary data is supporting context only.

Rules:

* Secondary data must use smaller muted text.
* Secondary data should sit below the primary line or inside a grouped support cell.
* Small support values must not become standalone columns unless sorting or business workflow clearly requires it.

### Group Related Fields

Group related values into one column whenever possible to keep tables compact and scannable.

Preferred grouped pairs:

* Order date + expected date
* Name + code
* City + area
* Quantity + UOM
* Warehouse + source document
* Phone + email

Rules:

* Use short stacked labels when grouping values.
* Do not create separate low-value columns for tightly related fields.
* If a field is rarely used for scanning, move it to details or the record form instead of keeping it visible in the table.

### Status Chips

Status presentation must be reusable and consistent across all pages.

Rules:

* Use the shared badge/chip component only.
* Use one primary business status and one optional secondary progress/status chip.
* Status labels must be short and standardized.
* Color mapping must stay fixed across modules:
* success = green
* warning = orange
* neutral or in progress = blue or gray
* destructive or canceled = red
* Do not invent page-specific badge colors.

### Actions Pattern

All list pages must use the same action structure.

Rules:

* Each row must expose one primary action button.
* The primary action label should default to `View`.
* All secondary actions must live inside the row overflow menu.
* Do not place multiple inline text links across the row.
* Do not switch action placement between similar pages.
* If a row has no secondary actions, keep only the primary action.
* Overflow menus must render through the shared portal-based overlay component, not inside the grid DOM tree.
* Overflow menus must be positioned from the trigger button bounding box and must not rely on `overflow: visible` hacks.
* Overflow menus must appear above grid content, sticky headers, and surrounding layout chrome.

Allowed secondary actions:

* `Edit`
* `Delete`
* `Activate`
* `Deactivate`
* `Create receipt`
* other workflow-specific actions when needed

### Density and Spacing

List pages are enterprise work surfaces and must stay compact.

Rules:

* Rows must feel dense but readable on desktop.
* Vertical spacing must be consistent across all pages.
* Avoid oversized card-like rows inside standard tables.
* Keep as many records visible above the fold as possible.

### Interaction

Rows must be predictable to use.

Rules:

* Hover state must be visible.
* Click targets must be obvious.
* Sticky table headers should be used on long lists.
* Sorting should be supported on meaningful columns when implemented.
* Row click behavior must never conflict with row action buttons.
* Dropdown and overflow menus must close on outside click and `Escape`.
* Shared overflow menus must support keyboard navigation with arrow keys and trapped tab focus while open.

### Responsive Behavior

Desktop:

* Use the standard table layout.
* Keep the primary identifier first.
* Keep actions pinned to the far right.

Tablet:

* Reduce visible columns.
* Group more metadata into stacked cells.

Mobile:

* Transform table rows into stacked compact list items.
* Preserve order: primary, secondary, status, actions.
* Do not force full-width desktop tables on small screens.

### Anti-Patterns

The following are not allowed in list pages:

* too many equal-weight columns
* multiple inline text actions scattered across the row
* oversized row spacing
* ungrouped related metadata
* inconsistent badge styles
* page-specific action placement
* visually flat rows with no primary anchor
* standalone columns for tiny support values that should be grouped
* rendering dropdown panels inside a clipped table or grid container
* using absolute-positioned menus that depend on the table cell as the containing block

### Enforcement

All future list pages must follow this standard by default.

Requirements:

* Use the shared list row hierarchy.
* Use the shared badge system.
* Use the shared `View + More` action pattern when secondary actions exist.
* Use grouped metadata before adding new columns.
* Match the shared compact density and responsive behavior.
* Document any exception before implementation.
* Use portal-based overlay rendering for all list-row dropdown menus.

## List View Component Contract

Every reusable list view implementation must satisfy this contract.

### Required Structure

Each list/table component must support:

* a primary cell with primary text and optional secondary line
* grouped secondary cells for related business context
* a status area that can show one primary chip and one optional secondary chip
* a row action area with one primary button and an overflow menu
* compact footer pagination
* empty, loading, and error states

### Required Visual Rules

The shared list view component must:

* keep row padding compact and consistent
* keep primary text visually dominant
* keep secondary text muted and smaller
* keep action alignment pinned to the right on desktop
* support mobile stacked presentation without changing the business order of row data
* render overflow menus in a root-level overlay layer such as `document.body`
* position overflow menus with viewport-aware fixed positioning and flipping

### Required Action API

List rows must be implementable with:

* one primary action
* zero or more overflow actions
* optional destructive overflow actions
* keyboard navigation and focus management for the overflow menu

The component contract must reject these patterns in implementation reviews:

* more than one inline primary action
* destructive actions shown as plain text links
* row-level action layouts that differ from the shared pattern

### Required Content Contract

For every new list page, developers must explicitly map:

* Primary identifier
* Main related entity
* Grouped metadata cell
* Primary status
* Optional secondary status
* Primary action
* Overflow actions

If a page cannot fit this contract cleanly, the implementation must document why and what alternative pattern is being used.

## Workflow Section Rules

Complex task pages must separate the user journey into visually distinct sections instead of one continuous surface.

### Summary vs Data Table Distinction

Use a dedicated summary panel when the page includes progress, totals, or next-step guidance.

Rules:

* summary metrics must live in their own card or panel, not inside a data table toolbar
* summary content must answer "where am I in this task?" before the user interacts with tables
* summary actions such as `Auto-fill FIFO` should sit next to the summary metrics, not inside selected-row tables
* summary panels should appear before selected-work and available-work tables

### Section Separation

Pages with multiple work areas must make the sequence obvious.

Preferred order:

1. Summary or progress area
2. Selected or in-progress records
3. Available records or next actions

Rules:

* each work area must have a title and short helper text
* spacing between sections must be stronger than spacing inside a section
* panels for selected work and available work must not visually merge together
* search and filter controls must be attached to the section they control

### Empty States

Empty states on workflow screens must guide the next action.

Rules:

* say what is currently empty
* explain what the user should do next
* avoid generic messages such as `No data`

Preferred examples:

* `No targets added yet`
* `Search and add one or more documents from the section below`
* `No open documents available`

### Task and Action Tables

Tables used for choosing, adding, or adjusting records must feel different from passive report tables.

Rules:

* the primary document identifier must be the strongest text in the row
* helper context should sit under the primary identifier in muted text
* numeric columns must align consistently and use tabular figures when possible
* row hover must clearly signal actionability
* action buttons such as `Add` must be easy to find at the row edge
* table headers should be quieter than row content

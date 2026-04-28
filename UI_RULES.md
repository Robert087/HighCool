# UI_RULES.md

## Purpose

This file defines the UI and UX rules for the ERP frontend.

All frontend work must follow these rules.
Do not introduce page-level design decisions that conflict with this file.

The goal is to build a UI that feels:

* modern
* professional
* structured
* ERP-focused
* clean
* predictable
* fast for daily operations

This is an operational ERP system, not a marketing website and not a generic SaaS dashboard.

---

# 1. Core UI Principles

## 1.1 Structure over decoration

The UI must prioritize:

* clarity
* alignment
* spacing discipline
* information hierarchy
* operational efficiency

Do not prioritize flashy visuals over usability.

## 1.2 Full-width working layout

Pages must use the screen width properly.

Avoid:

* narrow centered forms
* excessive empty space
* small floating content boxes

Prefer:

* wide working surfaces
* structured horizontal layouts
* visible data grids

## 1.3 ERP-first design

This system is for operations-heavy workflows.

Design must support:

* fast data entry
* reading rows and tables quickly
* editing multiple values efficiently
* understanding status and business context immediately

---

# 2. Global App Layout

Use a consistent application shell:

* Left sidebar navigation
* Top header bar
* Full-width main content area

## 2.1 Sidebar

The sidebar should:

* stay on the left
* be visually stable
* contain grouped navigation items
* support active states clearly
* support icons in a restrained way
* allow scrolling if needed

Do not make the sidebar overly decorative.

## 2.2 Top Bar

The top bar should be simple and useful.

It may contain:

* page title
* breadcrumb
* current user
* environment or branch indicator
* quick actions if relevant

Keep it clean and aligned.
Do not overcrowd it.

## 2.3 Main Content

Main content should:

* fill most of the available width
* have consistent padding
* use clear section separation
* avoid random spacing

---

# 3. Page Layout Rules

Every major page must follow this general structure:

1. Page Header
2. Form/Header Information Section
3. Main Grid/Table Section
4. Secondary Details Section
5. Bottom or top action area

## 3.1 Page Header

The page header must include:

* page title
* document identifier if relevant
* status badge if relevant
* primary actions

Examples:

* Save Draft
* Post
* Cancel
* Print

Do not scatter important actions around the page.

## 3.2 Form Section

Header form fields must use a grid layout.

Use:

* 2 columns on screen 
* aligned labels
* consistent input heights
* consistent row spacing

Do not stack all inputs vertically unless the screen is mobile-sized.

## 3.3 Main Grid Section

The table/grid is the center of gravity in ERP pages.

It must:

* be full width
* feel structured
* have clear headers
* align columns properly
* support editing if needed
* remain readable even with many rows

## 3.4 Secondary Details

Secondary details such as:

* item components
* line notes
* extra calculations
* sub-rows

must be visually attached to the parent record.

Do not place them in unrelated floating areas.

## 3.5 Actions

Action buttons must be:

* obvious
* grouped logically
* consistently positioned

Primary actions should always look primary.
Dangerous actions should be visually distinct but not overly dramatic.

---

# 4. Forms

## 4.1 Input alignment

All inputs must:

* share consistent height
* align in grid columns
* have predictable spacing
* use consistent label placement

## 4.2 Labels

Labels must:

* be short
* clear
* consistently positioned
* easy to scan

Do not use inconsistent label styles across pages.

## 4.3 Density

Use compact but readable spacing.

The UI should feel efficient, not crowded.
Do not create large empty areas inside operational forms.

## 4.4 Validation

Validation messages must:

* appear close to the field
* be short and clear
* explain the issue directly

Avoid vague messages.

Examples:

* "Supplier is required."
* "Received quantity must be greater than zero."
* "Shortage reason is required when actual quantity is less than expected."

## 4.6 Bilingual completion

Arabic and English support must be complete at the workflow level, not only at the page frame level.

Rules:
- no visible hardcoded UI strings in reusable components or shipped screens
- localize labels, placeholders, helper text, validation, empty states, confirmation text, workflow hints, and read-only messages
- selectors, dropdown prompts, drawer titles, inline row-editor text, and pagination text must be localized
- every touched form and grid must be checked in both LTR and RTL
- mixed Arabic labels with English codes and numbers must remain readable

## 4.7 Filters

Filters are production workflow controls, not decorative UI.

Rules:
- major grids must expose localized search, quick filters, advanced filters, and reset/clear actions
- filter labels, options, date ranges, and applied-filter chips must be localized
- filter behavior must stay compatible with server-side pagination and sorting
- translated filters are not sufficient unless the controls are wired and usable

## 4.5 Section Separation Rules

When a page contains multiple related workflows in the same screen, the UI must separate them into clearly distinct sections.

Rules:
- each section must have a title
- each section may have helper text if the step is not obvious
- spacing between sections must be larger than spacing inside a section
- summary information must not visually blend with data tables
- tables used for selection must be visually separated from tables used for results or chosen records
- use cards, panels, or clear section containers for grouping
- the user flow must be obvious from top to bottom

Preferred step order for mixed workflow screens:
- summary or status overview first
- selected or in-progress work second
- available records or next actions third

Use:
- stronger outer spacing between sections than within section content
- section-level headings with short helper text
- dedicated panels for summaries instead of merging summary chips into table toolbars

Avoid:
- one continuous block where multiple tasks visually share the same surface
- placing search tools far from the table they control
- mixing selected records and available records in one visual area

## 4.6 Empty State Rules

Empty states must explain both:
- current state
- next action

Do:
- "No targets added yet"
- "Search and add documents from the list below"
- "No open documents available"
- "Choose a supplier first to load available documents"

Do not:
- show only technical empty text like "No data" or "No allocations selected"
- leave the user without a next step

## 4.7 Task Table Rules

Tables used for performing actions must not look identical to passive reporting tables.

Rules:
- actionable rows must have a clear row action
- important values must be emphasized
- row hover must be visible
- action columns must be obvious and consistent
- search/filter tools must belong to the relevant section, not float ambiguously
- the primary document or business record must carry the strongest text weight
- numeric values must align consistently for fast scanning
- headers must be quieter than the row content
- selected-task tables and available-task tables should feel related but not identical

---

# 5. Tables and Data Grids

Tables are a core part of the ERP UX.

## 5.1 General table rules

Tables must:

* occupy available width
* use consistent column sizing
* have clear headers
* have compact row height
* support scanning left to right easily

## 5.2 Header behavior

Where useful, use sticky headers.
Headers should stay readable and distinct.

## 5.3 Row design

Rows must:

* feel organized
* not be overly tall
* not use noisy styling
* use subtle separators if needed

## 5.4 Editable grids

If a table is editable:

* keep controls aligned
* keep interaction obvious
* avoid visual clutter
* distinguish editable fields from read-only cells cleanly

## 5.5 Empty states

Empty states must be clear and calm.

Examples:

* "No purchase receipt lines added yet."
* "No stock movements found for this filter."

---

# 6. Status and Business Context

ERP systems depend on visible business state.

## 6.1 Status visibility

Statuses such as:

* Draft
* Posted
* Canceled
* Open
* Resolved

must be clearly visible near the page title or document header.

## 6.2 Business context visibility

Users should quickly understand:

* which supplier/customer they are viewing
* which warehouse is selected
* which date range/filter is active
* what document they are editing

Do not hide important business context deep in the page.

---

# 7. Dashboard Rules

The dashboard should be useful, not decorative.

## 7.1 Dashboard purpose

The dashboard should help users:

* navigate quickly
* see important business signals
* take common actions
* monitor current activity

## 7.2 Dashboard content

Good dashboard content includes:

* quick actions
* KPI summaries
* pending work indicators
* trend widgets
* operational alerts

Do not overload the dashboard with meaningless cards.

## 7.3 Dashboard layout

Use a clear hierarchy:

* primary summary area
* quick actions
* KPI row
* useful charts/tables

---

# 8. Visual Style Rules

## 8.1 Theme

Use a light professional theme.

Preferred characteristics:

* light background
* white surfaces
* subtle borders
* minimal shadows
* restrained accent color

## 8.2 Shadows

Use shadows sparingly.
Prefer structure and borders over heavy elevation.

## 8.3 Borders

Borders should be subtle but helpful.
Use them to separate sections and inputs cleanly.

## 8.4 Radius

Use modest rounded corners.
Do not over-round everything.

## 8.5 Color use

Color should support meaning, not decoration.

Use color for:

* primary actions
* statuses
* warnings
* errors
* highlights

Do not use many competing accent colors.

---

# 9. Spacing Rules

Spacing must be systematic.

## 9.1 Consistency

Use a spacing scale and apply it consistently.

Do not use random margins or paddings.

## 9.2 Section spacing

Major sections should have clear separation.
Elements within a section should be tighter than the spacing between sections.

## 9.3 Avoid wasted space

Do not create large blank areas in forms or transaction pages.

---

# 10. Responsive Behavior

The system is desktop-first, but must remain usable on smaller screens.

## 10.1 Desktop

Desktop is the primary experience.

Prioritize:

* wide layouts
* visible grids
* efficient form editing

## 10.2 Tablet

Tablet should preserve structure as much as possible.

## 10.3 Mobile

On mobile:

* stack sections logically
* preserve important actions
* avoid breaking critical workflows
* keep data readable

Do not design mobile-first at the expense of desktop ERP usability.

---

# 11. Reusable Components

Frontend should use reusable layout and UI primitives.

Preferred reusable components include:

* AppLayout
* SidebarNav
* TopBar
* PageHeader
* StatusBadge
* FormGrid
* FormSection
* DataGrid
* ActionBar
* FilterBar
* SummaryCard
* EmptyState
* SectionTitle

Do not rebuild page structure from scratch every time.

---

# 12. Page-Specific ERP Rules

## 12.1 Purchase Order

Must feel like:

* structured transaction entry
* strong header context
* lines table as main focus
* actions clearly visible

## 12.2 Purchase Receipt

Must feel like:

* operational receiving form
* line-level detail is easy to read
* actual components are attached to the relevant line
* shortage-related input is obvious when needed

## 12.3 Statements and reports

Must feel:

* tabular
* filterable
* easy to scan
* traceable to source documents

---

# 13. UX Behavior Rules

## 13.1 Predictability

UI behavior must be consistent.
Users should not wonder where actions or fields will appear.

## 13.2 Speed

Frequent actions should take as few steps as possible.

## 13.3 Clarity

Never hide critical information behind visual complexity.

## 13.4 Safety

Posting, canceling, and other critical actions must be deliberate and clear.

---

# 14. What to Avoid

Do NOT introduce:

* narrow centered content layouts
* random card placement
* excessive gradients
* overly decorative dashboard widgets
* large empty spaces
* stacked forms without grid alignment
* hidden actions
* flashy consumer-app visuals
* inconsistent page structures
* module-by-module design inconsistency

---

# 15. Definition of Good UI in This Project

A page is considered well-designed only if:

* it uses width well
* it has clear hierarchy
* it supports operational work
* it feels structured
* forms are aligned
* tables are central and readable
* actions are obvious
* statuses are visible
* spacing is consistent
* design feels professional and modern

---

# 16. Mandatory Rule for All Future UI Work

Any new page or redesign must:

* follow this file
* reuse the common layout system
* avoid one-off layout experiments
* preserve consistency with the ERP design language

If a design decision conflicts with this file, this file wins.

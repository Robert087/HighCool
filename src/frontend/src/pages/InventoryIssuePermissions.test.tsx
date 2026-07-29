import { renderToStaticMarkup } from "react-dom/server";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n";
import { Permissions } from "../services/permissions";
import { getInventoryIssueFormCapabilities, validateInventoryIssueForm } from "./InventoryIssueFormPage";
import { InventoryIssuesPage } from "./InventoryIssuesPage";

const authState = vi.hoisted(() => ({
  permissions: new Set<string>(),
}));

vi.mock("../features/auth/AuthProvider", () => ({
  useAuth: () => ({
    hasPermission: (permission: string) => authState.permissions.has(permission),
  }),
}));

function renderIssuesPage() {
  return renderToStaticMarkup(
    <I18nProvider>
      <MemoryRouter>
        <InventoryIssuesPage />
      </MemoryRouter>
    </I18nProvider>,
  );
}

function hasOnly(...permissions: string[]) {
  return (permission: string) => permissions.includes(permission);
}

describe("Inventory issue permissions", () => {
  beforeEach(() => {
    authState.permissions.clear();
  });

  it("hides the create action when create permission is missing", () => {
    const markup = renderIssuesPage();

    expect(markup).not.toContain("New issue");
    expect(markup).not.toContain("/inventory-issues/new");
  });

  it("shows the create action when create permission is granted", () => {
    authState.permissions.add(Permissions.InventoryIssueCreate);

    const markup = renderIssuesPage();

    expect(markup).toContain("New issue");
    expect(markup).toContain("/inventory-issues/new");
  });

  it("allows post-only users to post existing drafts without save access", () => {
    expect(getInventoryIssueFormCapabilities("Draft", true, hasOnly(Permissions.InventoryIssuePost))).toEqual({
      canCancelIssue: false,
      canPostIssue: true,
      canSaveDraft: false,
    });
  });

  it("does not let post-only users create and post a new unsaved draft", () => {
    expect(getInventoryIssueFormCapabilities("Draft", false, hasOnly(Permissions.InventoryIssuePost))).toEqual({
      canCancelIssue: false,
      canPostIssue: false,
      canSaveDraft: false,
    });
  });

  it("allows post permission to cancel existing posted issues", () => {
    expect(getInventoryIssueFormCapabilities("Posted", true, hasOnly(Permissions.InventoryIssuePost))).toEqual({
      canCancelIssue: true,
      canPostIssue: false,
      canSaveDraft: false,
    });
  });

  it("allows create-only users to save drafts without post or cancel actions", () => {
    expect(getInventoryIssueFormCapabilities("Draft", true, hasOnly(Permissions.InventoryIssueCreate))).toEqual({
      canCancelIssue: false,
      canPostIssue: false,
      canSaveDraft: true,
    });
  });

  it("keeps view-only users read-only", () => {
    expect(getInventoryIssueFormCapabilities("Draft", true, hasOnly(Permissions.InventoryIssueView))).toEqual({
      canCancelIssue: false,
      canPostIssue: false,
      canSaveDraft: false,
    });
  });

  it("validates required fields, duplicate items, and positive quantities", () => {
    const errors = validateInventoryIssueForm({
      issueNo: "",
      issueDate: "",
      warehouseId: "",
      reason: "",
      referenceNo: "",
      requestedBy: "",
      notes: "",
      lines: [
        {
          lineNo: 1,
          itemId: "item-1",
          uomId: "",
          quantity: 0,
          baseQty: 0,
          notes: "",
        },
        {
          lineNo: 2,
          itemId: "item-1",
          uomId: "uom-1",
          quantity: -1,
          baseQty: 0,
          notes: "",
        },
      ],
    });

    expect(errors.issueDate).toEqual(["module.inventoryIssues.validation.dateRequired"]);
    expect(errors.warehouseId).toEqual(["module.inventoryIssues.validation.warehouseRequired"]);
    expect(errors.reason).toEqual(["module.inventoryIssues.validation.reasonRequired"]);
    expect(errors["lines.0.uomId"]).toEqual(["module.inventoryIssues.validation.uomRequired"]);
    expect(errors["lines.0.quantity"]).toEqual(["module.inventoryIssues.validation.quantityRequired"]);
    expect(errors["lines.1.itemId"]).toEqual(["module.inventoryIssues.validation.duplicateItem"]);
    expect(errors["lines.1.quantity"]).toEqual(["module.inventoryIssues.validation.quantityRequired"]);
  });

  it("accepts a valid localized issue payload shape", () => {
    expect(validateInventoryIssueForm({
      issueNo: "",
      issueDate: "2026-07-30",
      warehouseId: "warehouse-1",
      reason: "Damage",
      referenceNo: "",
      requestedBy: "",
      notes: "",
      lines: [
        {
          lineNo: 1,
          itemId: "item-1",
          uomId: "uom-1",
          quantity: 1,
          baseQty: 0,
          notes: "",
        },
      ],
    })).toEqual({});
  });
});

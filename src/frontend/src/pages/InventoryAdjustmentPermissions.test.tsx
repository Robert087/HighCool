import { renderToStaticMarkup } from "react-dom/server";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n";
import { Permissions } from "../services/permissions";
import { getInventoryAdjustmentFormCapabilities } from "./InventoryAdjustmentFormPage";
import { InventoryAdjustmentsPage } from "./InventoryAdjustmentsPage";

const authState = vi.hoisted(() => ({
  permissions: new Set<string>(),
}));

vi.mock("../features/auth/AuthProvider", () => ({
  useAuth: () => ({
    hasPermission: (permission: string) => authState.permissions.has(permission),
  }),
}));

function renderAdjustmentsPage() {
  return renderToStaticMarkup(
    <I18nProvider>
      <MemoryRouter>
        <InventoryAdjustmentsPage />
      </MemoryRouter>
    </I18nProvider>,
  );
}

function hasOnly(...permissions: string[]) {
  return (permission: string) => permissions.includes(permission);
}

describe("Inventory adjustment permissions", () => {
  beforeEach(() => {
    authState.permissions.clear();
  });

  it("hides the create action when create permission is missing", () => {
    const markup = renderAdjustmentsPage();

    expect(markup).not.toContain("New adjustment");
    expect(markup).not.toContain("/inventory-adjustments/new");
  });

  it("shows the create action when create permission is granted", () => {
    authState.permissions.add(Permissions.InventoryAdjustmentCreate);

    const markup = renderAdjustmentsPage();

    expect(markup).toContain("New adjustment");
    expect(markup).toContain("/inventory-adjustments/new");
  });

  it("allows post-only users to post existing drafts without save access", () => {
    expect(getInventoryAdjustmentFormCapabilities("Draft", true, hasOnly(Permissions.InventoryAdjustmentPost))).toEqual({
      canCancelAdjustment: false,
      canPostAdjustment: true,
      canSaveDraft: false,
    });
  });

  it("does not let post-only users create and post a new unsaved draft", () => {
    expect(getInventoryAdjustmentFormCapabilities("Draft", false, hasOnly(Permissions.InventoryAdjustmentPost))).toEqual({
      canCancelAdjustment: false,
      canPostAdjustment: false,
      canSaveDraft: false,
    });
  });

  it("allows post permission to cancel existing posted adjustments", () => {
    expect(getInventoryAdjustmentFormCapabilities("Posted", true, hasOnly(Permissions.InventoryAdjustmentPost))).toEqual({
      canCancelAdjustment: true,
      canPostAdjustment: false,
      canSaveDraft: false,
    });
  });

  it("keeps view-only users read-only", () => {
    expect(getInventoryAdjustmentFormCapabilities("Draft", true, hasOnly(Permissions.InventoryStockLedgerView))).toEqual({
      canCancelAdjustment: false,
      canPostAdjustment: false,
      canSaveDraft: false,
    });
  });
});

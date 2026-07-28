import { renderToStaticMarkup } from "react-dom/server";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n";
import { Permissions } from "../services/permissions";
import { getInventoryTransferFormCapabilities } from "./InventoryTransferFormPage";
import { InventoryTransfersPage } from "./InventoryTransfersPage";

const authState = vi.hoisted(() => ({
  permissions: new Set<string>(),
}));

vi.mock("../features/auth/AuthProvider", () => ({
  useAuth: () => ({
    hasPermission: (permission: string) => authState.permissions.has(permission),
  }),
}));

function renderTransfersPage() {
  return renderToStaticMarkup(
    <I18nProvider>
      <MemoryRouter>
        <InventoryTransfersPage />
      </MemoryRouter>
    </I18nProvider>,
  );
}

function hasOnly(...permissions: string[]) {
  return (permission: string) => permissions.includes(permission);
}

describe("Inventory transfer permissions", () => {
  beforeEach(() => {
    authState.permissions.clear();
  });

  it("hides the create action when create permission is missing", () => {
    const markup = renderTransfersPage();

    expect(markup).not.toContain("New transfer");
    expect(markup).not.toContain("/inventory-transfers/new");
  });

  it("shows the create action when create permission is granted", () => {
    authState.permissions.add(Permissions.InventoryTransferCreate);

    const markup = renderTransfersPage();

    expect(markup).toContain("New transfer");
    expect(markup).toContain("/inventory-transfers/new");
  });

  it("allows post-only users to post existing drafts without save access", () => {
    expect(getInventoryTransferFormCapabilities("Draft", true, hasOnly(Permissions.InventoryTransferPost))).toEqual({
      canCancelTransfer: false,
      canPostTransfer: true,
      canSaveDraft: false,
    });
  });

  it("does not let post-only users create and post a new unsaved draft", () => {
    expect(getInventoryTransferFormCapabilities("Draft", false, hasOnly(Permissions.InventoryTransferPost))).toEqual({
      canCancelTransfer: false,
      canPostTransfer: false,
      canSaveDraft: false,
    });
  });

  it("allows post permission to cancel existing posted transfers", () => {
    expect(getInventoryTransferFormCapabilities("Posted", true, hasOnly(Permissions.InventoryTransferPost))).toEqual({
      canCancelTransfer: true,
      canPostTransfer: false,
      canSaveDraft: false,
    });
  });

  it("keeps view-only users read-only", () => {
    expect(getInventoryTransferFormCapabilities("Draft", true, hasOnly(Permissions.InventoryStockLedgerView))).toEqual({
      canCancelTransfer: false,
      canPostTransfer: false,
      canSaveDraft: false,
    });
  });
});

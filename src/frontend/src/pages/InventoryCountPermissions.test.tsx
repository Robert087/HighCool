import { renderToStaticMarkup } from "react-dom/server";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n";
import { Permissions } from "../services/permissions";
import { getInventoryCountFormCapabilities } from "./InventoryCountFormPage";
import { InventoryCountsPage } from "./InventoryCountsPage";

const authState = vi.hoisted(() => ({
  permissions: new Set<string>(),
}));

vi.mock("../features/auth/AuthProvider", () => ({
  useAuth: () => ({
    hasPermission: (permission: string) => authState.permissions.has(permission),
  }),
}));

function renderCountsPage() {
  return renderToStaticMarkup(
    <I18nProvider>
      <MemoryRouter>
        <InventoryCountsPage />
      </MemoryRouter>
    </I18nProvider>,
  );
}

function hasOnly(...permissions: string[]) {
  return (permission: string) => permissions.includes(permission);
}

describe("Inventory count permissions", () => {
  beforeEach(() => {
    authState.permissions.clear();
  });

  it("hides the create action when create permission is missing", () => {
    const markup = renderCountsPage();

    expect(markup).not.toContain("New count");
    expect(markup).not.toContain("/inventory-counts/new");
  });

  it("shows the create action when create permission is granted", () => {
    authState.permissions.add(Permissions.InventoryCountCreate);

    const markup = renderCountsPage();

    expect(markup).toContain("New count");
    expect(markup).toContain("/inventory-counts/new");
  });

  it("allows post-only users to post existing drafts without save access", () => {
    expect(getInventoryCountFormCapabilities("Draft", true, hasOnly(Permissions.InventoryCountPost))).toEqual({
      canCancelCount: false,
      canPostCount: true,
      canSaveDraft: false,
    });
  });

  it("does not let post-only users create and post a new unsaved draft", () => {
    expect(getInventoryCountFormCapabilities("Draft", false, hasOnly(Permissions.InventoryCountPost))).toEqual({
      canCancelCount: false,
      canPostCount: false,
      canSaveDraft: false,
    });
  });

  it("allows post permission to cancel existing posted counts", () => {
    expect(getInventoryCountFormCapabilities("Posted", true, hasOnly(Permissions.InventoryCountPost))).toEqual({
      canCancelCount: true,
      canPostCount: false,
      canSaveDraft: false,
    });
  });

  it("keeps view-only users read-only", () => {
    expect(getInventoryCountFormCapabilities("Draft", true, hasOnly(Permissions.InventoryCountView))).toEqual({
      canCancelCount: false,
      canPostCount: false,
      canSaveDraft: false,
    });
  });
});

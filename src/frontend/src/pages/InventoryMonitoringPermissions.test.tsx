import { renderToStaticMarkup } from "react-dom/server";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n";
import { Permissions } from "../services/permissions";
import { InventoryReorderSettingsPage, validateReorderSettingsForm } from "./InventoryReorderSettingsPage";

const authState = vi.hoisted(() => ({
  permissions: new Set<string>(),
}));

vi.mock("react-router-dom", async () => {
  const actual = await vi.importActual<typeof import("react-router-dom")>("react-router-dom");
  return {
    ...actual,
    useParams: () => ({ itemId: "item-1" }),
  };
});

vi.mock("../features/auth/AuthProvider", () => ({
  useAuth: () => ({
    hasPermission: (permission: string) => authState.permissions.has(permission),
  }),
}));

vi.mock("../components/ui", async () => {
  const actual = await vi.importActual<typeof import("../components/ui")>("../components/ui");
  return {
    ...actual,
    useToast: () => ({ showToast: vi.fn() }),
  };
});

function renderSettingsPage() {
  return renderToStaticMarkup(
    <I18nProvider>
      <MemoryRouter>
        <InventoryReorderSettingsPage />
      </MemoryRouter>
    </I18nProvider>,
  );
}

describe("Inventory monitoring permissions", () => {
  beforeEach(() => {
    authState.permissions.clear();
  });

  it("keeps view-only reorder settings users read-only", () => {
    authState.permissions.add(Permissions.InventoryMonitorView);

    const markup = renderSettingsPage();

    expect(markup).toContain("Reorder Settings");
    expect(markup).not.toContain("Save settings");
  });

  it("shows save action for monitor managers", () => {
    authState.permissions.add(Permissions.InventoryMonitorManage);

    const markup = renderSettingsPage();

    expect(markup).toContain("Save settings");
  });

  it("validates reorder thresholds and optional values", () => {
    const errors = validateReorderSettingsForm({
      enableMonitoring: true,
      minimumStock: -1,
      reorderPoint: -2,
      maximumStock: 0,
      reorderQuantity: 0,
      safetyStock: -1,
      leadTimeDays: -1,
    });

    expect(errors.minimumStock).toEqual(["module.inventoryMonitoring.validation.minimumStock"]);
    expect(errors.reorderPoint).toEqual(["module.inventoryMonitoring.validation.reorderPoint"]);
    expect(errors.maximumStock).toEqual(["module.inventoryMonitoring.validation.maximumStock"]);
    expect(errors.reorderQuantity).toEqual(["module.inventoryMonitoring.validation.reorderQuantity"]);
    expect(errors.safetyStock).toEqual(["module.inventoryMonitoring.validation.safetyStock"]);
    expect(errors.leadTimeDays).toEqual(["module.inventoryMonitoring.validation.leadTimeDays"]);
  });

  it("accepts valid reorder settings", () => {
    expect(validateReorderSettingsForm({
      enableMonitoring: true,
      minimumStock: 2,
      reorderPoint: 5,
      maximumStock: 20,
      reorderQuantity: 8,
      safetyStock: "",
      leadTimeDays: "",
    })).toEqual({});
  });
});

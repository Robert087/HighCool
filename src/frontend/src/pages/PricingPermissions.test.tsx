import { renderToStaticMarkup } from "react-dom/server";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n";
import { Permissions } from "../services/permissions";
import { derivePriceListCurrency, ItemPriceFormPage, validateItemPriceForm } from "./ItemPriceFormPage";
import { PriceListFormPage, validatePriceListForm } from "./PriceListFormPage";

const authState = vi.hoisted(() => ({
  permissions: new Set<string>(),
}));

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

vi.mock("../services/pricingApi", async () => {
  const actual = await vi.importActual<typeof import("../services/pricingApi")>("../services/pricingApi");
  return {
    ...actual,
    getPricingFilterOptions: vi.fn(async () => ({ priceLists: [], items: [], uoms: [], categories: [], currencies: [] })),
  };
});

function renderPriceListForm(path = "/price-lists/new") {
  return renderToStaticMarkup(
    <I18nProvider>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/price-lists/new" element={<PriceListFormPage />} />
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );
}

function renderItemPriceForm(path = "/item-prices/new") {
  return renderToStaticMarkup(
    <I18nProvider>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/item-prices/new" element={<ItemPriceFormPage />} />
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );
}

describe("pricing permissions and validation", () => {
  beforeEach(() => {
    authState.permissions.clear();
  });

  it("keeps view-only price list users read-only on forms", () => {
    authState.permissions.add(Permissions.PricingPriceListView);

    const markup = renderPriceListForm();

    expect(markup).toContain("Create Price List");
    expect(markup).not.toContain("Create price list</button>");
  });

  it("shows price list save action for managers", () => {
    authState.permissions.add(Permissions.PricingPriceListManage);

    const markup = renderPriceListForm();

    expect(markup).toContain("Create price list</button>");
  });

  it("validates price list defaults and currency", () => {
    expect(validatePriceListForm({
      code: "",
      name: "",
      type: "Selling",
      currency: "EG",
      isDefault: true,
      isActive: false,
      description: "",
    })).toEqual({
      code: ["module.pricing.validation.codeRequired"],
      name: ["module.pricing.validation.nameRequired"],
      currency: ["module.pricing.validation.currency"],
      isDefault: ["module.pricing.validation.defaultActive"],
    });
  });

  it("keeps view-only item price users read-only on forms", () => {
    authState.permissions.add(Permissions.PricingItemPriceView);

    const markup = renderItemPriceForm();

    expect(markup).toContain("Create Item Price");
    expect(markup).not.toContain("Create item price</button>");
  });

  it("validates item price scope, rate, quantity, and dates", () => {
    expect(validateItemPriceForm({
      priceListId: "",
      itemId: "",
      uomId: "",
      currency: "",
      rate: 0,
      minimumQuantity: 0,
      validFrom: "2026-02-10",
      validTo: "2026-02-09",
      isActive: true,
      notes: "",
    })).toEqual({
      priceListId: ["module.pricing.validation.priceListRequired"],
      itemId: ["module.pricing.validation.itemRequired"],
      uomId: ["module.pricing.validation.uomRequired"],
      rate: ["module.pricing.validation.rate"],
      minimumQuantity: ["module.pricing.validation.minimumQuantity"],
      validTo: ["module.pricing.validation.validTo"],
    });
  });

  it("derives item price currency from the selected price list option", () => {
    expect(derivePriceListCurrency([
      { id: "selling", code: "SELL", name: "Selling", currency: "EGP" },
      { id: "buying", code: "BUY", name: "Buying", currency: "USD" },
    ], "buying")).toBe("USD");
  });
});

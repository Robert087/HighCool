import { renderToStaticMarkup } from "react-dom/server";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../i18n";
import { RouteGate } from "./RouteGate";

let authenticated = true;
let authLoading = false;
let permissionAllowed = true;
let featureLoading = false;
let featureAllowed = true;

vi.mock("../features/auth/AuthProvider", () => ({
  useAuth: () => ({
    hasPermission: () => permissionAllowed,
    isAuthenticated: authenticated,
    isLoading: authLoading,
    workspace: { setupCompleted: true },
  }),
}));

vi.mock("../features/auth/FeatureConfigurationProvider", () => ({
  useFeatureConfiguration: () => ({
    hasFeature: () => featureAllowed,
    isLoading: featureLoading,
  }),
}));

function renderGate() {
  return renderToStaticMarkup(
    <I18nProvider>
      <MemoryRouter>
        <RouteGate permission="items.view" feature="inventoryEnabled">
          <div>Inventory page</div>
        </RouteGate>
      </MemoryRouter>
    </I18nProvider>,
  );
}

describe("RouteGate", () => {
  beforeEach(() => {
    authenticated = true;
    authLoading = false;
    permissionAllowed = true;
    featureLoading = false;
    featureAllowed = true;
  });

  it("renders children only when both permission and feature checks pass", () => {
    expect(renderGate()).toContain("Inventory page");
  });

  it("renders permission denied before feature disabled when permission is missing", () => {
    permissionAllowed = false;
    featureAllowed = false;

    const markup = renderGate();

    expect(markup).toContain("Access denied");
    expect(markup).not.toContain("Feature disabled");
  });

  it("renders feature disabled when the feature check fails", () => {
    featureAllowed = false;

    const markup = renderGate();

    expect(markup).toContain("Feature disabled");
    expect(markup).not.toContain("Access denied");
  });

  it("renders nothing while feature configuration is loading", () => {
    featureLoading = true;

    expect(renderGate()).toBe("");
  });
});

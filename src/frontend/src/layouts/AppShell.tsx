import type { PropsWithChildren } from "react";
import { NavLink } from "react-router-dom";
import { StatusPanel } from "../components/StatusPanel";

const navigationItems = [
  { label: "Dashboard", to: "/" },
  { label: "Items", to: "/items" },
  { label: "Components", to: "/item-components" },
  { label: "Item UOM Rules", to: "/item-uom-conversions" },
  { label: "Suppliers", to: "/suppliers" },
  { label: "Warehouses", to: "/warehouses" },
  { label: "UOMs", to: "/uoms" },
];

export function AppShell({ children }: PropsWithChildren) {
  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div>
          <p className="eyebrow">HighCool</p>
          <h1>ERP Foundation</h1>
          <p className="sidebar-copy">
            Modular monolith foundation prepared for backend-led business flows.
          </p>
        </div>

        <nav aria-label="Primary">
          <ul className="nav-list">
            {navigationItems.map((item) => (
              <li key={item.to}>
                <NavLink
                  className={({ isActive }) => `nav-item ${isActive ? "nav-item--active" : ""}`}
                  to={item.to}
                >
                  {item.label}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>

        <StatusPanel />
      </aside>

      <main className="content">{children}</main>
    </div>
  );
}

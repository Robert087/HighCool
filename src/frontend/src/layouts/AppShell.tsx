import { useEffect, useMemo, useState, type PropsWithChildren } from "react";
import { Link, NavLink, useLocation } from "react-router-dom";
import { Badge, Button, PageContainer, useToast } from "../components/ui";

const navigationGroups = [
  {
    id: "main",
    label: "Main",
    items: [
      { label: "Dashboard", to: "/", icon: "dashboard" },
    ],
  },
  {
    id: "purchasing",
    label: "Purchasing",
    items: [
      { label: "Purchase Orders", to: "/purchase-orders", icon: "document" },
      { label: "Purchase Receipts", to: "/purchase-receipts", icon: "receipt" },
    ],
  },
  {
    id: "inventory",
    label: "Inventory",
    items: [
      { label: "Open Shortages", to: "/open-shortages", icon: "alert" },
      { label: "Shortage Resolutions", to: "/shortage-resolutions", icon: "resolve" },
      { label: "Stock Balance", to: "/stock-balances", icon: "balance" },
      { label: "Stock Card", to: "/stock-movements", icon: "ledger" },
    ],
  },
  {
    id: "statements",
    label: "Statements",
    items: [
      { label: "Supplier Statement", to: "/supplier-statements", icon: "statement" },
      { label: "Supplier Payments", to: "/payments", icon: "payment" },
    ],
  },
  {
    id: "master-data",
    label: "Master Data",
    items: [
      { label: "Customers", to: "/customers", icon: "customers" },
      { label: "Items", to: "/items", icon: "items" },
      { label: "UOM Conversions", to: "/uom-conversions", icon: "conversion" },
      { label: "Suppliers", to: "/suppliers", icon: "suppliers" },
      { label: "Warehouses", to: "/warehouses", icon: "warehouse" },
      { label: "UOMs", to: "/uoms", icon: "uom" },
    ],
  },
] as const;

const SIDEBAR_COLLAPSED_KEY = "hc-sidebar-collapsed";
const SIDEBAR_GROUPS_KEY = "hc-sidebar-groups";

type NavigationGroup = (typeof navigationGroups)[number];
type NavigationItem = NavigationGroup["items"][number];
type GroupState = Record<string, boolean>;
type IconName = NavigationItem["icon"];

function SidebarIcon({ name }: { name: IconName }) {
  const commonProps = {
    "aria-hidden": true,
    className: "app-sidebar__nav-icon-svg",
    fill: "none",
    stroke: "currentColor",
    strokeLinecap: "round" as const,
    strokeLinejoin: "round" as const,
    strokeWidth: 1.8,
    viewBox: "0 0 24 24",
  };

  switch (name) {
    case "dashboard":
      return <svg {...commonProps}><path d="M4 5h7v6H4zM13 5h7v4h-7zM13 11h7v8h-7zM4 13h7v6H4z" /></svg>;
    case "document":
      return <svg {...commonProps}><path d="M7 3h7l5 5v13H7z" /><path d="M14 3v5h5" /><path d="M10 13h6M10 17h6" /></svg>;
    case "receipt":
      return <svg {...commonProps}><path d="M8 4h8l3 4v12l-3-2-2 2-2-2-2 2-2-2-3 2V8z" /><path d="M9 10h6M9 14h6" /></svg>;
    case "alert":
      return <svg {...commonProps}><path d="M12 4 4 19h16L12 4z" /><path d="M12 10v4M12 17h.01" /></svg>;
    case "resolve":
      return <svg {...commonProps}><path d="M5 12a7 7 0 0 1 12-4" /><path d="M19 12a7 7 0 0 1-12 4" /><path d="M15 4h2v2M7 18H5v-2" /></svg>;
    case "balance":
      return <svg {...commonProps}><path d="M5 6h14M7 6v12M17 6v12M5 18h14" /><path d="M9.5 11.5h5M9.5 14.5h5" /></svg>;
    case "ledger":
      return <svg {...commonProps}><path d="M6 4h12v16H6z" /><path d="M9 8h6M9 12h6M9 16h4" /></svg>;
    case "statement":
      return <svg {...commonProps}><path d="M7 4h10v16H7z" /><path d="M9.5 8h5M9.5 12h5M9.5 16h5" /></svg>;
    case "payment":
      return <svg {...commonProps}><path d="M4 7h16v10H4z" /><path d="M4 10h16" /><path d="M8 15h3" /></svg>;
    case "customers":
      return <svg {...commonProps}><path d="M7.5 13a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7Z" /><path d="M14.5 14.5a3 3 0 1 0 0-6" /><path d="M3.5 20a5.5 5.5 0 0 1 8 0M13.5 20a4.8 4.8 0 0 1 6 0" /></svg>;
    case "items":
      return <svg {...commonProps}><path d="m12 3 8 4.5v9L12 21l-8-4.5v-9z" /><path d="m12 12 8-4.5M12 12 4 7.5M12 12v9" /></svg>;
    case "conversion":
      return <svg {...commonProps}><path d="M7 7h10M7 7l3-3M7 7l3 3M17 17H7M17 17l-3-3M17 17l-3 3" /></svg>;
    case "suppliers":
      return <svg {...commonProps}><path d="M4 20h16" /><path d="M6 20V8l6-4 6 4v12" /><path d="M9 12h.01M15 12h.01M9 16h.01M15 16h.01" /></svg>;
    case "warehouse":
      return <svg {...commonProps}><path d="M3 10 12 4l9 6v10H3z" /><path d="M8 14h8M8 18h8" /></svg>;
    case "uom":
      return <svg {...commonProps}><path d="M6 7h12M6 12h8M6 17h12" /><path d="M18 5v14" /></svg>;
    default:
      return null;
  }
}

function ChevronIcon({ expanded }: { expanded: boolean }) {
  return (
    <svg aria-hidden="true" className={`app-sidebar__chevron ${expanded ? "app-sidebar__chevron--expanded" : ""}`} fill="none" stroke="currentColor" strokeLinecap="round" strokeLinejoin="round" strokeWidth="1.8" viewBox="0 0 24 24">
      <path d="m9 6 6 6-6 6" />
    </svg>
  );
}

function SidebarToggleIcon({ collapsed }: { collapsed: boolean }) {
  return (
    <svg aria-hidden="true" className={`app-sidebar__toggle-icon ${collapsed ? "app-sidebar__toggle-icon--collapsed" : ""}`} fill="none" stroke="currentColor" strokeLinecap="round" strokeLinejoin="round" strokeWidth="1.8" viewBox="0 0 24 24">
      <path d="m15 6-6 6 6 6" />
    </svg>
  );
}

function readStoredBoolean(key: string, fallback: boolean) {
  if (typeof window === "undefined") {
    return fallback;
  }

  const value = window.localStorage.getItem(key);
  if (value == null) {
    return fallback;
  }

  return value === "true";
}

function getGroupForPath(pathname: string) {
  if (pathname.startsWith("/purchase-orders") || pathname.startsWith("/purchase-receipts")) {
    return "purchasing";
  }

  if (pathname.startsWith("/open-shortages") || pathname.startsWith("/shortage-resolutions") || pathname.startsWith("/stock-balances") || pathname.startsWith("/stock-movements")) {
    return "inventory";
  }

  if (pathname.startsWith("/supplier-statements")) {
    return "statements";
  }

  if (pathname.startsWith("/payments")) {
    return "statements";
  }

  if (pathname.startsWith("/customers") || pathname.startsWith("/items") || pathname.startsWith("/uom-conversions") || pathname.startsWith("/suppliers") || pathname.startsWith("/warehouses") || pathname.startsWith("/uoms")) {
    return "master-data";
  }

  return "main";
}

function getDefaultGroupState(activeGroupId: string): GroupState {
  return Object.fromEntries(
    navigationGroups.map((group) => [group.id, group.id === "main" || group.id === activeGroupId]),
  );
}

function readStoredGroupState(activeGroupId: string): GroupState {
  if (typeof window === "undefined") {
    return getDefaultGroupState(activeGroupId);
  }

  const rawValue = window.localStorage.getItem(SIDEBAR_GROUPS_KEY);
  if (!rawValue) {
    return getDefaultGroupState(activeGroupId);
  }

  try {
    const parsed = JSON.parse(rawValue) as Record<string, boolean>;
    return navigationGroups.reduce<GroupState>((state, group) => {
      state[group.id] = parsed[group.id] ?? (group.id === "main" || group.id === activeGroupId);
      return state;
    }, {} as GroupState);
  } catch {
    return getDefaultGroupState(activeGroupId);
  }
}

type RouteMeta = {
  section: string;
  eyebrow: string;
  title: string;
  subtitle: string;
};

function getRouteMeta(pathname: string): RouteMeta {
  if (pathname.startsWith("/items")) {
    return {
      section: "Master Data",
      eyebrow: pathname.includes("/new") || pathname.includes("/edit") ? "Item editor" : "Catalog",
      title: pathname.includes("/new") ? "Create item" : pathname.includes("/edit") ? "Edit item" : "Items",
      subtitle: "Search, review, and maintain core item records.",
    };
  }

  if (pathname.startsWith("/customers")) {
    return {
      section: "Master Data",
      eyebrow: pathname.includes("/new") || pathname.includes("/edit") ? "Customer editor" : "Directory",
      title: pathname.includes("/new") ? "Create customer" : pathname.includes("/edit") ? "Edit customer" : "Customers",
      subtitle: "Keep customer identities, contact details, and commercial terms organized.",
    };
  }

  if (pathname.startsWith("/purchase-orders")) {
    return {
      section: "Purchasing",
      eyebrow: pathname.includes("/new") || pathname.includes("/edit") ? "Order editor" : "Purchase orders",
      title: pathname.includes("/new") ? "Create purchase order" : pathname.includes("/edit") ? "Edit purchase order" : "Purchase orders",
      subtitle: "Define expected supplier quantities before receipt posting and shortage capture.",
    };
  }

  if (pathname.startsWith("/purchase-receipts")) {
    return {
      section: "Purchasing",
      eyebrow: pathname.includes("/new") || pathname.includes("/edit") ? "Receipt editor" : "Purchase receipts",
      title: pathname.includes("/new") ? "Create purchase receipt" : pathname.includes("/edit") ? "Edit purchase receipt" : "Purchase receipts",
      subtitle: "Capture actual deliveries, linked purchase order context, and component shortages with full traceability.",
    };
  }

  if (pathname.startsWith("/stock-balances")) {
    return {
      section: "Inventory",
      eyebrow: "Stock balance",
      title: "Stock balance",
      subtitle: "View warehouse balances derived from append-only stock ledger entries only.",
    };
  }

  if (pathname.startsWith("/open-shortages")) {
    return {
      section: "Inventory",
      eyebrow: "Shortage control",
      title: "Open shortages",
      subtitle: "Track unresolved receipt shortages, supplier accountability, and the remaining quantity or value still open.",
    };
  }

  if (pathname.startsWith("/shortage-resolutions")) {
    return {
      section: "Inventory",
      eyebrow: pathname.includes("/new") || pathname.includes("/edit") ? "Resolution editor" : "Shortage resolutions",
      title: pathname.includes("/new") ? "Create shortage resolution" : pathname.includes("/edit") ? "Edit shortage resolution" : "Shortage resolutions",
      subtitle: "Resolve shortage rows physically or financially with full allocation traceability back to the original receipt shortage.",
    };
  }

  if (pathname.startsWith("/stock-movements")) {
    return {
      section: "Inventory",
      eyebrow: "Stock card",
      title: "Stock card",
      subtitle: "Trace transaction history, source document references, and running balances per item and warehouse.",
    };
  }

  if (pathname.startsWith("/uom-conversions")) {
    return {
      section: "Master Data",
      eyebrow: pathname.includes("/new") || pathname.includes("/edit") ? "Conversion editor" : "Measurement rules",
      title: pathname.includes("/new") ? "Create conversion" : pathname.includes("/edit") ? "Edit conversion" : "UOM conversions",
      subtitle: "Keep global conversion logic clear and traceable.",
    };
  }

  if (pathname.startsWith("/suppliers")) {
    return {
      section: "Master Data",
      eyebrow: pathname.includes("/new") || pathname.includes("/edit") ? "Supplier editor" : "Directory",
      title: pathname.includes("/new") ? "Create supplier" : pathname.includes("/edit") ? "Edit supplier" : "Suppliers",
      subtitle: "Keep supplier identities and contacts organized.",
    };
  }

  if (pathname.startsWith("/supplier-statements")) {
    return {
      section: "Statements",
      eyebrow: "Supplier statement",
      title: "Supplier statement",
      subtitle: "Review supplier balances that are derived only from posted procurement and shortage-financial-resolution documents.",
    };
  }

  if (pathname.startsWith("/payments")) {
    const isEditorRoute = pathname.endsWith("/new") || pathname.endsWith("/edit") || /^\/payments\/[^/]+$/.test(pathname);

    return {
      section: "Statements",
      eyebrow: isEditorRoute ? "Payment editor" : "Supplier payments",
      title: pathname.endsWith("/new") ? "Create supplier payment" : isEditorRoute ? "Supplier payment" : "Supplier payments",
      subtitle: "Settle supplier-side open balances through mandatory, traceable payment allocations.",
    };
  }

  if (pathname.startsWith("/warehouses")) {
    return {
      section: "Master Data",
      eyebrow: pathname.includes("/new") || pathname.includes("/edit") ? "Warehouse editor" : "Locations",
      title: pathname.includes("/new") ? "Create warehouse" : pathname.includes("/edit") ? "Edit warehouse" : "Warehouses",
      subtitle: "Manage warehouse records and location references.",
    };
  }

  if (pathname.startsWith("/uoms")) {
    return {
      section: "Master Data",
      eyebrow: pathname.includes("/new") || pathname.includes("/edit") ? "UOM editor" : "Measurement",
      title: pathname.includes("/new") ? "Create UOM" : pathname.includes("/edit") ? "Edit UOM" : "Units of measure",
      subtitle: "Maintain the shared measurement catalog.",
    };
  }

  if (pathname === "/" || pathname === "/home") {
    return {
      section: "Workspace",
      eyebrow: "Today",
      title: "Dashboard",
      subtitle: "A cleaner workspace for setup, review, and next actions.",
    };
  }

  return {
    section: "Workspace",
    eyebrow: "Workspace",
    title: "HighCool ERP",
    subtitle: "Move through the workspace with fewer clicks and less noise.",
  };
}

function greetingForHour(hour: number) {
  if (hour < 12) {
    return "Good morning";
  }

  if (hour < 18) {
    return "Good afternoon";
  }

  return "Good evening";
}

export function AppShell({ children }: PropsWithChildren) {
  const location = useLocation();
  const { showToast } = useToast();
  const activeGroupId = getGroupForPath(location.pathname);
  const [sidebarCollapsed, setSidebarCollapsed] = useState(() => readStoredBoolean(SIDEBAR_COLLAPSED_KEY, false));
  const [sidebarGroups, setSidebarGroups] = useState<GroupState>(() => readStoredGroupState(activeGroupId));
  const routeMeta = getRouteMeta(location.pathname);
  const isDashboard = location.pathname === "/" || location.pathname === "/home";
  const greeting = `${greetingForHour(new Date().getHours())}, Robert`;
  const [isOnline, setIsOnline] = useState(() => navigator.onLine);

  useEffect(() => {
    window.localStorage.setItem(SIDEBAR_COLLAPSED_KEY, String(sidebarCollapsed));
  }, [sidebarCollapsed]);

  useEffect(() => {
    window.localStorage.setItem(SIDEBAR_GROUPS_KEY, JSON.stringify(sidebarGroups));
  }, [sidebarGroups]);

  useEffect(() => {
    setSidebarGroups((current) => ({
      ...current,
      main: true,
      [activeGroupId]: true,
    }));
  }, [activeGroupId]);

  useEffect(() => {
    const handleOnline = () => setIsOnline(true);
    const handleOffline = () => setIsOnline(false);

    window.addEventListener("online", handleOnline);
    window.addEventListener("offline", handleOffline);

    return () => {
      window.removeEventListener("online", handleOnline);
      window.removeEventListener("offline", handleOffline);
    };
  }, []);

  function handleAiAssist() {
    showToast({
      tone: "info",
      title: "Ask AI",
      description: "Use AI for quick help with missing setup, open shortages, and the next operational step.",
    });
  }

  function toggleGroup(groupId: string) {
    setSidebarGroups((current) => ({
      ...current,
      [groupId]: !current[groupId],
    }));
  }

  const workspaceStatus = useMemo(() => ({
    connectionLabel: isOnline ? "Connected" : "Offline",
    draftLabel: "0 drafts pending",
  }), [isOnline]);

  return (
    <div className={`app-shell ${sidebarCollapsed ? "app-shell--collapsed" : ""}`}>
      <aside className={`app-sidebar ${sidebarCollapsed ? "app-sidebar--collapsed" : ""}`}>
        <div className="app-sidebar__top">
          <div className="app-sidebar__brand">
            <div className="app-sidebar__brand-mark">HC</div>
            <div className="app-sidebar__brand-copy">
              <h1 className="app-sidebar__title">HighCool ERP</h1>
            </div>
          </div>

          <Button
            aria-label={sidebarCollapsed ? "Expand sidebar" : "Collapse sidebar"}
            className="app-sidebar__toggle"
            size="sm"
            variant="ghost"
            onClick={() => setSidebarCollapsed((current) => !current)}
          >
            <SidebarToggleIcon collapsed={sidebarCollapsed} />
          </Button>
        </div>

        <nav aria-label="Primary" className="app-sidebar__nav">
          {navigationGroups.map((group) => (
            <section key={group.label} className="app-sidebar__group">
              {!sidebarCollapsed ? (
                <button
                  aria-controls={`sidebar-group-${group.id}`}
                  aria-expanded={sidebarGroups[group.id]}
                  className="app-sidebar__group-toggle"
                  type="button"
                  onClick={() => toggleGroup(group.id)}
                >
                  <span className="app-sidebar__group-label">{group.label}</span>
                  <ChevronIcon expanded={sidebarGroups[group.id]} />
                </button>
              ) : (
                <div aria-hidden="true" className="app-sidebar__group-label app-sidebar__group-label--cluster" />
              )}

              <div
                id={`sidebar-group-${group.id}`}
                className={`app-sidebar__group-body ${sidebarCollapsed || sidebarGroups[group.id] ? "app-sidebar__group-body--expanded" : ""}`}
              >
                <ul className="app-sidebar__nav-list">
                  {group.items.map((item) => (
                    <li key={item.to}>
                      <NavLink
                        aria-label={item.label}
                        className={({ isActive }) =>
                          `app-sidebar__nav-item ${isActive ? "app-sidebar__nav-item--active" : ""}`
                        }
                        title={sidebarCollapsed ? item.label : undefined}
                        to={item.to}
                      >
                        <span className="app-sidebar__nav-icon" aria-hidden="true">
                          <SidebarIcon name={item.icon} />
                        </span>
                        <span className="app-sidebar__nav-label">{item.label}</span>
                      </NavLink>
                    </li>
                  ))}
                </ul>
              </div>
            </section>
          ))}
        </nav>

        <div className="app-sidebar__footer">
          <div className="app-sidebar__status" title={sidebarCollapsed ? `${workspaceStatus.connectionLabel} · ${workspaceStatus.draftLabel}` : undefined}>
            <div className="app-sidebar__status-header">
              <span className={`status-dot ${isOnline ? "online" : "offline"}`} />
              <span className="app-sidebar__status-title">Workspace status</span>
            </div>
            <div className="app-sidebar__status-copy">
              <span className="app-sidebar__status-line">{workspaceStatus.connectionLabel}</span>
              <span className="app-sidebar__status-line">{workspaceStatus.draftLabel}</span>
            </div>
          </div>
        </div>
      </aside>

      <div className="app-main">
        {isDashboard ? (
          <header className="app-topbar">
            <PageContainer width="wide" className="app-topbar__container">
              <div className="app-topbar__utility">
                <div className="app-topbar__breadcrumbs" aria-label="Section context">
                  <span>HighCool ERP</span>
                  <span className="app-topbar__separator">/</span>
                  <span>Dashboard</span>
                </div>

                <div className="app-topbar__utility-actions">
                  <Badge tone="primary">Offline drafts only</Badge>
                  <Button size="sm" variant="ghost" onClick={handleAiAssist}>
                    Ask AI
                  </Button>
                </div>
              </div>

              <div className="app-topbar__welcome">
                <div className="app-topbar__headline">
                  <h2 className="app-topbar__title">{greeting}</h2>
                  <p className="app-topbar__description">Here&apos;s what needs your attention today.</p>
                </div>

                <div className="app-topbar__actions app-topbar__actions--compact">
                  <div className="app-topbar__quick-actions app-topbar__quick-actions--compact">
                    <Link className="hc-button hc-button--ghost hc-button--sm" to="/items">
                      Review items
                    </Link>
                    <Link className="hc-button hc-button--ghost hc-button--sm" to="/suppliers">
                      Review suppliers
                    </Link>
                    <Link className="hc-button hc-button--ghost hc-button--sm" to="/open-shortages">
                      Open shortages
                    </Link>
                  </div>
                </div>
              </div>
            </PageContainer>
          </header>
        ) : null}

        <main className="app-main__content">
          <PageContainer width="wide" className="app-main__page">
            {children}
          </PageContainer>
        </main>
      </div>
    </div>
  );
}

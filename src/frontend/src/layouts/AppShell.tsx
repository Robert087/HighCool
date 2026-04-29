import { useEffect, useMemo, useRef, useState, type PropsWithChildren } from "react";
import { Link, NavLink, useLocation, useNavigate } from "react-router-dom";
import { Badge, Button, PageContainer, useToast } from "../components/ui";
import { useAuth } from "../features/auth/AuthProvider";
import { useFeatureConfiguration } from "../features/auth/FeatureConfigurationProvider";
import { useI18n } from "../i18n";
import { Permissions } from "../services/permissions";
import { DISABLE_FEATURE_GATING } from "../config/temporaryFlags";

const navigationGroups = [
  {
    id: "workspace",
    label: "nav.workspace",
    items: [
      { label: "nav.dashboard", to: "/workspace", icon: "dashboard" },
    ],
  },
  {
    id: "procurement",
    label: "nav.procurement",
    items: [
      { label: "nav.purchaseOrders", to: "/procurement/purchase-orders", icon: "document" },
      { label: "nav.purchaseReceipts", to: "/procurement/purchase-receipts", icon: "receipt" },
      { label: "nav.purchaseReturns", to: "/procurement/purchase-returns", icon: "document" },
    ],
  },
  {
    id: "inventory",
    label: "nav.inventory",
    items: [
      { label: "nav.stockCard", to: "/inventory/stock-ledger", icon: "ledger" },
      { label: "nav.stockBalance", to: "/stock-balances", icon: "balance" },
      { label: "nav.items", to: "/items", icon: "items" },
      { label: "nav.warehouses", to: "/inventory/warehouses", icon: "warehouse" },
      { label: "nav.uoms", to: "/uoms", icon: "uom" },
      { label: "nav.uomConversions", to: "/uom-conversions", icon: "conversion" },
    ],
  },
  {
    id: "shortages",
    label: "nav.shortageResolutions",
    items: [
      { label: "app.openShortages", to: "/open-shortages", icon: "alert" },
      { label: "nav.shortageResolutions", to: "/shortage-resolutions", icon: "resolve" },
    ],
  },
  {
    id: "suppliers",
    label: "nav.suppliersModule",
    items: [
      { label: "nav.suppliers", to: "/suppliers", icon: "suppliers" },
      { label: "nav.customers", to: "/customers", icon: "customers" },
    ],
  },
  {
    id: "supplier-financials",
    label: "nav.supplierFinancials",
    items: [
      { label: "nav.supplierStatement", to: "/supplier-financials", icon: "statement", exact: true },
      { label: "nav.supplierPayments", to: "/supplier-financials/payments", icon: "payment" },
    ],
  },
  {
    id: "settings",
    label: "nav.settings",
    items: [
      { label: "settings.nav.users", to: "/settings/users", icon: "settings" },
      { label: "settings.nav.roles", to: "/settings/roles", icon: "settings" },
    ],
  },
] as const;

const SIDEBAR_COLLAPSED_KEY = "hc-sidebar-collapsed";
const SIDEBAR_GROUPS_KEY = "hc-sidebar-groups";
const THEME_STORAGE_KEY = "hc-theme";

type NavigationGroup = (typeof navigationGroups)[number];
type NavigationItem = NavigationGroup["items"][number];
type GroupState = Record<string, boolean>;
type IconName = "dashboard" | "document" | "receipt" | "alert" | "resolve" | "balance" | "ledger" | "statement" | "payment" | "customers" | "items" | "conversion" | "suppliers" | "warehouse" | "uom" | "settings";
type MenuState = "notifications" | "user" | null;
type SearchResult = {
  id: string;
  categoryKey: string;
  labelKey: string;
  keywords: string[];
  to: string;
};
type NotificationItem = {
  id: string;
  detailKey: string;
  titleKey: string;
  tone: "danger" | "warning" | "primary";
  to: string;
};

const searchIndex: SearchResult[] = [
  ...navigationGroups.flatMap((group) =>
    group.items.map((item) => ({
      id: item.to,
      categoryKey: group.label,
      labelKey: item.label,
      keywords: [item.to, item.icon, group.id],
      to: item.to,
    })),
  ),
  {
    id: "global-search-po",
    categoryKey: "appBar.search.category.documents",
    labelKey: "appBar.search.result.purchaseOrders",
    keywords: ["po", "purchase order", "orders", "transactions", "documents"],
    to: "/purchase-orders",
  },
  {
    id: "global-search-grn",
    categoryKey: "appBar.search.category.documents",
    labelKey: "appBar.search.result.goodsReceipts",
    keywords: ["grn", "goods receipt", "receipts", "documents", "warehouse"],
    to: "/purchase-receipts",
  },
  {
    id: "global-search-items",
    categoryKey: "appBar.search.category.records",
    labelKey: "appBar.search.result.items",
    keywords: ["items", "catalog", "sku", "inventory"],
    to: "/items",
  },
  {
    id: "global-search-suppliers",
    categoryKey: "appBar.search.category.records",
    labelKey: "appBar.search.result.suppliers",
    keywords: ["suppliers", "vendors", "contacts", "partners"],
    to: "/suppliers",
  },
  {
    id: "global-search-customers",
    categoryKey: "appBar.search.category.records",
    labelKey: "appBar.search.result.customers",
    keywords: ["customers", "accounts", "contacts"],
    to: "/customers",
  },
  {
    id: "global-search-stock",
    categoryKey: "appBar.search.category.reports",
    labelKey: "appBar.search.result.stockBalance",
    keywords: ["reports", "balances", "stock", "inventory", "summary"],
    to: "/stock-balances",
  },
  {
    id: "global-search-statements",
    categoryKey: "appBar.search.category.reports",
    labelKey: "appBar.search.result.supplierStatement",
    keywords: ["reports", "statement", "supplier", "finance"],
    to: "/supplier-statements",
  },
];

const enterpriseNotifications: NotificationItem[] = [
  {
    id: "approvals",
    titleKey: "appBar.notifications.approvals.title",
    detailKey: "appBar.notifications.approvals.detail",
    tone: "primary",
    to: "/purchase-orders",
  },
  {
    id: "posting-errors",
    titleKey: "appBar.notifications.errors.title",
    detailKey: "appBar.notifications.errors.detail",
    tone: "danger",
    to: "/purchase-receipts",
  },
  {
    id: "shortages",
    titleKey: "appBar.notifications.shortages.title",
    detailKey: "appBar.notifications.shortages.detail",
    tone: "warning",
    to: "/open-shortages",
  },
];

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
    case "settings":
      return <svg {...commonProps}><path d="m12 3 1.6 2.6 3 .5-2.1 2.1.5 3-3-1.6-3 1.6.5-3-2.1-2.1 3-.5L12 3z" /><circle cx="12" cy="12" r="3.1" /></svg>;
    default:
      return null;
  }
}

function AppBarIcon({ name }: { name: "search" | "spark" | "bell" | "globe" | "sun" | "moon" | "chevron" | "profile" | "building" | "settings" | "logout" }) {
  const commonProps = {
    "aria-hidden": true,
    className: "app-globalbar__icon-svg",
    fill: "none",
    stroke: "currentColor",
    strokeLinecap: "round" as const,
    strokeLinejoin: "round" as const,
    strokeWidth: 1.8,
    viewBox: "0 0 24 24",
  };

  switch (name) {
    case "search":
      return <svg {...commonProps}><path d="m21 21-4.35-4.35" /><circle cx="11" cy="11" r="6.5" /></svg>;
    case "spark":
      return <svg {...commonProps}><path d="m12 3 1.3 3.7L17 8l-3.7 1.3L12 13l-1.3-3.7L7 8l3.7-1.3L12 3z" /><path d="m18.5 14.5.7 2 .8.3-.8.3-.7 2-.7-2-.8-.3.8-.3.7-2zM6 15l.9 2.5L9.5 18l-2.6.5L6 21l-.9-2.5L2.5 18l2.6-.5L6 15z" /></svg>;
    case "bell":
      return <svg {...commonProps}><path d="M15 17H5.8A1.8 1.8 0 0 1 4 15.2c0-.4.1-.8.3-1.1L6 11.3V9a6 6 0 1 1 12 0v2.3l1.7 2.8c.2.3.3.7.3 1.1a1.8 1.8 0 0 1-1.8 1.8H15" /><path d="M9.5 17a2.5 2.5 0 0 0 5 0" /></svg>;
    case "globe":
      return <svg {...commonProps}><circle cx="12" cy="12" r="9" /><path d="M3 12h18M12 3a14.6 14.6 0 0 1 0 18M12 3a14.6 14.6 0 0 0 0 18" /></svg>;
    case "sun":
      return <svg {...commonProps}><circle cx="12" cy="12" r="3.5" /><path d="M12 2.5v2.2M12 19.3v2.2M4.7 4.7l1.6 1.6M17.7 17.7l1.6 1.6M2.5 12h2.2M19.3 12h2.2M4.7 19.3l1.6-1.6M17.7 6.3l1.6-1.6" /></svg>;
    case "moon":
      return <svg {...commonProps}><path d="M20 14.2A7.8 7.8 0 1 1 9.8 4 6.3 6.3 0 0 0 20 14.2z" /></svg>;
    case "chevron":
      return <svg {...commonProps}><path d="m9 6 6 6-6 6" /></svg>;
    case "profile":
      return <svg {...commonProps}><path d="M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8Z" /><path d="M4 20a8 8 0 0 1 16 0" /></svg>;
    case "building":
      return <svg {...commonProps}><path d="M4 20h16" /><path d="M6 20V6h12v14" /><path d="M9 10h.01M15 10h.01M9 14h.01M15 14h.01" /></svg>;
    case "settings":
      return <svg {...commonProps}><path d="m12 3 1.2 2.4 2.7.4-2 1.9.5 2.7-2.4-1.3-2.4 1.3.5-2.7-2-1.9 2.7-.4L12 3z" /><circle cx="12" cy="12" r="3.4" /><path d="m4.5 14.5 2.2.3M17.3 14.8l2.2-.3M6.8 9.2 5.2 7.6M18.8 7.6l-1.6 1.6M12 18.8V21" /></svg>;
    case "logout":
      return <svg {...commonProps}><path d="M10 17 15 12 10 7" /><path d="M15 12H4" /><path d="M13 4h4a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-4" /></svg>;
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

function readStoredTheme() {
  if (typeof window === "undefined") {
    return "light" as const;
  }

  const value = window.localStorage.getItem(THEME_STORAGE_KEY);
  return value === "dark" ? "dark" as const : "light" as const;
}

function getGroupForPath(pathname: string) {
  if (pathname.startsWith("/workspace") || pathname.startsWith("/dashboard") || pathname === "/" || pathname === "/home") {
    return "workspace";
  }

  if (pathname.startsWith("/purchase-orders") || pathname.startsWith("/purchase-receipts") || pathname.startsWith("/purchase-returns")) {
    return "procurement";
  }
  if (pathname.startsWith("/procurement/")) {
    return "procurement";
  }

  if (pathname.startsWith("/open-shortages") || pathname.startsWith("/shortage-resolutions")) {
    return "shortages";
  }
  if (pathname.startsWith("/stock-balances") || pathname.startsWith("/stock-movements") || pathname.startsWith("/items") || pathname.startsWith("/warehouses") || pathname.startsWith("/uoms") || pathname.startsWith("/uom-conversions")) {
    return "inventory";
  }
  if (pathname.startsWith("/inventory/")) {
    return "inventory";
  }

  if (pathname.startsWith("/suppliers") || pathname.startsWith("/customers")) {
    return "suppliers";
  }

  if (pathname.startsWith("/supplier-statements") || pathname.startsWith("/payments")) {
    return "supplier-financials";
  }
  if (pathname.startsWith("/supplier-financials")) {
    return "supplier-financials";
  }

  if (pathname.startsWith("/settings")) {
    return "settings";
  }

  return "workspace";
}

function getDefaultGroupState(activeGroupId: string): GroupState {
  return Object.fromEntries(
    navigationGroups.map((group) => [group.id, group.id === "workspace" || group.id === activeGroupId]),
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
      state[group.id] = parsed[group.id] ?? (group.id === "workspace" || group.id === activeGroupId);
      return state;
    }, {} as GroupState);
  } catch {
    return getDefaultGroupState(activeGroupId);
  }
}

function getInitials(value: string) {
  return value
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? "")
    .join("");
}

function greetingForHour(hour: number) {
  if (hour < 12) {
    return "app.goodMorning";
  }

  if (hour < 18) {
    return "app.goodAfternoon";
  }

  return "app.goodEvening";
}

export function AppShell({ children }: PropsWithChildren) {
  const location = useLocation();
  const navigate = useNavigate();
  const { showToast } = useToast();
  const { direction, locale, setLocale, t } = useI18n();
  const { hasPermission, isAuthenticated, logout, switchOrganization, workspace } = useAuth();
  const { features } = useFeatureConfiguration();
  const isSetupRoute = location.pathname.startsWith("/setup/organization");
  const activeGroupId = getGroupForPath(location.pathname);
  const filteredGroups = useMemo(() => navigationGroups
    .map((group) => ({
      ...group,
      items: group.items.filter((item) => {
        // TEMPORARILY_DISABLED: Feature gating bypassed until all module keys/routes are aligned.
        // Restore legacy behavior by showing all operational modules in sidebar.
        if (DISABLE_FEATURE_GATING) {
          return true;
        }

        const permission = navigationPermissions[item.to];
        return !permission || hasPermission(permission);
      }),
    }))
    .filter((group) => {
      if (group.items.length === 0) {
        return false;
      }

      if (DISABLE_FEATURE_GATING) {
        return true;
      }

      if (!workspace?.setupCompleted || !features) {
        return group.id === "workspace";
      }

      return featureGroupVisibility[group.id as keyof typeof featureGroupVisibility](features);
    }), [features, hasPermission, workspace?.setupCompleted]);
  const activeGroup = filteredGroups.find((group) => group.id === activeGroupId) ?? filteredGroups[0];
  const [sidebarCollapsed, setSidebarCollapsed] = useState(() => readStoredBoolean(SIDEBAR_COLLAPSED_KEY, false));
  const [sidebarGroups, setSidebarGroups] = useState<GroupState>(() => readStoredGroupState(activeGroupId));
  const [theme, setTheme] = useState<"light" | "dark">(() => readStoredTheme());
  const [isOnline, setIsOnline] = useState(() => navigator.onLine);
  const [searchQuery, setSearchQuery] = useState("");
  const [isSearchOpen, setIsSearchOpen] = useState(false);
  const [openMenu, setOpenMenu] = useState<MenuState>(null);
  const appBarRef = useRef<HTMLElement | null>(null);
  const searchInputRef = useRef<HTMLInputElement | null>(null);
  const userName = workspace?.fullName ?? "Workspace";
  const notificationCount = enterpriseNotifications.length;
  const isDraftMode = /\/new$|\/edit$/.test(location.pathname);
  const isDashboard = location.pathname === "/workspace" || location.pathname === "/dashboard" || location.pathname === "/" || location.pathname === "/home";
  const aiActive = isOnline;

  useEffect(() => {
    window.localStorage.setItem(SIDEBAR_COLLAPSED_KEY, String(sidebarCollapsed));
  }, [sidebarCollapsed]);

  useEffect(() => {
    window.localStorage.setItem(SIDEBAR_GROUPS_KEY, JSON.stringify(sidebarGroups));
  }, [sidebarGroups]);

  useEffect(() => {
    window.localStorage.setItem(THEME_STORAGE_KEY, theme);
    document.documentElement.dataset.theme = theme;
    document.body.dataset.theme = theme;
  }, [theme]);

  useEffect(() => {
    if (DISABLE_FEATURE_GATING) {
      setSidebarGroups(Object.fromEntries(navigationGroups.map((group) => [group.id, true])) as GroupState);
      return;
    }

    setSidebarGroups((current) => ({
      ...current,
      workspace: true,
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

  useEffect(() => {
    const handlePointerDown = (event: MouseEvent) => {
      if (appBarRef.current && !appBarRef.current.contains(event.target as Node)) {
        setOpenMenu(null);
        setIsSearchOpen(false);
      }
    };

    document.addEventListener("mousedown", handlePointerDown);
    return () => document.removeEventListener("mousedown", handlePointerDown);
  }, []);

  useEffect(() => {
    const handleShortcut = (event: KeyboardEvent) => {
      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        searchInputRef.current?.focus();
        setIsSearchOpen(true);
      }
    };

    window.addEventListener("keydown", handleShortcut);
    return () => window.removeEventListener("keydown", handleShortcut);
  }, []);

  useEffect(() => {
    setOpenMenu(null);
    setIsSearchOpen(false);
  }, [location.pathname]);

  function toggleTheme() {
    setTheme((current) => (current === "light" ? "dark" : "light"));
  }

  function toggleGroup(groupId: string) {
    setSidebarGroups((current) => ({
      ...current,
      [groupId]: !current[groupId],
    }));
  }

  function handleDeferredAction(messageKey: string) {
    showToast({
      tone: "info",
      title: t("appBar.comingSoon.title"),
      description: t(messageKey),
    });
    setOpenMenu(null);
  }

  const searchResults = useMemo(() => {
    const normalizedQuery = searchQuery.trim().toLowerCase();
    const results = normalizedQuery.length === 0
      ? searchIndex.slice(0, 6)
      : searchIndex.filter((entry) =>
        [entry.id, ...entry.keywords, t(entry.labelKey), t(entry.categoryKey)].join(" ").toLowerCase().includes(normalizedQuery),
      );

    return results.slice(0, 7);
  }, [searchQuery, t]);

  const workspaceStatus = useMemo(() => ({
    connectionLabel: isOnline ? t("app.connected") : t("app.offline"),
    draftLabel: t("app.draftsPending", { count: 0 }),
  }), [isOnline, t]);
  const greeting = `${t(greetingForHour(new Date().getHours()))}, ${userName}`;

  if (!isAuthenticated) {
    return <>{children}</>;
  }

  if (isSetupRoute) {
    return (
      <div className="app-shell app-shell--setup" dir={direction}>
        <main className="app-main__content">
          <PageContainer width="wide" className="app-main__page app-main__page--setup">
            {children}
          </PageContainer>
        </main>
      </div>
    );
  }

  return (
    <div className={`app-shell ${sidebarCollapsed ? "app-shell--collapsed" : ""}`} dir={direction}>
      <aside className={`app-sidebar ${sidebarCollapsed ? "app-sidebar--collapsed" : ""}`}>
        <div className="app-sidebar__top">
          <div className="app-sidebar__brand">
            <div className="app-sidebar__brand-mark">HC</div>
            <div className="app-sidebar__brand-copy">
              <h1 className="app-sidebar__title">{t("app.productName")}</h1>
            </div>
          </div>

          <Button
            aria-label={sidebarCollapsed ? t("app.expandSidebar") : t("app.collapseSidebar")}
            className="app-sidebar__toggle"
            size="sm"
            variant="ghost"
            onClick={() => setSidebarCollapsed((current) => !current)}
          >
            <SidebarToggleIcon collapsed={sidebarCollapsed} />
          </Button>
        </div>

        <nav aria-label={t("app.primaryNavigation")} className="app-sidebar__nav">
          {filteredGroups.map((group) => (
            <section key={group.id} className="app-sidebar__group">
              {!sidebarCollapsed ? (
                <button
                  aria-controls={`sidebar-group-${group.id}`}
                  aria-expanded={sidebarGroups[group.id]}
                  className="app-sidebar__group-toggle"
                  type="button"
                  onClick={() => toggleGroup(group.id)}
                >
                  <span className="app-sidebar__group-label">{t(group.label)}</span>
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
                        end={Boolean((item as { exact?: boolean }).exact)}
                        aria-label={t(item.label)}
                        className={({ isActive }) =>
                          `app-sidebar__nav-item ${isActive ? "app-sidebar__nav-item--active" : ""}`
                        }
                        title={sidebarCollapsed ? t(item.label) : undefined}
                        to={item.to}
                      >
                        <span className="app-sidebar__nav-icon" aria-hidden="true">
                          <SidebarIcon name={item.icon} />
                        </span>
                        <span className="app-sidebar__nav-label">{t(item.label)}</span>
                      </NavLink>
                    </li>
                  ))}
                </ul>
              </div>
            </section>
          ))}
        </nav>

        <div className="app-sidebar__footer">
          <div className="app-sidebar__theme-switcher" role="group" aria-label={t("dashboard.theme.label")}>
            <Button
              size="sm"
              title={theme === "light" ? t("dashboard.theme.dark") : t("dashboard.theme.light")}
              variant="secondary"
              onClick={toggleTheme}
            >
              {sidebarCollapsed ? (theme === "light" ? "Dark" : "Light") : (theme === "light" ? t("dashboard.theme.dark") : t("dashboard.theme.light"))}
            </Button>
          </div>
          <div className="app-sidebar__locale-switcher" role="group" aria-label={t("app.language")}>
            <Button
              size="sm"
              title={locale === "en" ? t("app.language.ar") : t("app.language.en")}
              variant="secondary"
              onClick={() => setLocale(locale === "en" ? "ar" : "en")}
            >
              {sidebarCollapsed ? (locale === "en" ? "AR" : "EN") : (locale === "en" ? t("app.language.ar") : t("app.language.en"))}
            </Button>
          </div>
          <div className="app-sidebar__status" title={sidebarCollapsed ? `${workspaceStatus.connectionLabel} · ${workspaceStatus.draftLabel}` : undefined}>
            <div className="app-sidebar__status-header">
              <span className={`status-dot ${isOnline ? "online" : "offline"}`} />
              <span className="app-sidebar__status-title">{t("app.workspaceStatus")}</span>
            </div>
            <div className="app-sidebar__status-copy">
              <span className="app-sidebar__status-line">{workspaceStatus.connectionLabel}</span>
              <span className="app-sidebar__status-line">{workspaceStatus.draftLabel}</span>
            </div>
          </div>
        </div>
      </aside>

      <div className="app-main">
        <header ref={appBarRef} className="app-globalbar">
          <div className="app-globalbar__center">
            <div className={`app-globalbar__search ${isSearchOpen ? "app-globalbar__search--open" : ""}`}>
              <AppBarIcon name="search" />
              <input
                ref={searchInputRef}
                aria-label={t("appBar.search.label")}
                className="app-globalbar__search-input"
                placeholder={t("appBar.search.placeholder")}
                type="search"
                value={searchQuery}
                onBlur={() => {
                  window.setTimeout(() => setIsSearchOpen(false), 120);
                }}
                onChange={(event) => setSearchQuery(event.target.value)}
                onFocus={() => {
                  setIsSearchOpen(true);
                  setOpenMenu(null);
                }}
              />
              <span className="app-globalbar__search-shortcut">{t("appBar.search.shortcut")}</span>

              {isSearchOpen ? (
                <div className="app-globalbar__search-panel">
                  <div className="app-globalbar__search-panel-header">
                    <span>{t("appBar.search.instantResults")}</span>
                    <span>{t("appBar.search.coverage")}</span>
                  </div>

                  {searchResults.length > 0 ? (
                    <ul className="app-globalbar__search-results">
                      {searchResults.map((result) => (
                        <li key={result.id}>
                          <Link className="app-globalbar__search-result" to={result.to}>
                            <span className="app-globalbar__search-result-copy">
                              <span className="app-globalbar__search-result-title">{t(result.labelKey)}</span>
                              <span className="app-globalbar__search-result-meta">{t(result.categoryKey)}</span>
                            </span>
                            <AppBarIcon name="chevron" />
                          </Link>
                        </li>
                      ))}
                    </ul>
                  ) : (
                    <div className="app-globalbar__search-empty">
                      <span>{t("appBar.search.noResults")}</span>
                      <span>{t("appBar.search.noResultsHint")}</span>
                    </div>
                  )}
                </div>
              ) : null}
            </div>
          </div>

          <div className="app-globalbar__right">
            <div className="app-globalbar__status-group">
              <Badge className="app-globalbar__status-badge app-globalbar__status-badge--mode" tone={isDraftMode ? "warning" : "success"}>
                {t("appBar.modeLabel")}: {isDraftMode ? t("appBar.mode.draft") : t("appBar.mode.live")}
              </Badge>
              <Badge className="app-globalbar__status-badge app-globalbar__status-badge--ai" tone={aiActive ? "primary" : "neutral"}>
                {t("appBar.aiLabel")}: {aiActive ? t("appBar.ai.active") : t("appBar.ai.idle")}
              </Badge>
            </div>

            <div className="app-globalbar__actions">
              <div className={`app-globalbar__menu app-globalbar__menu--notifications ${openMenu === "notifications" ? "app-globalbar__menu--open" : ""}`}>
                <button
                  aria-expanded={openMenu === "notifications"}
                  aria-label={t("appBar.notifications.label")}
                  className="app-globalbar__icon-button"
                  type="button"
                  onClick={() => {
                    setOpenMenu((current) => current === "notifications" ? null : "notifications");
                    setIsSearchOpen(false);
                  }}
                >
                  <AppBarIcon name="bell" />
                  <span className="app-globalbar__badge-count">{notificationCount}</span>
                </button>

                {openMenu === "notifications" ? (
                  <div className="app-globalbar__dropdown app-globalbar__dropdown--notifications">
                    <div className="app-globalbar__dropdown-header">
                      <div>
                        <p className="app-globalbar__dropdown-eyebrow">{t("appBar.notifications.eyebrow")}</p>
                        <h2 className="app-globalbar__dropdown-title">{t("appBar.notifications.title")}</h2>
                      </div>
                      <Badge tone="primary">{notificationCount}</Badge>
                    </div>

                    <ul className="app-globalbar__notification-list">
                      {enterpriseNotifications.map((notification) => (
                        <li key={notification.id}>
                          <Link className="app-globalbar__notification-item" to={notification.to}>
                            <Badge tone={notification.tone}>{t(notification.titleKey)}</Badge>
                            <span className="app-globalbar__notification-copy">
                              <span className="app-globalbar__notification-title">{t(notification.titleKey)}</span>
                              <span className="app-globalbar__notification-detail">{t(notification.detailKey)}</span>
                            </span>
                          </Link>
                        </li>
                      ))}
                    </ul>
                  </div>
                ) : null}
              </div>

              <button
                aria-label={t("appBar.languageSwitch")}
                className="app-globalbar__icon-button app-globalbar__icon-button--text"
                type="button"
                onClick={() => setLocale(locale === "en" ? "ar" : "en")}
              >
                <AppBarIcon name="globe" />
                <span>{locale === "en" ? "AR" : "EN"}</span>
              </button>

              <button
                aria-label={theme === "light" ? t("dashboard.theme.dark") : t("dashboard.theme.light")}
                className="app-globalbar__icon-button"
                type="button"
                onClick={toggleTheme}
              >
                <AppBarIcon name={theme === "light" ? "moon" : "sun"} />
              </button>

              <div className={`app-globalbar__menu ${openMenu === "user" ? "app-globalbar__menu--open" : ""}`}>
                <button
                  aria-expanded={openMenu === "user"}
                  aria-label={t("appBar.userMenu.label")}
                  className="app-globalbar__profile-button"
                  type="button"
                  onClick={() => {
                    setOpenMenu((current) => current === "user" ? null : "user");
                    setIsSearchOpen(false);
                  }}
                >
                  <span className="app-globalbar__avatar" aria-hidden="true">{getInitials(userName)}</span>
                  <span className="app-globalbar__profile-copy">
                    <span className="app-globalbar__profile-name">{userName}</span>
                    <span className="app-globalbar__profile-role">{workspace?.roles[0]?.name ?? t("appBar.userMenu.roleValue")}</span>
                  </span>
                  <AppBarIcon name="chevron" />
                </button>

                {openMenu === "user" ? (
                  <div className="app-globalbar__dropdown app-globalbar__dropdown--user">
                    <div className="app-globalbar__dropdown-header app-globalbar__dropdown-header--user">
                      <span className="app-globalbar__avatar app-globalbar__avatar--lg" aria-hidden="true">{getInitials(userName)}</span>
                      <div>
                        <h2 className="app-globalbar__dropdown-title">{userName}</h2>
                        <p className="app-globalbar__dropdown-detail">{workspace?.roles[0]?.name ?? t("appBar.userMenu.roleValue")} · {workspace?.organizationName ?? t("appBar.companyName")}</p>
                      </div>
                    </div>

                    <div className="app-globalbar__menu-list">
                      <button className="app-globalbar__menu-item" type="button" onClick={() => setOpenMenu(null)}>
                        <AppBarIcon name="spark" />
                        <span>{t("appBar.userMenu.role")}: {workspace?.roles[0]?.name ?? t("appBar.userMenu.roleValue")}</span>
                      </button>
                      {workspace?.organizations.map((organization) => (
                        <button
                          key={organization.organizationId}
                          className="app-globalbar__menu-item"
                          type="button"
                          onClick={async () => {
                            setOpenMenu(null);
                            if (organization.organizationId !== workspace.organizationId) {
                              await switchOrganization(organization.organizationId);
                            }
                          }}
                        >
                          <AppBarIcon name="building" />
                          <span>{organization.name}</span>
                        </button>
                      ))}
                      <button className="app-globalbar__menu-item app-globalbar__menu-item--danger" type="button" onClick={async () => {
                        setOpenMenu(null);
                        await logout(false);
                        navigate("/login");
                      }}>
                        <AppBarIcon name="logout" />
                        <span>{t("appBar.userMenu.logout")}</span>
                      </button>
                    </div>
                  </div>
                ) : null}
              </div>
            </div>
          </div>
        </header>

        {isDashboard ? (
          <section className="app-dashboard-hero">
            <div className="app-dashboard-hero__copy">
              <p className="app-dashboard-hero__eyebrow">{t("app.workspace")}</p>
              <h2 className="app-dashboard-hero__title">{greeting}</h2>
              <p className="app-dashboard-hero__description">{t("dashboard.topbar.description")}</p>
            </div>

            <div className="app-dashboard-hero__actions">
              <Link className="app-dashboard-hero__action app-dashboard-hero__action--primary" to="/purchase-orders/new">
                {t("dashboard.quickActions.createPo.label")}
              </Link>
              <Link className="app-dashboard-hero__action" to="/purchase-receipts/new">
                {t("dashboard.quickActions.postReceipt.label")}
              </Link>
              <Link className="app-dashboard-hero__action" to="/stock-movements">
                {t("dashboard.quickActions.viewStockCard.label")}
              </Link>
            </div>
          </section>
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

const navigationPermissions: Record<string, string | undefined> = {
  "/workspace": undefined,
  "/procurement/purchase-orders": Permissions.ProcurementPurchaseOrderView,
  "/procurement/purchase-receipts": Permissions.ProcurementPurchaseReceiptView,
  "/procurement/purchase-returns": Permissions.ProcurementPurchaseReturnView,
  "/inventory/stock-ledger": Permissions.InventoryStockLedgerView,
  "/inventory/warehouses": Permissions.InventoryWarehouseManage,
  "/supplier-financials": Permissions.SupplierFinancialsPayablesView,
  "/supplier-financials/payments": Permissions.SupplierFinancialsPayablesView,
  "/customers": Permissions.CustomersView,
  "/purchase-orders": Permissions.ProcurementPurchaseOrderView,
  "/purchase-receipts": Permissions.ProcurementPurchaseReceiptView,
  "/purchase-returns": Permissions.ProcurementPurchaseReturnView,
  "/open-shortages": Permissions.ShortageView,
  "/shortage-resolutions": Permissions.ShortageView,
  "/stock-balances": Permissions.InventoryStockLedgerView,
  "/stock-movements": Permissions.InventoryStockLedgerView,
  "/supplier-statements": Permissions.SupplierFinancialsPayablesView,
  "/payments": Permissions.SupplierFinancialsPayablesView,
  "/items": Permissions.ItemsView,
  "/uom-conversions": Permissions.UomsManage,
  "/suppliers": Permissions.SuppliersView,
  "/warehouses": Permissions.InventoryWarehouseManage,
  "/uoms": Permissions.UomsManage,
  "/settings/users": Permissions.SettingsUsersManage,
  "/settings/roles": Permissions.SettingsRolesManage,
};

const featureGroupVisibility = {
  workspace: () => true,
  procurement: (features: { procurementEnabled: boolean }) => features.procurementEnabled,
  inventory: (features: { inventoryEnabled: boolean }) => features.inventoryEnabled,
  shortages: (features: { inventoryEnabled: boolean }) => features.inventoryEnabled,
  suppliers: (features: { suppliersEnabled: boolean }) => features.suppliersEnabled,
  "supplier-financials": (features: { supplierFinancialsEnabled: boolean }) => features.supplierFinancialsEnabled,
  settings: (features: { settingsEnabled?: boolean }) => features.settingsEnabled !== false,
} as const;

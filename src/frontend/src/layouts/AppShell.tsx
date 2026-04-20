import { useState, type PropsWithChildren } from "react";
import { Link, NavLink, useLocation } from "react-router-dom";
import { Badge, Button, PageContainer, useToast } from "../components/ui";
import { StatusPanel } from "../components/StatusPanel";

const navigationGroups = [
  {
    label: "Workspace",
    items: [{ label: "Dashboard", shortLabel: "DB", to: "/" }],
  },
  {
    label: "Master Data",
    items: [
      { label: "Items", shortLabel: "IT", to: "/items" },
      { label: "Components", shortLabel: "CP", to: "/item-components" },
      { label: "Item UOM Rules", shortLabel: "UR", to: "/item-uom-conversions" },
      { label: "Suppliers", shortLabel: "SU", to: "/suppliers" },
      { label: "Warehouses", shortLabel: "WH", to: "/warehouses" },
      { label: "UOMs", shortLabel: "UM", to: "/uoms" },
    ],
  },
];

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

  if (pathname.startsWith("/item-components")) {
    return {
      section: "Master Data",
      eyebrow: pathname.includes("/new") || pathname.includes("/edit") ? "Component editor" : "Assemblies",
      title: pathname.includes("/new") ? "Create component row" : pathname.includes("/edit") ? "Edit component row" : "Item components",
      subtitle: "Manage parent and component relationships in one place.",
    };
  }

  if (pathname.startsWith("/item-uom-conversions")) {
    return {
      section: "Master Data",
      eyebrow: pathname.includes("/new") || pathname.includes("/edit") ? "Conversion editor" : "Measurement rules",
      title: pathname.includes("/new") ? "Create conversion" : pathname.includes("/edit") ? "Edit conversion" : "Item UOM rules",
      subtitle: "Keep item-specific conversion logic clear and traceable.",
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
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const routeMeta = getRouteMeta(location.pathname);
  const isDashboard = location.pathname === "/" || location.pathname === "/home";
  const todayLabel = new Intl.DateTimeFormat("en-US", {
    weekday: "long",
    month: "long",
    day: "numeric",
  }).format(new Date());
  const greeting = `${greetingForHour(new Date().getHours())}.`;

  function handleAiAssist() {
    showToast({
      tone: "info",
      title: "AI workspace assistant",
      description: "Assistant actions will surface next-step guidance, review prompts, and exception hints as more modules go live.",
    });
  }

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
            {sidebarCollapsed ? ">>" : "<<"}
          </Button>
        </div>

        <nav aria-label="Primary" className="app-sidebar__nav">
          {navigationGroups.map((group) => (
            <section key={group.label} className="app-sidebar__group">
              <p className="app-sidebar__group-label">{group.label}</p>
              <ul className="app-sidebar__nav-list">
                {group.items.map((item) => (
                  <li key={item.to}>
                    <NavLink
                      className={({ isActive }) =>
                        `app-sidebar__nav-item ${isActive ? "app-sidebar__nav-item--active" : ""}`
                      }
                      to={item.to}
                    >
                      <span className="app-sidebar__nav-bullet" aria-hidden="true" />
                      <span className="app-sidebar__nav-short">{item.shortLabel}</span>
                      <span className="app-sidebar__nav-label">{item.label}</span>
                    </NavLink>
                  </li>
                ))}
              </ul>
            </section>
          ))}
        </nav>

        <div className="app-sidebar__footer">
          <StatusPanel />
        </div>
      </aside>

      <div className="app-main">
        <header className="app-topbar">
          <PageContainer width="wide" className="app-topbar__container">
            <div className="app-topbar__utility">
              <div className="app-topbar__breadcrumbs" aria-label="Section context">
                <span>HighCool ERP</span>
                <span className="app-topbar__separator">/</span>
                <span>{routeMeta.section}</span>
              </div>

              <div className="app-topbar__utility-actions">
                <Badge tone="neutral">{todayLabel}</Badge>
                <Badge tone="primary">Offline drafts only</Badge>
                <Button size="sm" variant="ghost" onClick={handleAiAssist}>
                  AI workspace
                </Button>
              </div>
            </div>

            {isDashboard ? (
              <div className="app-topbar__welcome">
                <div className="app-topbar__headline">
                  <p className="app-topbar__eyebrow">{routeMeta.eyebrow}</p>
                  <h2 className="app-topbar__title">{greeting}</h2>
                  <p className="app-topbar__description">{routeMeta.subtitle}</p>
                </div>

                <div className="app-topbar__actions app-topbar__actions--compact">
                  <div className="app-topbar__quick-actions app-topbar__quick-actions--compact">
                    <Link className="hc-button hc-button--ghost hc-button--sm" to="/items">
                      Open items
                    </Link>
                    <Link className="hc-button hc-button--ghost hc-button--sm" to="/suppliers">
                      Review suppliers
                    </Link>
                    <Link className="hc-button hc-button--ghost hc-button--sm" to="/warehouses">
                      Check warehouses
                    </Link>
                  </div>
                </div>
              </div>
            ) : (
              <div className="app-topbar__compact">
                <div className="app-topbar__compact-copy">
                  <p className="app-topbar__eyebrow">{routeMeta.eyebrow}</p>
                  <p className="app-topbar__compact-text">{routeMeta.subtitle}</p>
                </div>

                <div className="app-topbar__compact-meta">
                  <Badge tone="neutral">{routeMeta.section}</Badge>
                  <Button size="sm" variant="ghost" onClick={handleAiAssist}>
                    Ask AI
                  </Button>
                </div>
              </div>
            )}
          </PageContainer>
        </header>

        <main className="app-main__content">
          <PageContainer width="wide" className="app-main__page">
            {children}
          </PageContainer>
        </main>
      </div>
    </div>
  );
}

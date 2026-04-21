import { Link } from "react-router-dom";
import { Badge, Card } from "../components/ui";

const moduleCards = [
  {
    title: "Customers",
    description: "Customer accounts, credit limits, and contact details.",
    href: "/customers",
    status: "Ready",
  },
  {
    title: "Items",
    description: "Clean item records, roles, and base UOM setup.",
    href: "/items",
    status: "Ready",
  },
  {
    title: "Suppliers",
    description: "Supplier names, statement names, and contact details.",
    href: "/suppliers",
    status: "Ready",
  },
  {
    title: "Warehouses",
    description: "Warehouse identities and location references.",
    href: "/warehouses",
    status: "Ready",
  },
  {
    title: "Units of Measure",
    description: "Shared measurement rules for the catalog.",
    href: "/uoms",
    status: "Ready",
  },
];

const nextActions = [
  {
    title: "Check item roles",
    description: "Make sure sellable and component flags are aligned before downstream flows arrive.",
    href: "/items",
  },
  {
    title: "Review supplier statement names",
    description: "Keep external-facing names clean before statement generation is introduced.",
    href: "/suppliers",
  },
  {
    title: "Review customer credit controls",
    description: "Confirm active customers and payment terms before sales and collections modules arrive.",
    href: "/customers",
  },
  {
    title: "Validate warehouse coverage",
    description: "Confirm active warehouse records and locations before inventory posting goes live.",
    href: "/warehouses",
  },
];

export function DashboardPage() {
  return (
    <section className="hc-home">
      <Card className="hero" padding="lg">
        <Badge tone="primary">Workspace snapshot</Badge>
        <h2>Master data is ready for a cleaner daily workflow.</h2>
        <p className="hero-copy">
          The shared shell, lists, and forms now follow one calm enterprise rhythm. Review setup, tidy records, and move quickly between core modules.
        </p>
      </Card>

      <div className="hc-home__stats">
        <Card className="hc-home__stat" muted padding="md">
          <p className="hc-home__stat-value">7</p>
          <p className="hc-home__stat-label">Core master-data routes live</p>
        </Card>
        <Card className="hc-home__stat" muted padding="md">
          <p className="hc-home__stat-value">1</p>
          <p className="hc-home__stat-label">Shared visual system across lists and forms</p>
        </Card>
        <Card className="hc-home__stat" muted padding="md">
          <p className="hc-home__stat-value">Draft</p>
          <p className="hc-home__stat-label">Offline support remains draft-only</p>
        </Card>
      </div>

      <div className="hc-overview-grid">
        {moduleCards.map((card) => (
          <Card key={card.title} className="hc-summary-card" padding="md">
            <div className="hc-summary-card__header">
              <Badge tone="neutral">{card.status}</Badge>
              <h3 className="hc-summary-card__title">{card.title}</h3>
            </div>
            <p className="hc-summary-card__description">{card.description}</p>
            <div className="hc-summary-card__footer">
              <Link className="hc-summary-card__link" to={card.href}>
                Open module
              </Link>
            </div>
          </Card>
        ))}
      </div>

      <div className="hc-home__panels">
        <Card className="hc-home__panel" padding="md">
          <div className="hc-home__panel-header">
            <h3 className="hc-home__panel-title">Next best actions</h3>
            <p className="hc-home__panel-copy">A short review list to keep setup tidy.</p>
          </div>

          <div className="hc-home__list">
            {nextActions.map((action) => (
              <div key={action.title} className="hc-home__list-item">
                <div className="hc-home__list-main">
                  <p className="hc-home__list-title">{action.title}</p>
                  <p className="hc-home__list-copy">{action.description}</p>
                </div>

                <Link className="hc-button hc-button--secondary hc-button--sm" to={action.href}>
                  Open
                </Link>
              </div>
            ))}
          </div>
        </Card>

        <Card className="hc-home__panel" muted padding="md">
          <div className="hc-home__panel-header">
            <h3 className="hc-home__panel-title">AI workspace hints</h3>
            <p className="hc-home__panel-copy">Assistant guidance should feel present, not noisy.</p>
          </div>

          <div className="hc-home__list">
            <div className="hc-home__list-item">
              <div className="hc-home__list-main">
                <p className="hc-home__list-title">Setup prompts</p>
                <p className="hc-home__list-copy">The header assistant can surface quick review prompts as modules expand.</p>
              </div>
            </div>
            <div className="hc-home__list-item">
              <div className="hc-home__list-main">
                <p className="hc-home__list-title">Exception-first guidance</p>
                <p className="hc-home__list-copy">Use AI for concise nudges around missing setup, inactive records, and follow-up work.</p>
              </div>
            </div>
            <div className="hc-home__list-item">
              <div className="hc-home__list-main">
                <p className="hc-home__list-title">No chat clutter</p>
                <p className="hc-home__list-copy">The assistant layer stays embedded in headers, helpers, and lightweight actions.</p>
              </div>
            </div>
          </div>
        </Card>
      </div>
    </section>
  );
}

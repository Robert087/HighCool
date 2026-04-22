import { Link } from "react-router-dom";
import { Button, Card, useToast } from "../components/ui";

const quickLinks = [
  {
    title: "Purchase Orders",
    eyebrow: "Purchasing",
    meta: "Draft, post, and review supplier demand",
    href: "/purchase-orders",
    actionLabel: "Open orders",
    actionVariant: "primary",
    cardTone: "primary",
  },
  {
    title: "Purchase Receipts",
    eyebrow: "Warehouse",
    meta: "Capture deliveries and component shortages",
    href: "/purchase-receipts",
    actionLabel: "Open receipts",
    actionVariant: "secondary",
    cardTone: "supporting",
  },
  {
    title: "Stock Balance",
    eyebrow: "Inventory",
    meta: "Check on-hand quantities by warehouse",
    href: "/stock-balances",
    actionLabel: "View balance",
    actionVariant: "ghost",
    cardTone: "neutral",
  },
  {
    title: "Supplier Statement",
    eyebrow: "Statements",
    meta: "Review payable and receivable position",
    href: "/supplier-statements",
    actionLabel: "Open statement",
    actionVariant: "ghost",
    cardTone: "calm",
  },
] as const;

const attentionItems = [
  {
    title: "Review open shortages",
    detail: "Confirm unresolved component shortages before new resolution work starts.",
    href: "/open-shortages",
    actionLabel: "Review",
    actionVariant: "secondary",
  },
  {
    title: "Check pending purchase receipts",
    detail: "Keep receipt capture current for posted purchase orders and warehouse stock.",
    href: "/purchase-receipts",
    actionLabel: "Open",
    actionVariant: "ghost",
  },
  {
    title: "Validate supplier master data",
    detail: "Clean supplier names and statement identities before deeper statement usage grows.",
    href: "/suppliers",
    actionLabel: "Review",
    actionVariant: "ghost",
  },
  {
    title: "Audit item setup",
    detail: "Check item roles, base UOMs, and component definitions before posting volume increases.",
    href: "/items",
    actionLabel: "Open",
    actionVariant: "ghost",
  },
] as const;

const aiSuggestions = [
  "Show draft purchase orders that still need posting",
  "Where do I have open shortages by supplier?",
  "What should I review before posting receipts today?",
];

export function DashboardPage() {
  const { showToast } = useToast();

  return (
    <section className="hc-home">
      <div className="hc-home__stats">
        <Card className="hc-home__stat hc-home__stat--layered hc-home__stat--primary" muted padding="md">
          <p className="hc-home__stat-kicker">Purchasing</p>
          <p className="hc-home__stat-value">2</p>
          <p className="hc-home__stat-label">Core purchasing workflows live</p>
        </Card>
        <Card className="hc-home__stat hc-home__stat--layered hc-home__stat--supporting" muted padding="md">
          <p className="hc-home__stat-kicker">Inventory</p>
          <p className="hc-home__stat-value">4</p>
          <p className="hc-home__stat-label">Inventory views ready for daily review</p>
        </Card>
        <Card className="hc-home__stat hc-home__stat--layered hc-home__stat--neutral" muted padding="md">
          <p className="hc-home__stat-kicker">Workspace</p>
          <p className="hc-home__stat-value">0</p>
          <p className="hc-home__stat-label">Drafts pending in this workspace</p>
        </Card>
      </div>

      <div className="hc-home__shortcut-grid">
        {quickLinks.map((link) => (
          <Card
            key={link.title}
            className={`hc-home__shortcut-card hc-home__shortcut-card--surface hc-home__shortcut-card--${link.cardTone}`}
            padding="md"
          >
            <div className="hc-home__shortcut-copy">
              <p className="hc-home__shortcut-eyebrow">{link.eyebrow}</p>
              <h3 className="hc-home__shortcut-title">{link.title}</h3>
              <p className="hc-home__shortcut-meta">{link.meta}</p>
            </div>
            <Link className={`hc-button hc-button--${link.actionVariant} hc-button--sm`} to={link.href}>
              {link.actionLabel}
            </Link>
          </Card>
        ))}
      </div>

      <div className="hc-home__panels">
        <Card className="hc-home__panel hc-home__panel--attention" padding="md">
          <div className="hc-home__panel-header">
            <h3 className="hc-home__panel-title">Needs attention</h3>
          </div>

          <div className="hc-home__list">
            {attentionItems.map((action) => (
              <div key={action.title} className="hc-home__list-item">
                <div className="hc-home__list-main">
                  <p className="hc-home__list-title">{action.title}</p>
                  <p className="hc-home__list-copy">{action.detail}</p>
                </div>

                <Link className={`hc-button hc-button--${action.actionVariant} hc-button--sm`} to={action.href}>
                  {action.actionLabel}
                </Link>
              </div>
            ))}
          </div>
        </Card>

        <Card className="hc-home__panel hc-home__panel--ai" padding="md">
          <div className="hc-home__panel-header">
            <h3 className="hc-home__panel-title">Ask AI</h3>
          </div>

          <div className="hc-home__ai">
            <div className="hc-home__ai-input" aria-label="Ask AI suggestions">
              <span className="hc-home__ai-prompt">Ask AI about purchasing, shortages, or setup</span>
              <Button
                size="sm"
                variant="primary"
                onClick={() => {
                  showToast({
                    tone: "info",
                    title: "Ask AI",
                    description: "Start with shortages, pending receipts, or setup checks for the clearest guidance.",
                  });
                }}
              >
                Open
              </Button>
            </div>
            <div className="hc-home__chip-row">
              {aiSuggestions.map((suggestion) => (
                <button
                  key={suggestion}
                  className="hc-home__chip"
                  type="button"
                  onClick={() => {
                    showToast({
                      tone: "info",
                      title: "AI suggestion",
                      description: suggestion,
                    });
                  }}
                >
                  {suggestion}
                </button>
              ))}
            </div>
          </div>
        </Card>
      </div>
    </section>
  );
}

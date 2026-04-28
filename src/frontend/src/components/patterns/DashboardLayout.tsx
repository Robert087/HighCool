import { Link, useNavigate } from "react-router-dom";
import { Badge, Button, Card, useI18n } from "../ui";

type DashboardTone = "urgent" | "pending" | "normal";
type DashboardIcon = "alert" | "receipt" | "document" | "statement" | "inventory" | "check" | "clock";

export interface DashboardAttentionItem {
  count: number;
  ctaLabel: string;
  description?: string;
  icon: DashboardIcon;
  id: string;
  title: string;
  to: string;
  tone: DashboardTone;
}

export interface DashboardActionItem {
  icon: DashboardIcon;
  id: string;
  label: string;
  meta?: string;
  to: string;
}

export interface DashboardKpiItem {
  description?: string;
  id: string;
  label: string;
  value: string;
}

export interface DashboardWorkItem {
  icon: DashboardIcon;
  id: string;
  meta?: string;
  title: string;
  to: string;
}

export interface DashboardWorkSection {
  description?: string;
  id: string;
  items: DashboardWorkItem[];
  title: string;
}

interface DashboardLayoutProps {
  attentionDescription?: string;
  attentionItems: DashboardAttentionItem[];
  attentionTitle: string;
  kpis: DashboardKpiItem[];
  kpiTitle: string;
  quickActionDescription?: string;
  quickActionTitle: string;
  quickActions: DashboardActionItem[];
  workSections: DashboardWorkSection[];
}

function DashboardGlyph({ icon }: { icon: DashboardIcon }) {
  const commonProps = {
    "aria-hidden": true,
    className: "hc-dashboard-icon__svg",
    fill: "none",
    stroke: "currentColor",
    strokeLinecap: "round" as const,
    strokeLinejoin: "round" as const,
    strokeWidth: 1.8,
    viewBox: "0 0 24 24",
  };

  switch (icon) {
    case "alert":
      return <svg {...commonProps}><path d="M12 4 4 19h16L12 4z" /><path d="M12 10v4M12 17h.01" /></svg>;
    case "receipt":
      return <svg {...commonProps}><path d="M8 4h8l3 4v12l-3-2-2 2-2-2-2 2-2-2-3 2V8z" /><path d="M9 10h6M9 14h6" /></svg>;
    case "document":
      return <svg {...commonProps}><path d="M7 3h7l5 5v13H7z" /><path d="M14 3v5h5" /><path d="M10 13h6M10 17h6" /></svg>;
    case "statement":
      return <svg {...commonProps}><path d="M7 4h10v16H7z" /><path d="M9.5 8h5M9.5 12h5M9.5 16h5" /></svg>;
    case "inventory":
      return <svg {...commonProps}><path d="M5 6h14M7 6v12M17 6v12M5 18h14" /><path d="M9.5 11.5h5M9.5 14.5h5" /></svg>;
    case "check":
      return <svg {...commonProps}><path d="m5 12 4 4L19 6" /></svg>;
    case "clock":
      return <svg {...commonProps}><circle cx="12" cy="12" r="8" /><path d="M12 8v5l3 2" /></svg>;
    default:
      return null;
  }
}

function attentionToneClass(tone: DashboardTone) {
  if (tone === "urgent") return "hc-dashboard-attention-item--urgent";
  if (tone === "pending") return "hc-dashboard-attention-item--pending";
  return "hc-dashboard-attention-item--normal";
}

function attentionBadgeTone(tone: DashboardTone) {
  if (tone === "urgent") return "danger" as const;
  if (tone === "pending") return "warning" as const;
  return "primary" as const;
}

export function DashboardLayout({
  attentionDescription,
  attentionItems,
  attentionTitle,
  kpis,
  kpiTitle,
  quickActionDescription,
  quickActionTitle,
  quickActions,
  workSections,
}: DashboardLayoutProps) {
  const { translateText } = useI18n();
  const navigate = useNavigate();

  return (
    <section className="hc-dashboard">
      <Card className="hc-dashboard__section hc-dashboard__section--attention" padding="md">
        <div className="hc-dashboard__section-header">
          <div className="hc-dashboard__section-copy">
            <h2 className="hc-dashboard__section-title">{translateText(attentionTitle)}</h2>
            {attentionDescription ? <p className="hc-dashboard__section-description">{translateText(attentionDescription)}</p> : null}
          </div>
        </div>

        <div className="hc-dashboard-attention-list">
          {attentionItems.slice(0, 5).map((item, index) => (
            <div
              key={item.id}
              className={`hc-dashboard-attention-item ${attentionToneClass(item.tone)} ${index === 0 ? "hc-dashboard-attention-item--top" : ""}`}
              onClick={() => navigate(item.to)}
              onKeyDown={(event) => {
                if (event.key === "Enter" || event.key === " ") {
                  event.preventDefault();
                  navigate(item.to);
                }
              }}
              role="link"
              tabIndex={0}
              aria-label={translateText(item.title)}
            >
              <div className="hc-dashboard-attention-item__main">
                <span className="hc-dashboard-icon hc-dashboard-icon--attention">
                  <DashboardGlyph icon={item.icon} />
                </span>
                <div className="hc-dashboard-attention-item__copy">
                  <p className="hc-dashboard-attention-item__title">{translateText(item.title)}</p>
                  {item.description ? <p className="hc-dashboard-attention-item__description">{translateText(item.description)}</p> : null}
                </div>
              </div>

              <div className="hc-dashboard-attention-item__meta">
                <Badge className="hc-dashboard-attention-item__badge" tone={attentionBadgeTone(item.tone)}>
                  {item.count}
                </Badge>
                <Link className="hc-dashboard-attention-item__cta" to={item.to}>
                  <Button size="sm" variant={index === 0 ? "primary" : "secondary"}>{translateText(item.ctaLabel)}</Button>
                </Link>
              </div>
            </div>
          ))}
        </div>
      </Card>

      <Card className="hc-dashboard__section" padding="md">
        <div className="hc-dashboard__section-header">
          <div className="hc-dashboard__section-copy">
            <h2 className="hc-dashboard__section-title">{translateText(quickActionTitle)}</h2>
            {quickActionDescription ? <p className="hc-dashboard__section-description">{translateText(quickActionDescription)}</p> : null}
          </div>
        </div>

        <div className="hc-dashboard-actions-grid">
          {quickActions.slice(0, 8).map((action) => (
            <Link key={action.id} className="hc-dashboard-action" to={action.to}>
              <span className="hc-dashboard-icon hc-dashboard-icon--action">
                <DashboardGlyph icon={action.icon} />
              </span>
              <span className="hc-dashboard-action__copy">
                <span className="hc-dashboard-action__label">{translateText(action.label)}</span>
                {action.meta ? <span className="hc-dashboard-action__meta">{translateText(action.meta)}</span> : null}
              </span>
            </Link>
          ))}
        </div>
      </Card>

      <div className="hc-dashboard-kpi-block">
        <div className="hc-dashboard__section-copy">
          <h2 className="hc-dashboard__section-title">{translateText(kpiTitle)}</h2>
        </div>

        <div className="hc-dashboard-kpi-grid">
          {kpis.slice(0, 4).map((kpi) => (
            <Card key={kpi.id} className="hc-dashboard-kpi" padding="md">
              <p className="hc-dashboard-kpi__label">{translateText(kpi.label)}</p>
              <p className="hc-dashboard-kpi__value">{kpi.value}</p>
              {kpi.description ? <p className="hc-dashboard-kpi__description">{translateText(kpi.description)}</p> : null}
            </Card>
          ))}
        </div>
      </div>

      <div className="hc-dashboard-work-grid">
        {workSections.slice(0, 2).map((section) => (
          <Card key={section.id} className="hc-dashboard__section hc-dashboard__section--work" padding="md">
            <div className="hc-dashboard__section-header">
              <div className="hc-dashboard__section-copy">
                <h2 className="hc-dashboard__section-title">{translateText(section.title)}</h2>
                {section.description ? <p className="hc-dashboard__section-description">{translateText(section.description)}</p> : null}
              </div>
            </div>

            <div className="hc-dashboard-work-list">
              {section.items.slice(0, 5).map((item) => (
                <Link key={item.id} className="hc-dashboard-work-item" to={item.to}>
                  <span className="hc-dashboard-icon hc-dashboard-icon--work">
                    <DashboardGlyph icon={item.icon} />
                  </span>
                  <span className="hc-dashboard-work-item__copy">
                    <span className="hc-dashboard-work-item__title">{translateText(item.title)}</span>
                    {item.meta ? <span className="hc-dashboard-work-item__meta">{translateText(item.meta)}</span> : null}
                  </span>
                </Link>
              ))}
            </div>
          </Card>
        ))}
      </div>
    </section>
  );
}

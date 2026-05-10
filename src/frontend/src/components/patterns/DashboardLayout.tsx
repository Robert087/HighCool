import { Link } from "react-router-dom";
import { Badge, useI18n } from "../ui";

type DashboardTone = "neutral" | "primary" | "success" | "warning" | "danger";
type DashboardIcon =
  | "alert"
  | "receipt"
  | "document"
  | "statement"
  | "inventory"
  | "supplier"
  | "trend";

export interface DashboardAlertItem {
  actionLabel: string;
  count: string;
  description: string;
  icon: DashboardIcon;
  id: string;
  title: string;
  to: string;
  tone: DashboardTone;
}

export interface DashboardKpiItem {
  actionLabel: string;
  description: string;
  icon: DashboardIcon;
  id: string;
  moduleLabel: string;
  to: string;
  title: string;
  tone: DashboardTone;
  value: string;
}

export interface DashboardActivityItem {
  context: string;
  id: string;
  label: string;
  to: string;
  tone: DashboardTone;
  value: number;
  valueLabel: string;
}

export interface DashboardQueueItem {
  count: string;
  eta: string;
  id: string;
  owner: string;
  title: string;
  to: string;
  tone: DashboardTone;
}

export interface DashboardFinanceMetric {
  id: string;
  title: string;
  to: string;
  tone: DashboardTone;
  value: string;
}

interface DashboardLayoutProps {
  activityChart: DashboardActivityItem[];
  activitySummary?: string | null;
  alerts: DashboardAlertItem[];
  alertsSummary?: string | null;
  financialMetrics: DashboardFinanceMetric[];
  financialSummary?: string | null;
  kpis: DashboardKpiItem[];
  queues: DashboardQueueItem[];
  queuesSummary?: string | null;
}

function DashboardGlyph({ icon }: { icon: DashboardIcon }) {
  const commonProps = {
    "aria-hidden": true,
    className: "hc-erp-dashboard-icon__svg",
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
    case "supplier":
      return <svg {...commonProps}><path d="M4 20h16" /><path d="M6 20V8l6-4 6 4v12" /><path d="M9 12h.01M15 12h.01M9 16h.01M15 16h.01" /></svg>;
    case "trend":
      return <svg {...commonProps}><path d="M4 16 9 11l4 3 7-8" /><path d="M20 6v5h-5" /></svg>;
    default:
      return null;
  }
}

function badgeToneFor(tone: DashboardTone) {
  if (tone === "danger") return "danger" as const;
  if (tone === "warning") return "warning" as const;
  if (tone === "success") return "success" as const;
  if (tone === "primary") return "primary" as const;
  return "neutral" as const;
}

export function DashboardLayout({
  activityChart,
  activitySummary,
  alerts,
  alertsSummary,
  financialMetrics,
  financialSummary,
  kpis,
  queues,
  queuesSummary,
}: DashboardLayoutProps) {
  const { t, translateText } = useI18n();
  const maxActivityValue = activityChart.reduce((max, item) => Math.max(max, item.value), 0);

  return (
    <section className="hc-erp-dashboard">
      {kpis.length > 0 ? (
        <section className="hc-erp-dashboard__overview-grid">
          {kpis.map((kpi) => (
            <Link key={kpi.id} className={`hc-erp-overview-card hc-erp-overview-card--${kpi.tone}`} to={kpi.to}>
              <div className="hc-erp-overview-card__topline">
                <div className="hc-erp-overview-card__lead">
                  <span className="hc-erp-overview-card__icon">
                    <DashboardGlyph icon={kpi.icon} />
                  </span>
                  <span className="hc-erp-overview-card__module">{translateText(kpi.moduleLabel)}</span>
                </div>
                <strong className="hc-erp-overview-card__value">{kpi.value}</strong>
              </div>
              <div className="hc-erp-overview-card__copy">
                <p className="hc-erp-overview-card__title">{translateText(kpi.title)}</p>
              </div>
              <span className="hc-erp-overview-card__action">{translateText(kpi.actionLabel)}</span>
            </Link>
          ))}
        </section>
      ) : null}

      <div className="hc-erp-dashboard__grid hc-erp-dashboard__grid--primary">
        <section className="hc-erp-dashboard-panel hc-erp-dashboard-panel--critical">
          <div className="hc-erp-dashboard__section-header">
            <p className="hc-erp-dashboard__section-eyebrow">{t("dashboard.sections.alerts.eyebrow")}</p>
            <h2 className="hc-erp-dashboard__section-title">{t("dashboard.sections.alerts.title")}</h2>
            <p className="hc-erp-dashboard__section-description">{t("dashboard.sections.alerts.description")}</p>
          </div>

          {alerts.length > 0 ? (
            <div className="hc-erp-dashboard__alert-list">
              {alerts.map((alert) => (
                <Link
                  key={alert.id}
                  aria-label={translateText(alert.title)}
                  className={`hc-erp-alert-card hc-erp-alert-card--${alert.tone}`}
                  to={alert.to}
                >
                  <div className="hc-erp-alert-card__main">
                    <span className="hc-erp-alert-card__icon">
                      <DashboardGlyph icon={alert.icon} />
                    </span>
                    <div className="hc-erp-alert-card__copy">
                      <div className="hc-erp-alert-card__title-row">
                        <p className="hc-erp-alert-card__title">{translateText(alert.title)}</p>
                        <Badge tone={badgeToneFor(alert.tone)}>{alert.count}</Badge>
                      </div>
                      <p className="hc-erp-alert-card__description">{alert.description}</p>
                    </div>
                  </div>
                  <span className="hc-erp-alert-card__action">{translateText(alert.actionLabel)}</span>
                </Link>
              ))}
            </div>
          ) : alertsSummary ? (
            <p className="hc-erp-dashboard__summary">{alertsSummary}</p>
          ) : null}
        </section>

        <section className="hc-erp-dashboard-panel">
          <div className="hc-erp-dashboard__section-header">
            <p className="hc-erp-dashboard__section-eyebrow">{t("dashboard.sections.activity.eyebrow")}</p>
            <h2 className="hc-erp-dashboard__section-title">{t("dashboard.sections.activity.title")}</h2>
            <p className="hc-erp-dashboard__section-description">{t("dashboard.sections.activity.description")}</p>
          </div>

          {activityChart.length > 0 ? (
            <div className="hc-erp-activity-chart">
              {activityChart.map((item) => {
                const width = maxActivityValue > 0 ? `${Math.max((item.value / maxActivityValue) * 100, 8)}%` : "0%";

                return (
                  <Link key={item.id} className="hc-erp-activity-chart__row" to={item.to}>
                    <div className="hc-erp-activity-chart__copy">
                      <div className="hc-erp-activity-chart__headline">
                        <span className="hc-erp-activity-chart__label">{translateText(item.label)}</span>
                        <strong className="hc-erp-activity-chart__value">{item.valueLabel}</strong>
                      </div>
                      <div className="hc-erp-activity-chart__bar">
                        <span className={`hc-erp-activity-chart__bar-fill hc-erp-activity-chart__bar-fill--${item.tone}`} style={{ width }} />
                      </div>
                      <span className="hc-erp-activity-chart__context">{item.context}</span>
                    </div>
                  </Link>
                );
              })}
            </div>
          ) : activitySummary ? (
            <p className="hc-erp-dashboard__summary">{activitySummary}</p>
          ) : null}
        </section>
      </div>

      <div className="hc-erp-dashboard__grid hc-erp-dashboard__grid--secondary">
        <section className="hc-erp-dashboard-panel">
          <div className="hc-erp-dashboard__section-header">
            <p className="hc-erp-dashboard__section-eyebrow">{t("dashboard.sections.queues.eyebrow")}</p>
            <h2 className="hc-erp-dashboard__section-title">{t("dashboard.sections.queues.title")}</h2>
            <p className="hc-erp-dashboard__section-description">{t("dashboard.sections.queues.description")}</p>
          </div>

          {queues.length > 0 ? (
            <div className="hc-erp-dashboard__queue-list">
              {queues.map((queue) => (
                <Link key={queue.id} className={`hc-erp-queue-card hc-erp-queue-card--${queue.tone}`} to={queue.to}>
                  <div className="hc-erp-queue-card__copy">
                    <div className="hc-erp-queue-card__headline">
                      <p className="hc-erp-queue-card__title">{translateText(queue.title)}</p>
                      <Badge tone={badgeToneFor(queue.tone)}>{queue.count}</Badge>
                    </div>
                    <div className="hc-erp-queue-card__meta">
                      <span>{queue.owner}</span>
                      <span>{queue.eta}</span>
                    </div>
                  </div>
                </Link>
              ))}
            </div>
          ) : queuesSummary ? (
            <p className="hc-erp-dashboard__summary">{queuesSummary}</p>
          ) : null}
        </section>

        <section className="hc-erp-dashboard-panel hc-erp-dashboard-panel--secondary">
          <div className="hc-erp-dashboard__section-header">
            <p className="hc-erp-dashboard__section-eyebrow">{t("dashboard.sections.finance.eyebrow")}</p>
            <h2 className="hc-erp-dashboard__section-title">{t("dashboard.sections.finance.title")}</h2>
            <p className="hc-erp-dashboard__section-description">{t("dashboard.sections.finance.description")}</p>
          </div>

          {financialMetrics.length > 0 ? (
            <div className="hc-erp-finance-grid">
              {financialMetrics.map((metric) => (
                <Link key={metric.id} className={`hc-erp-finance-card hc-erp-finance-card--${metric.tone}`} to={metric.to}>
                  <span className="hc-erp-finance-card__label">{translateText(metric.title)}</span>
                  <strong className="hc-erp-finance-card__value">{metric.value}</strong>
                </Link>
              ))}
            </div>
          ) : financialSummary ? (
            <p className="hc-erp-dashboard__summary hc-erp-dashboard__summary--secondary">{financialSummary}</p>
          ) : null}
        </section>
      </div>
    </section>
  );
}

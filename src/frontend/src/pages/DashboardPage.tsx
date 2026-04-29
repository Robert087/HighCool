import { useEffect, useMemo, useState } from "react";
import {
  type DashboardActivityItem,
  DashboardLayout,
  type DashboardAlertItem,
  type DashboardFinanceMetric,
  type DashboardKpiItem,
  type DashboardQueueItem,
} from "../components/patterns";
import { Button, EmptyState, SkeletonLoader } from "../components/ui";
import { useI18n } from "../i18n";
import { ApiError } from "../services/api";
import { loadDashboardSnapshot, type DashboardSnapshot } from "../services/dashboardApi";

function formatQueueAge(date: string | null, formatDate: ReturnType<typeof useI18n>["formatDate"], t: ReturnType<typeof useI18n>["t"]) {
  if (!date) {
    return t("dashboard.queue.noOldest");
  }

  return t("dashboard.queue.oldest", {
    date: formatDate(date, { day: "numeric", month: "short" }),
  });
}

function DashboardSkeleton() {
  return (
    <section className="hc-erp-dashboard">
      <SkeletonLoader variant="rect" height="17rem" />
      <SkeletonLoader variant="rect" height="11rem" />
      <SkeletonLoader variant="rect" height="10rem" />
      <SkeletonLoader variant="rect" height="5.5rem" />
    </section>
  );
}

export function DashboardPage() {
  const { formatCurrency, formatDate, formatNumber, t } = useI18n();
  const [snapshot, setSnapshot] = useState<DashboardSnapshot | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        setError("");
        const nextSnapshot = await loadDashboardSnapshot();
        if (active) {
          setSnapshot(nextSnapshot);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : t("dashboard.loadError"));
        }
      } finally {
        if (active) {
          setLoading(false);
        }
      }
    }

    void load();

    return () => {
      active = false;
    };
  }, [reloadKey, t]);

  const dashboardModel = useMemo(() => {
    if (!snapshot) {
      return null;
    }

    const alerts: Array<DashboardAlertItem & { rawCount: number }> = [
      {
        id: "open-shortages",
        icon: "alert" as const,
        title: "dashboard.alerts.shortages.title",
        description: t("dashboard.alerts.shortages.summary", {
          qty: formatNumber(snapshot.openShortageQty),
        }),
        count: formatNumber(snapshot.openShortageCount),
        actionLabel: "dashboard.actions.reviewShortages",
        to: "/open-shortages",
        tone: "danger" as const,
        rawCount: snapshot.openShortageCount,
      },
      {
        id: "pending-receipts",
        icon: "receipt" as const,
        title: "dashboard.alerts.receipts.title",
        description: t("dashboard.alerts.receipts.summary", {
          count: formatNumber(snapshot.pendingReceiptCount),
        }),
        count: formatNumber(snapshot.pendingReceiptCount),
        actionLabel: "dashboard.actions.reviewReceipts",
        to: "/purchase-receipts",
        tone: "warning" as const,
        rawCount: snapshot.pendingReceiptCount,
      },
      {
        id: "negative-stock",
        icon: "inventory" as const,
        title: "dashboard.alerts.negativeStock.title",
        description: t("dashboard.alerts.negativeStock.summary", {
          count: formatNumber(snapshot.negativeStockCount),
        }),
        count: formatNumber(snapshot.negativeStockCount),
        actionLabel: "dashboard.actions.viewStockCard",
        to: "/stock-balances",
        tone: "danger" as const,
        rawCount: snapshot.negativeStockCount,
      },
      {
        id: "unposted-transactions",
        icon: "document" as const,
        title: "dashboard.alerts.unposted.title",
        description: t("dashboard.alerts.unposted.summary", {
          count: formatNumber(snapshot.unpostedTransactionCount),
        }),
        count: formatNumber(snapshot.unpostedTransactionCount),
        actionLabel: "dashboard.actions.reviewTransactions",
        to: "/purchase-orders",
        tone: "warning" as const,
        rawCount: snapshot.unpostedTransactionCount,
      },
    ].filter((alert) => alert.rawCount > 0);

    const kpis: Array<DashboardKpiItem & { rawCount: number }> = [
      {
        id: "overview-approvals",
        icon: "document" as const,
        moduleLabel: "dashboard.modules.purchasing",
        title: "dashboard.overview.pendingApprovals.title",
        value: formatNumber(snapshot.approvalsQueueCount),
        description: t("dashboard.overview.pendingApprovals.description", {
          count: formatNumber(snapshot.approvalsQueueCount),
        }),
        actionLabel: "dashboard.actions.reviewApprovals",
        to: "/purchase-orders",
        tone: "warning" as const,
        rawCount: snapshot.approvalsQueueCount,
      },
      {
        id: "overview-receipts",
        icon: "receipt" as const,
        moduleLabel: "dashboard.modules.inventory",
        title: "dashboard.overview.receiptsWaitingPosting.title",
        value: formatNumber(snapshot.pendingReceiptCount),
        description: t("dashboard.overview.receiptsWaitingPosting.description", {
          count: formatNumber(snapshot.pendingReceiptCount),
        }),
        to: "/purchase-receipts",
        actionLabel: "dashboard.actions.openReceipts",
        tone: "warning" as const,
        rawCount: snapshot.pendingReceiptCount,
      },
      {
        id: "overview-unposted",
        icon: "document" as const,
        moduleLabel: "dashboard.modules.purchasing",
        title: "dashboard.overview.unpostedTransactions.title",
        value: formatNumber(snapshot.unpostedTransactionCount),
        description: t("dashboard.overview.unpostedTransactions.description", {
          count: formatNumber(snapshot.unpostedTransactionCount),
        }),
        actionLabel: "dashboard.actions.reviewTransactions",
        to: "/purchase-orders",
        tone: "warning" as const,
        rawCount: snapshot.unpostedTransactionCount,
      },
      {
        id: "overview-payments",
        icon: "statement" as const,
        moduleLabel: "dashboard.modules.finance",
        title: "dashboard.overview.draftPayments.title",
        value: formatNumber(snapshot.financeDraftCount),
        description: t("dashboard.overview.draftPayments.description", {
          count: formatNumber(snapshot.financeDraftCount),
        }),
        actionLabel: "dashboard.actions.openPayments",
        to: "/payments",
        tone: "primary" as const,
        rawCount: snapshot.financeDraftCount,
      },
    ];

    const queues: Array<DashboardQueueItem & { rawCount: number }> = [
      {
        id: "approvals",
        title: "dashboard.queues.approvals.title",
        count: formatNumber(snapshot.approvalsQueueCount),
        owner: t("dashboard.queues.approvals.owner"),
        eta: formatQueueAge(snapshot.oldestApprovalDate, formatDate, t),
        to: "/purchase-orders",
        tone: "warning" as const,
        rawCount: snapshot.approvalsQueueCount,
      },
      {
        id: "warehouse",
        title: "dashboard.queues.warehouse.title",
        count: formatNumber(snapshot.warehouseQueueCount),
        owner: t("dashboard.queues.warehouse.owner"),
        eta: formatQueueAge(snapshot.oldestWarehouseDate, formatDate, t),
        to: "/purchase-receipts",
        tone: "primary" as const,
        rawCount: snapshot.warehouseQueueCount,
      },
      {
        id: "supplier-validation",
        title: "dashboard.queues.validation.title",
        count: formatNumber(snapshot.supplierIssueCount),
        owner: t("dashboard.queues.validation.owner"),
        eta: snapshot.supplierIssueCount > 0
          ? t("dashboard.queues.validation.meta", {
            suppliers: snapshot.supplierIssueNames.join(", "),
          })
          : formatQueueAge(snapshot.oldestSupplierIssueDate, formatDate, t),
        to: "/suppliers",
        tone: "danger" as const,
        rawCount: snapshot.supplierIssueCount,
      },
    ].filter((queue) => queue.rawCount > 0);

    const financialMetrics: DashboardFinanceMetric[] = [];

    if (snapshot.statementDebitsToday > 0) {
      financialMetrics.push({
        id: "supplier-debits",
        title: "dashboard.finance.debitsToday.title",
        value: formatCurrency(snapshot.statementDebitsToday, { currency: "EGP" }),
        to: "/supplier-statements",
        tone: "warning",
      });
    }

    if (snapshot.statementCreditsToday > 0) {
      financialMetrics.push({
        id: "supplier-credits",
        title: "dashboard.finance.creditsToday.title",
        value: formatCurrency(snapshot.statementCreditsToday, { currency: "EGP" }),
        to: "/supplier-statements",
        tone: "success",
      });
    }

    if (snapshot.financeDraftCount > 0) {
      financialMetrics.push({
        id: "draft-payments",
        title: "dashboard.finance.draftPayments.title",
        value: formatNumber(snapshot.financeDraftCount),
        to: "/payments",
        tone: "primary",
      });
    }

    const activityChart: DashboardActivityItem[] = [
      {
        id: "activity-orders",
        label: "dashboard.activity.orders",
        tone: "primary" as const,
        to: "/purchase-orders",
        value: snapshot.ordersTodayCount,
        valueLabel: formatNumber(snapshot.ordersTodayCount),
        context: t("dashboard.activity.orders.context"),
      },
      {
        id: "activity-receipts",
        label: "dashboard.activity.receipts",
        tone: "success" as const,
        to: "/purchase-receipts",
        value: snapshot.postedReceiptsTodayCount,
        valueLabel: formatNumber(snapshot.postedReceiptsTodayCount),
        context: snapshot.pendingReceiptCount > 0
          ? t("dashboard.activity.receipts.context.pending", {
            count: formatNumber(snapshot.pendingReceiptCount),
          })
          : t("dashboard.activity.receipts.context.clear"),
      },
      {
        id: "activity-returns",
        label: "dashboard.activity.returns",
        tone: "warning" as const,
        to: "/purchase-returns",
        value: snapshot.postedReturnsTodayCount,
        valueLabel: formatNumber(snapshot.postedReturnsTodayCount),
        context: t("dashboard.activity.returns.context"),
      },
      {
        id: "activity-statements",
        label: "dashboard.activity.statements",
        tone: "neutral" as const,
        to: "/supplier-statements",
        value: snapshot.statementEntriesTodayCount,
        valueLabel: formatNumber(snapshot.statementEntriesTodayCount),
        context: t("dashboard.activity.statements.context", {
          debit: formatCurrency(snapshot.statementDebitsToday, { currency: "EGP" }),
          credit: formatCurrency(snapshot.statementCreditsToday, { currency: "EGP" }),
        }),
      },
    ].filter((item) => item.value > 0);

    return {
      activityChart,
      activitySummary: activityChart.length === 0 ? t("dashboard.summary.noOperationalActivity") : null,
      alerts,
      alertsSummary: alerts.length === 0 ? t("dashboard.summary.noExceptions") : null,
      financialMetrics,
      financialSummary: financialMetrics.length === 0 ? t("dashboard.summary.noFinanceActivity") : null,
      kpis,
      queues,
      queuesSummary: queues.length === 0 ? t("dashboard.summary.noQueues") : null,
    };
  }, [formatCurrency, formatDate, formatNumber, snapshot, t]);

  if (loading && !snapshot) {
    return <DashboardSkeleton />;
  }

  if (error && !dashboardModel) {
    return (
      <EmptyState
        centered
        action={<Button variant="secondary" onClick={() => setReloadKey((value) => value + 1)}>{t("common.retry")}</Button>}
        description={error}
        title="dashboard.loadError"
      />
    );
  }

  if (!dashboardModel) {
    return null;
  }

  return (
    <DashboardLayout
      activityChart={dashboardModel.activityChart}
      activitySummary={dashboardModel.activitySummary}
      alerts={dashboardModel.alerts}
      alertsSummary={dashboardModel.alertsSummary}
      financialMetrics={dashboardModel.financialMetrics}
      financialSummary={dashboardModel.financialSummary}
      kpis={dashboardModel.kpis}
      queues={dashboardModel.queues}
      queuesSummary={dashboardModel.queuesSummary}
    />
  );
}

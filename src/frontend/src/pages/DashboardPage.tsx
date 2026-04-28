import { DashboardLayout, type DashboardActionItem, type DashboardAttentionItem, type DashboardKpiItem, type DashboardWorkSection } from "../components/patterns";

const attentionItems: DashboardAttentionItem[] = [
  {
    id: "shortages",
    icon: "alert",
    title: "Review open shortages",
    description: "Resolve unresolved component shortages.",
    count: 12,
    ctaLabel: "Review",
    to: "/open-shortages",
    tone: "urgent",
  },
  {
    id: "receipts",
    icon: "receipt",
    title: "Check pending purchase receipts",
    description: "Capture receipts waiting on warehouse action.",
    count: 8,
    ctaLabel: "Open",
    to: "/purchase-receipts",
    tone: "pending",
  },
  {
    id: "suppliers",
    icon: "statement",
    title: "Validate supplier master data",
    description: "Fix supplier records before statement activity grows.",
    count: 3,
    ctaLabel: "Review",
    to: "/suppliers",
    tone: "normal",
  },
  {
    id: "items",
    icon: "inventory",
    title: "Audit item setup",
    description: "Check item setup before posting volume grows.",
    count: 5,
    ctaLabel: "Open",
    to: "/items",
    tone: "normal",
  },
];

const quickActions: DashboardActionItem[] = [
  {
    id: "purchase-orders",
    icon: "document",
    label: "Purchase Orders",
    meta: "Create and post supplier demand",
    to: "/purchase-orders",
  },
  {
    id: "purchase-receipts",
    icon: "receipt",
    label: "Purchase Receipts",
    meta: "Capture deliveries and shortages",
    to: "/purchase-receipts",
  },
  {
    id: "stock-balance",
    icon: "inventory",
    label: "Stock Balance",
    meta: "Check on-hand quantities by warehouse",
    to: "/stock-balances",
  },
  {
    id: "supplier-statement",
    icon: "statement",
    label: "Supplier Statement",
    meta: "Review supplier balance position",
    to: "/supplier-statements",
  },
];

const kpis: DashboardKpiItem[] = [
  {
    id: "purchasing",
    label: "Purchasing",
    value: "2",
    description: "Core purchasing workflows live",
  },
  {
    id: "inventory",
    label: "Inventory",
    value: "4",
    description: "Inventory views ready for daily review",
  },
  {
    id: "workspace",
    label: "Workspace",
    value: "0",
    description: "Drafts pending in this workspace",
  },
];

const workSections: DashboardWorkSection[] = [
  {
    id: "recent",
    title: "Recent work",
    description: "Open the latest work items without scanning full modules.",
    items: [
      { id: "recent-1", icon: "document", title: "Purchase order drafts", meta: "Continue supplier demand planning", to: "/purchase-orders" },
      { id: "recent-2", icon: "receipt", title: "Latest purchase receipts", meta: "Review recently captured warehouse receipts", to: "/purchase-receipts" },
      { id: "recent-3", icon: "statement", title: "Supplier statement review", meta: "Check recent supplier movement", to: "/supplier-statements" },
    ],
  },
  {
    id: "pending",
    title: "Pending work",
    description: "System-driven tasks that still need user action.",
    items: [
      { id: "pending-1", icon: "clock", title: "Posted orders waiting for receipt capture", meta: "Move from purchasing into warehouse receiving", to: "/purchase-receipts" },
      { id: "pending-2", icon: "alert", title: "Shortages requiring resolution", meta: "Resolve physical or financial shortages", to: "/shortage-resolutions" },
      { id: "pending-3", icon: "check", title: "Supplier balances requiring settlement review", meta: "Verify open supplier payment targets", to: "/payments" },
    ],
  },
];

export function DashboardPage() {
  return (
    <DashboardLayout
      attentionDescription="Start with the highest-priority operational blockers."
      attentionItems={attentionItems}
      attentionTitle="Needs attention"
      kpis={kpis}
      kpiTitle="KPI overview"
      quickActionDescription="Jump into the modules used most often."
      quickActionTitle="Quick actions"
      quickActions={quickActions}
      workSections={workSections}
    />
  );
}

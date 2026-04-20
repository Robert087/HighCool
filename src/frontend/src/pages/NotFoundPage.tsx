import { Link } from "react-router-dom";
import { EmptyState, PageHeader } from "../components/ui";

export function NotFoundPage() {
  return (
    <section className="hc-list-page">
      <PageHeader eyebrow="Workspace" title="Page not found" description="This route is not available in the current workspace." />
      <div className="hc-card hc-card--md">
        <EmptyState
          title="This page is unavailable"
          description="Use the sidebar or head back to the dashboard."
          action={<Link className="hc-button hc-button--primary hc-button--md" to="/">Return to dashboard</Link>}
          centered
        />
      </div>
    </section>
  );
}

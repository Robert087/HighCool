import { Link } from "react-router-dom";
import { Card, EmptyState } from "../components/ui";

export function AccessDeniedPage() {
  return (
    <section className="hc-list-page">
      <Card padding="lg">
        <EmptyState
          centered
          title="accessDenied.title"
          description="accessDenied.description"
          action={<Link className="hc-button hc-button--primary hc-button--md" to="/workspace">accessDenied.action</Link>}
        />
      </Card>
    </section>
  );
}

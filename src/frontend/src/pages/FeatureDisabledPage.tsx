import { Link } from "react-router-dom";
import { Card, EmptyState } from "../components/ui";

export function FeatureDisabledPage() {
  return (
    <section className="hc-list-page">
      <Card padding="lg">
        <EmptyState
          centered
          title="featureDisabled.title"
          description="featureDisabled.description"
          action={<Link className="hc-button hc-button--primary hc-button--md" to="/workspace">featureDisabled.action</Link>}
        />
      </Card>
    </section>
  );
}

import { Link } from "react-router-dom";
import { PageHeader } from "../ui";

interface MasterDataPageHeaderProps {
  title: string;
  description: string;
  actionLabel: string;
  actionTo: string;
}

export function MasterDataPageHeader({
  actionLabel,
  actionTo,
  description,
  title,
}: MasterDataPageHeaderProps) {
  return (
    <PageHeader
      eyebrow="Master Data"
      title={title}
      description={description}
      actions={
        <Link className="hc-button hc-button--primary hc-button--md" to={actionTo}>
          {actionLabel}
        </Link>
      }
    />
  );
}

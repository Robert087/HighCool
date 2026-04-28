import { Badge } from "../ui";
import { useI18n } from "../../i18n";

interface StatusBadgeProps {
  isActive: boolean;
}

export function StatusBadge({ isActive }: StatusBadgeProps) {
  const { t } = useI18n();
  return <Badge tone={isActive ? "success" : "neutral"}>{isActive ? t("status.active") : t("status.inactive")}</Badge>;
}

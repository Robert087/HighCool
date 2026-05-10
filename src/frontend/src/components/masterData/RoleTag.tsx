import { Badge } from "../ui";
import { useI18n } from "../../i18n";

interface RoleTagProps {
  label: string;
}

export function RoleTag({ label }: RoleTagProps) {
  const { translateText } = useI18n();

  return (
    <Badge className="hc-role-tag" tone="neutral">
      {translateText(label)}
    </Badge>
  );
}

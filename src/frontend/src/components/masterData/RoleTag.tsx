import { Badge } from "../ui";

interface RoleTagProps {
  label: string;
}

export function RoleTag({ label }: RoleTagProps) {
  return (
    <Badge className="hc-role-tag" tone="neutral">
      {label}
    </Badge>
  );
}

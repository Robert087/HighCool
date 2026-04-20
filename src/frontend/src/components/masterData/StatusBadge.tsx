import { Badge } from "../ui";

interface StatusBadgeProps {
  isActive: boolean;
}

export function StatusBadge({ isActive }: StatusBadgeProps) {
  return <Badge tone={isActive ? "success" : "warning"}>{isActive ? "Active" : "Inactive"}</Badge>;
}

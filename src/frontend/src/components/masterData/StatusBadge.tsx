import { Badge } from "../ui";

interface StatusBadgeProps {
  isActive: boolean;
}

export function StatusBadge({ isActive }: StatusBadgeProps) {
  return <Badge tone={isActive ? "success" : "neutral"}>{isActive ? "Active" : "Inactive"}</Badge>;
}

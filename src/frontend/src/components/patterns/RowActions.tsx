import type { PropsWithChildren, ReactNode } from "react";
import { OverflowMenu, type OverflowMenuItem } from "../ui";

interface RowActionsProps extends PropsWithChildren {
  menuItems?: OverflowMenuItem[];
  primaryAction?: ReactNode;
}

export function RowActions({ children, menuItems = [], primaryAction }: RowActionsProps) {
  if (children) {
    return <div className="hc-table__actions">{children}</div>;
  }

  return (
    <div className="hc-table__actions">
      {primaryAction}
      {menuItems.length > 0 ? <OverflowMenu items={menuItems} /> : null}
    </div>
  );
}

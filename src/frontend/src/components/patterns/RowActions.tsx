import type { PropsWithChildren } from "react";

export function RowActions({ children }: PropsWithChildren) {
  return <div className="hc-table__actions">{children}</div>;
}

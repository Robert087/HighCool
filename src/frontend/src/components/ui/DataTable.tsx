import type { ReactNode } from "react";
import { Card } from "./Card";

interface DataTableProps {
  columns: ReactNode;
  rows: ReactNode;
  footer?: ReactNode;
  emptyState?: ReactNode;
  hasData: boolean;
}

export function DataTable({ columns, emptyState, footer, hasData, rows }: DataTableProps) {
  return (
    <Card className="hc-table-card" padding="md">
      {hasData ? (
        <>
          <div className="hc-table-scroll">
            <table className="hc-table">
              <thead>{columns}</thead>
              <tbody>{rows}</tbody>
            </table>
          </div>
          {footer ? <div className="hc-table__footer">{footer}</div> : null}
        </>
      ) : (
        <div className="hc-table__empty-wrap">{emptyState}</div>
      )}
    </Card>
  );
}

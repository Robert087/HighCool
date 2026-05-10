import type { ReactNode } from "react";
import { Card } from "./Card";
import { localizeReactNode, useI18n } from "../../i18n";

interface DataTableProps {
  columns: ReactNode;
  rows: ReactNode;
  footer?: ReactNode;
  emptyState?: ReactNode;
  hasData: boolean;
}

export function DataTable({ columns, emptyState, footer, hasData, rows }: DataTableProps) {
  const { translateText } = useI18n();

  return (
    <Card className="hc-table-card" padding="md">
      {hasData ? (
        <>
          <div className="hc-table-scroll">
            <table className="hc-table">
              <thead>{localizeReactNode(columns, translateText)}</thead>
              <tbody>{localizeReactNode(rows, translateText)}</tbody>
            </table>
          </div>
          {footer ? <div className="hc-table__footer">{localizeReactNode(footer, translateText)}</div> : null}
        </>
      ) : (
        <div className="hc-table__empty-wrap">{localizeReactNode(emptyState, translateText)}</div>
      )}
    </Card>
  );
}

import { Link } from "react-router-dom";
import { DataTable, EmptyState, Button, Pagination } from "../ui";
import { RowActions } from "../patterns";
import { type Item } from "../../services/masterDataApi";
import { RoleTag } from "../masterData/RoleTag";
import { StatusBadge } from "../masterData/StatusBadge";

interface ItemsTableProps {
  hasFilters: boolean;
  items: Item[];
  safePage: number;
  totalPages: number;
  onDeactivate: (id: string) => void;
  onPageChange: (page: number) => void;
}

const PAGE_SIZE = 10;

export function ItemsTable({
  hasFilters,
  items,
  onDeactivate,
  onPageChange,
  safePage,
  totalPages,
}: ItemsTableProps) {
  return (
    <DataTable
      hasData={items.length > 0}
      columns={
        <tr>
          <th scope="col">Item</th>
          <th scope="col">Base UOM</th>
          <th scope="col">Roles</th>
          <th scope="col">Status</th>
          <th scope="col" className="hc-table__head-actions" aria-label="Actions" />
        </tr>
      }
      rows={items.map((item) => (
        <tr key={item.id} className="hc-table__row">
          <td>
            <div className="hc-table__cell-strong">
              <span className="hc-table__title">{item.name}</span>
              <span className="hc-table__subtitle">{item.code}</span>
            </div>
          </td>
          <td>
            <div className="hc-table__cell-strong">
              <span className="hc-table__title">{item.baseUomCode}</span>
              <span className="hc-table__subtitle">Base unit: {item.baseUomName}</span>
            </div>
          </td>
          <td>
            <div className="hc-role-tags">
              {item.isSellable ? <RoleTag label="Sellable" /> : null}
              {item.isComponent ? <RoleTag label="Component" /> : null}
              {!item.isSellable && !item.isComponent ? <span className="hc-table__subtitle">No roles</span> : null}
            </div>
          </td>
          <td>
            <StatusBadge isActive={item.isActive} />
          </td>
          <td className="hc-table__cell-actions">
            <RowActions>
              <Link className="hc-button hc-button--secondary hc-button--sm hc-table__action-button" to={`/items/${item.id}/edit`}>
                Edit
              </Link>
              {item.isActive ? (
                <Button className="hc-table__action-button" size="sm" variant="ghost" onClick={() => void onDeactivate(item.id)}>
                  Deactivate
                </Button>
              ) : null}
            </RowActions>
          </td>
        </tr>
      ))}
      footer={
        <>
          <p className="hc-table__footer-note">Client-side pagination for the current result set.</p>
          <Pagination
            currentPage={safePage}
            onPageChange={onPageChange}
            pageSize={PAGE_SIZE}
            totalCount={items.length}
            totalPages={totalPages}
          />
        </>
      }
      emptyState={
        hasFilters ? (
          <EmptyState title="No items match the current filters" description="Try a broader search or reset the filters." />
        ) : (
          <EmptyState
            title="No items yet"
            description="Add your first item to start the catalog."
            action={
              <Link className="hc-button hc-button--primary hc-button--md" to="/items/new">
                Create item
              </Link>
            }
          />
        )
      }
    />
  );
}

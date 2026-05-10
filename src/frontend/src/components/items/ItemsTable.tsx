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
          <th scope="col">table.item</th>
          <th scope="col">table.baseUom</th>
          <th scope="col">table.roles</th>
          <th scope="col">table.status</th>
          <th scope="col" className="hc-table__head-actions" aria-label="common.actions" />
        </tr>
      }
      rows={items.map((item) => (
        <tr key={item.id} className="hc-table__row">
          <td>
            <div className="hc-table__cell-strong hc-table__primary-cell">
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
              {item.isSellable ? <RoleTag label="role.sellable" /> : null}
              {item.hasComponents ? <RoleTag label="role.hasComponents" /> : null}
              {!item.isSellable && !item.hasComponents ? <span className="hc-table__subtitle">role.noSpecialFlags</span> : null}
            </div>
          </td>
          <td>
            <div className="hc-table__status-stack">
              <StatusBadge isActive={item.isActive} />
            </div>
          </td>
          <td className="hc-table__cell-actions">
            <RowActions
              primaryAction={(
                <Link className="hc-button hc-button--secondary hc-button--sm hc-table__action-button" to={`/items/${item.id}/edit`}>
                  common.view
                </Link>
              )}
              menuItems={[
                { label: "common.edit", to: `/items/${item.id}/edit` },
                ...(item.isActive ? [{ label: "common.deactivate", onSelect: () => void onDeactivate(item.id) }] : []),
              ]}
            />
          </td>
        </tr>
      ))}
      footer={
        <>
          <p className="hc-table__footer-note">module.items.clientPaginationNote</p>
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
          <EmptyState title="module.items.emptyFiltered" description="module.items.emptyFilteredDescription" />
        ) : (
          <EmptyState
            title="module.items.empty"
            description="module.items.emptyDescription"
            action={
              <Link className="hc-button hc-button--primary hc-button--md" to="/items/new">
                module.items.new
              </Link>
            }
          />
        )
      }
    />
  );
}

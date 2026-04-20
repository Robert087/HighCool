import { MasterDataFilterToolbar } from "../masterData";

interface ItemsFilterToolbarProps {
  hasFilters: boolean;
  resultLabel: string;
  search: string;
  status: string;
  onSearchChange: (value: string) => void;
  onStatusChange: (value: string) => void;
}

export function ItemsFilterToolbar({
  hasFilters,
  resultLabel,
  search,
  status,
  onSearchChange,
  onStatusChange,
}: ItemsFilterToolbarProps) {
  return (
    <MasterDataFilterToolbar
      hasFilters={hasFilters}
      resultLabel={resultLabel}
      searchLabel="Search"
      searchPlaceholder="Search items"
      searchValue={search}
      statusValue={status}
      emptyText="All item records"
      filteredText="Filtered item records"
      onSearchChange={onSearchChange}
      onStatusChange={onStatusChange}
    />
  );
}

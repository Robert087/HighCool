import { MasterDataFilterToolbar } from "../masterData";
import { FilterDropdown } from "../ui";
import { type ItemCategory } from "../../services/masterDataApi";

interface ItemsFilterToolbarProps {
  hasFilters: boolean;
  resultLabel: string;
  categories: ItemCategory[];
  categoryId: string;
  search: string;
  status: string;
  onCategoryChange: (value: string) => void;
  onSearchChange: (value: string) => void;
  onStatusChange: (value: string) => void;
}

export function ItemsFilterToolbar({
  hasFilters,
  resultLabel,
  categories,
  categoryId,
  search,
  status,
  onCategoryChange,
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
      onExtraReset={() => onCategoryChange("")}
      extraFilters={(
        <FilterDropdown aria-label="module.items.filters.category" value={categoryId} onChange={(event) => onCategoryChange(event.target.value)}>
          <option value="">module.items.filters.allCategories</option>
          {categories.map((category) => (
            <option key={category.id} value={category.id}>
              {category.code} - {category.name}
            </option>
          ))}
        </FilterDropdown>
      )}
    />
  );
}

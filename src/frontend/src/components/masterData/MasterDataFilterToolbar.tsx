import { FilterDropdown, FiltersToolbar, FilterTextInput, type FilterChip } from "../ui";

interface MasterDataFilterToolbarProps {
  hasFilters: boolean;
  resultLabel: string;
  searchLabel: string;
  searchPlaceholder: string;
  searchValue: string;
  searchWidth?: "wide" | "full";
  statusValue?: string;
  statusEnabled?: boolean;
  emptyText: string;
  filteredText: string;
  onSearchChange: (value: string) => void;
  onStatusChange?: (value: string) => void;
}

export function MasterDataFilterToolbar({
  emptyText,
  filteredText,
  hasFilters,
  onSearchChange,
  onStatusChange,
  resultLabel,
  searchLabel,
  searchPlaceholder,
  searchValue,
  searchWidth = "wide",
  statusEnabled = true,
  statusValue = "all",
}: MasterDataFilterToolbarProps) {
  const activeFilters: FilterChip[] = [];

  if (searchValue.trim()) {
    activeFilters.push({
      key: "search",
      label: `Search: ${searchValue.trim()}`,
      onRemove: () => onSearchChange(""),
    });
  }

  if (statusEnabled && statusValue !== "all") {
    activeFilters.push({
      key: "status",
      label: `Status: ${statusValue === "active" ? "Active" : "Inactive"}`,
      onRemove: () => onStatusChange?.("all"),
    });
  }

  return (
    <FiltersToolbar
      activeFilters={activeFilters}
      search={(
        <FilterTextInput
          aria-label={searchLabel}
          placeholder={searchPlaceholder}
          value={searchValue}
          onChange={(event) => onSearchChange(event.target.value)}
        />
      )}
      primaryFilters={statusEnabled ? (
        <FilterDropdown aria-label="Status filter" value={statusValue} onChange={(event) => onStatusChange?.(event.target.value)}>
          <option value="all">Status</option>
          <option value="active">Active</option>
          <option value="inactive">Inactive</option>
        </FilterDropdown>
      ) : null}
      resultLabel={resultLabel}
      resetLabel="Reset"
      onReset={() => {
        onSearchChange("");
        if (statusEnabled) {
          onStatusChange?.("all");
        }
      }}
    />
  );
}

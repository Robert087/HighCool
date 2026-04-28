import { FilterDropdown, FiltersToolbar, FilterTextInput, type FilterChip } from "../ui";
import { useI18n } from "../../i18n";

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
  const { t } = useI18n();
  const activeFilters: FilterChip[] = [];

  if (searchValue.trim()) {
    activeFilters.push({
      key: "search",
      label: t("masterData.filter.searchChip", { value: searchValue.trim() }),
      onRemove: () => onSearchChange(""),
    });
  }

  if (statusEnabled && statusValue !== "all") {
    activeFilters.push({
      key: "status",
      label: t("masterData.filter.statusChip", {
        value: statusValue === "active" ? t("status.active") : t("status.inactive"),
      }),
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
        <FilterDropdown aria-label="masterData.filter.statusAria" value={statusValue} onChange={(event) => onStatusChange?.(event.target.value)}>
          <option value="all">common.status</option>
          <option value="active">status.active</option>
          <option value="inactive">status.inactive</option>
        </FilterDropdown>
      ) : null}
      resultLabel={resultLabel}
      resetLabel="common.resetFilters"
      onReset={() => {
        onSearchChange("");
        if (statusEnabled) {
          onStatusChange?.("all");
        }
      }}
    />
  );
}

import { Badge, Input, Select } from "../ui";

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
  return (
    <div className="hc-card hc-card--md hc-filter-bar">
      <div className={`hc-filter-bar__controls ${searchWidth === "full" ? "hc-filter-bar__controls--single" : ""}`}>
        <label className="hc-filter-bar__field hc-filter-bar__field--search">
          <span className="hc-filter-bar__label">{searchLabel}</span>
          <Input
            className="hc-filter-bar__input"
            placeholder={searchPlaceholder}
            value={searchValue}
            onChange={(event) => onSearchChange(event.target.value)}
          />
        </label>

        {statusEnabled ? (
          <label className="hc-filter-bar__field hc-filter-bar__field--status">
            <span className="hc-filter-bar__label">Status</span>
            <Select className="hc-filter-bar__input" value={statusValue} onChange={(event) => onStatusChange?.(event.target.value)}>
              <option value="all">All statuses</option>
              <option value="active">Active</option>
              <option value="inactive">Inactive</option>
            </Select>
          </label>
        ) : null}
      </div>

      <div className="hc-filter-bar__meta">
        <Badge tone="neutral">{resultLabel}</Badge>
        <p className="hc-filter-bar__result">{hasFilters ? filteredText : emptyText}</p>
      </div>
    </div>
  );
}

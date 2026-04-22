import { useEffect, useId, useState, type ReactNode } from "react";
import { cn } from "../../lib/cn";
import { Badge } from "./Badge";
import { Button } from "./Button";
import { Card } from "./Card";
import { Field } from "./Field";
import { Input } from "./Input";

export interface FiltersPanelChip {
  key: string;
  label: string;
  onRemove: () => void;
}

export interface FiltersPanelProps {
  activeFilters?: FiltersPanelChip[];
  advancedFilters?: ReactNode;
  advancedLabel?: string;
  className?: string;
  dateRange?: ReactNode;
  defaultAdvancedOpen?: boolean;
  description?: string;
  hasFilters: boolean;
  helperText?: string;
  onReset: () => void;
  primaryFilters?: ReactNode;
  resetLabel?: string;
  resultLabel: string;
  search: ReactNode;
  title?: string;
}

interface FiltersDateRangeProps {
  fromLabel?: string;
  fromValue: string;
  label?: string;
  onFromChange: (value: string) => void;
  onToChange: (value: string) => void;
  toLabel?: string;
  toValue: string;
}

export function FiltersDateRange({
  fromLabel = "From",
  fromValue,
  label = "Date range",
  onFromChange,
  onToChange,
  toLabel = "To",
  toValue,
}: FiltersDateRangeProps) {
  return (
    <Field className="hc-filters-panel__date-field" label={label}>
      <div className="hc-filters-panel__date-range" role="group" aria-label={typeof label === "string" ? label : "Date range"}>
        <label className="hc-filters-panel__date-input">
          <span className="hc-filters-panel__date-label">{fromLabel}</span>
          <Input type="date" value={fromValue} onChange={(event) => onFromChange(event.target.value)} />
        </label>
        <label className="hc-filters-panel__date-input">
          <span className="hc-filters-panel__date-label">{toLabel}</span>
          <Input type="date" value={toValue} onChange={(event) => onToChange(event.target.value)} />
        </label>
      </div>
    </Field>
  );
}

export function FiltersPanel({
  activeFilters = [],
  advancedFilters,
  advancedLabel = "Advanced filters",
  className,
  dateRange,
  defaultAdvancedOpen = false,
  description,
  hasFilters,
  helperText,
  onReset,
  primaryFilters,
  resetLabel = "Reset filters",
  resultLabel,
  search,
  title = "Filters",
}: FiltersPanelProps) {
  const advancedPanelId = useId();
  const [advancedOpen, setAdvancedOpen] = useState(defaultAdvancedOpen);

  useEffect(() => {
    if (defaultAdvancedOpen) {
      setAdvancedOpen(true);
    }
  }, [defaultAdvancedOpen]);

  return (
    <Card className={cn("hc-filters-panel", className)} padding="sm">
      <div className="hc-filters-panel__header">
        <div className="hc-filters-panel__copy">
          <h2 className="hc-filters-panel__title">{title}</h2>
          {description ? <p className="hc-filters-panel__description">{description}</p> : null}
        </div>

        <div className="hc-filters-panel__meta">
          <Badge tone="neutral">{resultLabel}</Badge>
          {helperText ? <p className="hc-filters-panel__helper">{helperText}</p> : null}
          <Button size="sm" variant="ghost" disabled={!hasFilters} onClick={onReset}>
            {resetLabel}
          </Button>
        </div>
      </div>

      <div className="hc-filters-panel__search">{search}</div>

      {primaryFilters || dateRange ? (
        <div className="hc-filters-panel__primary">
          {primaryFilters}
          {dateRange}
        </div>
      ) : null}

      {advancedFilters ? (
        <div className="hc-filters-panel__advanced">
          <button
            aria-controls={advancedPanelId}
            aria-expanded={advancedOpen}
            className="hc-filters-panel__toggle"
            type="button"
            onClick={() => setAdvancedOpen((current) => !current)}
          >
            <span>{advancedLabel}</span>
            <span className="hc-filters-panel__toggle-indicator" aria-hidden="true">
              {advancedOpen ? "Hide" : "Show"}
            </span>
          </button>

          {advancedOpen ? (
            <div className="hc-filters-panel__advanced-grid" id={advancedPanelId}>
              {advancedFilters}
            </div>
          ) : null}
        </div>
      ) : null}

      {activeFilters.length > 0 ? (
        <div className="hc-filters-panel__chips" aria-label="Active filters">
          {activeFilters.map((filter) => (
            <button
              key={filter.key}
              aria-label={`Remove ${filter.label}`}
              className="hc-filters-panel__chip"
              type="button"
              onClick={filter.onRemove}
            >
              <span>{filter.label}</span>
              <span className="hc-filters-panel__chip-action" aria-hidden="true">
                Clear
              </span>
            </button>
          ))}
        </div>
      ) : null}
    </Card>
  );
}

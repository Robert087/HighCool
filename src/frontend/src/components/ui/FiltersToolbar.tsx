import { useEffect, useState, type ReactNode } from "react";
import { cn } from "../../lib/cn";
import { Badge } from "./Badge";
import { Button } from "./Button";
import { Card } from "./Card";
import { Input, type InputProps } from "./Input";
import { Select, type SelectProps } from "./Select";

export interface FilterChip {
  key: string;
  label: string;
  onRemove: () => void;
}

interface FiltersDrawerProps {
  children: ReactNode;
  onClose: () => void;
  open: boolean;
  title?: string;
}

interface ActiveFilterChipsProps {
  filters: FilterChip[];
}

interface FilterDateRangeInlineProps {
  fromValue: string;
  onFromChange: (value: string) => void;
  onToChange: (value: string) => void;
  toValue: string;
}

export interface FiltersToolbarProps {
  activeFilters?: FilterChip[];
  dateRange?: ReactNode;
  mobileFilters?: ReactNode;
  mobileTriggerOnly?: boolean;
  onReset: () => void;
  primaryFilters?: ReactNode;
  resetLabel?: string;
  resultLabel: string;
  search: ReactNode;
  secondaryActiveCount?: number;
  secondaryFilters?: ReactNode;
}

export function FilterDropdown({ className, ...props }: SelectProps) {
  return <Select className={cn("hc-filter-toolbar__control", className)} {...props} />;
}

export function FilterTextInput({ className, ...props }: InputProps) {
  return <Input className={cn("hc-filter-toolbar__control", className)} {...props} />;
}

export function FilterDateRangeInline({
  fromValue,
  onFromChange,
  onToChange,
  toValue,
}: FilterDateRangeInlineProps) {
  return (
    <div className="hc-filter-toolbar__date-range" role="group" aria-label="Date range">
      <Input
        aria-label="From date"
        className="hc-filter-toolbar__control"
        type="date"
        value={fromValue}
        onChange={(event) => onFromChange(event.target.value)}
      />
      <span className="hc-filter-toolbar__date-separator" aria-hidden="true">
        to
      </span>
      <Input
        aria-label="To date"
        className="hc-filter-toolbar__control"
        type="date"
        value={toValue}
        onChange={(event) => onToChange(event.target.value)}
      />
    </div>
  );
}

export function ActiveFilterChips({ filters }: ActiveFilterChipsProps) {
  if (filters.length === 0) {
    return null;
  }

  return (
    <div className="hc-filter-toolbar__chips" aria-label="Active filters">
      {filters.map((filter) => (
        <button
          key={filter.key}
          aria-label={`Remove ${filter.label}`}
          className="hc-filter-toolbar__chip"
          type="button"
          onClick={filter.onRemove}
        >
          <span className="hc-filter-toolbar__chip-label">{filter.label}</span>
          <span className="hc-filter-toolbar__chip-action" aria-hidden="true">
            x
          </span>
        </button>
      ))}
    </div>
  );
}

export function FiltersDrawer({ children, onClose, open, title = "More filters" }: FiltersDrawerProps) {
  useEffect(() => {
    if (!open) {
      return undefined;
    }

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        onClose();
      }
    }

    window.addEventListener("keydown", handleKeyDown);

    return () => {
      document.body.style.overflow = previousOverflow;
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [onClose, open]);

  if (!open) {
    return null;
  }

  return (
    <div className="hc-filter-drawer" role="dialog" aria-modal="true" aria-label={title}>
      <button className="hc-filter-drawer__backdrop" type="button" aria-label="Close filters" onClick={onClose} />
      <Card className="hc-filter-drawer__panel" padding="md">
        <div className="hc-filter-drawer__header">
          <strong className="hc-filter-drawer__title">{title}</strong>
          <Button size="sm" variant="ghost" onClick={onClose}>
            Close
          </Button>
        </div>
        <div className="hc-filter-drawer__content">{children}</div>
      </Card>
    </div>
  );
}

export function FiltersToolbar({
  activeFilters = [],
  dateRange,
  mobileFilters,
  mobileTriggerOnly = false,
  onReset,
  primaryFilters,
  resetLabel = "Reset",
  resultLabel,
  search,
  secondaryActiveCount = 0,
  secondaryFilters,
}: FiltersToolbarProps) {
  const [drawerOpen, setDrawerOpen] = useState(secondaryActiveCount > 0);

  useEffect(() => {
    if (secondaryActiveCount > 0) {
      setDrawerOpen(true);
    }
  }, [secondaryActiveCount]);

  const drawerContent = mobileFilters ?? secondaryFilters;
  const hasDrawer = Boolean(drawerContent);

  return (
    <>
      <Card className="hc-filter-toolbar" padding="sm">
        <div className="hc-filter-toolbar__bar">
          <div className="hc-filter-toolbar__search">{search}</div>
          {primaryFilters ? <div className="hc-filter-toolbar__primary">{primaryFilters}</div> : null}
          {dateRange ? <div className="hc-filter-toolbar__date">{dateRange}</div> : null}
          <div className="hc-filter-toolbar__actions">
            <Badge tone="neutral">{resultLabel}</Badge>
            {hasDrawer ? (
              <Button
                className={mobileTriggerOnly ? "hc-filter-toolbar__mobile-trigger" : undefined}
                size="sm"
                variant="secondary"
                onClick={() => setDrawerOpen(true)}
              >
                {secondaryActiveCount > 0 ? `More filters (${secondaryActiveCount})` : "More filters"}
              </Button>
            ) : null}
            <Button size="sm" variant="ghost" disabled={activeFilters.length === 0} onClick={onReset}>
              {resetLabel}
            </Button>
          </div>
        </div>

        <ActiveFilterChips filters={activeFilters} />
      </Card>

      {hasDrawer ? (
        <FiltersDrawer open={drawerOpen} onClose={() => setDrawerOpen(false)}>
          <div className="hc-filter-drawer__section hc-filter-drawer__section--mobile">{mobileFilters}</div>
          <div className={cn("hc-filter-drawer__section", mobileFilters ? "hc-filter-drawer__section--desktop" : undefined)}>
            {secondaryFilters}
          </div>
        </FiltersDrawer>
      ) : null}
    </>
  );
}

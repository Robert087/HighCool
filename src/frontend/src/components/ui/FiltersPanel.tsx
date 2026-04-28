import { useEffect, useId, useState, type ReactNode } from "react";
import { cn } from "../../lib/cn";
import { useI18n } from "../../i18n";
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
  fromLabel = "common.from",
  fromValue,
  label = "common.date",
  onFromChange,
  onToChange,
  toLabel = "common.to",
  toValue,
}: FiltersDateRangeProps) {
  const { translateText } = useI18n();

  return (
    <Field className="hc-filters-panel__date-field" label={label}>
      <div className="hc-filters-panel__date-range" role="group" aria-label={typeof label === "string" ? translateText(label) : translateText("common.date")}>
        <label className="hc-filters-panel__date-input">
          <span className="hc-filters-panel__date-label">{translateText(fromLabel)}</span>
          <Input type="date" value={fromValue} onChange={(event) => onFromChange(event.target.value)} />
        </label>
        <label className="hc-filters-panel__date-input">
          <span className="hc-filters-panel__date-label">{translateText(toLabel)}</span>
          <Input type="date" value={toValue} onChange={(event) => onToChange(event.target.value)} />
        </label>
      </div>
    </Field>
  );
}

export function FiltersPanel({
  activeFilters = [],
  advancedFilters,
  advancedLabel = "common.advancedFilters",
  className,
  dateRange,
  defaultAdvancedOpen = false,
  description,
  hasFilters,
  helperText,
  onReset,
  primaryFilters,
  resetLabel = "common.resetFilters",
  resultLabel,
  search,
  title = "common.filters",
}: FiltersPanelProps) {
  const { t } = useI18n();
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
          <h2 className="hc-filters-panel__title">{t(title)}</h2>
          {description ? <p className="hc-filters-panel__description">{t(description)}</p> : null}
        </div>

        <div className="hc-filters-panel__meta">
          <Badge tone="neutral">{resultLabel}</Badge>
          {helperText ? <p className="hc-filters-panel__helper">{t(helperText)}</p> : null}
          <Button size="sm" variant="ghost" disabled={!hasFilters} onClick={onReset}>
            {t(resetLabel)}
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
            <span>{t(advancedLabel)}</span>
            <span className="hc-filters-panel__toggle-indicator" aria-hidden="true">
              {advancedOpen ? t("app.hide") : t("app.show")}
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
        <div className="hc-filters-panel__chips" aria-label={t("common.activeFilters")}>
          {activeFilters.map((filter) => (
            <button
              key={filter.key}
              aria-label={t("common.removeFilter", { label: filter.label })}
              className="hc-filters-panel__chip"
              type="button"
              onClick={filter.onRemove}
            >
              <span>{filter.label}</span>
              <span className="hc-filters-panel__chip-action" aria-hidden="true">
                {t("app.clear")}
              </span>
            </button>
          ))}
        </div>
      ) : null}
    </Card>
  );
}

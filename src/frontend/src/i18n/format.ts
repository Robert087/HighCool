import { getRuntimeLocale } from "./runtime";

interface NumberFormatOptions {
  maximumFractionDigits?: number;
  minimumFractionDigits?: number;
}

interface CurrencyFormatOptions extends NumberFormatOptions {
  currency?: string | null;
}

export function formatDate(value: string | number | Date | null | undefined, options?: Intl.DateTimeFormatOptions) {
  if (!value) {
    return "";
  }

  return new Intl.DateTimeFormat(getRuntimeLocale(), options ?? {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(new Date(value));
}

export function formatNumber(value: number, options?: NumberFormatOptions) {
  return new Intl.NumberFormat(getRuntimeLocale(), {
    maximumFractionDigits: options?.maximumFractionDigits ?? 2,
    minimumFractionDigits: options?.minimumFractionDigits ?? 0,
  }).format(value);
}

export function formatQuantity(value: number) {
  return formatNumber(value, {
    maximumFractionDigits: 6,
    minimumFractionDigits: 0,
  });
}

export function formatCurrency(value: number, options?: CurrencyFormatOptions) {
  const formattedValue = formatNumber(value, {
    maximumFractionDigits: options?.maximumFractionDigits ?? 2,
    minimumFractionDigits: options?.minimumFractionDigits ?? 2,
  });

  return options?.currency ? `${formattedValue} ${options.currency}` : formattedValue;
}

export function formatPercent(value: number, maximumFractionDigits = 2) {
  return new Intl.NumberFormat(getRuntimeLocale(), {
    style: "percent",
    maximumFractionDigits,
  }).format(value);
}

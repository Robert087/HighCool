import {
  createContext,
  useContext,
  useEffect,
  useMemo,
  useState,
  type PropsWithChildren,
} from "react";
import { formatCurrency, formatDate, formatNumber, formatPercent, formatQuantity } from "./format";
import { messagesByLocale, type SupportedLocale } from "./messages";
import { setRuntimeLocale } from "./runtime";
import { interpolate } from "./utils";

type Direction = "ltr" | "rtl";

interface I18nContextValue {
  direction: Direction;
  formatCurrency: typeof formatCurrency;
  formatDate: typeof formatDate;
  formatNumber: typeof formatNumber;
  formatPercent: typeof formatPercent;
  formatQuantity: typeof formatQuantity;
  isRtl: boolean;
  locale: SupportedLocale;
  setLocale: (locale: SupportedLocale) => void;
  t: (key: string, values?: Record<string, string | number | null | undefined>) => string;
  translateText: (value: string) => string;
}

const I18nContext = createContext<I18nContextValue | null>(null);
const LOCALE_STORAGE_KEY = "hc-locale";

function getDirection(locale: SupportedLocale): Direction {
  return locale === "ar" ? "rtl" : "ltr";
}

function readStoredLocale(): SupportedLocale {
  if (typeof window === "undefined") {
    return "en";
  }

  const value = window.localStorage.getItem(LOCALE_STORAGE_KEY);
  return value === "ar" ? "ar" : "en";
}

function humanizeMissingKey(key: string) {
  const lastToken = key.split(".").pop() ?? key;
  const words = lastToken
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/[_-]+/g, " ")
    .trim();

  if (!words) {
    return key;
  }

  return words.charAt(0).toUpperCase() + words.slice(1);
}

export function I18nProvider({ children }: PropsWithChildren) {
  const [locale, setLocale] = useState<SupportedLocale>(() => readStoredLocale());

  useEffect(() => {
    window.localStorage.setItem(LOCALE_STORAGE_KEY, locale);
    setRuntimeLocale(locale);
    document.documentElement.lang = locale;
    document.documentElement.dir = getDirection(locale);
    document.body.dir = getDirection(locale);
    document.body.dataset.locale = locale;
  }, [locale]);

  const value = useMemo<I18nContextValue>(() => {
    const dictionary = messagesByLocale[locale];
    const direction = getDirection(locale);

    const t = (key: string, values?: Record<string, string | number | null | undefined>) => {
      const template = dictionary[key] ?? messagesByLocale.en[key] ?? humanizeMissingKey(key);
      return interpolate(template, values);
    };

    return {
      direction,
      formatCurrency,
      formatDate,
      formatNumber,
      formatPercent,
      formatQuantity,
      isRtl: direction === "rtl",
      locale,
      setLocale,
      t,
      translateText: (value: string) => t(value),
    };
  }, [locale]);

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n() {
  const context = useContext(I18nContext);
  if (!context) {
    throw new Error("useI18n must be used within an I18nProvider.");
  }

  return context;
}

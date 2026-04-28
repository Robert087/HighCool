import type { SupportedLocale } from "./messages";

let runtimeLocale: SupportedLocale = "en";

export function getRuntimeLocale() {
  return runtimeLocale;
}

export function setRuntimeLocale(locale: SupportedLocale) {
  runtimeLocale = locale;
}

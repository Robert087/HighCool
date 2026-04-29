const ACCESS_TOKEN_KEY = "hc-access-token";

export function getStoredAccessToken() {
  if (typeof window === "undefined") {
    return null;
  }

  return window.localStorage.getItem(ACCESS_TOKEN_KEY);
}

export function storeAccessToken(token: string | null) {
  if (typeof window === "undefined") {
    return;
  }

  if (!token) {
    window.localStorage.removeItem(ACCESS_TOKEN_KEY);
    return;
  }

  window.localStorage.setItem(ACCESS_TOKEN_KEY, token);
}

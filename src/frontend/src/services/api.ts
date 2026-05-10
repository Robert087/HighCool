import { getStoredAccessToken, storeAccessToken } from "../features/auth/authStorage";

export type ValidationErrors = Record<string, string[]>;
export type SortDirection = "Asc" | "Desc";

export interface PaginatedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  appliedFilters?: unknown;
  sort: {
    sortBy: string;
    direction: SortDirection;
  };
}

export interface PaginationParams {
  page: number;
  pageSize: number;
  sortBy?: string;
  sortDirection?: SortDirection;
}

export class ApiError extends Error {
  status: number;
  validationErrors?: ValidationErrors;

  constructor(message: string, status: number, validationErrors?: ValidationErrors) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.validationErrors = validationErrors;
  }
}

function getApiBaseUrl() {
  const configuredBaseUrl = import.meta.env.VITE_API_BASE_URL;
  if (configuredBaseUrl && configuredBaseUrl.trim().length > 0) {
    return configuredBaseUrl.replace(/\/$/, "");
  }

  return "";
}

function resolveRequestUrl(input: string) {
  if (/^https?:\/\//i.test(input)) {
    return input;
  }

  const baseUrl = getApiBaseUrl();
  if (!input.startsWith("/")) {
    return baseUrl ? `${baseUrl}/${input}` : input;
  }

  return baseUrl ? `${baseUrl}${input}` : input;
}

function getFallbackRequestUrls(input: string) {
  if (!import.meta.env.DEV || getApiBaseUrl() || !input.startsWith("/api")) {
    return [];
  }

  if (typeof window === "undefined") {
    return [];
  }

  const { port, protocol } = window.location;
  const isFrontendDevOrigin = port === "5173" || port === "4173";

  if (!isFrontendDevOrigin) {
    return [];
  }

  return [`${protocol}//localhost:5080${input}`, `${protocol}//127.0.0.1:5080${input}`];
}

function normalizeValidationErrors(errors: ValidationErrors | undefined): ValidationErrors | undefined {
  if (!errors) {
    return undefined;
  }

  return Object.fromEntries(
    Object.entries(errors).map(([key, value]) => {
      const normalizedKey = key.length > 0 ? key[0].toLowerCase() + key.slice(1) : key;
      return [normalizedKey, value];
    }),
  );
}

export async function requestJson<T>(input: string, init?: RequestInit): Promise<T> {
  const accessToken = getStoredAccessToken();
  const url = resolveRequestUrl(input);
  const fallbackUrls = getFallbackRequestUrls(input).filter((candidate) => candidate !== url);

  if (import.meta.env.DEV) {
    console.info(`[api] ${init?.method ?? "GET"} ${url}`);
  }

  let response: Response;

  try {
    response = await fetch(url, {
      headers: {
        "Content-Type": "application/json",
        ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
        ...(init?.headers ?? {}),
      },
      ...init,
    });
  } catch (error) {
    let fallbackError: unknown = null;

    for (const fallbackUrl of fallbackUrls) {
      if (import.meta.env.DEV) {
        console.warn(`[api] retrying direct backend request after network error: ${fallbackUrl}`);
      }

      try {
        response = await fetch(fallbackUrl, {
          headers: {
            "Content-Type": "application/json",
            ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
            ...(init?.headers ?? {}),
          },
          ...init,
        });

        return await handleResponse<T>(response, fallbackUrl);
      } catch (currentFallbackError) {
        fallbackError = currentFallbackError;
      }
    }

    const message = import.meta.env.DEV
      ? `Unable to reach the API. Tried ${[url, ...fallbackUrls].join(" and ")}. Make sure the backend is running on http://localhost:5080.`
      : "Unable to reach the server. Please try again.";

    if (import.meta.env.DEV) {
      console.error(`[api] network error for ${url}`, error);
      if (fallbackError) {
        console.error("[api] fallback request error", fallbackError);
      }
    }

    throw new ApiError(message, 0);
  }

  if (response.status === 404 && fallbackUrls.length > 0) {
    for (const fallbackUrl of fallbackUrls) {
      if (import.meta.env.DEV) {
        console.warn(`[api] received 404 for ${url}, retrying ${fallbackUrl}`);
      }

      response = await fetch(fallbackUrl, {
        headers: {
          "Content-Type": "application/json",
          ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
          ...(init?.headers ?? {}),
        },
        ...init,
      });

      if (response.ok || response.status !== 404) {
        break;
      }
    }
  }

  return handleResponse<T>(response, url);
}

async function handleResponse<T>(response: Response, url: string): Promise<T> {
  if (response.status === 204) {
    return undefined as T;
  }

  const contentType = response.headers.get("content-type") ?? "";
  const payload = contentType.includes("application/json")
    ? await response.json()
    : await response.text();

  if (!response.ok) {
    if (response.status === 401) {
      storeAccessToken(null);
    }

    const validationErrors = typeof payload === "object" && payload && "errors" in payload
      ? normalizeValidationErrors(payload.errors as ValidationErrors)
      : undefined;

    const firstValidationMessage = validationErrors
      ? Object.values(validationErrors).flat()[0]
      : undefined;

    const message = typeof payload === "object" && payload && "message" in payload
      ? String(payload.message)
      : (firstValidationMessage ?? (response.statusText || "Request failed"));

    if (import.meta.env.DEV) {
      console.warn(`[api] ${response.status} ${url}: ${message}`);
    }

    throw new ApiError(message, response.status, validationErrors);
  }

  return payload as T;
}

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
  const response = await fetch(input, {
    headers: {
      "Content-Type": "application/json",
      ...(init?.headers ?? {}),
    },
    ...init,
  });

  if (response.status === 204) {
    return undefined as T;
  }

  const contentType = response.headers.get("content-type") ?? "";
  const payload = contentType.includes("application/json")
    ? await response.json()
    : await response.text();

  if (!response.ok) {
    const validationErrors = typeof payload === "object" && payload && "errors" in payload
      ? normalizeValidationErrors(payload.errors as ValidationErrors)
      : undefined;

    const firstValidationMessage = validationErrors
      ? Object.values(validationErrors).flat()[0]
      : undefined;

    const message = typeof payload === "object" && payload && "message" in payload
      ? String(payload.message)
      : (firstValidationMessage ?? (response.statusText || "Request failed"));

    throw new ApiError(message, response.status, validationErrors);
  }

  return payload as T;
}

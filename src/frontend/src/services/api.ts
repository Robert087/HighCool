export type ValidationErrors = Record<string, string[]>;

const configuredApiBaseUrl = import.meta.env.VITE_API_BASE_URL?.trim().replace(/\/+$/, "") ?? "";

export function buildApiUrl(path: string): string {
  if (/^https?:\/\//i.test(path)) {
    return path;
  }

  const normalizedPath = path.startsWith("/") ? path : `/${path}`;

  if (!configuredApiBaseUrl) {
    return normalizedPath;
  }

  return `${configuredApiBaseUrl}${normalizedPath}`;
}

console.log("API:", configuredApiBaseUrl || "(same-origin)");

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
  const response = await fetch(buildApiUrl(input), {
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

    const message = typeof payload === "object" && payload && "message" in payload
      ? String(payload.message)
      : response.statusText || "Request failed";

    throw new ApiError(message, response.status, validationErrors);
  }

  return payload as T;
}

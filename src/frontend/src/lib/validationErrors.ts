import type { ValidationErrors } from "../services/api";

export function getFirstValidationMessage(errors: ValidationErrors): string | null {
  for (const messages of Object.values(errors)) {
    const message = messages.find((entry) => entry.trim().length > 0);
    if (message) {
      return message;
    }
  }

  return null;
}

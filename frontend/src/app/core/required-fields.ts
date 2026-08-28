/**
 * Shared "required field" check used across every form in the app that
 * has required fields (client registration/edit, employee
 * registration/edit, system/product creation, agreements, ticket
 * submission, and so on) — mirrors the backend's RequiredFieldValidator
 * so both sides raise the same clear message for the same rule, and a
 * form can't be submitted with any required field left blank.
 *
 * Pass fields in the order they appear on the form so the reported field
 * matches what someone would hit first reading top-to-bottom.
 */
export interface RequiredField {
  label: string;
  value: string | null | undefined;
}

/**
 * Returns the label of the first blank required field, or null if every
 * required field has a value. Blank means empty/whitespace-only —
 * matching the backend's string.IsNullOrWhiteSpace check.
 */
export function firstMissingRequiredField(fields: RequiredField[]): string | null {
  for (const field of fields) {
    if (!field.value || !field.value.trim()) return field.label;
  }
  return null;
}

/** Convenience wrapper — returns a ready-to-display error message, or null if nothing is missing. */
export function requiredFieldsError(fields: RequiredField[]): string | null {
  const missing = firstMissingRequiredField(fields);
  return missing ? `${missing} is required.` : null;
}

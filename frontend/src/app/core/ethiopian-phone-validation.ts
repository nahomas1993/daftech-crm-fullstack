/**
 * Shared shape check for every Ethiopian phone number field in the app —
 * Client "Phone Number", Employee "Phone Number", and the Client
 * "IT Support Contact" field. All three accept only the two carriers
 * DAFTECH supports for this shape:
 *   - Ethio Telecom: +2519XXXXXXXX
 *   - Safaricom Ethiopia: +2517XXXXXXXX
 * Used to drive the live "Invalid ___" hint under each of these fields on
 * the client/employee registration and edit forms — mirrors the
 * server-side check in RequiredFieldValidator.EnsureValidEthiopianPhone,
 * which is the source of truth (this is a client-side convenience, not a
 * security boundary).
 *
 * All three fields are required, so callers should run the separate
 * required-field check first and only call this once a value is present
 * — but isValidEthiopianPhone treats empty as valid on its own so it's
 * still safe to use directly against a live "Invalid …" hint that should
 * stay quiet on an untouched, still-empty field.
 */
export const ETHIOPIAN_PHONE_PATTERN = /^\+251(9|7)\d{8}$/;

export function isValidEthiopianPhone(value: string | null | undefined): boolean {
  if (!value || !value.trim()) return true;
  return ETHIOPIAN_PHONE_PATTERN.test(value.trim());
}

/** Standard inline error message for an invalid Ethiopian phone number, parameterized by field label. */
export function invalidEthiopianPhoneMessage(label: string): string {
  return `Invalid ${label} — use +2519XXXXXXXX (Ethio Telecom) or +2517XXXXXXXX (Safaricom).`;
}

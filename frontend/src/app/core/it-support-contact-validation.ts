/**
 * A client's IT Support Contact Number must be a valid Ethiopian mobile
 * number on one of the two carriers DAFTECH supports for this field:
 *   - Ethio Telecom: +2519XXXXXXXX
 *   - Safaricom Ethiopia: +2517XXXXXXXX
 * Used to drive the live "Invalid IT Support Contact Number" hint under
 * the field on the client registration and edit forms — mirrors the
 * server-side check in RequiredFieldValidator.EnsureValidItSupportContact,
 * which is the source of truth (this is a client-side convenience, not a
 * security boundary). The field itself is optional, so an empty value is
 * treated as valid here — this only judges the *shape* of whatever's been
 * typed so far.
 */
const IT_SUPPORT_CONTACT_PATTERN = /^\+251(9|7)\d{8}$/;

export function isValidItSupportContact(value: string | null | undefined): boolean {
  if (!value || !value.trim()) return true;
  return IT_SUPPORT_CONTACT_PATTERN.test(value.trim());
}

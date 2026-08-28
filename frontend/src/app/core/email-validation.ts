/**
 * Very small, deliberate rule: for now every client/employee email must
 * end in "@gmail.com" — used to drive the live "Invalid email" hint under
 * the email box on both registration and edit forms (client and
 * employee). Empty is treated as valid here since "required" is already
 * enforced separately (see required-fields.ts); this only judges the
 * *shape* of whatever's been typed so far.
 */
export function isValidRegistrationEmail(email: string | null | undefined): boolean {
  if (!email || !email.trim()) return true;
  return email.trim().toLowerCase().endsWith('@gmail.com');
}

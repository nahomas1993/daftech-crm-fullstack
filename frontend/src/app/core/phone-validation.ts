/**
 * Ethiopian phone numbers, mirroring PhoneNumberValidator on the server
 * (the server is the real gate; this only drives the live hint and the
 * pre-submit check). Accepts mobile and landline/company numbers, with or
 * without spaces, dashes, dots or brackets:
 *   0911223344, 0111136187, +251911223344, +251111136187, 251111136187
 * Nine digits follow the country code and start with 1-5 (landline area
 * codes) or 7 / 9 (mobile). Empty is treated as valid here because
 * "required" is enforced separately; this only judges what was typed.
 */
const ETHIOPIAN_NUMBER = /^(?:\+251|251|0)([1-579]\d{8})$/;

export function isValidEthiopianPhone(value: string | null | undefined): boolean {
  if (!value || !value.trim()) return true;
  return ETHIOPIAN_NUMBER.test(value.trim().replace(/[\s\-.()]/g, ''));
}

export const PHONE_HINT = 'e.g. 0911223344 or +251111136187';
export const PHONE_ERROR = 'Invalid phone number. Use an Ethiopian number such as 0911223344 or +251111136187.';

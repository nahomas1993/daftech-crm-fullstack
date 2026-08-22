/**
 * Shared password-strength rule used everywhere a new password is set
 * (staff change-password, portal change-password, staff settings, signup).
 * Keeping this in one place means every form enforces the exact same rule
 * and shows the exact same message — if the requirement ever changes,
 * it changes once here.
 *
 * Rule: at least 8 characters, with at least one lowercase letter, one
 * uppercase letter, and one number.
 */
export const PASSWORD_STRENGTH_HINT = 'At least 8 characters, with a lowercase letter, an uppercase letter, and a number.';

export function passwordStrengthError(password: string): string | null {
  if (password.length < 8) return 'Password must be at least 8 characters.';
  if (!/[a-z]/.test(password)) return 'Password must include at least one lowercase letter.';
  if (!/[A-Z]/.test(password)) return 'Password must include at least one uppercase letter.';
  if (!/[0-9]/.test(password)) return 'Password must include at least one number.';
  return null;
}

/**
 * Decodes the payload of a JWT without verifying its signature — safe to
 * do client-side since we're only reading our own claims for display/
 * routing purposes; the server independently verifies the signature on
 * every request. Never trust this for anything security-sensitive.
 */
export interface DecodedAccessToken {
  sub: string; // account id
  daftech_account_type: 'Employee' | 'Client';
  unique_name: string;
  role?: string | string[];
  exp: number;
}

export function decodeAccessToken(token: string): DecodedAccessToken | null {
  try {
    const payload = token.split('.')[1];
    const normalized = payload.replace(/-/g, '+').replace(/_/g, '/');
    const padded = normalized.padEnd(normalized.length + ((4 - (normalized.length % 4)) % 4), '=');
    const json = atob(padded);
    return JSON.parse(json) as DecodedAccessToken;
  } catch {
    return null;
  }
}

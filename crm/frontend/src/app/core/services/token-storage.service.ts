import { Injectable, signal } from '@angular/core';

export interface StoredTokens {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string; // ISO string
}

const STORAGE_KEY = 'daftech_auth_tokens';

/**
 * Persists the JWT access/refresh token pair in localStorage so a page
 * refresh doesn't log the user out (the whole reason this exists — see
 * Final_version_fix.docx item 2). Only the tokens live here; the actual
 * Employee/Client profile is re-fetched or carried in AuthService as
 * before. Exposes a signal so components/guards can react to login/logout
 * without polling.
 */
@Injectable({ providedIn: 'root' })
export class TokenStorageService {
  private readonly _tokens = signal<StoredTokens | null>(this.readFromStorage());
  readonly tokens = this._tokens.asReadonly();

  get accessToken(): string | null {
    return this._tokens()?.accessToken ?? null;
  }

  get refreshToken(): string | null {
    return this._tokens()?.refreshToken ?? null;
  }

  /** True once the access token is expired or within 30s of expiring — the interceptor treats this as "needs refresh". */
  isAccessTokenExpiringSoon(): boolean {
    const tokens = this._tokens();
    if (!tokens) return true;
    const expiresAt = new Date(tokens.accessTokenExpiresAt).getTime();
    return Date.now() >= expiresAt - 30_000;
  }

  setTokens(tokens: StoredTokens): void {
    this._tokens.set(tokens);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(tokens));
    } catch {
      // localStorage can throw in private-browsing/storage-full edge cases —
      // the app still works for this session, it just won't survive a refresh.
    }
  }

  clear(): void {
    this._tokens.set(null);
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // See setTokens — safe to ignore.
    }
  }

  private readFromStorage(): StoredTokens | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? (JSON.parse(raw) as StoredTokens) : null;
    } catch {
      return null;
    }
  }
}

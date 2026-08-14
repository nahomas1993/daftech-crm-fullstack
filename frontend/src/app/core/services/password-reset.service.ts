import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { PasswordResetRequest, PasswordResetOtpIssuedResult } from '../models';
import { API_BASE_URL } from './api-base';

/**
 * Admin's "Password Reset Requests" queue. Unlike ClientService this does
 * NOT auto-fetch in the constructor — every endpoint here is Admin-only, so
 * eagerly calling it for every instantiation (e.g. from a client/employee
 * session) would just produce noisy 403s. The signup-requests page pattern
 * (pendingRequests() filtering a full list) is intentionally not reused
 * here for the same reason — callers ask for exactly the list they need.
 */
@Injectable({ providedIn: 'root' })
export class PasswordResetService {
  private readonly _pending = signal<PasswordResetRequest[]>([]);
  readonly pending = this._pending.asReadonly();

  constructor(private http: HttpClient) {}

  async refreshPending(): Promise<void> {
    const list = await firstValueFrom(
      this.http.get<PasswordResetRequest[]>(`${API_BASE_URL}/password-reset-requests/pending`)
    );
    this._pending.set(list);
  }

  async getAll(): Promise<PasswordResetRequest[]> {
    return firstValueFrom(this.http.get<PasswordResetRequest[]>(`${API_BASE_URL}/password-reset-requests`));
  }

  /** Issues a fresh one-time password and emails it. The response's oneTimePassword is shown ONCE — display it to the Admin now. */
  async issueOtp(requestId: string): Promise<PasswordResetOtpIssuedResult> {
    const result = await firstValueFrom(
      this.http.post<PasswordResetOtpIssuedResult>(`${API_BASE_URL}/password-reset-requests/${requestId}/issue-otp`, {})
    );
    await this.refreshPending();
    return result;
  }

  async dismiss(requestId: string, reason: string): Promise<void> {
    await firstValueFrom(
      this.http.post<PasswordResetRequest>(`${API_BASE_URL}/password-reset-requests/${requestId}/dismiss`, { reason })
    );
    await this.refreshPending();
  }
}

import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom, Observable, catchError, map, of, tap, throwError, timeout } from 'rxjs';
import { Router } from '@angular/router';
import { Employee, Client, DeviceType } from '../models';
import { API_BASE_URL } from './api-base';
import { SessionService } from './session.service';
import { TokenStorageService, StoredTokens } from './token-storage.service';
import { decodeAccessToken } from './jwt.util';
import { IdleTimeoutService } from './idle-timeout.service';

export interface LoginResult {
  success: boolean;
  message?: string;
  ipAddress?: string;
}

interface AuthTokenResultDto {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly _currentEmployee = signal<Employee | null>(null);
  private readonly _currentClient = signal<Client | null>(null);
  private readonly _restoring = signal<boolean>(false);

  readonly currentEmployee = this._currentEmployee.asReadonly();
  readonly currentClient = this._currentClient.asReadonly();
  readonly restoring = this._restoring.asReadonly();

  isStaffAuthenticated(): boolean {
    return this._currentEmployee() !== null;
  }

  isClientAuthenticated(): boolean {
    return this._currentClient() !== null;
  }

  staffMustChangePassword(): boolean {
    return this._currentEmployee()?.mustChangePassword ?? false;
  }

  clientMustChangePassword(): boolean {
    return this._currentClient()?.mustChangePassword ?? false;
  }

  constructor(
    private http: HttpClient,
    private sessions: SessionService,
    private tokenStorage: TokenStorageService,
    private idleTimeout: IdleTimeoutService,
    private router: Router
  ) {}

  /**
   * Starts (or restarts) the 15-minute idle-logout watch for whichever
   * account type is currently signed in. Called after every successful
   * login/session-restore/password-change re-login. On timeout, forces a
   * full logout (revoking the refresh token, clearing local state) and
   * sends the user back to the sign-in page — same effect as clicking
   * "Log out" themselves, just triggered by inactivity instead.
   */
  private startIdleWatch(): void {
    this.idleTimeout.start(() => {
      void (async () => {
        if (this._currentEmployee()) await this.logoutStaff();
        else if (this._currentClient()) await this.logoutClient();
        this.router.navigateByUrl('/login');
      })();
    });
  }

  async restoreSession(): Promise<void> {
    const refreshToken = this.tokenStorage.refreshToken;
    if (!refreshToken) return;

    this._restoring.set(true);
    try {
      if (this.tokenStorage.isAccessTokenExpiringSoon()) {
        await firstValueFrom(this.refreshTokens());
      }

      const accessToken = this.tokenStorage.accessToken;
      const decoded = accessToken ? decodeAccessToken(accessToken) : null;
      if (!decoded) {
        this.tokenStorage.clear();
        return;
      }

      if (decoded.daftech_account_type === 'Employee') {
        const employee = await firstValueFrom(this.http.get<Employee>(`${API_BASE_URL}/employees/${decoded.sub}`));
        this._currentEmployee.set(employee);
        this.sessions.startHeartbeat('Employee', employee.id);
        this.startIdleWatch();
      } else {
        const client = await firstValueFrom(this.http.get<Client>(`${API_BASE_URL}/clients/${decoded.sub}`));
        this._currentClient.set(client);
        this.sessions.startHeartbeat('Client', client.id);
        this.startIdleWatch();
      }
    } catch {
      this.tokenStorage.clear();
      this._currentEmployee.set(null);
      this._currentClient.set(null);
    } finally {
      this._restoring.set(false);
    }
  }

  /**
   * Unified login — the single entry point behind the one login page for
   * Admins, Employees, and Clients. The server (see AuthController.Login)
   * determines the account type itself by which table the username
   * belongs to; this method just routes local state based on whichever
   * type comes back. accountType in the result is the source of truth for
   * routing, not anything guessed client-side.
   */
  async login(
    username: string,
    password: string,
    deviceType: DeviceType = 'Laptop',
    deviceIdentifier: string = 'WEB-SESSION'
  ): Promise<LoginResult & { accountType?: 'Employee' | 'Client' }> {
    const result = await firstValueFrom(
      this.http.post<{
        success: boolean; message?: string; accountType: 'Employee' | 'Client' | null;
        employee: Employee | null; client: Client | null;
        mustChangePassword: boolean; tokens: AuthTokenResultDto | null;
      }>(`${API_BASE_URL}/auth/login`, { username, password, deviceType, deviceIdentifier }).pipe(
        // Without this, a request that never reaches the server (network
        // failure, the /api/ proxy failing to resolve/reach the backend,
        // a non-JSON error body, etc.) surfaces here as a raw HttpErrorResponse
        // rather than a clean Error — which some paths upstream then try to
        // read .success off, producing "Cannot read properties of null
        // (reading 'success')" instead of a useful message. Normalize it to
        // an Error with a real message so callers' catch blocks (see
        // LoginComponent.attempt()) always get something sane to display.
        catchError((err) => throwError(() => new Error(err?.error?.message ?? 'Could not reach the server.')))
      )
    );

    if (result.success && result.accountType === 'Employee' && result.employee) {
      this._currentEmployee.set(result.employee);
      // Heartbeat/idle-watch need a real access token to authenticate their
      // calls — starting them while tokens is null (the must-change-password
      // case, where no token is issued until the OTP is replaced) sends an
      // unauthenticated sessions/touch, which 401s, which the interceptor
      // treats as a dead session and force-logs-out — wiping
      // _currentEmployee right out from under the change-password screen
      // before the person can finish it. Only start these once real tokens
      // actually exist.
      if (result.tokens) {
        this.tokenStorage.setTokens(result.tokens);
        this.sessions.startHeartbeat('Employee', result.employee.id);
        this.startIdleWatch();
      }
    } else if (result.success && result.accountType === 'Client' && result.client) {
      this._currentClient.set(result.client);
      if (result.tokens) {
        this.tokenStorage.setTokens(result.tokens);
        this.sessions.startHeartbeat('Client', result.client.id);
        this.startIdleWatch();
      }
    }

    return { success: result.success, message: result.message, accountType: result.accountType ?? undefined };
  }

  async loginEmployee(
    username: string,
    password: string,
    deviceType: DeviceType = 'Laptop',
    deviceIdentifier: string = 'WEB-SESSION'
  ): Promise<LoginResult> {
    const result = await firstValueFrom(
      this.http.post<{
        success: boolean; message?: string; ipAddress: string; employee: Employee | null;
        mustChangePassword: boolean; tokens: AuthTokenResultDto | null;
      }>(`${API_BASE_URL}/auth/employee-login`, { username, password, deviceType, deviceIdentifier })
    );

    if (result.success && result.employee) {
      this._currentEmployee.set(result.employee);
      // See the identical comment in login() above — don't start
      // heartbeat/idle-watch without a real token, or the resulting 401
      // triggers a forced logout mid change-password flow.
      if (result.tokens) {
        this.tokenStorage.setTokens(result.tokens);
        this.sessions.startHeartbeat('Employee', result.employee.id);
        this.startIdleWatch();
      }
    }
    return { success: result.success, message: result.message, ipAddress: result.ipAddress };
  }

  async changeEmployeePassword(currentPassword: string, newPassword: string, confirmNewPassword: string): Promise<void> {
    const employee = this._currentEmployee();
    if (!employee) throw new Error('Not logged in.');
    await firstValueFrom(
      this.http.post(`${API_BASE_URL}/auth/employee/${employee.id}/change-password`, {
        currentPassword, newPassword, confirmNewPassword,
      })
    );
    this._currentEmployee.set({ ...employee, mustChangePassword: false });

    const result = await firstValueFrom(
      this.http.post<{ tokens: AuthTokenResultDto | null }>(`${API_BASE_URL}/auth/employee-login`, {
        username: employee.username, password: newPassword, deviceType: 'Laptop', deviceIdentifier: 'WEB-SESSION',
      })
    );
    // This re-login is what actually issues real tokens post-change — start
    // heartbeat/idle-watch here too, same as every other successful login
    // path, so this session is tracked from the moment it has a valid token.
    if (result.tokens) {
      this.tokenStorage.setTokens(result.tokens);
      this.sessions.startHeartbeat('Employee', employee.id);
      this.startIdleWatch();
    }
  }

  async loginClient(username: string, password: string): Promise<LoginResult> {
    const result = await firstValueFrom(
      this.http.post<{
        success: boolean; message?: string; client: Client | null;
        mustChangePassword: boolean; tokens: AuthTokenResultDto | null;
      }>(`${API_BASE_URL}/auth/client-login`, { username, password })
    );

    if (result.success && result.client) {
      this._currentClient.set(result.client);
      if (result.tokens) {
        this.tokenStorage.setTokens(result.tokens);
        this.sessions.startHeartbeat('Client', result.client.id);
        this.startIdleWatch();
      }
    }
    return { success: result.success, message: result.message };
  }

  async changeClientPassword(currentPassword: string, newPassword: string, confirmNewPassword: string): Promise<void> {
    const client = this._currentClient();
    if (!client) throw new Error('Not logged in.');
    await firstValueFrom(
      this.http.post(`${API_BASE_URL}/auth/client/${client.id}/change-password`, {
        currentPassword, newPassword, confirmNewPassword,
      })
    );
    this._currentClient.set({ ...client, mustChangePassword: false });

    const result = await firstValueFrom(
      this.http.post<{ tokens: AuthTokenResultDto | null }>(`${API_BASE_URL}/auth/client-login`, {
        username: client.username, password: newPassword,
      })
    );
    if (result.tokens) {
      this.tokenStorage.setTokens(result.tokens);
      this.sessions.startHeartbeat('Client', client.id);
      this.startIdleWatch();
    }
  }

  /**
   * "Forgot password" — there's no emailed reset link in this system; this
   * just queues the request for an Admin to review and issue a fresh
   * one-time password. Always resolves with the same generic message,
   * whether or not the username matched a real account.
   */
  async forgotPassword(accountType: 'Employee' | 'Client', username: string, note?: string): Promise<string> {
    const result = await firstValueFrom(
      this.http.post<{ message: string }>(`${API_BASE_URL}/auth/forgot-password`, { accountType, username, note })
    );
    return result.message;
  }

  refreshTokens(): Observable<void> {
    const refreshToken = this.tokenStorage.refreshToken;
    if (!refreshToken) {
      // Must be an RxJS error (not a synchronous throw) so it flows through
      // the interceptor's catchError -> forceLogoutAfterRefreshFailure().
      // A synchronous throw here bypassed that path entirely, leaving the
      // app stuck retrying every request with a dead token forever instead
      // of logging the user out cleanly.
      return throwError(() => new Error('No refresh token available.'));
    }

    // Bounded, like every other request here — otherwise a slow/asleep API
    // leaves refreshInProgress stuck true in the interceptor (see
    // auth.interceptor.ts), which then queues every other in-flight request
    // behind a refresh that never settles. A timeout still flows into
    // forceLogoutAfterRefreshFailure() via the interceptor's catchError,
    // same as any other refresh failure.
    return this.http.post<AuthTokenResultDto>(`${API_BASE_URL}/auth/refresh`, { refreshToken }).pipe(
      timeout(15_000),
      tap((tokens) => this.tokenStorage.setTokens(tokens)),
      map(() => void 0)
    );
  }

  forceLogoutAfterRefreshFailure(): void {
    this.tokenStorage.clear();
    this.sessions.stopHeartbeat();
    this.idleTimeout.stop();
    this._currentEmployee.set(null);
    this._currentClient.set(null);
  }

  async logoutStaff(): Promise<void> {
    const employee = this._currentEmployee();
    await this.revokeRefreshTokenBestEffort();
    this.tokenStorage.clear();
    this.idleTimeout.stop();
    this._currentEmployee.set(null);
    if (employee) await this.sessions.closeSession('Employee', employee.id);
  }

  async logoutClient(): Promise<void> {
    const client = this._currentClient();
    await this.revokeRefreshTokenBestEffort();
    this.tokenStorage.clear();
    this.idleTimeout.stop();
    this._currentClient.set(null);
    if (client) await this.sessions.closeSession('Client', client.id);
  }

  private async revokeRefreshTokenBestEffort(): Promise<void> {
    const refreshToken = this.tokenStorage.refreshToken;
    if (!refreshToken) return;
    try {
      await firstValueFrom(this.http.post(`${API_BASE_URL}/auth/logout`, { refreshToken }));
    } catch {
      // Best-effort
    }
  }
}
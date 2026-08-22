import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { SessionActivity, SessionAccountType, LoginSessionHistoryEntry } from '../models';
import { API_BASE_URL } from './api-base';

const HEARTBEAT_INTERVAL_MS = 60_000; // 60s — comfortably inside the backend's 5-minute offline threshold

@Injectable({ providedIn: 'root' })
export class SessionService {
  private heartbeatHandle: ReturnType<typeof setInterval> | null = null;

  constructor(private http: HttpClient) {}

  /** Starts the periodic heartbeat ping for the given account. Call once after login. */
  startHeartbeat(accountType: SessionAccountType, accountId: string): void {
    this.stopHeartbeat();
    void this.touch(accountType, accountId);
    this.heartbeatHandle = setInterval(() => void this.touch(accountType, accountId), HEARTBEAT_INTERVAL_MS);
  }

  stopHeartbeat(): void {
    if (this.heartbeatHandle !== null) {
      clearInterval(this.heartbeatHandle);
      this.heartbeatHandle = null;
    }
  }

  private async touch(accountType: SessionAccountType, accountId: string): Promise<void> {
    try {
      // Body is empty — the server derives the calling account from the JWT
      // access token, not from client-supplied fields (see SessionsController.Touch).
      await firstValueFrom(this.http.post(`${API_BASE_URL}/sessions/touch`, {}));
    } catch {
      // A missed heartbeat isn't user-facing — the backend's offline sweep handles the fallback.
    }
  }

  async closeSession(accountType: SessionAccountType, accountId: string): Promise<void> {
    this.stopHeartbeat();
    try {
      await firstValueFrom(this.http.post(`${API_BASE_URL}/sessions/close`, {}));
    } catch {
      // Best-effort — logging out client-side still proceeds even if this fails.
    }
  }

  async getActivity(): Promise<SessionActivity[]> {
    return firstValueFrom(this.http.get<SessionActivity[]>(`${API_BASE_URL}/sessions/activity`));
  }

  async getHistory(accountType: SessionAccountType, accountId: string): Promise<LoginSessionHistoryEntry[]> {
    return firstValueFrom(
      this.http.get<LoginSessionHistoryEntry[]>(`${API_BASE_URL}/sessions/history`, { params: { accountType, accountId } })
    );
  }
}

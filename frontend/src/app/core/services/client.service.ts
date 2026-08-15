import { Injectable, signal } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Client, ClientRegisteredResult, PagedResult } from '../models';
import { API_BASE_URL } from './api-base';

@Injectable({ providedIn: 'root' })
export class ClientService {
  private readonly _clients = signal<Client[]>([]);
  private readonly _loaded = signal(false);
  readonly clients = this._clients.asReadonly();

  // Paged state for the main Clients table. Separate from the full-list
  // cache above, which pendingRequests()/approvedClients()/reports still
  // rely on for filtering across the whole dataset.
  private readonly _page = signal(1);
  private readonly _pageSize = signal(20);
  private readonly _totalCount = signal(0);
  private readonly _totalPages = signal(0);
  private readonly _pagedClients = signal<Client[]>([]);
  readonly pagedClients = this._pagedClients.asReadonly();
  readonly page = this._page.asReadonly();
  readonly pageSize = this._pageSize.asReadonly();
  readonly totalCount = this._totalCount.asReadonly();
  readonly totalPages = this._totalPages.asReadonly();

  constructor(private http: HttpClient) {
    void this.refresh();
    void this.refreshPaged();
  }

  async refresh(): Promise<void> {
    const list = await firstValueFrom(this.http.get<Client[]>(`${API_BASE_URL}/clients`));
    this._clients.set(list);
    this._loaded.set(true);
  }

  async refreshPaged(page = this._page(), pageSize = this._pageSize()): Promise<void> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    const result = await firstValueFrom(
      this.http.get<PagedResult<Client>>(`${API_BASE_URL}/clients/paged`, { params })
    );
    this._page.set(result.page);
    this._pageSize.set(result.pageSize);
    this._totalCount.set(result.totalCount);
    this._totalPages.set(result.totalPages);
    this._pagedClients.set(result.items);
  }

  async goToPage(page: number): Promise<void> {
    await this.refreshPaged(page);
  }

  pendingRequests(): Client[] {
    return this._clients().filter(c => c.accountStatus === 'Pending');
  }

  approvedClients(): Client[] {
    return this._clients().filter(c => c.accountStatus === 'Approved');
  }

  getById(id: string): Client | undefined {
    return this._clients().find(c => c.id === id);
  }

  async submitSignup(data: {
    name: string; phoneNumber: string; email: string; office: string; location: string;
    region?: string; city?: string; woreda?: string;
  }): Promise<Client> {
    const client = await firstValueFrom(this.http.post<Client>(`${API_BASE_URL}/clients/signup`, data));
    await Promise.all([this.refresh(), this.refreshPaged()]);
    return client;
  }

  /**
   * Admin registers a client directly — Approved and credentialed
   * immediately. The response's oneTimePassword is shown ONCE; the caller
   * must display it to the Admin now.
   */
  async registerClient(data: {
    name: string; phoneNumber: string; email: string; office: string; location: string;
    region?: string; city?: string; woreda?: string;
    kycType: string; kycContact: string; itSupportContact?: string;
  }): Promise<ClientRegisteredResult> {
    const result = await firstValueFrom(this.http.post<ClientRegisteredResult>(`${API_BASE_URL}/clients/register`, data));
    await Promise.all([this.refresh(), this.refreshPaged()]);
    return result;
  }

  async resendCredentialEmail(clientId: string): Promise<{ emailSent: boolean; emailError?: string }> {
    return firstValueFrom(this.http.post<{ emailSent: boolean; emailError?: string }>(`${API_BASE_URL}/clients/${clientId}/resend-credential-email`, {}));
  }

  async approve(clientId: string): Promise<void> {
    await firstValueFrom(this.http.post<Client>(`${API_BASE_URL}/clients/${clientId}/approve`, {}));
    await Promise.all([this.refresh(), this.refreshPaged()]);
  }

  async reject(clientId: string, reason: string): Promise<void> {
    await firstValueFrom(this.http.post<Client>(`${API_BASE_URL}/clients/${clientId}/reject`, { reason }));
    await Promise.all([this.refresh(), this.refreshPaged()]);
  }

  /** Edits the plain profile fields. Account status/credentials go through approve/reject/resendCredentialEmail instead. */
  async updateClient(id: string, data: {
    name: string; phoneNumber: string; email: string; office: string; location: string;
    region?: string; city?: string; woreda?: string;
    kycType: string; kycContact: string; itSupportContact?: string;
  }): Promise<void> {
    await firstValueFrom(this.http.put<Client>(`${API_BASE_URL}/clients/${id}`, data));
    await Promise.all([this.refresh(), this.refreshPaged()]);
  }

  /** Soft-deletes the account — removes it from the Clients list and blocks login; agreements/tickets/trainings it's referenced by are kept intact. */
  async deleteClient(id: string): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`${API_BASE_URL}/clients/${id}`));
    await Promise.all([this.refresh(), this.refreshPaged()]);
  }
}
